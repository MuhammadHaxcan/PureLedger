using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories;
using PureLedger.Services;
using PureLedger.ViewModels.Reporting;
using PureLedger.ViewModels.StockTransfer;
using Xunit;

namespace PureLedger.Backend.Tests;

public sealed class StockTransferProductReportWorkflowTests
{
    [Fact]
    public async Task EditPartialReceiptChargeCancellationAndDelete_ReconcileQuantityAndValueAtEachDate()
    {
        await using var context = CreateContext();
        var purchaseDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-4);
        var dispatchDate = purchaseDate.AddDays(1);
        var receiptDate = dispatchDate.AddDays(1);
        await SeedAsync(context, purchaseDate);
        var transfers = CreateTransfers(context);
        var reporting = CreateReporting(context);

        await transfers.CreateAsync(Request(dispatchDate, (100, 4, 12m), (101, 2, 20m)), 0, true);
        var rootId = await RootIdAsync(context);
        AssertPosition(await ReportAsync(reporting, dispatchDate), 100, 6, 4, 52m);
        AssertPosition(await ReportAsync(reporting, dispatchDate), 101, 3, 2, 60m);
        Assert.Equal(10, (await reporting.GetProductStockBreakdownAsync(1, 100, dispatchDate)).TotalStockPosition);

        context.ChangeTracker.Clear(); // Each transfer action normally has a fresh request DbContext.
        await transfers.EditAsync(rootId, new EditStockTransferRequest {
            ProjectId = 1, SourceLocationId = 1, DestinationLocationId = 2,
            TransferDate = dispatchDate, ExpectedRowVersion = await VersionAsync(context, rootId),
            Items = [new() { ProductAccountId = 100, Quantity = 5, UnitCost = 10 },
                     new() { ProductAccountId = 101, Quantity = 1, UnitCost = 20 }]
        }, 0, true);
        AssertPosition(await ReportAsync(reporting, dispatchDate), 100, 5, 5, 50m);
        AssertPosition(await ReportAsync(reporting, dispatchDate), 101, 4, 1, 80m);
        Assert.Equal(5, Assert.Single((await reporting.GetProductStockBreakdownAsync(1, 100, dispatchDate)).Locations).Quantity);

        context.ChangeTracker.Clear();
        await transfers.ReceiveAsync(rootId, new ReceiveStockTransferRequest {
            ExpectedRowVersion = await VersionAsync(context, rootId), ReceiptDate = receiptDate,
            SelectedProductAccountIds = [100], Charges = [
                new() { PayeeAccountId = 300, Amount = 5, TargetType = "Products",
                    SelectedProductAccountIds = [100] },
                new() { PayeeAccountId = 300, Amount = 7, TargetType = "OtherAccount",
                    TargetAccountId = 301 }
            ]
        }, 0, true);
        AssertPosition(await ReportAsync(reporting, dispatchDate), 100, 5, 5, 50m);
        var received = await ReportAsync(reporting, receiptDate);
        AssertPosition(received, 100, 10, 0, 105m);
        AssertPosition(received, 101, 4, 1, 80m);
        var a = received.ProductSummaries.Single(x => x.ProductId == 100);
        Assert.Equal((15, 5, 5m, 0m, 0m),
            (a.TotalDebitQty, a.TotalCreditQty, a.TotalDebit - 100m, a.TotalCredit, a.COGS));
        Assert.Equal((20, 6, 14), (received.Totals.TotalDebitQty,
            received.Totals.TotalCreditQty, received.Totals.BalanceQuantity));
        AssertPosition(await ReportAsync(reporting, receiptDate, 1), 100, 5, 0, 50m);
        AssertPosition(await ReportAsync(reporting, receiptDate, 2), 100, 5, 0, 55m);
        var breakdown = await reporting.GetProductStockBreakdownAsync(1, 100, receiptDate);
        Assert.Equal((10, 0, 10, 0), (breakdown.TotalQuantity, breakdown.InTransitQuantity,
            breakdown.TotalStockPosition, breakdown.ReconciliationDifference));
        Assert.Equal(5, breakdown.Locations.Single(x => x.LocationId == 1).Quantity);
        Assert.Equal(5, breakdown.Locations.Single(x => x.LocationId == 2).Quantity);

