using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories;
using PureLedger.Services;
using PureLedger.ViewModels.Reporting;
using Xunit;

namespace PureLedger.Backend.Tests;

public sealed class ProductStockTransitReportTests
{
    private static readonly DateOnly PurchaseDate = new(2026, 1, 1);
    private static readonly DateOnly DispatchDate = new(2026, 1, 2);
    private static readonly DateOnly ReceiptDate = new(2026, 1, 4);
    private static readonly DateOnly CancellationDate = new(2026, 1, 5);

    [Fact]
    public async Task ProductWithoutTransfers_RetainsTheExistingPeriodAverageAndCOGS()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false,
            new(100, 10, 100, 0, 1), new(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, false,
            new(100, 5, 100, 0, 1), new(300, 0, 0, 100));
        AddVoucher(context, 3, ReceiptDate, false,
            new(100, 3, 0, 60, 1), new(300, 0, 60, 0));
        await context.SaveChangesAsync();

        var report = await ReportAsync(CreateService(context), startDate: DispatchDate,
            endDate: ReceiptDate, locationId: 1);
        var product = Assert.Single(report.ProductSummaries);
        Assert.Equal((10, 5, 3, 12), (product.PreviousBalance,
            product.TotalDebitQty, product.TotalCreditQty, product.QtyBalance));
        Assert.Equal(200m / 15m, product.AverageRate);
        Assert.Equal((100m, 60m), (product.TotalDebit, product.TotalCredit));
        Assert.InRange(product.COGS, 39.9999m, 40.0001m);
        Assert.InRange(product.Margin, 19.9999m, 20.0001m);
        Assert.InRange(product.InventoryValue, 159.9999m, 160.0001m);
        Assert.Equal((5, 3, 12), (report.Totals.TotalDebitQty,
            report.Totals.TotalCreditQty, report.Totals.BalanceQuantity));
    }

    [Fact]
    public async Task PendingDispatch_ReducesAvailableStockAtSourceButDoesNotCreateDestinationStockOrSales()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false,
            new(100, 10, 100, 0, 1), new(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, true,
            new(100, 4, 0, 40, 1), new(200, 0, 40, 0, 2));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var beforeDispatch = await ReportAsync(service, endDate: PurchaseDate);
        Assert.Equal(10, Assert.Single(beforeDispatch.ProductSummaries).QtyBalance);

        var report = await ReportAsync(service, startDate: DispatchDate, endDate: DispatchDate);
        var product = Assert.Single(report.ProductSummaries);
        Assert.Equal(10, product.PreviousBalance);
        Assert.Equal(0, product.TotalDebitQty);
        Assert.Equal(4, product.TotalCreditQty);
        Assert.Equal(4, product.TransferOutwardQuantity);
        Assert.Equal(0, product.TransferInwardQuantity);
        Assert.Equal(6, product.QtyBalance);
        Assert.Equal(4, product.InTransitQuantity);
        Assert.Equal(60, product.InventoryValue);
        Assert.Equal(10, product.AverageRate);
        Assert.Equal(0, product.TotalCredit);
        Assert.Equal(0, product.COGS);
        Assert.Equal(0, product.Margin);
        Assert.Equal(-4, report.Totals.NetQuantity);
        Assert.Equal(6, report.Totals.BalanceQuantity);

        var breakdown = await service.GetProductStockBreakdownAsync(1, 100, DispatchDate);
        Assert.Equal(6, breakdown.TotalQuantity);
        Assert.Equal(4, breakdown.InTransitQuantity);
        Assert.Equal(10, breakdown.TotalStockPosition);
        var route = Assert.Single(breakdown.InTransitRoutes);
        Assert.Equal((1, 2), (route.SourceLocationId, route.DestinationLocationId));
        Assert.Equal(6, Assert.Single(breakdown.Locations).Quantity);
        Assert.Equal("Source", breakdown.Locations[0].LocationName);
        Assert.Equal(0, breakdown.UnassignedQuantity);
        Assert.Equal(0, breakdown.AssignmentIssueCount);
        Assert.Equal(0, breakdown.ReconciliationDifference);
        Assert.Equal(6, Assert.Single((await ReportAsync(service, endDate: DispatchDate, locationId: 1)).ProductSummaries).QtyBalance);
        Assert.Empty((await ReportAsync(service, endDate: DispatchDate, locationId: 2)).ProductSummaries);
    }

    [Fact]
    public async Task ReceiptAndCancellation_ReturnStockToTheirWarehouses_WithoutDoubleCountingCommercialAmounts()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false,
            new(100, 10, 100, 0, 1), new(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, true,
            new(100, 4, 0, 40, 1), new(200, 0, 40, 0, 2));
        AddVoucher(context, 5, DispatchDate, true,
            new(100, 2, 0, 20, 1), new(200, 0, 20, 0, 2));
        AddVoucher(context, 3, ReceiptDate, true,
            new(100, 4, 40, 0, 2), new(200, 0, 0, 40));
        AddVoucher(context, 4, CancellationDate, true,
            new(100, 2, 20, 0, 1), new(200, 0, 0, 20));
        AddOutcomeReference(context, 2, 3, StockTransferService.ReceiptReferenceType);
        AddOutcomeReference(context, 5, 4, StockTransferService.CancellationReferenceType);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var inTransit = Assert.Single((await ReportAsync(service, endDate: DispatchDate)).ProductSummaries);
        Assert.Equal(4, inTransit.QtyBalance);
        Assert.Equal(10, inTransit.TotalDebitQty);
        Assert.Equal(6, inTransit.TotalCreditQty);
        Assert.Equal(6, inTransit.InTransitQuantity);
        var partial = Assert.Single((await ReportAsync(service, endDate: ReceiptDate)).ProductSummaries);
        Assert.Equal(8, partial.QtyBalance);
        Assert.Equal(14, partial.TotalDebitQty);
        Assert.Equal(6, partial.TotalCreditQty);
        Assert.Equal(4, partial.TransferInwardQuantity);
        Assert.Equal(2, partial.InTransitQuantity);
        var partialLocations = await service.GetProductStockBreakdownAsync(1, 100, ReceiptDate);
        Assert.Equal(8, partialLocations.TotalQuantity);
        Assert.Equal([4, 4], partialLocations.Locations.Select(x => x.Quantity).ToArray());
        Assert.Equal(2, partialLocations.InTransitQuantity);

        var final = Assert.Single((await ReportAsync(service, endDate: CancellationDate)).ProductSummaries);
        Assert.Equal(10, final.QtyBalance);
        Assert.Equal(16, final.TotalDebitQty);
        Assert.Equal(6, final.TotalCreditQty);
        Assert.Equal(0, final.InTransitQuantity);
        Assert.Equal(100, final.InventoryValue);
        Assert.Equal(6, final.TransferInwardQuantity);
        Assert.Equal(6, final.TransferOutwardQuantity);
        Assert.Equal(100, final.TotalDebit);
        Assert.Equal(0, final.TotalCredit);
        Assert.Equal(0, final.COGS);
        var finalLocations = await service.GetProductStockBreakdownAsync(1, 100, CancellationDate);
        Assert.Equal(10, finalLocations.TotalQuantity);
        Assert.Equal(6, finalLocations.Locations.Single(x => x.LocationId == 1).Quantity);
        Assert.Equal(4, finalLocations.Locations.Single(x => x.LocationId == 2).Quantity);
        Assert.Equal(0, finalLocations.ReconciliationDifference);
    }

    [Fact]
    public async Task FullyDispatchedProduct_RemainsVisibleAsInTransitWhenExcludingNil()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false,
            new(100, 10, 100, 0, 1), new(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, true,
            new(100, 10, 0, 100, 1), new(200, 0, 100, 0, 2));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var visible = Assert.Single((await ReportAsync(service, endDate: DispatchDate)).ProductSummaries);
        Assert.Equal(0, visible.QtyBalance);
        Assert.Equal(10, visible.TotalDebitQty);
        Assert.Equal(10, visible.TotalCreditQty);
        Assert.Equal(10, visible.InTransitQuantity);
        Assert.Equal(10, visible.AverageRate);
        Assert.Equal(0, visible.InventoryValue);
        Assert.Equal(0, (await service.GetProductStockBreakdownAsync(1, 100, DispatchDate)).TotalQuantity);
        var filtered = await ReportAsync(service, endDate: DispatchDate, excludeNil: true);
        Assert.Equal(100, Assert.Single(filtered.ProductSummaries).ProductId);
        Assert.Equal(1, filtered.TotalRecords);
    }

    [Fact]
    public async Task ReceivingCharge_ChangesInventoryValueWithoutChangingWarehouseQuantities()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false,
            new(100, 10, 100, 0, 1), new(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, true,
            new(100, 4, 0, 40, 1), new(200, 0, 40, 0, 2));
        AddVoucher(context, 3, ReceiptDate, true,
            new(100, 4, 40, 0, 2), new(200, 0, 0, 40));
        AddOutcomeReference(context, 2, 3, StockTransferService.ReceiptReferenceType);
        AddVoucher(context, 4, ReceiptDate, true,
            new(100, 0, 8, 0), new(300, 0, 0, 8));
        context.JournalVoucherReferences.Add(new JournalVoucherReference
        {
            MainVoucherId = 1003,
            ReferencedVoucherId = 1004,
            ReferenceType = StockTransferService.ChargeReferenceType
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var report = Assert.Single((await ReportAsync(service, endDate: ReceiptDate)).ProductSummaries);
        Assert.Equal(10, report.QtyBalance);
        Assert.Equal(14, report.TotalDebitQty);
        Assert.Equal(4, report.TotalCreditQty);
        Assert.Equal(0, report.InTransitQuantity);
        Assert.Equal(108, report.InventoryValue);
        Assert.Equal(10.8m, report.AverageRate);
        Assert.Equal(108, report.TotalDebit);
        var destination = Assert.Single((await ReportAsync(service, endDate: ReceiptDate, locationId: 2)).ProductSummaries);
        Assert.Equal(4, destination.QtyBalance);
        Assert.Equal(4, destination.TotalDebitQty);
        Assert.Equal(0, destination.TotalCreditQty);
        Assert.Equal(48, destination.InventoryValue);
        Assert.Equal(8, destination.TotalDebit);
        var breakdown = await service.GetProductStockBreakdownAsync(1, 100, ReceiptDate);
        Assert.Equal(10, breakdown.TotalQuantity);
        Assert.Equal(0, breakdown.AssignmentIssueCount);
        Assert.Equal(0, breakdown.UnassignedQuantity);
        Assert.Equal(0, breakdown.ReconciliationDifference);
    }

    [Fact]
    public async Task WarehouseTransferQuantities_ExplainEachWarehouseBalance()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false,
            new(100, 3, 360, 0, 1), new(300, 0, 0, 360));
        AddVoucher(context, 2, DispatchDate, true,
            new(100, 2, 0, 240, 1), new(200, 0, 240, 0, 2));
        AddVoucher(context, 3, ReceiptDate, true,
            new(100, 2, 240, 0, 2), new(200, 0, 0, 240));
        AddOutcomeReference(context, 2, 3, StockTransferService.ReceiptReferenceType);
        AddVoucher(context, 4, ReceiptDate, true,
            new(100, 0, 500, 0), new(300, 0, 0, 500));
        context.JournalVoucherReferences.Add(new JournalVoucherReference {
            MainVoucherId = 1003, ReferencedVoucherId = 1004,
            ReferenceType = StockTransferService.ChargeReferenceType
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var source = await ReportAsync(service, endDate: ReceiptDate, locationId: 1);
        var destination = await ReportAsync(service, endDate: ReceiptDate, locationId: 2);
        var all = await ReportAsync(service, endDate: ReceiptDate);
        Assert.Equal((3, 2, 1, 120m), StockColumns(Assert.Single(source.ProductSummaries)));
        Assert.Equal((2, 0, 2, 370m), StockColumns(Assert.Single(destination.ProductSummaries)));
        Assert.Equal((5, 2, 3), (all.Totals.TotalDebitQty,
            all.Totals.TotalCreditQty, all.Totals.BalanceQuantity));
        Assert.Equal(1, source.Totals.BalanceQuantity);
        Assert.Equal(2, destination.Totals.BalanceQuantity);
        Assert.Equal(360m, Assert.Single(source.ProductSummaries).TotalDebit);
        Assert.Equal(500m, Assert.Single(destination.ProductSummaries).TotalDebit);
    }

    private static (int DebitQty, int CreditQty, int BalanceQty, decimal Rate) StockColumns(
        ProductSummaryViewModel product) =>
        (product.TotalDebitQty, product.TotalCreditQty, product.QtyBalance, product.AverageRate);

    [Fact]
    public async Task TransferReceiptAndCharge_ExplainPhysicalQuantities_WhileOrdinarySaleStillCounts()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false,
            new(100, 3, 360, 0, 1), new(300, 0, 0, 360));
        AddVoucher(context, 2, DispatchDate, true,
            new(100, 2, 0, 240, 1), new(200, 0, 240, 0, 2));
        AddVoucher(context, 3, ReceiptDate, true,
            new(100, 2, 240, 0, 2), new(200, 0, 0, 240));
        AddOutcomeReference(context, 2, 3, StockTransferService.ReceiptReferenceType);
        AddVoucher(context, 4, ReceiptDate, true,
            new(100, 0, 500, 0), new(300, 0, 0, 500));
        AddVoucher(context, 5, CancellationDate, false,
            new(100, 1, 0, 200, 2), new(300, 0, 200, 0));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var afterReceipt = await ReportAsync(service, endDate: ReceiptDate);
        var product = Assert.Single(afterReceipt.ProductSummaries);
        Assert.Equal((5, 2, 3), (product.TotalDebitQty, product.TotalCreditQty, product.QtyBalance));
        Assert.Equal((2, 2), (product.TransferOutwardQuantity, product.TransferInwardQuantity));
        Assert.Equal((860m, 0m), (product.TotalDebit, product.TotalCredit));
        Assert.Equal(3, afterReceipt.Totals.BalanceQuantity);

        var afterSale = await ReportAsync(service, endDate: CancellationDate);
        product = Assert.Single(afterSale.ProductSummaries);
        Assert.Equal((5, 3, 2), (product.TotalDebitQty, product.TotalCreditQty, product.QtyBalance));
        Assert.Equal(200m, product.TotalCredit);
        Assert.Equal(2, afterSale.Totals.BalanceQuantity);
    }

    [Theory]
    [InlineData(1, 20, 4, 4)]
    [InlineData(2, 24, 6, 2)]
    public async Task SaleAfterDestinationCharge_UsesTheReportAverageForOverallCOGS(
        int saleLocationId, int warehouseCOGS, int sourceQuantity, int destinationQuantity)
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false,
            new(100, 10, 100, 0, 1), new(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, true,
            new(100, 4, 0, 40, 1), new(200, 0, 40, 0, 2));
        AddVoucher(context, 3, ReceiptDate, true,
            new(100, 4, 40, 0, 2), new(200, 0, 0, 40));
        AddOutcomeReference(context, 2, 3, StockTransferService.ReceiptReferenceType);
        AddVoucher(context, 4, ReceiptDate, true,
            new(100, 0, 8, 0), new(300, 0, 0, 8));
        context.JournalVoucherReferences.Add(new JournalVoucherReference {
            MainVoucherId = 1003, ReferencedVoucherId = 1004,
            ReferenceType = StockTransferService.ChargeReferenceType
        });
        AddVoucher(context, 5, CancellationDate, false,
            new(100, 2, 0, 36, saleLocationId), new(300, 0, 36, 0));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var overall = Assert.Single((await ReportAsync(service, endDate: CancellationDate)).ProductSummaries);
        var source = Assert.Single((await ReportAsync(service, endDate: CancellationDate, locationId: 1)).ProductSummaries);
        var destination = Assert.Single((await ReportAsync(service, endDate: CancellationDate, locationId: 2)).ProductSummaries);
        Assert.Equal((sourceQuantity, destinationQuantity, 8),
            (source.QtyBalance, destination.QtyBalance, overall.QtyBalance));
        Assert.Equal(warehouseCOGS, saleLocationId == 1 ? source.COGS : destination.COGS);
        Assert.Equal(21.6m, overall.COGS);
        Assert.Equal(86.4m, overall.InventoryValue);
        Assert.Equal(10.8m, overall.AverageRate);
    }

    [Fact]
    public async Task TransferProduct_WithLaterPurchase_StillCalculatesCOGSFromTheReportedAverage()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false,
            new(100, 10, 100, 0, 1), new(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, true,
            new(100, 4, 0, 40, 1), new(200, 0, 40, 0, 2));
        AddVoucher(context, 3, ReceiptDate, true,
            new(100, 4, 40, 0, 2), new(200, 0, 0, 40));
        AddOutcomeReference(context, 2, 3, StockTransferService.ReceiptReferenceType);
        AddVoucher(context, 4, ReceiptDate, true,
            new(100, 0, 8, 0), new(300, 0, 0, 8));
        context.JournalVoucherReferences.Add(new JournalVoucherReference {
            MainVoucherId = 1003, ReferencedVoucherId = 1004,
            ReferenceType = StockTransferService.ChargeReferenceType
        });
        AddVoucher(context, 5, CancellationDate, false,
            new(100, 2, 0, 36, 2), new(300, 0, 36, 0));
        AddVoucher(context, 6, CancellationDate.AddDays(1), false,
            new(100, 10, 200, 0, 1), new(300, 0, 0, 200));
        await context.SaveChangesAsync();

        var product = Assert.Single((await ReportAsync(CreateService(context),
            endDate: CancellationDate.AddDays(1))).ProductSummaries);
        Assert.Equal(18, product.QtyBalance);
        Assert.Equal(15.4m, product.AverageRate);
        Assert.Equal(30.8m, product.COGS);
        Assert.Equal(5.2m, product.Margin);
        Assert.Equal((308m, 36m), (product.TotalDebit, product.TotalCredit));
        Assert.Equal(product.AverageRate * product.QtyBalance, product.InventoryValue);
    }

    [Fact]
    public async Task UnpostedAndUnprocessedMovements_DoNotChangeReportOrBreakdown()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false,
            new(100, 10, 100, 0, 1), new(300, 0, 0, 100));
        AddVoucherWithStatus(context, 2, DispatchDate, true, false, true,
            new(100, 3, 0, 30, 1), new(200, 0, 30, 0, 2));
        AddVoucherWithStatus(context, 3, DispatchDate, true, true, false,
            new(100, 2, 0, 20, 1), new(200, 0, 20, 0, 2));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        Assert.Equal(10, Assert.Single((await ReportAsync(service, endDate: DispatchDate)).ProductSummaries).QtyBalance);
        var breakdown = await service.GetProductStockBreakdownAsync(1, 100, DispatchDate);
        Assert.Equal(10, breakdown.TotalQuantity);
        Assert.Equal(10, Assert.Single(breakdown.Locations).Quantity);
    }

    [Fact]
    public async Task MissingSourceAssignment_OnDispatch_IsShownAsAnAuditableNegativeUnassignedMovement()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false,
            new(100, 10, 100, 0, 1), new(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, true,
            new(100, 4, 0, 40), new(200, 0, 40, 0, 2));
        await context.SaveChangesAsync();

        var breakdown = await CreateService(context).GetProductStockBreakdownAsync(1, 100, DispatchDate);

        Assert.Equal(6, breakdown.TotalQuantity);
        Assert.Equal(10, Assert.Single(breakdown.Locations).Quantity);
        Assert.Equal(-4, breakdown.UnassignedQuantity);
        Assert.Equal(1, breakdown.AssignmentIssueCount);
        Assert.Equal(0, breakdown.ReconciliationDifference);
    }

    [Fact]
    public async Task DuplicateWarehouseAssignments_AreAuditedWithoutMultiplyingReportQuantityOrValue()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false,
            new(100, 10, 100, 0, 1), new(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, true,
            new(100, 4, 0, 40, 1), new(200, 0, 40, 0, 2));
        context.DeliveryChallanEntries.Add(new DeliveryChallanEntry {
            DeliveryChallanEntryId = 99999, DeliveryChallanVoucherId = 3002,
            JournalEntryId = 10020, LocationId = 2, EntryDate = DispatchDate
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var report = Assert.Single((await ReportAsync(service, endDate: DispatchDate)).ProductSummaries);
        Assert.Equal((6, 4, 60m), (report.QtyBalance, report.InTransitQuantity, report.InventoryValue));
        Assert.Equal(6, (await ReportAsync(service, endDate: DispatchDate)).Totals.BalanceQuantity);
        var breakdown = await service.GetProductStockBreakdownAsync(1, 100, DispatchDate);
        Assert.Equal((6, -4, 1, 0), (breakdown.TotalQuantity, breakdown.AmbiguousQuantity,
            breakdown.AssignmentIssueCount, breakdown.ReconciliationDifference));
    }

    [Fact]
    public async Task DateRangeWithoutMovement_ShouldStillShowCarriedForwardStockAfterDispatch()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false,
            new(100, 10, 100, 0, 1), new(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, true,
            new(100, 4, 0, 40, 1), new(200, 0, 40, 0, 2));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var report = await ReportAsync(service, startDate: ReceiptDate, endDate: ReceiptDate);

        Assert.Equal(6, Assert.Single(report.ProductSummaries).QtyBalance);
        Assert.Equal(0, report.ProductSummaries[0].TotalDebitQty);
        Assert.Equal(0, report.ProductSummaries[0].TotalCreditQty);
        Assert.Equal(0, report.Totals.NetQuantity);
        Assert.Equal(6, report.Totals.BalanceQuantity);
        Assert.Equal(4, report.ProductSummaries[0].InTransitQuantity);
        Assert.Equal(6, (await service.GetProductStockBreakdownAsync(1, 100, ReceiptDate)).TotalQuantity);
    }

    [Fact]
    public async Task ExcludeNilPagination_SkipsZeroStockProductBeforePaging()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        context.Accounts.Add(new Account
        {
            AccountId = 101, CategoryId = 10, AccountGroupId = 2,
            AccountName = "Product B", IsEnabled = true
        });
        AddVoucher(context, 1, PurchaseDate, false,
            new(100, 10, 100, 0, 1), new(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, false,
            new(100, 10, 0, 100, 1), new(300, 0, 100, 0));
        AddVoucher(context, 3, PurchaseDate, false,
            new(101, 5, 50, 0, 1), new(300, 0, 0, 50));
        await context.SaveChangesAsync();

        var firstPage = await CreateService(context).GetProductReportAsync(new ProductReportRequest
        {
            ProjectId = "1", EndDate = DispatchDate, ExcludeNilProducts = true, PageSize = 1
        });

        Assert.Equal(101, Assert.Single(firstPage.ProductSummaries).ProductId);
        Assert.Equal(1, firstPage.TotalRecords);
    }

    [Fact]
    public async Task MultiplePages_KeepGlobalTotalsAndStableCounts()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        context.Accounts.Add(new Account {
            AccountId = 101, CategoryId = 10, AccountGroupId = 2,
            AccountName = "Product B", IsEnabled = true
        });
        AddVoucher(context, 1, PurchaseDate, false,
            new(100, 10, 100, 0, 1), new(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, true,
            new(100, 4, 0, 40, 1), new(200, 0, 40, 0, 2));
        AddVoucher(context, 3, PurchaseDate, false,
            new(101, 5, 50, 0, 1), new(300, 0, 0, 50));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var first = await service.GetProductReportAsync(new ProductReportRequest {
            ProjectId = "1", EndDate = DispatchDate, PageSize = 1,
            ExcludeNilProducts = true
        });
        var second = await service.GetProductReportAsync(new ProductReportRequest {
            ProjectId = "1", EndDate = DispatchDate, PageSize = 1, Page = 1,
            ExcludeNilProducts = true
        });

        Assert.Equal(2, first.TotalRecords);
        Assert.Equal(2, second.TotalRecords);
        Assert.True(first.HasMoreData);
        Assert.False(second.HasMoreData);
        Assert.Equal([100, 101], new[] {
            Assert.Single(first.ProductSummaries).ProductId,
            Assert.Single(second.ProductSummaries).ProductId
        });
        Assert.Equal(15, first.Totals.TotalDebitQty);
        Assert.Equal(15, second.Totals.TotalDebitQty);
        Assert.Equal(11, first.Totals.NetQuantity);
        Assert.Equal(11, second.Totals.NetQuantity);
        Assert.Equal(4, first.Totals.TotalCreditQty);
        Assert.Equal(4, second.Totals.TotalCreditQty);
        Assert.Equal(11, first.Totals.BalanceQuantity);
        Assert.Equal(11, second.Totals.BalanceQuantity);
    }

    [Fact]
    public async Task RouteEditAndGraphRemoval_RefreshTheDerivedTransitPosition()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        context.Locations.Add(new Location { LocationId = 3, LocationName = "Alternate" });
        AddVoucher(context, 1, PurchaseDate, false,
            new(100, 10, 100, 0, 1), new(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, true,
            new(100, 4, 0, 40, 1), new(200, 0, 40, 0, 2));
        await context.SaveChangesAsync();
        var service = CreateService(context);
        Assert.Equal(2, Assert.Single((await service.GetProductStockBreakdownAsync(1, 100, DispatchDate))
            .InTransitRoutes).DestinationLocationId);

        context.DeliveryChallanEntries.Single(x => x.JournalEntryId == 10021).LocationId = 3;
        await context.SaveChangesAsync();
        Assert.Equal(3, Assert.Single((await service.GetProductStockBreakdownAsync(1, 100, DispatchDate))
            .InTransitRoutes).DestinationLocationId);

        context.JournalVoucherReferences.RemoveRange(context.JournalVoucherReferences
            .Where(x => x.MainVoucherId == 1002 || x.ReferencedVoucherId == 1002));
        var challan = context.DeliveryChallanVouchers.Single(x => x.JournalVoucherId == 1002);
        context.DeliveryChallanEntries.RemoveRange(context.DeliveryChallanEntries
            .Where(x => x.DeliveryChallanVoucherId == challan.DeliveryChallanVoucherId));
        context.DeliveryChallanVouchers.Remove(challan);
        context.JournalEntries.RemoveRange(context.JournalEntries.Where(x => x.JournalVoucherId == 1002));
        context.JournalVouchers.Remove(context.JournalVouchers.Single(x => x.JournalVoucherId == 1002));
        await context.SaveChangesAsync();

        var afterDelete = await service.GetProductStockBreakdownAsync(1, 100, DispatchDate);
        Assert.Equal(10, afterDelete.TotalQuantity);
        Assert.Equal(0, afterDelete.InTransitQuantity);
        Assert.Empty(afterDelete.InTransitRoutes);
    }

    private static ReportingService CreateService(LedgerDbContext context) =>
        new(new UnitOfWork(context), new ApprovalGuardService(context), new PaymentReceiptClassifier(context), context);

    private static Task<ProductReportViewModel> ReportAsync(
        ReportingService service, DateOnly? startDate = null, DateOnly? endDate = null,
        int? locationId = null, bool excludeNil = false) =>
        service.GetProductReportAsync(new ProductReportRequest
        {
            ProjectId = "1",
            StartDate = startDate,
            EndDate = endDate,
            LocationId = locationId?.ToString(),
            ExcludeNilProducts = excludeNil
        });

    private static async Task SeedMastersAsync(LedgerDbContext context)
    {
        context.Projects.Add(new Project { ProjectId = 1, ProjectName = "Main" });
        context.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "General" });
        context.AccountGroups.AddRange(
            new AccountGroup { AccountGroupId = 1, AccountGroupName = "Account" },
            new AccountGroup { AccountGroupId = 2, AccountGroupName = "Product" });
        context.Categories.AddRange(
            new Category { CategoryId = 10, ProjectId = 1, AccountTypeId = 1, CategoryName = "Products" },
            new Category { CategoryId = 20, ProjectId = 1, AccountTypeId = 1, CategoryName = "Accounts" });
        context.Accounts.AddRange(
            new Account { AccountId = 100, CategoryId = 10, AccountGroupId = 2, AccountName = "Product A", IsEnabled = true },
            new Account { AccountId = 200, CategoryId = 20, AccountGroupId = 1, AccountName = "Stock In Transit", IsEnabled = true },
            new Account { AccountId = 300, CategoryId = 20, AccountGroupId = 1, AccountName = "Clearing", IsEnabled = true });
        context.Locations.AddRange(
            new Location { LocationId = 1, LocationName = "Source" },
            new Location { LocationId = 2, LocationName = "Destination" });
        await context.SaveChangesAsync();
    }

    private static void AddVoucher(
        LedgerDbContext context, int sequence, DateOnly date, bool isTransfer,
        params Movement[] movements) => AddVoucherWithStatus(context, sequence, date, isTransfer, true, true, movements);

    private static void AddOutcomeReference(LedgerDbContext context, int dispatchSequence,
        int outcomeSequence, string referenceType) =>
        context.JournalVoucherReferences.Add(new JournalVoucherReference {
            MainVoucherId = 1000 + dispatchSequence,
            ReferencedVoucherId = 1000 + outcomeSequence,
            ReferenceType = referenceType
        });

    private static void AddVoucherWithStatus(
        LedgerDbContext context, int sequence, DateOnly date, bool isTransfer,
        bool completed, bool processed, params Movement[] movements)
    {
        var voucherId = 1000 + sequence;
        var challanId = 3000 + sequence;
        context.JournalVouchers.Add(new JournalVoucher
        {
            JournalVoucherId = voucherId,
            ProjectId = 1,
            EntryDate = date,
            CreditableDate = date.AddDays(1),
            IsCompleted = completed,
            IsBaseVoucher = false,
            IsStockTransfer = isTransfer
        });
        context.DeliveryChallanVouchers.Add(new DeliveryChallanVoucher
        {
            DeliveryChallanVoucherId = challanId,
            JournalVoucherId = voucherId,
            DCDate = date
        });
        for (var index = 0; index < movements.Length; index++)
        {
            var movement = movements[index];
            var entryId = voucherId * 10 + index;
            context.JournalEntries.Add(new JournalEntry
            {
                JournalEntryId = entryId,
                JournalVoucherId = voucherId,
                AccountId = movement.AccountId,
                CategoryId = movement.AccountId is 100 or 101 ? 10 : 20,
                IsProcessed = processed,
                Quantity = movement.Quantity,
                Debit = movement.Debit,
                Credit = movement.Credit
            });
            if (movement.LocationId.HasValue)
                context.DeliveryChallanEntries.Add(new DeliveryChallanEntry
                {
                    DeliveryChallanEntryId = entryId,
                    DeliveryChallanVoucherId = challanId,
                    JournalEntryId = entryId,
                    LocationId = movement.LocationId.Value,
                    EntryDate = date
                });
        }
    }

    private sealed record Movement(int AccountId, int Quantity, decimal Debit, decimal Credit, int? LocationId = null);

    private static LedgerDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase($"product-stock-transit-{Guid.NewGuid():N}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new LedgerDbContext(options);
    }
}
