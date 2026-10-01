using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories;
using PureLedger.Services;
using PureLedger.ViewModels.Reporting;
using PureLedger.ViewModels.StockTransfer;
using Xunit;

namespace PureLedger.Backend.Tests;

// Run only against a disposable, migrated database named by PURELEDGER_TEST_POSTGRES.
public sealed class ProductStockTransitPostgresTests
{
    [PostgresFact]
    public async Task DeletingPendingAndPartiallyReceivedGraphs_RemovesTransitAtEveryAsOfDate()
    {
        int projectId = 0, sourceId = 0, destinationId = 0;
        try
        {
            await using var context = CreateContext();
            await context.Database.MigrateAsync();
            if (!await context.AccountGroups.AnyAsync(x => x.AccountGroupId == 1))
                context.AccountGroups.Add(new AccountGroup { AccountGroupId = 1, AccountGroupName = "Account" });
            if (!await context.AccountGroups.AnyAsync(x => x.AccountGroupId == 2))
                context.AccountGroups.Add(new AccountGroup { AccountGroupId = 2, AccountGroupName = "Product" });
            if (!await context.AccountTypes.AnyAsync(x => x.AccountTypeId == 1))
                context.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "General" });
            await context.SaveChangesAsync();

            var suffix = Guid.NewGuid().ToString("N")[..8];
            var project = new Project { ProjectName = $"Transit test {suffix}" };
            var source = new Location { LocationName = $"Src {suffix}" };
            var destination = new Location { LocationName = $"Dst {suffix}" };
            context.Projects.Add(project);
            context.Locations.AddRange(source, destination);
            await context.SaveChangesAsync();
            projectId = project.ProjectId;
            sourceId = source.LocationId;
            destinationId = destination.LocationId;

            var productCategory = new Category { ProjectId = projectId, AccountTypeId = 1, CategoryName = "Test products" };
            var transitCategory = new Category { ProjectId = projectId, AccountTypeId = 1, CategoryName = "Test transit" };
            context.Categories.AddRange(productCategory, transitCategory);
            await context.SaveChangesAsync();
            var productA = new Account { CategoryId = productCategory.CategoryId, AccountGroupId = 2, AccountName = "Product A" };
            var productB = new Account { CategoryId = productCategory.CategoryId, AccountGroupId = 2, AccountName = "Product B" };
            var transit = new Account { CategoryId = transitCategory.CategoryId, AccountGroupId = 1, AccountName = "Transit" };
            var clearing = new Account { CategoryId = transitCategory.CategoryId, AccountGroupId = 1, AccountName = "Clearing" };
            context.Accounts.AddRange(productA, productB, transit, clearing);
            await context.SaveChangesAsync();
            context.AppSettings.AddRange(
                new AppSetting { SettingKey = $"StockTransfer:{projectId}:InTransitCategoryId", SettingValue = transitCategory.CategoryId.ToString() },
                new AppSetting { SettingKey = $"StockTransfer:{projectId}:InTransitAccountId", SettingValue = transit.AccountId.ToString() });

