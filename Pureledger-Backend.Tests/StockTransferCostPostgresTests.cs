using Microsoft.EntityFrameworkCore;
using PureLedger.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories;
using PureLedger.Services;
using PureLedger.ViewModels.Reporting;
using PureLedger.ViewModels.StockTransfer;
using Xunit;

namespace PureLedger.Backend.Tests;

[CollectionDefinition("PostgreSQL integration", DisableParallelization = true)]
public sealed class PostgresIntegrationCollection { }

[Collection("PostgreSQL integration")]
public sealed class StockTransferCostPostgresTests
{
    [PostgresFact]
    public async Task ProductCartageWithoutChallan_IsIncludedInWarehouseCostAndAutomaticTransferPrice()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var context = fixture.Context();
        var original = await context.JournalEntries.SingleAsync(e => e.AccountId == fixture.ProductId && e.Quantity == 3);
        var charge = new JournalVoucher { ProjectId = fixture.ProjectId, EntryDate = fixture.Date.AddDays(-1),
            CreditableDate = fixture.Date.AddDays(-1), IsCompleted = true };
        context.JournalVouchers.Add(charge); await context.SaveChangesAsync();
        context.JournalEntries.AddRange(
            new JournalEntry { JournalVoucherId = charge.JournalVoucherId, AccountId = fixture.ProductId,
                CategoryId = original.CategoryId, Debit = 10, IsProcessed = true },
            new JournalEntry { JournalVoucherId = charge.JournalVoucherId, AccountId = fixture.ClearingId,
                CategoryId = original.CategoryId, Credit = 10, IsProcessed = true });
        context.JournalVoucherReferences.Add(new JournalVoucherReference { MainVoucherId = original.JournalVoucherId,
            ReferencedVoucherId = charge.JournalVoucherId, ReferenceType = "Cartage" });
        await context.SaveChangesAsync();
        var reportRequest = fixture.ReportRequest(); reportRequest.LocationId = fixture.SourceId.ToString();
        var source = Assert.Single((await fixture.Reporting(context).GetProductReportAsync(reportRequest)).ProductSummaries);
        Assert.Equal((3, 110m, 110m / 3, 110m), (source.QtyBalance, source.InventoryValue, source.AverageRate, source.TotalDebit));
        var request = fixture.Request(3); request.Items[0].UnitCost = null;
        await fixture.Transfers(context).CreateAsync(request, 0, true);
        var dispatch = await context.JournalEntries.Include(e => e.JournalVoucher)
            .SingleAsync(e => e.AccountId == fixture.ProductId && e.JournalVoucher!.IsStockTransfer && e.Credit > 0);
        Assert.Equal(110m, dispatch.Credit);
        var pending = Assert.Single((await fixture.Reporting(context).GetProductReportAsync(fixture.ReportRequest())).ProductSummaries);
        Assert.Equal((0, 3, 0m, 0m), (pending.QtyBalance, pending.InTransitQuantity, pending.InventoryValue, pending.AverageRate));
    }

    [PostgresFact]
    public async Task ConcurrentDispatches_CanOverdrawStock_AfterRetryingSerializationConflicts()
    {
        await using var fixture = await Fixture.CreateAsync();
        async Task<Exception?> DispatchAsync() {
            await using var context = fixture.Context();
            try { await fixture.Transfers(context).CreateAsync(fixture.Request(2), 0, true); return null; }
            catch (Exception ex) { return ex; }
        }
        var results = await Task.WhenAll(DispatchAsync(), DispatchAsync());
        Assert.Contains(results, x => x == null);
        foreach (var failure in results.Where(x => x != null))
        {
            Assert.IsType<StockTransferConflictException>(failure);
            Assert.Null(await DispatchAsync());
        }
        await using var verify = fixture.Context();
        Assert.Equal(2, await verify.JournalVouchers.CountAsync(x => x.ProjectId == fixture.ProjectId && x.IsStockTransfer));
        var product = Assert.Single((await fixture.Reporting(verify).GetProductReportAsync(fixture.ReportRequest())).ProductSummaries);
        Assert.Equal((3, 0, -1, 4, -33.3333m), (product.TotalDebitQty, product.TotalCreditQty,
            product.QtyBalance, product.InTransitQuantity, product.InventoryValue));
    }

    [PostgresFact]
    public async Task FractionalFullTransfer_ReceiptChargeAndInvalidChargeRollback_ReconcileExactValues()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var context = fixture.Context();
        var transfers = fixture.Transfers(context);
        await transfers.CreateAsync(fixture.Request(3), 0, true);
        var root = await context.JournalVouchers.AsNoTracking().SingleAsync(x => x.ProjectId == fixture.ProjectId && x.IsStockTransfer);
        Assert.Equal(100m, await context.JournalEntries.Where(x => x.JournalVoucherId == root.JournalVoucherId && x.AccountId == fixture.ProductId).SumAsync(x => x.Credit));
        context.ChangeTracker.Clear();
        var receipt = new ReceiveStockTransferRequest { ExpectedRowVersion = Convert.ToBase64String(root.RowVersion!),
            ReceiptDate = fixture.Date.AddDays(1), SelectedProductAccountIds = [fixture.ProductId],
            Charges = [new() { PayeeAccountId = fixture.ClearingId, Amount = 1, TargetType = "Products", AllocationMethod = "Manual",
                Allocations = [new() { ProductAccountId = fixture.ProductId, Amount = 0.9999m }] }] };
        await Assert.ThrowsAsync<StockTransferValidationException>(() => transfers.ReceiveAsync(root.JournalVoucherId, receipt, 0, true));
        context.ChangeTracker.Clear();
        Assert.Equal(1, await context.JournalVouchers.CountAsync(x => x.ProjectId == fixture.ProjectId && x.IsStockTransfer));
        Assert.Empty(await context.JournalVoucherReferences.Where(x => x.MainVoucherId == root.JournalVoucherId).ToListAsync());
        receipt.Charges[0].Allocations[0].Amount = 1;
        await transfers.ReceiveAsync(root.JournalVoucherId, receipt, 0, true);
        context.ChangeTracker.Clear();
        var all = Assert.Single((await fixture.Reporting(context).GetProductReportAsync(fixture.ReportRequest())).ProductSummaries);
        Assert.Equal((3, 0, 3, 101m, 0m, 101m), (all.TotalDebitQty, all.TotalCreditQty,
            all.QtyBalance, all.InventoryValue, all.COGS, all.TotalDebit));
        foreach (var id in await context.JournalVouchers.Where(x => x.ProjectId == fixture.ProjectId && x.IsStockTransfer).Select(x => x.JournalVoucherId).ToListAsync())
            Assert.Equal(await context.JournalEntries.Where(x => x.JournalVoucherId == id).SumAsync(x => x.Debit),
                await context.JournalEntries.Where(x => x.JournalVoucherId == id).SumAsync(x => x.Credit));
        var breakdown = await fixture.Reporting(context).GetProductStockBreakdownAsync(fixture.ProjectId, fixture.ProductId, fixture.Date.AddDays(1));
        Assert.Equal((3, 0, 0), (breakdown.TotalQuantity, breakdown.InTransitQuantity, breakdown.ReconciliationDifference));
    }

    [PostgresTheory]
    [InlineData(20)]
    [InlineData(50)]
    public async Task EnteredPrice_RevaluesReceivedStockInPostgresWithoutResidualSourceValue(decimal price)
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var context = fixture.Context();
        var request = fixture.Request(3); request.Items[0].UnitCost = price;
        var transfers = fixture.Transfers(context);
        await transfers.CreateAsync(request, 0, true);
        var pendingRequest = fixture.ReportRequest(); pendingRequest.EndDate = fixture.Date;
        var pending = Assert.Single((await fixture.Reporting(context).GetProductReportAsync(pendingRequest)).ProductSummaries);
        Assert.Equal((0, 3, 0m, 0m), (pending.QtyBalance, pending.InTransitQuantity,
            pending.InventoryValue, pending.AverageRate));
        var root = await context.JournalVouchers.AsNoTracking().SingleAsync(x => x.ProjectId == fixture.ProjectId && x.IsStockTransfer);
        context.ChangeTracker.Clear();
        await transfers.ReceiveAsync(root.JournalVoucherId, new ReceiveStockTransferRequest {
            ExpectedRowVersion = Convert.ToBase64String(root.RowVersion!), ReceiptDate = fixture.Date.AddDays(1),
            SelectedProductAccountIds = [fixture.ProductId]
        }, 0, true);
        var all = Assert.Single((await fixture.Reporting(context).GetProductReportAsync(fixture.ReportRequest())).ProductSummaries);
        Assert.Equal((3, 0, 3, price * 3, price, 100m, 0m), (all.TotalDebitQty, all.TotalCreditQty,
            all.QtyBalance, all.InventoryValue, all.AverageRate, all.TotalDebit, all.COGS));
        var sourceRequest = fixture.ReportRequest(); sourceRequest.LocationId = fixture.SourceId.ToString();
        var source = Assert.Single((await fixture.Reporting(context).GetProductReportAsync(sourceRequest)).ProductSummaries);
        Assert.Equal((0, 0m, 0m, 100m, price * 3, 0m, 0m),
            (source.QtyBalance, source.InventoryValue, source.AverageRate, source.TotalDebit,
             source.TotalCredit, source.COGS, source.Margin));
        var destinationRequest = fixture.ReportRequest(); destinationRequest.LocationId = fixture.DestinationId.ToString();
        var destination = Assert.Single((await fixture.Reporting(context).GetProductReportAsync(destinationRequest)).ProductSummaries);
        Assert.Equal((price * 3, price, price * 3, 0m, 0m),
            (destination.InventoryValue, destination.AverageRate, destination.TotalDebit,
             destination.TotalCredit, destination.Margin));
    }

    [PostgresTheory]
    [InlineData(20)]
    [InlineData(50)]
    public async Task OverdrawReceiptChargeAndReplenishment_KeepDestinationCOGSAndSourceValueCorrect(decimal transferPrice)
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var context = fixture.Context();
        var request = fixture.Request(4); request.Items[0].UnitCost = transferPrice;
        await fixture.Transfers(context).CreateAsync(request, 0, true);
        var root = await context.JournalVouchers.AsNoTracking().SingleAsync(v => v.ProjectId == fixture.ProjectId && v.IsStockTransfer);
        context.ChangeTracker.Clear();
        await fixture.Transfers(context).ReceiveAsync(root.JournalVoucherId, new() {
            ExpectedRowVersion = Convert.ToBase64String(root.RowVersion!), ReceiptDate = fixture.Date.AddDays(1),
            SelectedProductAccountIds = [fixture.ProductId], Charges = [new() { PayeeAccountId = fixture.ClearingId, Amount = 8, TargetType = "Products" }]
        }, 0, true);
        var before = Assert.Single((await fixture.Reporting(context).GetProductReportAsync(fixture.ReportRequest())).ProductSummaries);
        Assert.Equal((3, transferPrice + 2), (before.QtyBalance, before.AverageRate));
        await fixture.TradeAsync(context, fixture.SourceId, 1, 80, 0);
        await fixture.TradeAsync(context, fixture.DestinationId, 1, 0, 100);
        var sourceRequest = fixture.ReportRequest(); sourceRequest.LocationId = fixture.SourceId.ToString();
        var source = Assert.Single((await fixture.Reporting(context).GetProductReportAsync(sourceRequest)).ProductSummaries);
        Assert.Equal((0, 0m), (source.QtyBalance, source.InventoryValue));
        var all = Assert.Single((await fixture.Reporting(context).GetProductReportAsync(fixture.ReportRequest())).ProductSummaries);
        Assert.Equal((3, 3 * (transferPrice + 2), transferPrice + 2, 100 - transferPrice - 2, 4, 1),
            (all.QtyBalance, all.InventoryValue, all.COGS, all.Margin, all.TotalDebitQty, all.TotalCreditQty));
    }

    [PostgresFact]
    public async Task OverdrawCancellation_AfterReplenishment_RestoresRealPurchaseValue()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var context = fixture.Context();
        var request = fixture.Request(4); request.Items[0].UnitCost = 50;
        await fixture.Transfers(context).CreateAsync(request, 0, true);
        var root = await context.JournalVouchers.AsNoTracking().SingleAsync(v => v.ProjectId == fixture.ProjectId && v.IsStockTransfer);
        await fixture.TradeAsync(context, fixture.SourceId, 2, 160, 0);
        context.ChangeTracker.Clear();
        await fixture.Transfers(context).CancelRemainingAsync(root.JournalVoucherId, new() { ExpectedRowVersion = Convert.ToBase64String(root.RowVersion!) }, 0, true);
        var report = fixture.ReportRequest(); report.EndDate = BusinessClock.Today;
        var all = Assert.Single((await fixture.Reporting(context).GetProductReportAsync(report)).ProductSummaries);
        Assert.Equal((5, 260m, 0m, 0), (all.QtyBalance, all.InventoryValue, all.COGS, all.InTransitQuantity));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public int ProjectId, ProductId, ClearingId, SourceId, DestinationId;
        public DateOnly Date = BusinessClock.Today.AddDays(-2);
        public LedgerDbContext Context() => new(new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("PURELEDGER_TEST_POSTGRES")!).Options);
        public StockTransferService Transfers(LedgerDbContext context) => new(context, NullLogger<StockTransferService>.Instance);
        public ReportingService Reporting(LedgerDbContext context) => new(new UnitOfWork(context), new ApprovalGuardService(context), new PaymentReceiptClassifier(context), context);
        public ProductReportRequest ReportRequest() => new() { ProjectId = ProjectId.ToString(), AccountId = ProductId.ToString(), EndDate = Date.AddDays(1) };
        public CreateStockTransferRequest Request(int qty) => new() { ProjectId = ProjectId, SourceLocationId = SourceId,
            DestinationLocationId = DestinationId, TransferDate = Date,
            Items = [new() { ProductAccountId = ProductId, Quantity = qty, UnitCost = 33.3333m }] };
        public async Task TradeAsync(LedgerDbContext context, int location, int quantity, decimal debit, decimal credit) {
            var product = await context.Accounts.AsNoTracking().SingleAsync(a => a.AccountId == ProductId);
            var voucher = new JournalVoucher { ProjectId = ProjectId, EntryDate = Date.AddDays(1), CreditableDate = Date.AddDays(2), IsCompleted = true };
            context.JournalVouchers.Add(voucher); await context.SaveChangesAsync();
            var entry = new JournalEntry { JournalVoucherId = voucher.JournalVoucherId, AccountId = ProductId,
                CategoryId = product.CategoryId, Quantity = quantity, Debit = debit, Credit = credit, IsProcessed = true };
            context.JournalEntries.AddRange(entry, new JournalEntry { JournalVoucherId = voucher.JournalVoucherId,
                AccountId = ClearingId, CategoryId = product.CategoryId, Debit = credit, Credit = debit, IsProcessed = true });
            var dc = new DeliveryChallanVoucher { JournalVoucherId = voucher.JournalVoucherId, DCDate = voucher.EntryDate, IsCompleted = true };
            context.DeliveryChallanVouchers.Add(dc); await context.SaveChangesAsync();
            context.DeliveryChallanEntries.Add(new() { DeliveryChallanVoucherId = dc.DeliveryChallanVoucherId,
                JournalEntryId = entry.JournalEntryId, LocationId = location, EntryDate = voucher.EntryDate });
            await context.SaveChangesAsync();
        }
        public static async Task<Fixture> CreateAsync() {
            var fixture = new Fixture();
            await using var context = fixture.Context();
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var project = new Project { ProjectName = $"Cost test {suffix}" };
            var source = new Location { LocationName = $"Source {suffix}" };
            var destination = new Location { LocationName = $"Dest {suffix}" };
            context.Projects.Add(project); context.Locations.AddRange(source, destination); await context.SaveChangesAsync();
            (fixture.ProjectId, fixture.SourceId, fixture.DestinationId) = (project.ProjectId, source.LocationId, destination.LocationId);
            var category = new Category { ProjectId = project.ProjectId, AccountTypeId = 1, CategoryName = "Cost test" };
            context.Categories.Add(category); await context.SaveChangesAsync();
            var product = new Account { CategoryId = category.CategoryId, AccountGroupId = 2, AccountName = "Cost product", IsEnabled = true };
            var clearing = new Account { CategoryId = category.CategoryId, AccountGroupId = 1, AccountName = "Clearing", IsEnabled = true };
            var transit = new Account { CategoryId = category.CategoryId, AccountGroupId = 1, AccountName = "Transit", IsEnabled = true };
            context.Accounts.AddRange(product, clearing, transit); await context.SaveChangesAsync();
            (fixture.ProductId, fixture.ClearingId) = (product.AccountId, clearing.AccountId);
            context.AppSettings.AddRange(new AppSetting { SettingKey = $"StockTransfer:{project.ProjectId}:InTransitCategoryId", SettingValue = category.CategoryId.ToString() },
                new AppSetting { SettingKey = $"StockTransfer:{project.ProjectId}:InTransitAccountId", SettingValue = transit.AccountId.ToString() });
            var voucher = new JournalVoucher { ProjectId = project.ProjectId, EntryDate = fixture.Date.AddDays(-1), CreditableDate = fixture.Date, IsCompleted = true };
            context.JournalVouchers.Add(voucher); await context.SaveChangesAsync();
            var entry = new JournalEntry { JournalVoucherId = voucher.JournalVoucherId, AccountId = product.AccountId, CategoryId = category.CategoryId,
                Quantity = 3, Debit = 100, Rate = 33.3333m, IsProcessed = true };
            context.JournalEntries.AddRange(entry, new JournalEntry { JournalVoucherId = voucher.JournalVoucherId, AccountId = clearing.AccountId,
                CategoryId = category.CategoryId, Credit = 100, IsProcessed = true });
            var dc = new DeliveryChallanVoucher { JournalVoucherId = voucher.JournalVoucherId, DCDate = voucher.EntryDate, IsCompleted = true };
            context.DeliveryChallanVouchers.Add(dc); await context.SaveChangesAsync();
            context.DeliveryChallanEntries.Add(new DeliveryChallanEntry { DeliveryChallanVoucherId = dc.DeliveryChallanVoucherId,
                JournalEntryId = entry.JournalEntryId, LocationId = source.LocationId, EntryDate = voucher.EntryDate });
            await context.SaveChangesAsync(); return fixture;
        }
        public async ValueTask DisposeAsync() {
            await using var context = Context();
            await context.AppSettings.Where(x => x.SettingKey.StartsWith($"StockTransfer:{ProjectId}:" )).ExecuteDeleteAsync();
            await context.JournalVoucherReferences.Where(x => x.MainVoucher.ProjectId == ProjectId || x.ReferencedVoucher.ProjectId == ProjectId).ExecuteDeleteAsync();
            await context.DeliveryChallanEntries.Where(x => x.DeliveryChallanVoucher.JournalVoucher.ProjectId == ProjectId).ExecuteDeleteAsync();
            await context.DeliveryChallanVouchers.Where(x => x.JournalVoucher.ProjectId == ProjectId).ExecuteDeleteAsync();
            await context.JournalEntries.Where(x => x.JournalVoucher!.ProjectId == ProjectId).ExecuteDeleteAsync();
            await context.JournalVouchers.Where(x => x.ProjectId == ProjectId).ExecuteDeleteAsync();
            await context.Accounts.Where(x => x.Category!.ProjectId == ProjectId).ExecuteDeleteAsync();
            await context.Categories.Where(x => x.ProjectId == ProjectId).ExecuteDeleteAsync();
            await context.Projects.Where(x => x.ProjectId == ProjectId).ExecuteDeleteAsync();
            await context.Locations.Where(x => x.LocationId == SourceId || x.LocationId == DestinationId).ExecuteDeleteAsync();
        }
    }
}