        context.ChangeTracker.Clear();
        await transfers.CancelRemainingAsync(rootId, new StockTransferVersionRequest {
            ExpectedRowVersion = await VersionAsync(context, rootId), Reason = "Return pending line"
        }, 0, true);
        var final = await ReportAsync(reporting, DateOnly.FromDateTime(DateTime.Today));
        AssertPosition(final, 100, 10, 0, 105m);
        AssertPosition(final, 101, 5, 0, 100m);
        Assert.Equal(15, final.Totals.BalanceQuantity);
        Assert.Equal(5, (await reporting.GetProductStockBreakdownAsync(1, 101, DateOnly.FromDateTime(DateTime.Today)))
            .Locations.Single(x => x.LocationId == 1).Quantity);

        var receiptId = await context.JournalVoucherReferences.AsNoTracking()
            .Where(x => x.MainVoucherId == rootId && x.ReferenceType == StockTransferService.ReceiptReferenceType)
            .Select(x => x.ReferencedVoucherId).SingleAsync();
        context.ChangeTracker.Clear();
        await transfers.DeleteAsync(receiptId, new DeleteStockTransferRequest {
            ExpectedRowVersion = await VersionAsync(context, rootId), Reason = "Workflow test cleanup"
        }, 0, true);
        var afterDelete = await ReportAsync(reporting, DateOnly.FromDateTime(DateTime.Today));
        AssertPosition(afterDelete, 100, 10, 0, 100m);
        AssertPosition(afterDelete, 101, 5, 0, 100m);
        Assert.Equal(0, (await reporting.GetProductStockBreakdownAsync(1, 100, receiptDate)).InTransitQuantity);
        Assert.Equal(0, (await reporting.GetProductStockBreakdownAsync(1, 101, dispatchDate)).InTransitQuantity);
    }

    [Fact]
    public async Task InvalidOrStaleTransferActions_LeaveReportAndTransitUnchanged()
    {
        await using var context = CreateContext();
        var purchaseDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-4);
        var dispatchDate = purchaseDate.AddDays(1);
        await SeedAsync(context, purchaseDate);
        var transfers = CreateTransfers(context);
        var reporting = CreateReporting(context);

        await Assert.ThrowsAsync<StockTransferConflictException>(() => transfers.CreateAsync(
            Request(dispatchDate, (100, 11, 10m)), 0, true));
        await Assert.ThrowsAsync<StockTransferValidationException>(() => transfers.CreateAsync(
            Request(dispatchDate, (100, 1, 10m), (100, 1, 10m)), 0, true));
        AssertPosition(await ReportAsync(reporting, dispatchDate), 100, 10, 0, 100m);
        Assert.Empty(await context.JournalVouchers.Where(x => x.IsStockTransfer).ToListAsync());

        await transfers.CreateAsync(Request(dispatchDate, (100, 4, 10m)), 0, true);
        var rootId = await RootIdAsync(context);
        var originalVersion = await VersionAsync(context, rootId);
        context.ChangeTracker.Clear();
        await Assert.ThrowsAsync<StockTransferValidationException>(() => transfers.ReceiveAsync(rootId,
            new ReceiveStockTransferRequest { ExpectedRowVersion = originalVersion,
                ReceiptDate = dispatchDate.AddDays(-1), SelectedProductAccountIds = [100] }, 0, true));
        AssertPosition(await ReportAsync(reporting, dispatchDate), 100, 6, 4, 60m);

        context.ChangeTracker.Clear();
        await transfers.ReceiveAsync(rootId, new ReceiveStockTransferRequest {
            ExpectedRowVersion = originalVersion, ReceiptDate = dispatchDate.AddDays(1),
            SelectedProductAccountIds = [100]
        }, 0, true);
        context.ChangeTracker.Clear();
        await Assert.ThrowsAsync<StockTransferConflictException>(() => transfers.CancelRemainingAsync(rootId,
            new StockTransferVersionRequest { ExpectedRowVersion = originalVersion }, 0, true));
        var currentVersion = await VersionAsync(context, rootId);
        await Assert.ThrowsAsync<StockTransferValidationException>(() => transfers.ReceiveAsync(rootId,
            new ReceiveStockTransferRequest { ExpectedRowVersion = currentVersion,
                ReceiptDate = dispatchDate.AddDays(2), SelectedProductAccountIds = [100] }, 0, true));
        AssertPosition(await ReportAsync(reporting, dispatchDate.AddDays(2)), 100, 10, 0, 100m);
        Assert.Equal(4, Assert.Single((await reporting.GetProductStockBreakdownAsync(1, 100, dispatchDate))
            .InTransitRoutes).Quantity);
    }

    [Fact]
    public async Task MultiProductReceipt_AllocatesChargesToEachProductWithoutChangingQuantity()
    {
        await using var context = CreateContext();
        var purchaseDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-4);
        var dispatchDate = purchaseDate.AddDays(1);
        var receiptDate = dispatchDate.AddDays(1);
        await SeedAsync(context, purchaseDate);
        var transfers = CreateTransfers(context);
        var reporting = CreateReporting(context);

        await transfers.CreateAsync(Request(dispatchDate, (100, 4, 10m), (101, 2, 20m)), 0, true);
        var rootId = await RootIdAsync(context);
        context.ChangeTracker.Clear();
        await transfers.ReceiveAsync(rootId, new ReceiveStockTransferRequest {
            ExpectedRowVersion = await VersionAsync(context, rootId), ReceiptDate = receiptDate,
            SelectedProductAccountIds = [100, 101], Charges = [
                new() { PayeeAccountId = 300, Amount = 6, TargetType = "Products",
                    AllocationMethod = "ByQuantity", SelectedProductAccountIds = [100, 101] },
                new() { PayeeAccountId = 300, Amount = 3, TargetType = "Products",
                    AllocationMethod = "Manual", SelectedProductAccountIds = [100, 101],
                    Allocations = [new() { ProductAccountId = 100, Amount = 1 },
                                   new() { ProductAccountId = 101, Amount = 2 }] }
            ]
        }, 0, true);

        var report = await ReportAsync(reporting, receiptDate);
        AssertPosition(report, 100, 10, 0, 105m);
        AssertPosition(report, 101, 5, 0, 104m);
        Assert.Equal((15, 0, 209m), (report.Totals.BalanceQuantity,
            report.ProductSummaries.Sum(x => x.InTransitQuantity),
            report.ProductSummaries.Sum(x => x.InventoryValue)));
        AssertPosition(await ReportAsync(reporting, receiptDate, 2), 100, 4, 0, 45m);
        AssertPosition(await ReportAsync(reporting, receiptDate, 2), 101, 2, 0, 44m);
        Assert.Equal((10, 5), ((await reporting.GetProductStockBreakdownAsync(1, 100, receiptDate)).TotalQuantity,
            (await reporting.GetProductStockBreakdownAsync(1, 101, receiptDate)).TotalQuantity));
    }

    [Fact]
    public async Task BaseVoucherStock_ExcludedFromReport_CannotAuthorizeATransfer()
    {
        await using var context = CreateContext();
        var date = DateOnly.FromDateTime(DateTime.Today).AddDays(-2);
        await SeedAsync(context, date);
        context.JournalVouchers.Add(new JournalVoucher { JournalVoucherId = 1001, ProjectId = 1,
            EntryDate = date, CreditableDate = date, IsCompleted = true, IsBaseVoucher = true });
        context.JournalEntries.Add(new JournalEntry { JournalEntryId = 10010,
            JournalVoucherId = 1001, AccountId = 100, CategoryId = 10,
            IsProcessed = true, Quantity = 5, Debit = 50, Rate = 10 });
        context.DeliveryChallanVouchers.Add(new DeliveryChallanVoucher {
            DeliveryChallanVoucherId = 3001, JournalVoucherId = 1001,
            DCDate = date, IsCompleted = true });
        context.DeliveryChallanEntries.Add(new DeliveryChallanEntry {
            DeliveryChallanVoucherId = 3001, JournalEntryId = 10010,
            LocationId = 1, EntryDate = date });
        await context.SaveChangesAsync();

        AssertPosition(await ReportAsync(CreateReporting(context), date), 100, 10, 0, 100m);
        await Assert.ThrowsAsync<StockTransferConflictException>(() => CreateTransfers(context)
            .CreateAsync(Request(date.AddDays(1), (100, 11, 10m)), 0, true));
        Assert.Empty(await context.JournalVouchers.Where(x => x.IsStockTransfer).ToListAsync());
    }

    private static void AssertPosition(ProductReportViewModel report, int productId, int quantity,
        int transit, decimal value)
    {
        var product = report.ProductSummaries.Single(x => x.ProductId == productId);
        Assert.Equal((quantity, transit, value),
            (product.QtyBalance, product.InTransitQuantity, product.InventoryValue));
    }

    private static Task<ProductReportViewModel> ReportAsync(ReportingService service, DateOnly endDate,
        int? locationId = null) => service.GetProductReportAsync(new ProductReportRequest {
            ProjectId = "1", EndDate = endDate, LocationId = locationId?.ToString()
        });

    private static CreateStockTransferRequest Request(DateOnly date,
        params (int productId, int quantity, decimal cost)[] lines) => new() {
            ProjectId = 1, SourceLocationId = 1, DestinationLocationId = 2, TransferDate = date,
            Items = lines.Select(x => new CreateStockTransferItemRequest {
                ProductAccountId = x.productId, Quantity = x.quantity, UnitCost = x.cost
            }).ToList()
        };

    private static Task<int> RootIdAsync(LedgerDbContext context) => context.JournalVouchers
        .Where(x => x.IsStockTransfer && !x.IncomingReferences.Any())
        .Select(x => x.JournalVoucherId).SingleAsync();

    private static async Task<string> VersionAsync(LedgerDbContext context, int rootId) =>
        Convert.ToBase64String((await context.JournalVouchers.AsNoTracking()
            .SingleAsync(x => x.JournalVoucherId == rootId)).RowVersion!);

    private static StockTransferService CreateTransfers(LedgerDbContext context) =>
        new(context, NullLogger<StockTransferService>.Instance);

    private static ReportingService CreateReporting(LedgerDbContext context) =>
        new(new UnitOfWork(context), new ApprovalGuardService(context),
            new PaymentReceiptClassifier(context), context);

    private static async Task SeedAsync(LedgerDbContext context, DateOnly date)
    {
        context.Projects.Add(new Project { ProjectId = 1, ProjectName = "Main" });
        context.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "General" });
        context.AccountGroups.AddRange(new AccountGroup { AccountGroupId = 1, AccountGroupName = "Account" },
            new AccountGroup { AccountGroupId = 2, AccountGroupName = "Product" });
        context.Categories.AddRange(
            new Category { CategoryId = 10, ProjectId = 1, AccountTypeId = 1, CategoryName = "Products" },
            new Category { CategoryId = 20, ProjectId = 1, AccountTypeId = 1, CategoryName = "Accounts" });
        context.Accounts.AddRange(
            new Account { AccountId = 100, CategoryId = 10, AccountGroupId = 2, AccountName = "A", IsEnabled = true },
            new Account { AccountId = 101, CategoryId = 10, AccountGroupId = 2, AccountName = "B", IsEnabled = true },
            new Account { AccountId = 200, CategoryId = 20, AccountGroupId = 1, AccountName = "Transit", IsEnabled = true },
            new Account { AccountId = 300, CategoryId = 20, AccountGroupId = 1, AccountName = "Payee", IsEnabled = true },
            new Account { AccountId = 301, CategoryId = 20, AccountGroupId = 1, AccountName = "Expense", IsEnabled = true });
        context.Locations.AddRange(new Location { LocationId = 1, LocationName = "Source" },
            new Location { LocationId = 2, LocationName = "Destination" });
        context.AppSettings.AddRange(
            new AppSetting { SettingKey = "StockTransfer:1:InTransitCategoryId", SettingValue = "20" },
            new AppSetting { SettingKey = "StockTransfer:1:InTransitAccountId", SettingValue = "200" });
        context.JournalVouchers.Add(new JournalVoucher { JournalVoucherId = 1000, ProjectId = 1,
            EntryDate = date, CreditableDate = date.AddDays(1), IsCompleted = true });
        context.JournalEntries.AddRange(
            new JournalEntry { JournalEntryId = 10000, JournalVoucherId = 1000, AccountId = 100,
                CategoryId = 10, IsProcessed = true, Quantity = 10, Debit = 100, Rate = 10 },
            new JournalEntry { JournalEntryId = 10001, JournalVoucherId = 1000, AccountId = 101,
                CategoryId = 10, IsProcessed = true, Quantity = 5, Debit = 100, Rate = 20 },
            new JournalEntry { JournalEntryId = 10002, JournalVoucherId = 1000, AccountId = 300,
                CategoryId = 20, IsProcessed = true, Credit = 200 });
        context.DeliveryChallanVouchers.Add(new DeliveryChallanVoucher {
            DeliveryChallanVoucherId = 3000, JournalVoucherId = 1000, DCDate = date, IsCompleted = true });
        context.DeliveryChallanEntries.AddRange(
            new DeliveryChallanEntry { DeliveryChallanVoucherId = 3000, JournalEntryId = 10000,
                LocationId = 1, EntryDate = date },
            new DeliveryChallanEntry { DeliveryChallanVoucherId = 3000, JournalEntryId = 10001,
                LocationId = 1, EntryDate = date });
        await context.SaveChangesAsync();
    }

    private static LedgerDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase($"stock-transfer-report-workflow-{Guid.NewGuid():N}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        return new LedgerDbContext(options);
    }
}
