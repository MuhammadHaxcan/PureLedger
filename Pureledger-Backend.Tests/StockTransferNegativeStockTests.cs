using Microsoft.EntityFrameworkCore;
using PureLedger.Helpers;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Services;
using PureLedger.ViewModels.Reporting;
using PureLedger.ViewModels.StockTransfer;
using Xunit;

namespace PureLedger.Backend.Tests;

public sealed partial class StockTransferProductReportWorkflowTests
{
    private static async Task<int> OverdrawAsync(LedgerDbContext context, DateOnly date, int quantity = 15, decimal price = 15)
    {
        await CreateTransfers(context).CreateAsync(Request(date, (100, quantity, price)), 0, true);
        return await context.JournalVouchers.Where(v => v.IsStockTransfer && !v.IncomingReferences.Any())
            .OrderByDescending(v => v.JournalVoucherId).Select(v => v.JournalVoucherId).FirstAsync();
    }

    private static async Task ReceiveOverdrawAsync(LedgerDbContext context, int id, DateOnly date, decimal charge = 0)
    {
        context.ChangeTracker.Clear();
        await CreateTransfers(context).ReceiveAsync(id, new ReceiveStockTransferRequest {
            ExpectedRowVersion = await VersionAsync(context, id), ReceiptDate = date, SelectedProductAccountIds = [100],
            Charges = charge == 0 ? [] : [new() { PayeeAccountId = 300, Amount = charge, TargetType = "Products" }]
        }, 0, true);
    }

