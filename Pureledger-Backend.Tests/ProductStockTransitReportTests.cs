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
    public async Task ProductWithoutTransfers_UsesWarehouseMovingAverageAndSaleDateCOGS()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false,
            new Movement(100, 10, 100, 0, 1), new Movement(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, false,
            new Movement(100, 5, 100, 0, 1), new Movement(300, 0, 0, 100));
        AddVoucher(context, 3, ReceiptDate, false,
            new Movement(100, 3, 0, 60, 1), new Movement(300, 0, 60, 0));
        await context.SaveChangesAsync();

        var report = await ReportAsync(CreateService(context), startDate: DispatchDate,
            endDate: ReceiptDate, locationId: 1);
        var product = Assert.Single(report.ProductSummaries);
        Assert.Equal((10, 5, 3, 12), (product.PreviousBalance,
            product.TotalDebitQty, product.TotalCreditQty, product.QtyBalance));
        Assert.InRange(product.AverageRate, 13.3333m, 13.3334m);
        Assert.Equal((100m, 60m), (product.TotalDebit, product.TotalCredit));
        Assert.Equal(40m, product.COGS);
        Assert.Equal(20m, product.Margin);
        Assert.Equal(160m, product.InventoryValue);
        Assert.Equal((5, 3, 12), (report.Totals.TotalDebitQty,
            report.Totals.TotalCreditQty, report.Totals.BalanceQuantity));
    }

    [Fact]
    public async Task PendingDispatch_ReducesAvailableStockAtSourceButDoesNotCreateDestinationStockOrSales()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false,
            new Movement(100, 10, 100, 0, 1), new Movement(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, true,
            new Movement(100, 4, 0, 40, 1), new Movement(200, 0, 40, 0, 2));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var beforeDispatch = await ReportAsync(service, endDate: PurchaseDate);
        Assert.Equal(10, Assert.Single(beforeDispatch.ProductSummaries).QtyBalance);

        var report = await ReportAsync(service, startDate: DispatchDate, endDate: DispatchDate);
        var product = Assert.Single(report.ProductSummaries);
        Assert.Equal(10, product.PreviousBalance);
        Assert.Equal(0, product.TotalDebitQty);
        Assert.Equal(0, product.TotalCreditQty);
        Assert.Equal(4, product.TransferOutwardQuantity);
        Assert.Equal(0, product.TransferInwardQuantity);
        Assert.Equal(6, product.QtyBalance);
        Assert.Equal(4, product.InTransitQuantity);
        Assert.Equal(60, product.InventoryValue);
        Assert.Equal(10, product.AverageRate);
        Assert.Equal(0, product.TotalCredit);
        Assert.Equal(0, product.COGS);
        Assert.Equal(0, product.Margin);
        Assert.Equal(0, report.Totals.NetQuantity);
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
            new Movement(100, 10, 100, 0, 1), new Movement(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, true,
            new Movement(100, 4, 0, 40, 1), new Movement(200, 0, 40, 0, 2));
        AddVoucher(context, 5, DispatchDate, true,
            new Movement(100, 2, 0, 20, 1), new Movement(200, 0, 20, 0, 2));
        AddVoucher(context, 3, ReceiptDate, true,
            new Movement(100, 4, 40, 0, 2), new Movement(200, 0, 0, 40));
        AddVoucher(context, 4, CancellationDate, true,
            new Movement(100, 2, 20, 0, 1), new Movement(200, 0, 0, 20));
        AddOutcomeReference(context, 2, 3, StockTransferService.ReceiptReferenceType);
        AddOutcomeReference(context, 5, 4, StockTransferService.CancellationReferenceType);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var inTransit = Assert.Single((await ReportAsync(service, endDate: DispatchDate)).ProductSummaries);
        Assert.Equal(4, inTransit.QtyBalance);
        Assert.Equal(10, inTransit.TotalDebitQty);
        Assert.Equal(0, inTransit.TotalCreditQty);
        Assert.Equal(6, inTransit.InTransitQuantity);
        var partial = Assert.Single((await ReportAsync(service, endDate: ReceiptDate)).ProductSummaries);
        Assert.Equal(8, partial.QtyBalance);
        Assert.Equal(10, partial.TotalDebitQty);
        Assert.Equal(0, partial.TotalCreditQty);
        Assert.Equal(4, partial.TransferInwardQuantity);
        Assert.Equal(2, partial.InTransitQuantity);
        var partialLocations = await service.GetProductStockBreakdownAsync(1, 100, ReceiptDate);
        Assert.Equal(8, partialLocations.TotalQuantity);
        Assert.Equal([4, 4], partialLocations.Locations.Select(x => x.Quantity).ToArray());
        Assert.Equal(2, partialLocations.InTransitQuantity);

        var final = Assert.Single((await ReportAsync(service, endDate: CancellationDate)).ProductSummaries);
        Assert.Equal(10, final.QtyBalance);
        Assert.Equal(10, final.TotalDebitQty);
        Assert.Equal(0, final.TotalCreditQty);
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
            new Movement(100, 10, 100, 0, 1), new Movement(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, true,
            new Movement(100, 10, 0, 100, 1), new Movement(200, 0, 100, 0, 2));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var visible = Assert.Single((await ReportAsync(service, endDate: DispatchDate)).ProductSummaries);
        Assert.Equal(0, visible.QtyBalance);
        Assert.Equal(10, visible.TotalDebitQty);
        Assert.Equal(0, visible.TotalCreditQty);
        Assert.Equal(10, visible.InTransitQuantity);
        Assert.Equal(0, visible.AverageRate);
        Assert.Equal(0, visible.InventoryValue);
        Assert.Equal(0, (await service.GetProductStockBreakdownAsync(1, 100, DispatchDate)).TotalQuantity);
        var filtered = await ReportAsync(service, endDate: DispatchDate, excludeNil: true);
        Assert.Equal(100, Assert.Single(filtered.ProductSummaries).ProductId);
        Assert.Equal(1, filtered.TotalRecords);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(500)]
    public async Task FullyInTransit_ContributesNoCostOrRateUntilReceipt(decimal transferPrice)
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false, new Movement(100, 3, 360, 0, 1));
        AddVoucher(context, 2, DispatchDate, true,
            new Movement(100, 3, 0, transferPrice * 3, 1), new Movement(200, 0, transferPrice * 3, 0, 2));
        AddVoucher(context, 3, ReceiptDate, true, new Movement(100, 3, transferPrice * 3, 0, 2));
        AddOutcomeReference(context, 2, 3, StockTransferService.ReceiptReferenceType);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var pending = Assert.Single((await ReportAsync(service, endDate: DispatchDate, excludeNil: true)).ProductSummaries);
        Assert.Equal((0, 3, 0m, 0m, 0m), (pending.QtyBalance, pending.InTransitQuantity,
            pending.InventoryValue, pending.AverageRate, pending.COGS));
        var source = Assert.Single((await ReportAsync(service, endDate: DispatchDate, locationId: 1)).ProductSummaries);
        Assert.Equal((0, 0m, 0m, 360m, transferPrice * 3, 0m, 0m),
            (source.QtyBalance, source.InventoryValue, source.AverageRate, source.TotalDebit,
             source.TotalCredit, source.Margin, source.MarginPercentage));
        Assert.Empty((await ReportAsync(service, endDate: DispatchDate, locationId: 2)).ProductSummaries);
        var received = Assert.Single((await ReportAsync(service, startDate: ReceiptDate, endDate: ReceiptDate)).ProductSummaries);
        Assert.Equal((0, 0m, 3, 0, transferPrice * 3, transferPrice),
            (received.PreviousBalance, received.PreviousAvgRate, received.QtyBalance,
             received.InTransitQuantity, received.InventoryValue, received.AverageRate));
        var destination = Assert.Single((await ReportAsync(service, endDate: ReceiptDate, locationId: 2)).ProductSummaries);
        Assert.Equal((transferPrice * 3, 0m, 0m), (destination.TotalDebit, destination.TotalCredit, destination.Margin));
    }

    [Theory]
    [InlineData("Cartage")]
    [InlineData("ForCharges")]
    public async Task ProductChargeWithoutChallan_InheritsWarehouseAndFeedsLaterSaleCost(string referenceType)
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false, new Movement(100, 10, 100, 0, 1));
        AddVoucher(context, 2, DispatchDate, false, new Movement(100, 0, 20, 0), new Movement(300, 0, 0, 20));
        AddOutcomeReference(context, 1, 2, referenceType);
        AddVoucher(context, 3, ReceiptDate, false, new Movement(100, 4, 0, 80, 1));
        await context.SaveChangesAsync();
        var service = CreateService(context);
        foreach (int? location in new int?[] { null, 1 }) {
            var charged = Assert.Single((await ReportAsync(service, endDate: DispatchDate, locationId: location)).ProductSummaries);
            Assert.Equal((10, 120m, 12m, 120m), (charged.QtyBalance, charged.InventoryValue, charged.AverageRate, charged.TotalDebit));
            var sold = Assert.Single((await ReportAsync(service, endDate: ReceiptDate, locationId: location)).ProductSummaries);
            Assert.Equal((6, 72m, 12m, 48m, 32m, 10, 4), (sold.QtyBalance, sold.InventoryValue,
                sold.AverageRate, sold.COGS, sold.Margin, sold.TotalDebitQty, sold.TotalCreditQty));
        }
        Assert.Empty((await ReportAsync(service, endDate: ReceiptDate, locationId: 2)).ProductSummaries);
    }

    [Theory]
    [InlineData("Cartage")]
    [InlineData("ForCharges")]
    public async Task ChargeToOrdinaryAccount_DoesNotEnterProductCost(string referenceType)
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false, new Movement(100, 10, 100, 0, 1));
        AddVoucher(context, 2, DispatchDate, false, new Movement(300, 0, 20, 0), new Movement(200, 0, 0, 20));
        AddOutcomeReference(context, 1, 2, referenceType);
        await context.SaveChangesAsync();
        var product = Assert.Single((await ReportAsync(CreateService(context), endDate: DispatchDate, locationId: 1)).ProductSummaries);
        Assert.Equal((10, 100m, 10m, 100m), (product.QtyBalance, product.InventoryValue, product.AverageRate, product.TotalDebit));
    }

    [Theory]
    [InlineData("multiple-locations")]
    [InlineData("duplicate-assignment")]
    [InlineData("missing-assignment")]
    [InlineData("wrong-product")]
    [InlineData("multiple-parents")]
    public async Task CartageWithAmbiguousOrigin_DoesNotInventWarehouse(string defect)
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false, new Movement(100, 10, 100, 0, 1),
            new Movement(defect == "wrong-product" ? 200 : 100, 2, 20, 0, defect == "multiple-locations" ? 2 : 1));
        AddVoucher(context, 2, DispatchDate, false, new Movement(100, 0, 20, 0));
        if (defect != "wrong-product") AddOutcomeReference(context, 1, 2, "Cartage");
        else AddOutcomeReference(context, 99, 2, "Cartage");
        if (defect == "multiple-parents") AddOutcomeReference(context, 99, 2, "Cartage");
        if (defect == "missing-assignment") context.DeliveryChallanEntries.RemoveRange(context.DeliveryChallanEntries.Local);
        if (defect == "duplicate-assignment") context.DeliveryChallanEntries.Add(new DeliveryChallanEntry {
            JournalEntryId = 10010, DeliveryChallanVoucherId = 3001, LocationId = 1, EntryDate = PurchaseDate
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var all = Assert.Single((await ReportAsync(service, endDate: DispatchDate)).ProductSummaries);
        decimal assigned = 0;
        foreach (var location in new[] { 1, 2 })
            assigned += (await ReportAsync(service, endDate: DispatchDate, locationId: location)).ProductSummaries.Sum(p => p.TotalDebit);
        Assert.True(all.TotalDebit - assigned >= 20m);
    }

    [Fact]
    public async Task CartageUsesSameProductAndPreservesExplicitWarehouseAssignment()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        context.Accounts.Add(new Account { AccountId = 101, CategoryId = 10, AccountGroupId = 2, AccountName = "Other product" });
        AddVoucher(context, 1, PurchaseDate, false, new Movement(100, 10, 100, 0, 1), new Movement(101, 10, 200, 0, 2));
        AddVoucher(context, 2, DispatchDate, false, new Movement(100, 0, 20, 0), new Movement(101, 0, 40, 0));
        AddOutcomeReference(context, 1, 2, "Cartage");
        AddVoucher(context, 3, ReceiptDate, false, new Movement(100, 0, 5, 0, 2));
        AddOutcomeReference(context, 1, 3, "Cartage");
        await context.SaveChangesAsync();
        var service = CreateService(context);
        Assert.Equal(120m, Assert.Single((await ReportAsync(service, endDate: ReceiptDate, locationId: 1)).ProductSummaries).TotalDebit);
        var second = (await ReportAsync(service, endDate: ReceiptDate, locationId: 2)).ProductSummaries;
        Assert.Equal(240m, second.Single(p => p.ProductId == 101).TotalDebit);
        Assert.Equal(5m, second.Single(p => p.ProductId == 100).TotalDebit);
    }

    [Fact]
    public async Task PartialTransit_AverageUsesOnlyAvailableStockAcrossWarehouses()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false,
            new Movement(100, 3, 360, 0, 1), new Movement(100, 2, 80, 0, 2));
        AddVoucher(context, 2, DispatchDate, true,
            new Movement(100, 3, 0, 1500, 1), new Movement(200, 0, 1500, 0, 2));
        AddVoucher(context, 3, ReceiptDate, true, new Movement(100, 3, 1500, 0, 2));
        AddOutcomeReference(context, 2, 3, StockTransferService.ReceiptReferenceType);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var pending = Assert.Single((await ReportAsync(service, endDate: DispatchDate)).ProductSummaries);
        Assert.Equal((2, 3, 80m, 40m), (pending.QtyBalance, pending.InTransitQuantity,
            pending.InventoryValue, pending.AverageRate));
        var destination = Assert.Single((await ReportAsync(service, endDate: DispatchDate, locationId: 2)).ProductSummaries);
        Assert.Equal((2, 80m, 40m, 80m), (destination.QtyBalance, destination.InventoryValue,
            destination.AverageRate, destination.TotalDebit));
        var received = Assert.Single((await ReportAsync(service, startDate: ReceiptDate, endDate: ReceiptDate)).ProductSummaries);
        Assert.Equal((40m, 5, 1580m, 316m), (received.PreviousAvgRate, received.QtyBalance,
            received.InventoryValue, received.AverageRate));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(150)]
    public async Task WarehouseTransferAmounts_DoNotDiluteSaleMargin_InRowsTotalsOrOpeningAmounts(decimal transferPrice)
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false, new Movement(100, 10, 1000, 0, 1));
        AddVoucher(context, 2, DispatchDate, true,
            new Movement(100, 2, 0, transferPrice * 2, 1), new Movement(200, 0, transferPrice * 2, 0, 2));
        AddVoucher(context, 3, ReceiptDate, true, new Movement(100, 2, transferPrice * 2, 0, 2));
        AddOutcomeReference(context, 2, 3, StockTransferService.ReceiptReferenceType);
        AddVoucher(context, 4, ReceiptDate, false, new Movement(100, 1, 0, 200, 1));
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var report = await ReportAsync(service, endDate: ReceiptDate, locationId: 1);
        var source = Assert.Single(report.ProductSummaries);
        Assert.Equal((1000m, transferPrice * 2 + 200, 100m, 100m, 50m),
            (source.TotalDebit, source.TotalCredit, source.COGS, source.Margin, source.MarginPercentage));
        Assert.Equal((source.TotalDebit - source.TotalCredit, 700m, 100m),
            (source.Balance, source.InventoryValue, source.AverageRate));
        Assert.Equal((source.TotalCredit, 100m, 100m, 50m),
            (report.Totals.TotalCredit, report.Totals.TotalCOGS, report.Totals.TotalMargin, report.Totals.MarginPercentage));
        var period = Assert.Single((await ReportAsync(service, startDate: ReceiptDate,
            endDate: ReceiptDate, locationId: 1)).ProductSummaries);
        Assert.Equal((1000m, transferPrice * 2, 200m, 50m),
            (period.PreviousDebit, period.PreviousCredit, period.TotalCredit, period.MarginPercentage));
        var all = Assert.Single((await ReportAsync(service, endDate: ReceiptDate)).ProductSummaries);
        Assert.Equal((1000m, 200m, 100m, 100m, 50m),
            (all.TotalDebit, all.TotalCredit, all.COGS, all.Margin, all.MarginPercentage));
    }

    [Fact]
    public async Task ReceivingCharge_ChangesInventoryValueWithoutChangingWarehouseQuantities()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false,
            new Movement(100, 10, 100, 0, 1), new Movement(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, true,
            new Movement(100, 4, 0, 40, 1), new Movement(200, 0, 40, 0, 2));
        AddVoucher(context, 3, ReceiptDate, true,
            new Movement(100, 4, 40, 0, 2), new Movement(200, 0, 0, 40));
        AddOutcomeReference(context, 2, 3, StockTransferService.ReceiptReferenceType);
        AddVoucher(context, 4, ReceiptDate, true,
            new Movement(100, 0, 8, 0), new Movement(300, 0, 0, 8));
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
        Assert.Equal(10, report.TotalDebitQty);
        Assert.Equal(0, report.TotalCreditQty);
        Assert.Equal(0, report.InTransitQuantity);
        Assert.Equal(108, report.InventoryValue);
        Assert.Equal(10.8m, report.AverageRate);
        Assert.Equal(108, report.TotalDebit);
        var destination = Assert.Single((await ReportAsync(service, endDate: ReceiptDate, locationId: 2)).ProductSummaries);
        Assert.Equal(4, destination.QtyBalance);
        Assert.Equal(4, destination.TotalDebitQty);
        Assert.Equal(0, destination.TotalCreditQty);
        Assert.Equal(48, destination.InventoryValue);
        Assert.Equal(48, destination.TotalDebit);
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
            new Movement(100, 3, 360, 0, 1), new Movement(300, 0, 0, 360));
        AddVoucher(context, 2, DispatchDate, true,
            new Movement(100, 2, 0, 240, 1), new Movement(200, 0, 240, 0, 2));
        AddVoucher(context, 3, ReceiptDate, true,
            new Movement(100, 2, 240, 0, 2), new Movement(200, 0, 0, 240));
        AddOutcomeReference(context, 2, 3, StockTransferService.ReceiptReferenceType);
        AddVoucher(context, 4, ReceiptDate, true,
            new Movement(100, 0, 500, 0), new Movement(300, 0, 0, 500));
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
        Assert.Equal((3, 0, 3), (all.Totals.TotalDebitQty,
            all.Totals.TotalCreditQty, all.Totals.BalanceQuantity));
        Assert.Equal(1, source.Totals.BalanceQuantity);
        Assert.Equal(2, destination.Totals.BalanceQuantity);
        Assert.Equal(360m, Assert.Single(source.ProductSummaries).TotalDebit);
        Assert.Equal(740m, Assert.Single(destination.ProductSummaries).TotalDebit);
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
            new Movement(100, 3, 360, 0, 1), new Movement(300, 0, 0, 360));
        AddVoucher(context, 2, DispatchDate, true,
            new Movement(100, 2, 0, 240, 1), new Movement(200, 0, 240, 0, 2));
        AddVoucher(context, 3, ReceiptDate, true,
            new Movement(100, 2, 240, 0, 2), new Movement(200, 0, 0, 240));
        AddOutcomeReference(context, 2, 3, StockTransferService.ReceiptReferenceType);
        AddVoucher(context, 4, ReceiptDate, true,
            new Movement(100, 0, 500, 0), new Movement(300, 0, 0, 500));
        AddVoucher(context, 5, CancellationDate, false,
            new Movement(100, 1, 0, 200, 2), new Movement(300, 0, 200, 0));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var afterReceipt = await ReportAsync(service, endDate: ReceiptDate);
        var product = Assert.Single(afterReceipt.ProductSummaries);
        Assert.Equal((3, 0, 3), (product.TotalDebitQty, product.TotalCreditQty, product.QtyBalance));
        Assert.Equal((2, 2), (product.TransferOutwardQuantity, product.TransferInwardQuantity));
        Assert.Equal((860m, 0m), (product.TotalDebit, product.TotalCredit));
        Assert.Equal(3, afterReceipt.Totals.BalanceQuantity);

        var afterSale = await ReportAsync(service, endDate: CancellationDate);
        product = Assert.Single(afterSale.ProductSummaries);
        Assert.Equal((3, 1, 2), (product.TotalDebitQty, product.TotalCreditQty, product.QtyBalance));
        Assert.Equal(200m, product.TotalCredit);
        Assert.Equal(2, afterSale.Totals.BalanceQuantity);
    }

    [Theory]
    [InlineData(1, 20, 4, 4)]
    [InlineData(2, 24, 6, 2)]
    public async Task SaleAfterDestinationCharge_GlobalCOGSAndValueReconcileWithWarehouses(
        int saleLocationId, int warehouseCOGS, int sourceQuantity, int destinationQuantity)
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false,
            new Movement(100, 10, 100, 0, 1), new Movement(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, true,
            new Movement(100, 4, 0, 40, 1), new Movement(200, 0, 40, 0, 2));
        AddVoucher(context, 3, ReceiptDate, true,
            new Movement(100, 4, 40, 0, 2), new Movement(200, 0, 0, 40));
        AddOutcomeReference(context, 2, 3, StockTransferService.ReceiptReferenceType);
        AddVoucher(context, 4, ReceiptDate, true,
            new Movement(100, 0, 8, 0), new Movement(300, 0, 0, 8));
        context.JournalVoucherReferences.Add(new JournalVoucherReference {
            MainVoucherId = 1003, ReferencedVoucherId = 1004,
            ReferenceType = StockTransferService.ChargeReferenceType
        });
        AddVoucher(context, 5, CancellationDate, false,
            new Movement(100, 2, 0, 36, saleLocationId), new Movement(300, 0, 36, 0));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var overall = Assert.Single((await ReportAsync(service, endDate: CancellationDate)).ProductSummaries);
        var source = Assert.Single((await ReportAsync(service, endDate: CancellationDate, locationId: 1)).ProductSummaries);
        var destination = Assert.Single((await ReportAsync(service, endDate: CancellationDate, locationId: 2)).ProductSummaries);
        Assert.Equal((sourceQuantity, destinationQuantity, 8),
            (source.QtyBalance, destination.QtyBalance, overall.QtyBalance));
        Assert.Equal(warehouseCOGS, saleLocationId == 1 ? source.COGS : destination.COGS);
        Assert.Equal(warehouseCOGS, overall.COGS);
        Assert.Equal(source.COGS + destination.COGS, overall.COGS);
        Assert.Equal(source.InventoryValue + destination.InventoryValue, overall.InventoryValue);
        Assert.Equal(108m - warehouseCOGS, overall.InventoryValue);
        Assert.Equal(overall.InventoryValue / 8, overall.AverageRate);
    }

    [Fact]
    public async Task TransferProduct_LaterPurchaseDoesNotRepriceAnEarlierSale()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false,
            new Movement(100, 10, 100, 0, 1), new Movement(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, true,
            new Movement(100, 4, 0, 40, 1), new Movement(200, 0, 40, 0, 2));
        AddVoucher(context, 3, ReceiptDate, true,
            new Movement(100, 4, 40, 0, 2), new Movement(200, 0, 0, 40));
        AddOutcomeReference(context, 2, 3, StockTransferService.ReceiptReferenceType);
        AddVoucher(context, 4, ReceiptDate, true,
            new Movement(100, 0, 8, 0), new Movement(300, 0, 0, 8));
        context.JournalVoucherReferences.Add(new JournalVoucherReference {
            MainVoucherId = 1003, ReferencedVoucherId = 1004,
            ReferenceType = StockTransferService.ChargeReferenceType
        });
        AddVoucher(context, 5, CancellationDate, false,
            new Movement(100, 2, 0, 36, 2), new Movement(300, 0, 36, 0));
        AddVoucher(context, 6, CancellationDate.AddDays(1), false,
            new Movement(100, 10, 200, 0, 1), new Movement(300, 0, 0, 200));
        await context.SaveChangesAsync();

        var product = Assert.Single((await ReportAsync(CreateService(context),
            endDate: CancellationDate.AddDays(1))).ProductSummaries);
        Assert.Equal(18, product.QtyBalance);
        Assert.Equal(284m / 18m, product.AverageRate);
        Assert.Equal(24m, product.COGS);
        Assert.Equal(12m, product.Margin);
        Assert.Equal((308m, 36m), (product.TotalDebit, product.TotalCredit));
        Assert.Equal(product.AverageRate * product.QtyBalance, product.InventoryValue);
    }

    [Fact]
    public async Task UnpostedAndUnprocessedMovements_DoNotChangeReportOrBreakdown()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false,
            new Movement(100, 10, 100, 0, 1), new Movement(300, 0, 0, 100));
        AddVoucherWithStatus(context, 2, DispatchDate, true, false, true,
            new Movement(100, 3, 0, 30, 1), new Movement(200, 0, 30, 0, 2));
        AddVoucherWithStatus(context, 3, DispatchDate, true, true, false,
            new Movement(100, 2, 0, 20, 1), new Movement(200, 0, 20, 0, 2));
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
            new Movement(100, 10, 100, 0, 1), new Movement(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, true,
            new Movement(100, 4, 0, 40), new Movement(200, 0, 40, 0, 2));
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
            new Movement(100, 10, 100, 0, 1), new Movement(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, true,
            new Movement(100, 4, 0, 40, 1), new Movement(200, 0, 40, 0, 2));
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
            new Movement(100, 10, 100, 0, 1), new Movement(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, true,
            new Movement(100, 4, 0, 40, 1), new Movement(200, 0, 40, 0, 2));
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
            new Movement(100, 10, 100, 0, 1), new Movement(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, false,
            new Movement(100, 10, 0, 100, 1), new Movement(300, 0, 100, 0));
        AddVoucher(context, 3, PurchaseDate, false,
            new Movement(101, 5, 50, 0, 1), new Movement(300, 0, 0, 50));
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
            new Movement(100, 10, 100, 0, 1), new Movement(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, true,
            new Movement(100, 4, 0, 40, 1), new Movement(200, 0, 40, 0, 2));
        AddVoucher(context, 3, PurchaseDate, false,
            new Movement(101, 5, 50, 0, 1), new Movement(300, 0, 0, 50));
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
        Assert.Equal(15, first.Totals.NetQuantity);
        Assert.Equal(15, second.Totals.NetQuantity);
        Assert.Equal(0, first.Totals.TotalCreditQty);
        Assert.Equal(0, second.Totals.TotalCreditQty);
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
            new Movement(100, 10, 100, 0, 1), new Movement(300, 0, 0, 100));
        AddVoucher(context, 2, DispatchDate, true,
            new Movement(100, 4, 0, 40, 1), new Movement(200, 0, 40, 0, 2));
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

    [Fact]
    public async Task EnteredTransferValue_ChangesDestinationValuationWithoutDepletingSourceAtTransferPrice()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false, new Movement(100, 3, 360, 0, 1));
        AddVoucher(context, 2, DispatchDate, true, new Movement(100, 2, 0, 1000, 1), new Movement(200, 0, 1000, 0, 2));
        AddVoucher(context, 3, ReceiptDate, true, new Movement(100, 2, 1000, 0, 2), new Movement(200, 0, 0, 1000));
        AddOutcomeReference(context, 2, 3, StockTransferService.ReceiptReferenceType);
        AddVoucher(context, 4, ReceiptDate, true, new Movement(100, 0, 1000, 0));
        AddOutcomeReference(context, 3, 4, StockTransferService.ChargeReferenceType);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var all = Assert.Single((await ReportAsync(service, endDate: ReceiptDate)).ProductSummaries);
        var source = Assert.Single((await ReportAsync(service, endDate: ReceiptDate, locationId: 1)).ProductSummaries);
        var destination = Assert.Single((await ReportAsync(service, endDate: ReceiptDate, locationId: 2)).ProductSummaries);
        Assert.Equal((3, 0, 3, 2120m, 0m, 0m), (all.TotalDebitQty, all.TotalCreditQty,
            all.QtyBalance, all.InventoryValue, all.TotalCredit, all.COGS));
        Assert.Equal((1, 120m, 120m), (source.QtyBalance, source.InventoryValue, source.AverageRate));
        Assert.Equal((2, 2000m, 1000m, 2000m), (destination.QtyBalance, destination.InventoryValue,
            destination.AverageRate, destination.TotalDebit));
        Assert.Equal(source.InventoryValue + destination.InventoryValue, all.InventoryValue);
        Assert.Equal(1000m, (await context.JournalEntries.FindAsync(10020))!.Credit);
        Assert.Equal(1000m, (await context.JournalEntries.FindAsync(10030))!.Debit);
    }

    [Fact]
    public async Task LaterReceivingCharge_DoesNotRepriceEarlierSalesOrChangeQuantity()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false, new Movement(100, 10, 100, 0, 1));
        AddVoucher(context, 2, DispatchDate, true, new Movement(100, 10, 0, 100, 1));
        AddVoucher(context, 3, ReceiptDate, true, new Movement(100, 10, 100, 0, 2));
        AddOutcomeReference(context, 2, 3, StockTransferService.ReceiptReferenceType);
        AddVoucher(context, 4, ReceiptDate, false, new Movement(100, 6, 0, 120, 2));
        AddVoucher(context, 5, CancellationDate, true, new Movement(100, 0, 8, 0));
        AddOutcomeReference(context, 3, 5, StockTransferService.ChargeReferenceType);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var all = Assert.Single((await ReportAsync(service, endDate: CancellationDate)).ProductSummaries);
        Assert.Equal((10, 6, 4, 48m, 60m, 60m, 50m), (all.TotalDebitQty,
            all.TotalCreditQty, all.QtyBalance, all.InventoryValue, all.COGS, all.Margin, all.MarginPercentage));
        var chargesOnly = Assert.Single((await ReportAsync(service, startDate: CancellationDate,
            endDate: CancellationDate, locationId: 2)).ProductSummaries);
        Assert.Equal((0, 0, 4, 10m, 8m, 0m, 12m), (chargesOnly.TotalDebitQty,
            chargesOnly.TotalCreditQty, chargesOnly.PreviousBalance, chargesOnly.PreviousAvgRate,
            chargesOnly.TotalDebit, chargesOnly.COGS, chargesOnly.AverageRate));
    }

    [Fact]
    public async Task Cancellation_RestoresDispatchCostAfterSourceAverageChanges()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false, new Movement(100, 10, 100, 0, 1));
        AddVoucher(context, 2, DispatchDate, true, new Movement(100, 4, 0, 40, 1));
        AddVoucher(context, 3, ReceiptDate, false, new Movement(100, 6, 120, 0, 1));
        AddVoucher(context, 4, CancellationDate, true, new Movement(100, 4, 40, 0, 1));
        AddOutcomeReference(context, 2, 4, StockTransferService.CancellationReferenceType);
        await context.SaveChangesAsync();
        var all = Assert.Single((await ReportAsync(CreateService(context), endDate: CancellationDate)).ProductSummaries);
        Assert.Equal((16, 0, 16, 220m, 0, 13.75m), (all.TotalDebitQty, all.TotalCreditQty,
            all.QtyBalance, all.InventoryValue, all.InTransitQuantity, all.AverageRate));
    }

    [Fact]
    public async Task FullDepletion_FlushesFourDecimalRoundingResidue()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false, new Movement(100, 3, 100, 0, 1));
        AddVoucher(context, 2, DispatchDate, false, new Movement(100, 1, 0, 50, 1));
        AddVoucher(context, 3, ReceiptDate, true, new Movement(100, 2, 0, 66.6667m, 1));
        AddVoucher(context, 4, CancellationDate, true, new Movement(100, 2, 66.6667m, 0, 2));
        AddOutcomeReference(context, 3, 4, StockTransferService.ReceiptReferenceType);
        AddVoucher(context, 5, CancellationDate, false, new Movement(100, 1, 0, 50, 2));
        AddVoucher(context, 6, CancellationDate, false, new Movement(100, 1, 0, 50, 2));
        await context.SaveChangesAsync();
        var all = Assert.Single((await ReportAsync(CreateService(context), endDate: CancellationDate)).ProductSummaries);
        Assert.Equal((3, 3, 0, 0m, 100m, 50m), (all.TotalDebitQty, all.TotalCreditQty,
            all.QtyBalance, all.InventoryValue, all.COGS, all.Margin));
    }

    [Fact]
    public async Task ManualUnflaggedTransfer_PreservesItsCommercialClassification()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false, new Movement(100, 3, 360, 0, 1));
        AddVoucher(context, 2, DispatchDate, false, new Movement(100, 2, 0, 240, 1), new Movement(100, 2, 240, 0, 2));
        await context.SaveChangesAsync();
        var all = Assert.Single((await ReportAsync(CreateService(context), endDate: DispatchDate)).ProductSummaries);
        Assert.Equal((5, 2, 3, 600m, 240m, 360m, 240m, 0m),
            (all.TotalDebitQty, all.TotalCreditQty, all.QtyBalance, all.TotalDebit, all.TotalCredit,
             all.InventoryValue, all.COGS, all.Margin));
    }

    [Fact]
    public async Task CategoryFilter_UsesCompleteHistoryAndWarehouseFilterDoesNotDuplicateAmbiguousStock()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false, new Movement(100, 3, 90, 0, 1));
        context.JournalEntries.Local.Single().CategoryId = 20; // Product was moved to another category.
        AddVoucher(context, 2, DispatchDate, false, new Movement(100, 1, 0, 50, 1));
        AddVoucher(context, 3, ReceiptDate, false, new Movement(100, 2, 60, 0, 1));
        context.DeliveryChallanEntries.Add(new DeliveryChallanEntry {
            DeliveryChallanVoucherId = 3003, JournalEntryId = 10030, LocationId = 2, EntryDate = ReceiptDate });
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var filtered = Assert.Single((await service.GetProductReportAsync(new ProductReportRequest {
            ProjectId = "1", CategoryId = "10", AccountId = "100", EndDate = ReceiptDate })).ProductSummaries);
        Assert.Equal((4, 120m, 30m), (filtered.QtyBalance, filtered.InventoryValue, filtered.COGS));
        var warehouse = Assert.Single((await ReportAsync(service, endDate: ReceiptDate, locationId: 1)).ProductSummaries);
        Assert.Equal((2, 60m, 30m), (warehouse.QtyBalance, warehouse.InventoryValue, warehouse.COGS));
        Assert.Empty((await ReportAsync(service, endDate: ReceiptDate, locationId: 2)).ProductSummaries);
        var breakdown = await service.GetProductStockBreakdownAsync(1, 100, ReceiptDate);
        Assert.Equal((4, 2, 0), (breakdown.TotalQuantity, breakdown.AmbiguousQuantity, breakdown.ReconciliationDifference));
        Assert.Empty((await service.GetProductReportAsync(new ProductReportRequest { ProjectId = "99" })).ProductSummaries);
    }

    [Theory]
    [InlineData("quantity")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("source")]
    public async Task InvalidTransferOutcome_IsRejectedInsteadOfInventingCost(string invalid)
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false, new Movement(100, 10, 100, 0, 1));
        AddVoucher(context, 2, DispatchDate, true, new Movement(100, 4, 0, 40, 1));
        AddVoucher(context, 3, ReceiptDate, true, new Movement(100, invalid == "quantity" ? 3 : 4, 40, 0, 2));
        if (invalid != "missing") AddOutcomeReference(context, 2, 3,
            invalid == "source" ? StockTransferService.CancellationReferenceType : StockTransferService.ReceiptReferenceType);
        if (invalid == "duplicate") {
            AddVoucher(context, 4, CancellationDate, true, new Movement(100, 4, 40, 0, 2));
            AddOutcomeReference(context, 2, 4, StockTransferService.ReceiptReferenceType);
        }
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<InventoryIntegrityException>(() => ReportAsync(CreateService(context), endDate: CancellationDate));
    }

    [Fact]
    public async Task InvalidDateRangeAndVeryLargePage_AreHandledWithoutOverflow()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        AddVoucher(context, 1, PurchaseDate, false, new Movement(100, 10, 100, 0, 1));
        await context.SaveChangesAsync();
        var service = CreateService(context);
        await Assert.ThrowsAsync<ArgumentException>(() => ReportAsync(service,
            startDate: ReceiptDate, endDate: DispatchDate));
        var result = await service.GetProductReportAsync(new ProductReportRequest {
            ProjectId = "1", Page = int.MaxValue, PageSize = int.MaxValue });
        Assert.Empty(result.ProductSummaries);
        Assert.False(result.HasMoreData);
        Assert.Equal((1, 2500, 10), (result.TotalRecords, result.PageSize, result.Totals.BalanceQuantity));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(11)]
    [InlineData(42)]
    public async Task MixedWarehouseHistories_ConserveQuantityAndValueAtEveryPostingDate(int seed)
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        context.Locations.Add(new Location { LocationId = 3, LocationName = "Third" });
        var random = new Random(seed);
        var quantities = new int[4];
        decimal externalCost = 0;
        int debitQuantity = 0, creditQuantity = 0, sequence = 1;
        var service = CreateService(context);
        for (var step = 0; step < 30; step++)
        {
            var date = PurchaseDate.AddDays(step);
            var location = random.Next(1, 4);
            var action = random.Next(3);
            if (action == 0 || quantities[location] == 0)
            {
                var qty = random.Next(1, 8);
                var cost = random.Next(1, 10000) / 100m;
                AddVoucher(context, sequence++, date, false, new Movement(100, qty, cost, 0, location));
                quantities[location] += qty; debitQuantity += qty; externalCost += cost;
            }
            else if (action == 1)
            {
                var qty = random.Next(1, quantities[location] + 1);
                AddVoucher(context, sequence++, date, false, new Movement(100, qty, 0, qty * 50m, location));
                quantities[location] -= qty; creditQuantity += qty;
            }
            else
            {
                var qty = random.Next(1, quantities[location] + 1);
                var destination = location % 3 + 1;
                var dispatch = sequence++;
                var receipt = sequence++;
                var source = Assert.Single((await ReportAsync(service, endDate: date, locationId: location)).ProductSummaries);
                var carrying = qty == source.QtyBalance ? source.InventoryValue
                    : Math.Round(source.InventoryValue / source.QtyBalance * qty, 4, MidpointRounding.AwayFromZero);
                // Entered prices revalue only the destination; source uses carrying value.
                var posted = qty * random.Next(1, 100);
                AddVoucher(context, dispatch, date, true, new Movement(100, qty, 0, posted, location));
                AddVoucher(context, receipt, date, true, new Movement(100, qty, posted, 0, destination));
                AddOutcomeReference(context, dispatch, receipt, StockTransferService.ReceiptReferenceType);
                var charge = random.Next(1, 1000) / 100m;
                var chargeId = sequence++;
                AddVoucher(context, chargeId, date, true, new Movement(100, 0, charge, 0));
                AddOutcomeReference(context, receipt, chargeId, StockTransferService.ChargeReferenceType);
                quantities[location] -= qty; quantities[destination] += qty; externalCost += charge + posted - carrying;
            }
            await context.SaveChangesAsync();
            var all = Assert.Single((await ReportAsync(service, endDate: date)).ProductSummaries);
            Assert.Equal((debitQuantity, creditQuantity, quantities.Sum(), 0),
                (all.TotalDebitQty, all.TotalCreditQty, all.QtyBalance, all.InTransitQuantity));
            Assert.Equal(externalCost, all.InventoryValue + all.COGS);
            decimal warehouseValue = 0, warehouseCOGS = 0;
            for (var warehouse = 1; warehouse <= 3; warehouse++)
            {
                var product = (await ReportAsync(service, endDate: date, locationId: warehouse)).ProductSummaries.SingleOrDefault();
                Assert.Equal(quantities[warehouse], product?.QtyBalance ?? 0);
                warehouseValue += product?.InventoryValue ?? 0;
                warehouseCOGS += product?.COGS ?? 0;
                if (product?.QtyBalance == 0) Assert.Equal(0m, product.InventoryValue);
            }
            Assert.Equal((all.InventoryValue, all.COGS), (warehouseValue, warehouseCOGS));
        }
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
