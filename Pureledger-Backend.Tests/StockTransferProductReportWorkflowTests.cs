using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PureLedger.Helpers;
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

public sealed partial class StockTransferProductReportWorkflowTests
{
    [Fact]
    public async Task PurchaseCartage_EntersTransferSuggestion_AndReceivingExpenseFeedsDestinationSale()
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-6);
        await SeedAsync(context, date);
        var charge = new JournalVoucher { ProjectId = 1, EntryDate = date, CreditableDate = date, IsCompleted = true };
        context.JournalVouchers.Add(charge);
        await context.SaveChangesAsync();
        context.JournalEntries.AddRange(
            new JournalEntry { JournalVoucherId = charge.JournalVoucherId, AccountId = 100, CategoryId = 10, Debit = 20, IsProcessed = true },
            new JournalEntry { JournalVoucherId = charge.JournalVoucherId, AccountId = 300, CategoryId = 20, Credit = 20, IsProcessed = true });
        context.JournalVoucherReferences.Add(new JournalVoucherReference {
            MainVoucherId = 1000, ReferencedVoucherId = charge.JournalVoucherId, ReferenceType = "Cartage"
        });
        await context.SaveChangesAsync();
        await AddTradeAsync(context, date.AddDays(1), 1, 2, 0, 40);
        var transfers = CreateTransfers(context);
        var request = Request(date.AddDays(2), (100, 4, 0)); request.Items[0].UnitCost = null;
        await transfers.CreateAsync(request, 0, true);
        var rootId = await RootIdAsync(context);
        Assert.Equal(48m, await context.JournalEntries.Where(e => e.JournalVoucherId == rootId && e.AccountId == 100).Select(e => e.Credit).SingleAsync());
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(2)), 100, 4, 4, 48m);
        await transfers.ReceiveAsync(rootId, new ReceiveStockTransferRequest {
            ExpectedRowVersion = await VersionAsync(context, rootId), ReceiptDate = date.AddDays(3),
            SelectedProductAccountIds = [100], Charges = [
                new() { PayeeAccountId = 300, Amount = 8, TargetType = "Products" },
                new() { PayeeAccountId = 300, Amount = 7, TargetType = "OtherAccount", TargetAccountId = 301 }
            ]
        }, 0, true);
        await AddTradeAsync(context, date.AddDays(4), 2, 2, 0, 40);
        var report = await ReportAsync(CreateReporting(context), date.AddDays(4));
        var product = report.ProductSummaries.Single(p => p.ProductId == 100);
        Assert.Equal((6, 76m, 52m, 28m, 128m, 80m), (product.QtyBalance,
            product.InventoryValue, product.COGS, product.Margin, product.TotalDebit, product.TotalCredit));
        var destination = (await ReportAsync(CreateReporting(context), date.AddDays(4), 2)).ProductSummaries.Single(p => p.ProductId == 100);
        Assert.Equal((2, 28m, 14m, 28m, 56m), (destination.QtyBalance, destination.InventoryValue,
            destination.AverageRate, destination.COGS, destination.TotalDebit));
    }

    [Fact]
    public async Task EditPartialReceiptChargeCancellationAndDelete_ReconcileQuantityAndValueAtEachDate()
    {
        await using var context = CreateContext();
        var purchaseDate = BusinessClock.Today.AddDays(-4);
        var dispatchDate = purchaseDate.AddDays(1);
        var receiptDate = dispatchDate.AddDays(1);
        await SeedAsync(context, purchaseDate);
        var transfers = CreateTransfers(context);
        var reporting = CreateReporting(context);

        await transfers.CreateAsync(Request(dispatchDate, (100, 4, 10m), (101, 2, 20m)), 0, true);
        var rootId = await RootIdAsync(context);
        AssertPosition(await ReportAsync(reporting, dispatchDate), 100, 6, 4, 60m);
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
        Assert.Equal((10, 0, 5m, 0m, 0m),
            (a.TotalDebitQty, a.TotalCreditQty, a.TotalDebit - 100m, a.TotalCredit, a.COGS));
        Assert.Equal((15, 0, 14), (received.Totals.TotalDebitQty,
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
        var final = await ReportAsync(reporting, BusinessClock.Today);
        AssertPosition(final, 100, 10, 0, 105m);
        AssertPosition(final, 101, 5, 0, 100m);
        Assert.Equal(15, final.Totals.BalanceQuantity);
        Assert.Equal(5, (await reporting.GetProductStockBreakdownAsync(1, 101, BusinessClock.Today))
            .Locations.Single(x => x.LocationId == 1).Quantity);

        var receiptId = await context.JournalVoucherReferences.AsNoTracking()
            .Where(x => x.MainVoucherId == rootId && x.ReferenceType == StockTransferService.ReceiptReferenceType)
            .Select(x => x.ReferencedVoucherId).SingleAsync();
        context.ChangeTracker.Clear();
        await transfers.DeleteAsync(receiptId, new DeleteStockTransferRequest {
            ExpectedRowVersion = await VersionAsync(context, rootId), Reason = "Workflow test cleanup"
        }, 0, true);
        var afterDelete = await ReportAsync(reporting, BusinessClock.Today);
        AssertPosition(afterDelete, 100, 10, 0, 100m);
        AssertPosition(afterDelete, 101, 5, 0, 100m);
        Assert.Equal(0, (await reporting.GetProductStockBreakdownAsync(1, 100, receiptDate)).InTransitQuantity);
        Assert.Equal(0, (await reporting.GetProductStockBreakdownAsync(1, 101, dispatchDate)).InTransitQuantity);
    }

    [Fact]
    public async Task InvalidOrStaleTransferActions_LeaveReportAndTransitUnchanged()
    {
        await using var context = CreateContext();
        var purchaseDate = BusinessClock.Today.AddDays(-4);
        var dispatchDate = purchaseDate.AddDays(1);
        await SeedAsync(context, purchaseDate);
        var transfers = CreateTransfers(context);
        var reporting = CreateReporting(context);

        await Assert.ThrowsAsync<StockTransferValidationException>(() => transfers.CreateAsync(
            Request(dispatchDate, (100, 0, 10m)), 0, true));
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
    public async Task List_BatchLoadedProjectionsMatchPerTransferDetails_AndPageWithTotals()
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-4);
        await SeedAsync(context, date);
        var transfers = CreateTransfers(context);
        await transfers.CreateAsync(Request(date.AddDays(1), (100, 3, 10m)), 0, true);                // stays pending
        await transfers.CreateAsync(Request(date.AddDays(1), (100, 2, 10m)), 0, true);                // will be received
        await transfers.CreateAsync(Request(date.AddDays(1), (101, 2, 20m)), 0, true);                // will be cancelled
        context.ChangeTracker.Clear();
        var rootIds = await context.JournalVouchers
            .Where(x => x.IsStockTransfer && !x.IncomingReferences.Any())
            .OrderBy(x => x.JournalVoucherId).Select(x => x.JournalVoucherId).ToListAsync();
        Assert.Equal(3, rootIds.Count);
        var received = rootIds[1];
        var cancelled = rootIds[2];
        await transfers.ReceiveAsync(received, new ReceiveStockTransferRequest {
            ExpectedRowVersion = await VersionAsync(context, received), ReceiptDate = date.AddDays(2),
            SelectedProductAccountIds = [100] }, 0, true);
        context.ChangeTracker.Clear();
        await transfers.CancelRemainingAsync(cancelled, new StockTransferVersionRequest {
            ExpectedRowVersion = await VersionAsync(context, cancelled), Reason = "Not needed" }, 0, true);
        context.ChangeTracker.Clear();

        static JsonElement Json(object value) => JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement;
        var list = Json(await transfers.GetListAsync(new StockTransferListRequest(), 0, true));
        var items = list.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(3, list.GetProperty("totalCount").GetInt32());
        Assert.Equal(rootIds.OrderByDescending(x => x), items.Select(i => i.GetProperty("id").GetInt32()));

        // Every listed row equals the single-transfer projection.
        foreach (var item in items)
        {
            var id = item.GetProperty("id").GetInt32();
            var detail = Json((await transfers.GetDetailsAsync(id, 0, true))!);
            foreach (var field in new[] { "Status", "dispatchedQuantity", "receivedQuantity", "cancelledQuantity",
                         "pendingQuantity", "dispatchedValue", "pendingValue", "TransferNumber" })
                Assert.Equal(detail.GetProperty(field).ToString(), item.GetProperty(field).ToString());
            Assert.Equal(detail.GetProperty("items").GetArrayLength(), item.GetProperty("items").GetArrayLength());
        }
        var totals = list.GetProperty("totals");
        Assert.Equal(7, totals.GetProperty("dispatchedQuantity").GetInt32());
        Assert.Equal(2, totals.GetProperty("receivedQuantity").GetInt32());
        Assert.Equal(2, totals.GetProperty("cancelledQuantity").GetInt32());
        Assert.Equal(3, totals.GetProperty("pendingQuantity").GetInt32());

        var open = Json(await transfers.GetListAsync(new StockTransferListRequest { StatusGroup = "Open" }, 0, true));
        Assert.Equal(new[] { rootIds[0] }, open.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()));
        Assert.Equal(3, open.GetProperty("totals").GetProperty("pendingQuantity").GetInt32());

        var paged = Json(await transfers.GetListAsync(new StockTransferListRequest { Page = 1, PageSize = 1 }, 0, true));
        Assert.Equal(3, paged.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, paged.GetProperty("items").GetArrayLength());
        Assert.True(paged.GetProperty("hasMoreData").GetBoolean());
        Assert.Equal(7, paged.GetProperty("totals").GetProperty("dispatchedQuantity").GetInt32());
    }

    [Fact]
    public async Task MultiProductReceipt_AllocatesChargesToEachProductWithoutChangingQuantity()
    {
        await using var context = CreateContext();
        var purchaseDate = BusinessClock.Today.AddDays(-4);
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
    public async Task BaseVoucherStock_RemainsExcluded_WhenTransferExceedsEligibleStock()
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-2);
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
        await CreateTransfers(context).CreateAsync(Request(date.AddDays(1), (100, 11, 10m)), 0, true);
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(1)), 100, -1, 11, -10m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task NonPositiveTransferCost_IsRejectedBeforePosting(decimal suppliedCost)
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-3);
        await SeedAsync(context, date);
        await Assert.ThrowsAsync<StockTransferValidationException>(() => CreateTransfers(context)
            .CreateAsync(Request(date.AddDays(1), (100, 2, suppliedCost)), 0, true));
        Assert.Empty(await context.JournalVouchers.Where(x => x.IsStockTransfer).ToListAsync());
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(1)), 100, 10, 0, 100m);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(1000)]
    public async Task LowerAndHigherTransferPrices_RevalueDestinationAndItsSalesWithoutChangingSourceCost(decimal price)
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-4);
        await SeedAsync(context, date);
        var transfers = CreateTransfers(context);
        var reporting = CreateReporting(context);
        await transfers.CreateAsync(Request(date.AddDays(1), (100, 2, price)), 0, true);
        var rootId = await RootIdAsync(context);
        var entry = await context.JournalEntries.SingleAsync(x => x.JournalVoucherId == rootId && x.AccountId == 100);
        Assert.Equal((price, price * 2), (entry.Rate, entry.Credit));
        AssertPosition(await ReportAsync(reporting, date.AddDays(1)), 100, 8, 2, 80m);
        context.ChangeTracker.Clear();
        await transfers.ReceiveAsync(rootId, new ReceiveStockTransferRequest {
            ExpectedRowVersion = await VersionAsync(context, rootId), ReceiptDate = date.AddDays(2), SelectedProductAccountIds = [100]
        }, 0, true);
        AssertPosition(await ReportAsync(reporting, date.AddDays(2), 1), 100, 8, 0, 80m);
        AssertPosition(await ReportAsync(reporting, date.AddDays(2), 2), 100, 2, 0, 2 * price);
        var received = (await ReportAsync(reporting, date.AddDays(2))).ProductSummaries.Single(x => x.ProductId == 100);
        Assert.Equal((10, 0, 100m, 0m, 0m), (received.TotalDebitQty, received.TotalCreditQty,
            received.TotalDebit, received.TotalCredit, received.COGS));
        Assert.Equal(80 + price * 2, received.InventoryValue);
        context.ChangeTracker.Clear();
        await AddTradeAsync(context, date.AddDays(3), 2, 1, 0, 20);
        var sold = (await ReportAsync(reporting, date.AddDays(3))).ProductSummaries.Single(x => x.ProductId == 100);
        Assert.Equal((9, 80 + price, price, 20 - price, 10, 1), (sold.QtyBalance, sold.InventoryValue,
            sold.COGS, sold.Margin, sold.TotalDebitQty, sold.TotalCreditQty));
        AssertPosition(await ReportAsync(reporting, date.AddDays(3), 1), 100, 8, 0, 80m);
        var availability = System.Text.Json.JsonSerializer.SerializeToElement(await transfers.GetAvailabilityAsync(1, 2, 100,
            date.AddDays(3), null, 0, true));
        Assert.Equal(price, availability.GetProperty("suggestedUnitCost").GetDecimal());
    }

    [Theory]
    [InlineData(5)]
    [InlineData(15)]
    public async Task CancellingOverriddenTransfer_RestoresOriginalSourceValue(decimal price)
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-3);
        await SeedAsync(context, date);
        var transfers = CreateTransfers(context);
        await transfers.CreateAsync(Request(date.AddDays(1), (100, 2, price)), 0, true);
        var rootId = await RootIdAsync(context);
        context.ChangeTracker.Clear();
        await transfers.CancelRemainingAsync(rootId, new StockTransferVersionRequest {
            ExpectedRowVersion = await VersionAsync(context, rootId), Reason = "Cancelled override"
        }, 0, true);
        var result = await ReportAsync(CreateReporting(context), BusinessClock.Today);
        AssertPosition(result, 100, 10, 0, 100m);
        Assert.Equal((10, 0, 0m), (result.ProductSummaries[0].TotalDebitQty, result.ProductSummaries[0].TotalCreditQty, result.ProductSummaries[0].COGS));
    }

    [Fact]
    public async Task AutomaticCost_AfterSaleAndLaterPurchase_DepletesSourceWithoutResidualValue()
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-5);
        await SeedAsync(context, date);
        await AddTradeAsync(context, date.AddDays(1), 1, 5, 0, 100);
        await AddTradeAsync(context, date.AddDays(2), 1, 5, 150, 0);
        var transfers = CreateTransfers(context);
        var request = Request(date.AddDays(3), (100, 10, 20m));
        request.Items[0].UnitCost = null; // The server chooses cost when omitted.
        await transfers.CreateAsync(request, 0, true);
        var rootId = await RootIdAsync(context);
        var sourceEntry = await context.JournalEntries.AsNoTracking().SingleAsync(x => x.JournalVoucherId == rootId && x.AccountId == 100);
        Assert.Equal((20m, 200m), (sourceEntry.Rate, sourceEntry.Credit));
        var report = await ReportAsync(CreateReporting(context), date.AddDays(3));
        AssertPosition(report, 100, 0, 10, 0m);
        Assert.Equal((50m, 50m, 15, 5), (report.ProductSummaries[0].COGS,
            report.ProductSummaries[0].Margin, report.ProductSummaries[0].TotalDebitQty, report.ProductSummaries[0].TotalCreditQty));
        context.ChangeTracker.Clear();
        await transfers.ReceiveAsync(rootId, new ReceiveStockTransferRequest {
            ExpectedRowVersion = await VersionAsync(context, rootId), ReceiptDate = date.AddDays(4), SelectedProductAccountIds = [100]
        }, 0, true);
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(4), 1), 100, 0, 0, 0m);
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(4), 2), 100, 10, 0, 200m);
    }

    [Fact]
    public async Task FractionalCarryingCost_FullTransferUsesExactRemainingValue()
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-3);
        await SeedAsync(context, date);
        var purchase = await context.JournalEntries.SingleAsync(x => x.JournalEntryId == 10000);
        purchase.Quantity = 3;
        purchase.Rate = 33.3333m;
        await context.SaveChangesAsync();
        var transfers = CreateTransfers(context);
        await transfers.CreateAsync(Request(date.AddDays(1), (100, 3, 33.3333m)), 0, true);
        var rootId = await RootIdAsync(context);
        Assert.Equal(100m, (await context.JournalEntries.SingleAsync(x => x.JournalVoucherId == rootId && x.AccountId == 100)).Credit);
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(1)), 100, 0, 3, 0m);
        context.ChangeTracker.Clear();
        await transfers.ReceiveAsync(rootId, new ReceiveStockTransferRequest {
            ExpectedRowVersion = await VersionAsync(context, rootId), ReceiptDate = date.AddDays(2), SelectedProductAccountIds = [100]
        }, 0, true);
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(2)), 100, 3, 0, 100m);
        Assert.All(await context.JournalVouchers.Where(x => x.IsStockTransfer).Select(x => x.JournalVoucherId).ToListAsync(), id =>
            Assert.Equal(context.JournalEntries.Where(x => x.JournalVoucherId == id).Sum(x => x.Debit),
                context.JournalEntries.Where(x => x.JournalVoucherId == id).Sum(x => x.Credit)));
    }

    [Fact]
    public async Task BackdatedDispatch_CanExceedStockWithoutChangingLaterSaleClassification()
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-4);
        await SeedAsync(context, date);
        await AddTradeAsync(context, date.AddDays(2), 1, 8, 0, 160);
        await CreateTransfers(context).CreateAsync(Request(date.AddDays(1), (100, 3, 10m)), 0, true);
        var report = await ReportAsync(CreateReporting(context), date.AddDays(3));
        AssertPosition(report, 100, -1, 3, -10m);
        Assert.Equal((10, 8, 80m, 80m), (report.ProductSummaries[0].TotalDebitQty,
            report.ProductSummaries[0].TotalCreditQty, report.ProductSummaries[0].COGS, report.ProductSummaries[0].Margin));
    }

    [Fact]
    public async Task PendingEdit_UsesOriginalSameDayPostingSlotInsteadOfLaterPurchaseCost()
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-3);
        await SeedAsync(context, date);
        var transfers = CreateTransfers(context);
        await transfers.CreateAsync(Request(date.AddDays(1), (100, 2, 10m)), 0, true);
        var rootId = await RootIdAsync(context);
        context.ChangeTracker.Clear();
        await AddTradeAsync(context, date.AddDays(1), 1, 10, 300, 0);
        context.ChangeTracker.Clear();
        var availability = System.Text.Json.JsonSerializer.SerializeToElement(await transfers.GetAvailabilityAsync(
            1, 1, 100, date.AddDays(1), rootId, 0, true));
        Assert.Equal(10m, availability.GetProperty("suggestedUnitCost").GetDecimal());
        await transfers.EditAsync(rootId, new EditStockTransferRequest {
            ProjectId = 1, SourceLocationId = 1, DestinationLocationId = 2, TransferDate = date.AddDays(1),
            ExpectedRowVersion = await VersionAsync(context, rootId),
            Items = [new() { ProductAccountId = 100, Quantity = 3, UnitCost = 10 }]
        }, 0, true);
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(1)), 100, 17, 3, 370m);
    }

    [Theory]
    [InlineData("Equal", 0.0002, 0.0003)]
    [InlineData("ByQuantity", 0.0003, 0.0002)]
    [InlineData("ByRate", 0.0002, 0.0003)]
    [InlineData("ByValue", 0.0002, 0.0003)]
    public async Task TinyReceivingCharges_AllocateExactlyWithoutLosingValue(string method, decimal a, decimal b)
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-3);
        await SeedAsync(context, date);
        var transfers = CreateTransfers(context);
        await transfers.CreateAsync(Request(date.AddDays(1), (100, 4, 10m), (101, 2, 20m)), 0, true);
        var rootId = await RootIdAsync(context);
        context.ChangeTracker.Clear();
        await transfers.ReceiveAsync(rootId, new ReceiveStockTransferRequest {
            ExpectedRowVersion = await VersionAsync(context, rootId), ReceiptDate = date.AddDays(2),
            SelectedProductAccountIds = [100, 101], Charges = [new() {
                PayeeAccountId = 300, Amount = 0.0005m, AllocationMethod = method, TargetType = "Products" }]
        }, 0, true);
        var report = await ReportAsync(CreateReporting(context), date.AddDays(2));
        AssertPosition(report, 100, 10, 0, 100m + a);
        AssertPosition(report, 101, 5, 0, 100m + b);
        Assert.Equal(200.0005m, report.ProductSummaries.Sum(x => x.InventoryValue));
        Assert.Equal((15, 0), (report.Totals.TotalDebitQty, report.Totals.TotalCreditQty));
    }

    [Fact]
    public async Task MultiHopTransfer_CarriesCapitalizedChargesIntoNextWarehouseAndSaleCOGS()
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-5);
        await SeedAsync(context, date);
        context.Locations.Add(new Location { LocationId = 3, LocationName = "Third" });
        await context.SaveChangesAsync();
        var transfers = CreateTransfers(context);
        await transfers.CreateAsync(Request(date.AddDays(1), (100, 4, 15m)), 0, true);
        var first = await RootIdAsync(context);
        context.ChangeTracker.Clear();
        await transfers.ReceiveAsync(first, new ReceiveStockTransferRequest {
            ExpectedRowVersion = await VersionAsync(context, first), ReceiptDate = date.AddDays(2), SelectedProductAccountIds = [100],
            Charges = [new() { PayeeAccountId = 300, Amount = 8, TargetType = "Products" }]
        }, 0, true);
        context.ChangeTracker.Clear();
        var secondRequest = Request(date.AddDays(3), (100, 3, 17m));
        secondRequest.SourceLocationId = 2; secondRequest.DestinationLocationId = 3;
        await transfers.CreateAsync(secondRequest, 0, true);
        var second = await context.JournalVouchers.Where(x => x.IsStockTransfer && !x.IncomingReferences.Any() && x.JournalVoucherId != first)
            .Select(x => x.JournalVoucherId).SingleAsync();
        context.ChangeTracker.Clear();
        await transfers.ReceiveAsync(second, new ReceiveStockTransferRequest {
            ExpectedRowVersion = await VersionAsync(context, second), ReceiptDate = date.AddDays(4), SelectedProductAccountIds = [100]
        }, 0, true);
        context.ChangeTracker.Clear();
        await AddTradeAsync(context, date.AddDays(4), 3, 2, 0, 40);
        var reporting = CreateReporting(context);
        var all = (await ReportAsync(reporting, date.AddDays(4))).ProductSummaries.Single(x => x.ProductId == 100);
        Assert.Equal((8, 94m, 34m, 6m, 10, 2), (all.QtyBalance, all.InventoryValue, all.COGS,
            all.Margin, all.TotalDebitQty, all.TotalCreditQty));
        AssertPosition(await ReportAsync(reporting, date.AddDays(4), 1), 100, 6, 0, 60m);
        AssertPosition(await ReportAsync(reporting, date.AddDays(4), 2), 100, 1, 0, 17m);
        AssertPosition(await ReportAsync(reporting, date.AddDays(4), 3), 100, 1, 0, 17m);
        var availability = System.Text.Json.JsonSerializer.SerializeToElement(await transfers.GetAvailabilityAsync(
            1, 3, 100, date.AddDays(4), null, 0, true));
        Assert.Equal((1, 17m, 17m), (availability.GetProperty("availableQuantity").GetInt32(),
            availability.GetProperty("inventoryValue").GetDecimal(), availability.GetProperty("suggestedUnitCost").GetDecimal()));
    }

    [Fact]
    public async Task DeletingReceivedStockAlreadySold_IsRejectedWithoutChangingReport()
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-4);
        await SeedAsync(context, date);
        var transfers = CreateTransfers(context);
        await transfers.CreateAsync(Request(date.AddDays(1), (100, 4, 10m)), 0, true);
        var rootId = await RootIdAsync(context);
        context.ChangeTracker.Clear();
        await transfers.ReceiveAsync(rootId, new ReceiveStockTransferRequest {
            ExpectedRowVersion = await VersionAsync(context, rootId), ReceiptDate = date.AddDays(2), SelectedProductAccountIds = [100]
        }, 0, true);
        context.ChangeTracker.Clear();
        await AddTradeAsync(context, date.AddDays(3), 2, 3, 0, 60);
        context.ChangeTracker.Clear();
        var version = await VersionAsync(context, rootId);
        await Assert.ThrowsAsync<StockTransferConflictException>(() => transfers.DeleteAsync(rootId, new DeleteStockTransferRequest {
            ExpectedRowVersion = version, Reason = "Invalid deletion"
        }, 0, true));
        AssertPosition(await ReportAsync(CreateReporting(context), date.AddDays(3)), 100, 7, 0, 70m);
        Assert.Equal(2, await context.JournalVouchers.CountAsync(x => x.IsStockTransfer));
    }

    [Fact]
    public async Task PositiveQuantityWithUnusableCarryingCost_CannotPostATransfer()
    {
        await using var context = CreateContext();
        var date = BusinessClock.Today.AddDays(-3);
        await SeedAsync(context, date);
        var purchase = await context.JournalEntries.SingleAsync(x => x.JournalEntryId == 10000);
        purchase.Debit = 0.0001m; purchase.Rate = 0;
        await context.SaveChangesAsync();
        var request = Request(date.AddDays(1), (100, 1, 0)); request.Items[0].UnitCost = null;
        await Assert.ThrowsAsync<StockTransferValidationException>(() => CreateTransfers(context).CreateAsync(request, 0, true));
        Assert.Empty(await context.JournalVouchers.Where(x => x.IsStockTransfer).ToListAsync());
    }

    private static async Task AddTradeAsync(LedgerDbContext context, DateOnly date, int location, int quantity, decimal debit, decimal credit)
    {
        var voucher = new JournalVoucher { ProjectId = 1, EntryDate = date, CreditableDate = date.AddDays(1), IsCompleted = true };
        context.JournalVouchers.Add(voucher);
        await context.SaveChangesAsync();
        var product = new JournalEntry { JournalVoucherId = voucher.JournalVoucherId, AccountId = 100, CategoryId = 10,
            IsProcessed = true, Quantity = quantity, Debit = debit, Credit = credit };
        context.JournalEntries.Add(product);
        context.JournalEntries.Add(new JournalEntry { JournalVoucherId = voucher.JournalVoucherId, AccountId = 300,
            CategoryId = 20, IsProcessed = true, Debit = credit, Credit = debit });
        var challan = new DeliveryChallanVoucher { JournalVoucherId = voucher.JournalVoucherId, DCDate = date, IsCompleted = true };
        context.DeliveryChallanVouchers.Add(challan);
        await context.SaveChangesAsync();
        context.DeliveryChallanEntries.Add(new DeliveryChallanEntry { DeliveryChallanVoucherId = challan.DeliveryChallanVoucherId,
            JournalEntryId = product.JournalEntryId, LocationId = location, EntryDate = date });
        await context.SaveChangesAsync();
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
