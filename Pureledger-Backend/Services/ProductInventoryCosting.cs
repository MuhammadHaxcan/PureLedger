using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;

namespace PureLedger.Services;

/// <summary>
/// A stock-transfer voucher in the history cannot be valued. Reports and availability refuse to
/// guess, but the message names the voucher so it can be repaired instead of failing with a generic 500.
/// </summary>
public sealed class InventoryIntegrityException(int journalVoucherId, string problem)
    : InvalidOperationException($"Stock-transfer voucher JV {journalVoucherId} {problem}. Inventory cannot be valued until it is corrected.")
{
    public int JournalVoucherId { get; } = journalVoucherId;
}

// One chronological, warehouse-based valuation for reporting and transfer availability.
// Dispatch removes source carrying value; receipt adopts the entered transfer value.
// Their difference is an inventory valuation adjustment, never a sale or COGS.
internal sealed class ProductInventoryCosting(LedgerDbContext context)
{
    public async Task<InventoryCostResult> CalculateAsync(int projectId, DateOnly endDate,
        int? categoryId = null, int? productId = null, int? excludedDispatchId = null)
    {
        var query = context.JournalEntries.AsNoTracking()
            .Include(e => e.JournalVoucher).Include(e => e.Account)
            .Where(e => e.JournalVoucher!.ProjectId == projectId && e.JournalVoucher.IsCompleted &&
                !e.JournalVoucher.IsBaseVoucher && e.IsProcessed && e.Account!.AccountGroupId == 2 &&
                e.JournalVoucher.EntryDate <= endDate &&
                (!excludedDispatchId.HasValue || e.JournalVoucherId != excludedDispatchId.Value));
        // Select products by their category without truncating their earlier cost history.
        if (categoryId.HasValue) query = query.Where(e => e.Account!.CategoryId == categoryId.Value);
        if (productId.HasValue) query = query.Where(e => e.AccountId == productId.Value);
        var entries = await query.OrderBy(e => e.JournalVoucher!.EntryDate)
            .ThenBy(e => e.JournalVoucherId).ThenBy(e => e.JournalEntryId).ToListAsync();
        if (entries.Count == 0) return new InventoryCostResult([]);

        var entryIds = entries.Select(e => e.JournalEntryId).ToList();
        var assignments = await context.DeliveryChallanEntries.AsNoTracking()
            .Where(d => entryIds.Contains(d.JournalEntryId))
            .Select(d => new { d.JournalEntryId, d.LocationId }).ToListAsync();
        var byEntry = assignments.GroupBy(d => d.JournalEntryId)
            .ToDictionary(g => g.Key, g => g.ToList());
        int? Warehouse(int entryId) => byEntry.TryGetValue(entryId, out var ds) && ds.Count == 1
            ? ds[0].LocationId : null;

        var voucherIds = entries.Where(e => e.JournalVoucher!.IsStockTransfer)
            .Select(e => e.JournalVoucherId).Distinct().ToList();
        var references = await context.JournalVoucherReferences.AsNoTracking()
            .Where(r => voucherIds.Contains(r.ReferencedVoucherId) &&
                (r.ReferenceType == StockTransferService.ReceiptReferenceType ||
                 r.ReferenceType == StockTransferService.CancellationReferenceType ||
                 r.ReferenceType == StockTransferService.ChargeReferenceType))
            .Select(r => new { r.ReferencedVoucherId, r.MainVoucherId, r.ReferenceType }).ToListAsync();
        var incoming = references.GroupBy(r => r.ReferencedVoucherId).ToDictionary(g => g.Key, g => g.ToList());
        var receiptLocations = entries.Where(e => e.JournalVoucher!.IsStockTransfer && e.Quantity > 0 && e.Debit > 0)
            .GroupBy(e => (e.JournalVoucherId, e.AccountId))
            .ToDictionary(g => g.Key, g => g.Count() == 1 ? Warehouse(g.First().JournalEntryId) : null);

        // Cartage product debits have no challan of their own. Resolve their
        // warehouse from the linked voucher's same-product movements, never
        // from the description or an arbitrary location on that voucher.
        var ordinaryChargeIds = entries.Where(e => !e.JournalVoucher!.IsStockTransfer && e.Quantity == 0 && e.Debit > 0)
            .Select(e => e.JournalVoucherId).Distinct().ToList();
        var ordinaryChargeReferences = await context.JournalVoucherReferences.AsNoTracking()
            .Where(r => ordinaryChargeIds.Contains(r.ReferencedVoucherId) &&
                (r.ReferenceType == "Cartage" || r.ReferenceType == "ForCharges"))
            .Select(r => new { r.ReferencedVoucherId, r.MainVoucherId }).ToListAsync();
        var chargeParents = ordinaryChargeReferences.GroupBy(r => r.ReferencedVoucherId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var originalLocations = entries.Where(e => e.Quantity > 0 && (e.Debit > 0 || e.Credit > 0))
            .GroupBy(e => (e.JournalVoucherId, e.AccountId))
            .ToDictionary(g => g.Key, g => g.Select(e => Warehouse(e.JournalEntryId)).Distinct().ToList());

        var pools = new Dictionary<(int Product, int? Location), InventoryPool>();
        var dispatches = new Dictionary<(int Voucher, int Product), InventoryMovement>();
        var completed = new HashSet<(int Voucher, int Product)>();
        var movements = new List<InventoryMovement>();
        var intakes = new Dictionary<(int Voucher, int Product), List<InventoryMovement>>();
        foreach (var entry in entries)
        {
            var location = Warehouse(entry.JournalEntryId);
            var transfer = entry.JournalVoucher!.IsStockTransfer;
            if (!transfer && entry.Quantity == 0 && entry.Debit > 0 &&
                !byEntry.ContainsKey(entry.JournalEntryId) &&
                chargeParents.TryGetValue(entry.JournalVoucherId, out var originalRefs) && originalRefs.Count == 1 &&
                originalLocations.TryGetValue((originalRefs[0].MainVoucherId, entry.AccountId), out var originalWarehouses) &&
                originalWarehouses.Count == 1)
                location = originalWarehouses[0];
            incoming.TryGetValue(entry.JournalVoucherId, out var parents);
            if (transfer && parents is { Count: > 1 })
                throw new InventoryIntegrityException(entry.JournalVoucherId, "has multiple outcome references");
            var parent = parents?.SingleOrDefault();
            var isCharge = transfer && parent?.ReferenceType == StockTransferService.ChargeReferenceType;
            if (isCharge)
            {
                if (entry.Quantity != 0)
                    throw new InventoryIntegrityException(entry.JournalVoucherId, "is a receiving charge that moves quantity");
                receiptLocations.TryGetValue((parent!.MainVoucherId, entry.AccountId), out location);
            }
            var key = (entry.AccountId, location);
            if (!pools.TryGetValue(key, out var pool)) pools[key] = pool = new InventoryPool();
            var movement = new InventoryMovement(entry, location);

            if (entry.Debit > 0)
            {
                var value = entry.Debit;
                InventoryMovement? cancellation = null;
                if (transfer && entry.Quantity > 0)
                {
                    if (parent?.ReferenceType is not (StockTransferService.ReceiptReferenceType or
                        StockTransferService.CancellationReferenceType) ||
                        !dispatches.TryGetValue((parent.MainVoucherId, entry.AccountId), out var dispatch) ||
                        dispatch.Entry.Quantity != entry.Quantity ||
                        !completed.Add((parent.MainVoucherId, entry.AccountId)))
                        throw new InventoryIntegrityException(entry.JournalVoucherId, "has an outcome that does not match one complete dispatched product");
                    if (parent.ReferenceType == StockTransferService.CancellationReferenceType && location != dispatch.LocationId)
                        throw new InventoryIntegrityException(entry.JournalVoucherId, "is a cancellation that does not return to the source warehouse");
                    // Cancellation restores carrying value; receipt adopts its declared value.
                    if (parent.ReferenceType == StockTransferService.CancellationReferenceType)
                    {
                        cancellation = dispatch;
                        dispatch.IsCancelled = true;
                        // Undo shortage settlements too: purchases made while this
                        // transfer was pending become real source stock on cancellation.
                        value = Money(-dispatch.ValueChange + dispatch.SettlementVariance);
                    }
                    dispatch.OutcomeDate = entry.JournalVoucher.EntryDate;
                }
                movement.QuantityChange = entry.Quantity;
                if (entry.Quantity > 0)
                {
                    var returnedQuantity = 0;
                    var returnedValue = 0m;
                    if (cancellation != null)
                    {
                        foreach (var deficit in pool.Deficits.Where(d => d.Owner == cancellation).ToList())
                        {
                            returnedQuantity += deficit.Quantity;
                            returnedValue += deficit.Value;
                            pool.Deficits.Remove(deficit);
                        }
                        pool.Quantity += returnedQuantity;
                        pool.Value = Money(pool.Value + returnedValue);
                    }
                    var incomingQuantity = entry.Quantity - returnedQuantity;
                    var incomingValue = Money(value - returnedValue);
                    var settledValue = SettleDeficits(pool, movement, incomingQuantity, incomingValue);
                    movement.ValueChange = Money(returnedValue + settledValue);
                    pool.Quantity += incomingQuantity;
                    pool.Value = Money(pool.Value + settledValue);
                    if (value > 0) pool.LastRate = value / entry.Quantity;
                    var intakeKey = (entry.JournalVoucherId, entry.AccountId);
                    if (!intakes.TryGetValue(intakeKey, out var productIntakes))
                        intakes[intakeKey] = productIntakes = [];
                    productIntakes.Add(movement);
                }
                else
                {
                    // Charges allocated to receipts which filled a shortage are
                    // part of that settlement, rather than cost on zero stock.
                    var chargeParent = isCharge ? parent!.MainVoucherId :
                        chargeParents.TryGetValue(entry.JournalVoucherId, out var chargeRefs) && chargeRefs.Count == 1
                            ? chargeRefs[0].MainVoucherId : (int?)null;
                    if (chargeParent.HasValue && intakes.TryGetValue((chargeParent.Value, entry.AccountId), out var originals))
                    {
                        var sameWarehouse = originals.Where(m => m.LocationId == location).ToList();
                        var quantity = sameWarehouse.Sum(m => m.Entry.Quantity);
                        var settlements = sameWarehouse.SelectMany(m => m.Settlements)
                            .Where(s => !s.Owner.IsCancelled).ToList();
                        if (quantity > 0)
                        {
                            var coveredCharge = Money(value * settlements.Sum(s => s.Quantity) / quantity);
                            var remainingCharge = coveredCharge;
                            var remainingWeight = settlements.Sum(s => s.Quantity);
                            foreach (var settlement in settlements)
                            {
                                var share = settlement.Quantity == remainingWeight ? remainingCharge :
                                    Money(remainingCharge * settlement.Quantity / remainingWeight);
                                settlement.Owner.SettlementVariance = Money(settlement.Owner.SettlementVariance + share);
                                remainingCharge -= share;
                                remainingWeight -= settlement.Quantity;
                            }
                            value = Money(value - coveredCharge);
                            pool.LastRate += entry.Debit / quantity;
                        }
                    }
                    movement.ValueChange = value;
                    pool.Value = Money(pool.Value + value);
                }
                if (pool.Quantity > 0 && pool.Value >= 0) pool.LastRate = pool.Value / pool.Quantity;
            }
            else if (entry.Credit > 0)
            {
                var available = Math.Max(0, pool.Quantity);
                var cost = pool.Quantity > 0 && pool.Value >= 0
                    ? CarryingValue(pool.Quantity, pool.Value, entry.Quantity)
                    : Money((pool.LastRate > 0 ? pool.LastRate : transfer && entry.Quantity > 0
                        ? entry.Credit / entry.Quantity : 0) * entry.Quantity);
                // Missing/ambiguous source assignments cannot be repriced reliably.
                // Preserve their posted transfer value in the auditable unassigned pool.
                if (transfer && location == null) cost = entry.Credit;
                if (transfer)
                {
                    if (parent != null || entry.Quantity <= 0 || !dispatches.TryAdd(
                        (entry.JournalVoucherId, entry.AccountId), movement))
                        throw new InventoryIntegrityException(entry.JournalVoucherId, "has an invalid dispatch product line");
                    movement.IsDispatch = true;
                }
                else movement.SaleCost = cost;
                movement.QuantityChange = -entry.Quantity;
                movement.ValueChange = -cost;
                var shortage = Math.Max(0, entry.Quantity - available);
                if (shortage > 0)
                {
                    var availableCost = available > 0 ? pool.Value : 0;
                    pool.Deficits.Add(new InventoryDeficit(movement, shortage, Money(cost - availableCost)));
                }
                pool.Quantity -= entry.Quantity;
                pool.Value = Money(pool.Value - cost);
                if (entry.Quantity > 0 && cost > 0) pool.LastRate = cost / entry.Quantity;
            }
            movement.CostRate = pool.LastRate;
            movements.Add(movement);
        }
        return new InventoryCostResult(movements);
    }

    public static decimal Money(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
    public static decimal CarryingValue(int availableQuantity, decimal availableValue, int quantity) =>
        quantity >= availableQuantity ? Money(availableValue + availableValue / availableQuantity * (quantity - availableQuantity))
            : Money(availableValue / availableQuantity * quantity);

    private static decimal SettleDeficits(InventoryPool pool, InventoryMovement intake, int quantity, decimal value)
    {
        var remainingQuantity = quantity;
        var remainingValue = value;
        var provisionalValue = 0m;
        foreach (var deficit in pool.Deficits.ToList())
        {
            if (remainingQuantity == 0) break;
            var covered = Math.Min(deficit.Quantity, remainingQuantity);
            var actual = covered == remainingQuantity ? remainingValue : Money(remainingValue * covered / remainingQuantity);
            var provisional = covered == deficit.Quantity ? deficit.Value : Money(deficit.Value * covered / deficit.Quantity);
            deficit.Owner.SettlementVariance = Money(deficit.Owner.SettlementVariance + actual - provisional);
            intake.Settlements.Add(new InventoryDeficitSettlement(deficit.Owner, covered));
            remainingQuantity -= covered;
            remainingValue = Money(remainingValue - actual);
            provisionalValue = Money(provisionalValue + provisional);
            deficit.Quantity -= covered;
            deficit.Value = Money(deficit.Value - provisional);
            if (deficit.Quantity == 0) pool.Deficits.Remove(deficit);
        }
        return Money(remainingValue + provisionalValue);
    }

    private sealed class InventoryPool
    {
        public int Quantity;
        public decimal Value;
        public decimal LastRate;
        public List<InventoryDeficit> Deficits = [];
    }
}

internal sealed class InventoryMovement(JournalEntry entry, int? locationId)
{
    public JournalEntry Entry { get; } = entry;
    public int? LocationId { get; } = locationId;
    public int QuantityChange { get; set; }
    public decimal ValueChange { get; set; }
    public decimal SaleCost { get; set; }
    public bool IsDispatch { get; set; }
    public DateOnly? OutcomeDate { get; set; }
    public decimal CostRate { get; set; }
    public decimal SettlementVariance { get; set; }
    public bool IsCancelled { get; set; }
    public List<InventoryDeficitSettlement> Settlements { get; } = [];
}

internal sealed class InventoryDeficit(InventoryMovement owner, int quantity, decimal value)
{
    public InventoryMovement Owner { get; } = owner;
    public int Quantity { get; set; } = quantity;
    public decimal Value { get; set; } = value;
}

internal sealed record InventoryDeficitSettlement(InventoryMovement Owner, int Quantity);

internal sealed class InventoryCostResult(List<InventoryMovement> movements)
{
    public List<InventoryMovement> Movements { get; } = movements;
    private readonly Dictionary<int, List<InventoryMovement>> _products = movements
        .GroupBy(m => m.Entry.AccountId).ToDictionary(g => g.Key, g => g.ToList());

    public InventoryPosition Position(int productId, int? locationId = null, DateOnly? beforeDate = null,
        int? beforeVoucherId = null)
    {
        var history = _products.GetValueOrDefault(productId) ?? [];
        var selected = history.Where(m => (!locationId.HasValue || m.LocationId == locationId) &&
            (!beforeDate.HasValue || m.Entry.JournalVoucher!.EntryDate < beforeDate ||
             beforeVoucherId.HasValue && m.Entry.JournalVoucher!.EntryDate == beforeDate &&
             m.Entry.JournalVoucherId < beforeVoucherId)).ToList();
        var quantity = selected.Sum(m => m.QuantityChange);
        var value = selected.Sum(m => m.ValueChange);
        var transit = !locationId.HasValue ? selected.Where(m => m.IsDispatch &&
            (!m.OutcomeDate.HasValue || beforeDate.HasValue && m.OutcomeDate >= beforeDate)).ToList() : [];
        var transitQty = transit.Sum(m => m.Entry.Quantity);
        // Keep net quantities/values, but a shortage must not distort the unit
        // cost of stock actually available in another warehouse.
        var warehouses = selected.GroupBy(m => m.LocationId)
            .Select(g => new { Quantity = g.Sum(m => m.QuantityChange), Value = g.Sum(m => m.ValueChange) }).ToList();
        var positive = warehouses.Where(w => w.Quantity > 0).ToList();
        var rate = quantity != 0 ? value / quantity : 0m;
        if (!locationId.HasValue && warehouses.Any(w => w.Quantity < 0))
        {
            var availableQuantity = positive.Sum(w => w.Quantity);
            rate = availableQuantity > 0 ? positive.Sum(w => w.Value) / availableQuantity : 0m;
        }
        var lastRate = selected.LastOrDefault()?.CostRate ?? 0;
        var suggestedRate = quantity > 0 && value > 0 ? value / quantity : lastRate;
        return new InventoryPosition(quantity, value, Math.Max(0, rate), transitQty, suggestedRate,
            transitQty != 0 || warehouses.Any(w => w.Quantity != 0));
    }
}

internal sealed record InventoryPosition(int Quantity, decimal Value, decimal AverageRate,
    int TransitQuantity, decimal SuggestedRate, bool HasStockPosition);