            var purchaseDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-3);
            var dispatchDate = purchaseDate.AddDays(1);
            var receiptDate = dispatchDate.AddDays(1);
            var purchase = new JournalVoucher {
                ProjectId = projectId, EntryDate = purchaseDate,
                CreditableDate = purchaseDate.AddDays(1), IsCompleted = true
            };
            context.JournalVouchers.Add(purchase);
            await context.SaveChangesAsync();
            var purchaseA = new JournalEntry {
                JournalVoucherId = purchase.JournalVoucherId, AccountId = productA.AccountId,
                CategoryId = productCategory.CategoryId, IsProcessed = true,
                Quantity = 10, Debit = 100, Rate = 10
            };
            var purchaseB = new JournalEntry {
                JournalVoucherId = purchase.JournalVoucherId, AccountId = productB.AccountId,
                CategoryId = productCategory.CategoryId, IsProcessed = true,
                Quantity = 5, Debit = 50, Rate = 10
            };
            context.JournalEntries.AddRange(purchaseA, purchaseB, new JournalEntry {
                JournalVoucherId = purchase.JournalVoucherId, AccountId = clearing.AccountId,
                CategoryId = transitCategory.CategoryId, IsProcessed = true, Credit = 150
            });
            await context.SaveChangesAsync();
            var dc = new DeliveryChallanVoucher {
                JournalVoucherId = purchase.JournalVoucherId, DCDate = purchaseDate, IsCompleted = true
            };
            context.DeliveryChallanVouchers.Add(dc);
            await context.SaveChangesAsync();
            context.DeliveryChallanEntries.AddRange(
                new DeliveryChallanEntry { DeliveryChallanVoucherId = dc.DeliveryChallanVoucherId, JournalEntryId = purchaseA.JournalEntryId, LocationId = sourceId, EntryDate = purchaseDate },
                new DeliveryChallanEntry { DeliveryChallanVoucherId = dc.DeliveryChallanVoucherId, JournalEntryId = purchaseB.JournalEntryId, LocationId = sourceId, EntryDate = purchaseDate });
            await context.SaveChangesAsync();

            var transfers = new StockTransferService(context, NullLogger<StockTransferService>.Instance);
            var reporting = new ReportingService(new UnitOfWork(context), new ApprovalGuardService(context),
                new PaymentReceiptClassifier(context), context);
            async Task<int> DispatchAsync() {
                await transfers.CreateAsync(new CreateStockTransferRequest {
                    ProjectId = projectId, SourceLocationId = sourceId, DestinationLocationId = destinationId,
                    TransferDate = dispatchDate,
                    Items = [
                        new() { ProductAccountId = productA.AccountId, Quantity = 4, UnitCost = 10 },
                        new() { ProductAccountId = productB.AccountId, Quantity = 2, UnitCost = 10 }
                    ]
                }, userId: 0, isAdmin: true);
                return await context.JournalVouchers.Where(x => x.ProjectId == projectId && x.IsStockTransfer &&
                    !x.IncomingReferences.Any()).OrderByDescending(x => x.JournalVoucherId)
                    .Select(x => x.JournalVoucherId).FirstAsync();
            }
            async Task DeleteAsync(int rootId) {
                var version = Convert.ToBase64String((await context.JournalVouchers.AsNoTracking()
                    .SingleAsync(x => x.JournalVoucherId == rootId)).RowVersion!);
                await transfers.DeleteAsync(rootId, new DeleteStockTransferRequest {
                    ExpectedRowVersion = version, Reason = "Integration test cleanup"
                }, userId: 0, isAdmin: true);
            }

            var pendingId = await DispatchAsync();
            Assert.Equal(4, (await reporting.GetProductStockBreakdownAsync(projectId, productA.AccountId, dispatchDate)).InTransitQuantity);
            var pendingReport = await reporting.GetProductReportAsync(new ProductReportRequest {
                ProjectId = projectId.ToString(), EndDate = dispatchDate, ExcludeNilProducts = true
            });
            Assert.Equal(4, pendingReport.ProductSummaries.Single(x => x.ProductId == productA.AccountId).InTransitQuantity);
            Assert.Equal((10, 4, 6), pendingReport.ProductSummaries
                .Where(x => x.ProductId == productA.AccountId)
                .Select(x => (x.TotalDebitQty, x.TotalCreditQty, x.QtyBalance)).Single());
            Assert.Equal((15, 6, 9), (pendingReport.Totals.TotalDebitQty,
                pendingReport.Totals.TotalCreditQty, pendingReport.Totals.BalanceQuantity));
            await DeleteAsync(pendingId);
            Assert.Equal(0, (await reporting.GetProductStockBreakdownAsync(projectId, productA.AccountId, dispatchDate)).InTransitQuantity);
            Assert.Equal(10, (await reporting.GetProductStockBreakdownAsync(projectId, productA.AccountId, dispatchDate)).TotalQuantity);

            var partialId = await DispatchAsync();
            var receiveVersion = Convert.ToBase64String((await context.JournalVouchers.AsNoTracking()
                .SingleAsync(x => x.JournalVoucherId == partialId)).RowVersion!);
            await transfers.ReceiveAsync(partialId, new ReceiveStockTransferRequest {
                ExpectedRowVersion = receiveVersion, ReceiptDate = receiptDate,
                SelectedProductAccountIds = [productA.AccountId]
            }, userId: 0, isAdmin: true);
            Assert.Equal(0, (await reporting.GetProductStockBreakdownAsync(projectId, productA.AccountId, receiptDate)).InTransitQuantity);
            Assert.Equal(2, (await reporting.GetProductStockBreakdownAsync(projectId, productB.AccountId, receiptDate)).InTransitQuantity);
            var partialReport = await reporting.GetProductReportAsync(new ProductReportRequest {
                ProjectId = projectId.ToString(), EndDate = receiptDate, ExcludeNilProducts = true
            });
            Assert.Equal((14, 4, 10), partialReport.ProductSummaries
                .Where(x => x.ProductId == productA.AccountId)
                .Select(x => (x.TotalDebitQty, x.TotalCreditQty, x.QtyBalance)).Single());
            Assert.Equal((19, 6, 13), (partialReport.Totals.TotalDebitQty,
                partialReport.Totals.TotalCreditQty, partialReport.Totals.BalanceQuantity));
            await DeleteAsync(partialId);
            foreach (var asOf in new[] { dispatchDate, receiptDate }) {
                var a = await reporting.GetProductStockBreakdownAsync(projectId, productA.AccountId, asOf);
                var b = await reporting.GetProductStockBreakdownAsync(projectId, productB.AccountId, asOf);
                Assert.Equal((10, 0), (a.TotalQuantity, a.InTransitQuantity));
                Assert.Equal((5, 0), (b.TotalQuantity, b.InTransitQuantity));
            }
        }
        finally
        {
            if (projectId != 0) {
            await using var cleanup = CreateContext();
            var voucherIds = await cleanup.JournalVouchers.Where(x => x.ProjectId == projectId)
                .Select(x => x.JournalVoucherId).ToListAsync();
            var dcIds = await cleanup.DeliveryChallanVouchers.Where(x => voucherIds.Contains(x.JournalVoucherId))
                .Select(x => x.DeliveryChallanVoucherId).ToListAsync();
            await cleanup.JournalVoucherReferences.Where(x => voucherIds.Contains(x.MainVoucherId) ||
                voucherIds.Contains(x.ReferencedVoucherId)).ExecuteDeleteAsync();
            await cleanup.DeliveryChallanEntries.Where(x => dcIds.Contains(x.DeliveryChallanVoucherId)).ExecuteDeleteAsync();
            await cleanup.DeliveryChallanVouchers.Where(x => dcIds.Contains(x.DeliveryChallanVoucherId)).ExecuteDeleteAsync();
            await cleanup.JournalEntries.Where(x => voucherIds.Contains(x.JournalVoucherId)).ExecuteDeleteAsync();
            await cleanup.JournalVouchers.Where(x => voucherIds.Contains(x.JournalVoucherId)).ExecuteDeleteAsync();
            await cleanup.AppSettings.Where(x => x.SettingKey.StartsWith($"StockTransfer:{projectId}:")).ExecuteDeleteAsync();
            var categoryIds = await cleanup.Categories.Where(x => x.ProjectId == projectId)
                .Select(x => x.CategoryId).ToListAsync();
            await cleanup.Accounts.Where(x => categoryIds.Contains(x.CategoryId)).ExecuteDeleteAsync();
            await cleanup.Categories.Where(x => categoryIds.Contains(x.CategoryId)).ExecuteDeleteAsync();
            await cleanup.Projects.Where(x => x.ProjectId == projectId).ExecuteDeleteAsync();
            await cleanup.Locations.Where(x => x.LocationId == sourceId || x.LocationId == destinationId).ExecuteDeleteAsync();
            }
        }
    }

    private static LedgerDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("PURELEDGER_TEST_POSTGRES")!).Options;
        return new LedgerDbContext(options);
    }
}