    [Fact]
    public async Task OverdrawnDispatch_ReceiptChargeAndSale_ReconcileStockAndCommercialColumns()
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-6);
        await SeedAsync(context, date);
        int id = await OverdrawAsync(context, date.AddDays(1));
        var reporting = CreateReporting(context);
        AssertPosition(await ReportAsync(reporting, date.AddDays(1)), 100, -5, 15, -50);
        await ReceiveOverdrawAsync(context, id, date.AddDays(2), 15);
        await AddTradeAsync(context, date.AddDays(3), 2, 3, 0, 90);
        var p = (await ReportAsync(reporting, date.AddDays(3))).ProductSummaries.Single(x => x.ProductId == 100);
        Assert.Equal((7, 142m, 16m, 48m, 42m, 10, 3, 115m, 90m),
            (p.QtyBalance, p.InventoryValue, p.AverageRate, p.COGS, p.Margin, p.TotalDebitQty, p.TotalCreditQty, p.TotalDebit, p.TotalCredit));
        var popup = await reporting.GetProductStockBreakdownAsync(1, 100, date.AddDays(3));
        Assert.Equal((7, 0, 0), (popup.TotalQuantity, popup.InTransitQuantity, popup.ReconciliationDifference));
        Assert.Equal(-5, popup.Locations.Single(x => x.LocationId == 1).Quantity);
        Assert.Equal(12, popup.Locations.Single(x => x.LocationId == 2).Quantity);
        // Later purchase settles the shortage, without rewriting the destination's sale.
        await AddTradeAsync(context, date.AddDays(4), 1, 5, 100, 0);
        AssertPosition(await ReportAsync(reporting, date.AddDays(4), 1), 100, 0, 0, 0);
        var later = (await ReportAsync(reporting, date.AddDays(4))).ProductSummaries.Single(x => x.ProductId == 100);
        Assert.Equal((12, 192m, 48m, 42m, 215m), (later.QtyBalance, later.InventoryValue, later.COGS, later.Margin, later.TotalDebit));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(20)]
    [InlineData(1000)]
    public async Task PurchaseClearingShortage_LeavesZeroSourceValue_AndNextPurchaseUsesActualCost(decimal rate)
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-6);
        await SeedAsync(context, date);
        await OverdrawAsync(context, date.AddDays(1));
        await AddTradeAsync(context, date.AddDays(2), 1, 5, 5 * rate, 0);
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(2), 1), 100, 0, 0, 0);
        await AddTradeAsync(context, date.AddDays(3), 1, 5, 5 * rate, 0);
        var source = (await ReportAsync(CreateReporting(context), date.AddDays(3), 1)).ProductSummaries.Single(x => x.ProductId == 100);
        Assert.Equal((5, 5 * rate, rate), (source.QtyBalance, source.InventoryValue, source.AverageRate));
        await AddTradeAsync(context, date.AddDays(4), 1, 5, 0, 10 * rate);
        source = (await ReportAsync(CreateReporting(context), date.AddDays(4), 1)).ProductSummaries.Single(x => x.ProductId == 100);
        Assert.Equal((0, 0m, 5 * rate, 5 * rate), (source.QtyBalance, source.InventoryValue, source.COGS, source.Margin));
    }

    [Fact]
    public async Task PartialAndExcessReplenishment_KeepRemainingDeficitProvisional_AndValueOnlyRealExcess()
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-4);
        await SeedAsync(context, date);
        await OverdrawAsync(context, date.AddDays(1));
        await AddTradeAsync(context, date.AddDays(2), 1, 3, 60, 0);
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(2), 1), 100, -2, 0, -20);
        await AddTradeAsync(context, date.AddDays(3), 1, 6, 180, 0);
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(3), 1), 100, 4, 0, 120);
    }

    [Fact]
    public async Task EmptySource_UsesEnteredPrice_AndNilFilterKeepsOffsettingWarehousePositions()
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-5);
        await SeedAsync(context, date);
        (await context.JournalEntries.SingleAsync(e => e.JournalEntryId == 10000)).IsProcessed = false;
        await context.SaveChangesAsync();
        var automatic = Request(date.AddDays(1), (100, 3, 0)); automatic.Items[0].UnitCost = null;
        await Assert.ThrowsAsync<Services.StockTransferValidationException>(() => CreateTransfers(context).CreateAsync(automatic, 0, true));
        var id = await OverdrawAsync(context, date.AddDays(1), 3, 15);
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(1), 1), 100, -3, 0, -45);
        await ReceiveOverdrawAsync(context, id, date.AddDays(2));
        var report = await CreateReporting(context).GetProductReportAsync(new() {
            ProjectId = "1", AccountId = "100", StartDate = date.AddDays(3), EndDate = date.AddDays(4), ExcludeNilProducts = true
        });
        var p = Assert.Single(report.ProductSummaries);
        Assert.Equal((0, 0m, 15m, 0, 0), (p.QtyBalance, p.InventoryValue, p.AverageRate, p.TotalDebitQty, p.TotalCreditQty));
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(2), 2), 100, 3, 0, 45);
    }

    [Fact]
    public async Task EmptyOrNegativeSourceWithHistory_SuggestsLastKnownCostWithoutDivisionByZero()
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-5);
        await SeedAsync(context, date);
        await AddTradeAsync(context, date.AddDays(1), 1, 10, 0, 200);
        var request = Request(date.AddDays(2), (100, 4, 0)); request.Items[0].UnitCost = null;
        await CreateTransfers(context).CreateAsync(request, 0, true);
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(2), 1), 100, -4, 0, -40);
        var suggestion = System.Text.Json.JsonSerializer.SerializeToElement(await CreateTransfers(context)
            .GetAvailabilityAsync(1, 1, 100, date.AddDays(2), null, 0, true));
        Assert.Equal(10m, suggestion.GetProperty("suggestedUnitCost").GetDecimal());
        await AddTradeAsync(context, date.AddDays(3), 1, 2, 40, 0);
        request.TransferDate = date.AddDays(4);
        await CreateTransfers(context).CreateAsync(request, 0, true);
        var latest = await context.JournalEntries.Where(e => e.AccountId == 100 && e.JournalVoucher!.IsStockTransfer)
            .OrderByDescending(e => e.JournalEntryId).FirstAsync();
        Assert.Equal((20m, 80m), (latest.Rate, latest.Credit));
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(4), 1), 100, -6, 0, -100);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 20)]
    [InlineData(5, 5)]
    [InlineData(5, 20)]
    [InlineData(8, 20)]
    public async Task OverdrawnCancellation_ReversesSettlementsAndRestoresAllRealPurchases(int quantity, decimal price)
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-4);
        await SeedAsync(context, date);
        int id = await OverdrawAsync(context, date.AddDays(1));
        if (quantity > 0) await AddTradeAsync(context, date.AddDays(2), 1, quantity, quantity * price, 0);
        context.ChangeTracker.Clear();
        await CreateTransfers(context).CancelRemainingAsync(id, new() { ExpectedRowVersion = await VersionAsync(context, id) }, 0, true);
        AssertPosition(await ReportAsync(CreateReporting(context), BusinessClock.Today, 1), 100, 10 + quantity, 0, 100 + quantity * price);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MultipleDeficitsAndCancellations_RestorePurchasesInEitherCancellationOrder(bool reverse)
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-5);
        await SeedAsync(context, date);
        int first = await OverdrawAsync(context, date.AddDays(1));
        int second = await OverdrawAsync(context, date.AddDays(1), 2, 30);
        await AddTradeAsync(context, date.AddDays(2), 1, 3, 60, 0);
        await AddTradeAsync(context, date.AddDays(3), 1, 6, 180, 0);
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(3), 1), 100, 2, 0, 60);
        foreach (int id in reverse ? new[] { second, first } : new[] { first, second }) {
            context.ChangeTracker.Clear();
            await CreateTransfers(context).CancelRemainingAsync(id, new() { ExpectedRowVersion = await VersionAsync(context, id) }, 0, true);
        }
        AssertPosition(await ReportAsync(CreateReporting(context), BusinessClock.Today, 1), 100, 19, 0, 340);
    }

    [Fact]
    public async Task CancellationWithAnotherUnfilledDeficit_RestoresOwnCostAndSettlesOnlyRemainingShortage()
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-3);
        await SeedAsync(context, date);
        int first = await OverdrawAsync(context, date.AddDays(1));
        await OverdrawAsync(context, date.AddDays(1), 2, 30);
        context.ChangeTracker.Clear();
        await CreateTransfers(context).CancelRemainingAsync(first, new() { ExpectedRowVersion = await VersionAsync(context, first) }, 0, true);
        AssertPosition(await ReportAsync(CreateReporting(context), BusinessClock.Today, 1), 100, 8, 0, 80);
    }

    [Fact]
    public async Task OversizedPendingEdit_PreservesOriginalPostingOrder_AndCanBeCancelled()
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-3);
        await SeedAsync(context, date);
        int id = await OverdrawAsync(context, date.AddDays(1));
        context.ChangeTracker.Clear();
        await CreateTransfers(context).EditAsync(id, new() {
            ProjectId = 1, SourceLocationId = 1, DestinationLocationId = 2, TransferDate = date.AddDays(1),
            ExpectedRowVersion = await VersionAsync(context, id), Items = [new() { ProductAccountId = 100, Quantity = 20, UnitCost = 30 }]
        }, 0, true);
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(1)), 100, -10, 20, -100);
        context.ChangeTracker.Clear();
        await CreateTransfers(context).CancelRemainingAsync(id, new() { ExpectedRowVersion = await VersionAsync(context, id) }, 0, true);
        AssertPosition(await ReportAsync(CreateReporting(context), BusinessClock.Today), 100, 10, 0, 100);
    }

    [Theory]
    [InlineData("Equal")]
    [InlineData("ByQuantity")]
    [InlineData("ByRate")]
    [InlineData("ByValue")]
    public async Task OverdrawnMultiProductReceipt_AllocatesProductChargesExactly_AndExcludesMonetaryExpense(string allocation)
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-3);
        await SeedAsync(context, date);
        await CreateTransfers(context).CreateAsync(Request(date.AddDays(1), (100, 15, 15), (101, 8, 30)), 0, true);
        int id = await RootIdAsync(context);
        context.ChangeTracker.Clear();
        await CreateTransfers(context).ReceiveAsync(id, new() {
            ExpectedRowVersion = await VersionAsync(context, id), ReceiptDate = date.AddDays(2), SelectedProductAccountIds = [100, 101],
            Charges = [new() { PayeeAccountId = 300, Amount = 1, TargetType = "Products", AllocationMethod = allocation },
                new() { PayeeAccountId = 300, Amount = 7, TargetType = "OtherAccount", TargetAccountId = 301 }]
        }, 0, true);
        var report = await ReportAsync(CreateReporting(context), date.AddDays(2));
        Assert.Equal((15, 0, 15), (report.Totals.TotalDebitQty, report.Totals.TotalCreditQty, report.Totals.BalanceQuantity));
        Assert.Equal(356m, report.ProductSummaries.Sum(p => p.InventoryValue));
        Assert.Equal(201m, report.Totals.TotalDebit);
        Assert.Equal(0m, report.Totals.TotalCOGS);
    }

    [Fact]
    public async Task ReceiptChargeOnStockReturnedIntoDeficit_DoesNotLeaveValueOnZeroStock()
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-5);
        await SeedAsync(context, date);
        int id = await OverdrawAsync(context, date.AddDays(1));
        await ReceiveOverdrawAsync(context, id, date.AddDays(2));
        var back = Request(date.AddDays(3), (100, 5, 15)); back.SourceLocationId = 2; back.DestinationLocationId = 1;
        await CreateTransfers(context).CreateAsync(back, 0, true);
        int backId = await context.JournalVouchers.Where(v => v.IsStockTransfer && !v.IncomingReferences.Any())
            .OrderByDescending(v => v.JournalVoucherId).Select(v => v.JournalVoucherId).FirstAsync();
        await ReceiveOverdrawAsync(context, backId, date.AddDays(4), 10);
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(4), 1), 100, 0, 0, 0);
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(4)), 100, 10, 0, 150);
    }

    [Fact]
    public async Task CorruptTransferGraph_FailsWithTheNamedVoucherInsteadOfAGenericError()
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-5);
        await SeedAsync(context, date);
        int id = await OverdrawAsync(context, date.AddDays(1));
        await ReceiveOverdrawAsync(context, id, date.AddDays(2));
        var receipt = await context.JournalVoucherReferences.SingleAsync(r =>
            r.MainVoucherId == id && r.ReferenceType == StockTransferService.ReceiptReferenceType);
        context.JournalVoucherReferences.Add(new JournalVoucherReference
        {
            MainVoucherId = id, ReferencedVoucherId = receipt.ReferencedVoucherId,
            ReferenceType = StockTransferService.CancellationReferenceType
        });
        await context.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<InventoryIntegrityException>(
            () => ReportAsync(CreateReporting(context), date.AddDays(4)));

        Assert.Equal(receipt.ReferencedVoucherId, error.JournalVoucherId);
        Assert.Contains($"JV {receipt.ReferencedVoucherId}", error.Message);
    }

    [Fact]
    public async Task TransferBackIntoDeficit_SettlesVarianceWithoutResidualCost()
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-5);
        await SeedAsync(context, date);
        int id = await OverdrawAsync(context, date.AddDays(1));
        await ReceiveOverdrawAsync(context, id, date.AddDays(2));
        var back = Request(date.AddDays(3), (100, 5, 15)); back.SourceLocationId = 2; back.DestinationLocationId = 1;
        await CreateTransfers(context).CreateAsync(back, 0, true);
        int backId = await context.JournalVouchers.Where(v => v.IsStockTransfer && !v.IncomingReferences.Any())
            .OrderByDescending(v => v.JournalVoucherId).Select(v => v.JournalVoucherId).FirstAsync();
        await ReceiveOverdrawAsync(context, backId, date.AddDays(4));
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(4), 1), 100, 0, 0, 0);
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(4)), 100, 10, 0, 150);
    }

    [Fact]
    public async Task ProductCartageOnPurchaseFillingDeficit_IsSettledAndRestoredOnCancellation()
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-4);
        await SeedAsync(context, date);
        int id = await OverdrawAsync(context, date.AddDays(1));
        await AddTradeAsync(context, date.AddDays(2), 1, 5, 100, 0);
        int purchase = await context.JournalVouchers.Where(v => !v.IsStockTransfer).MaxAsync(v => v.JournalVoucherId);
        var charge = new JournalVoucher { ProjectId = 1, EntryDate = date.AddDays(2), CreditableDate = date.AddDays(3), IsCompleted = true };
        context.JournalVouchers.Add(charge); await context.SaveChangesAsync();
        context.JournalEntries.AddRange(new JournalEntry { JournalVoucherId = charge.JournalVoucherId, AccountId = 100, CategoryId = 10, Debit = 10, IsProcessed = true },
            new JournalEntry { JournalVoucherId = charge.JournalVoucherId, AccountId = 300, CategoryId = 20, Credit = 10, IsProcessed = true });
        context.JournalVoucherReferences.Add(new() { MainVoucherId = purchase, ReferencedVoucherId = charge.JournalVoucherId, ReferenceType = "Cartage" });
        await context.SaveChangesAsync();
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(2), 1), 100, 0, 0, 0);
        context.ChangeTracker.Clear();
        await CreateTransfers(context).CancelRemainingAsync(id, new() { ExpectedRowVersion = await VersionAsync(context, id) }, 0, true);
        AssertPosition(await ReportAsync(CreateReporting(context), BusinessClock.Today, 1), 100, 15, 0, 210);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task OverdrawPolicy_StillRejectsNonPositiveQuantities(int quantity)
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-2);
        await SeedAsync(context, date);
        await Assert.ThrowsAsync<Services.StockTransferValidationException>(() => CreateTransfers(context).CreateAsync(Request(date, (100, quantity, 10)), 0, true));
        Assert.Empty(await context.JournalVouchers.Where(v => v.IsStockTransfer).ToListAsync());
    }

    [Theory]
    [InlineData(0.0001)]
    [InlineData(1)]
    [InlineData(100)]
    public async Task FractionalDeficit_ReplenishmentAndCancellation_DoNotLeaveRoundingResidue(decimal purchaseValue)
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-3);
        await SeedAsync(context, date);
        var purchase = await context.JournalEntries.SingleAsync(e => e.JournalEntryId == 10000);
        purchase.Quantity = 3; purchase.Rate = 33.3333m;
        await context.SaveChangesAsync();
        int id = await OverdrawAsync(context, date.AddDays(1), 4, 33.3333m);
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(1), 1), 100, -1, 0, -33.3333m);
        await AddTradeAsync(context, date.AddDays(2), 1, 1, purchaseValue, 0);
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(2), 1), 100, 0, 0, 0);
        context.ChangeTracker.Clear();
        await CreateTransfers(context).CancelRemainingAsync(id, new() { ExpectedRowVersion = await VersionAsync(context, id) }, 0, true);
        AssertPosition(await ReportAsync(CreateReporting(context), BusinessClock.Today, 1), 100, 4, 0, 100 + purchaseValue);
    }

    [Fact]
    public async Task ChargePostedAfterDeficitTransferCancellation_IsAddedToRestoredStock()
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-4);
        await SeedAsync(context, date);
        int id = await OverdrawAsync(context, date.AddDays(1));
        await AddTradeAsync(context, date.AddDays(2), 1, 5, 100, 0);
        int purchase = await context.JournalVouchers.Where(v => !v.IsStockTransfer).MaxAsync(v => v.JournalVoucherId);
        context.ChangeTracker.Clear();
        await CreateTransfers(context).CancelRemainingAsync(id, new() { ExpectedRowVersion = await VersionAsync(context, id) }, 0, true);
        var today = BusinessClock.Today;
        var charge = new JournalVoucher { ProjectId = 1, EntryDate = today, CreditableDate = today, IsCompleted = true };
        context.JournalVouchers.Add(charge); await context.SaveChangesAsync();
        context.JournalEntries.AddRange(new JournalEntry { JournalVoucherId = charge.JournalVoucherId, AccountId = 100, CategoryId = 10, Debit = 10, IsProcessed = true },
            new JournalEntry { JournalVoucherId = charge.JournalVoucherId, AccountId = 300, CategoryId = 20, Credit = 10, IsProcessed = true });
        context.JournalVoucherReferences.Add(new() { MainVoucherId = purchase, ReferencedVoucherId = charge.JournalVoucherId, ReferenceType = "Cartage" });
        await context.SaveChangesAsync();
        AssertPosition(await ReportAsync(CreateReporting(context), today, 1), 100, 15, 0, 210);
    }
}
