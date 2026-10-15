using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories;
using PureLedger.Services;
using PureLedger.ViewModels.Home;
using Xunit;

namespace PureLedger.Backend.Tests;

// Regression: after a Delivery Challan is processed and the voucher is then edited (product, customer, qty ...),
// re-processing the DC must work and the product rows must end up linked to a DC entry again (that link is what
// the DC / invoice reports are built from).
public sealed class DeliveryChallanReprocessAfterEditTests
{
    private static readonly DateOnly Date = new(2026, 10, 14);

    public enum Edit
    {
        ProductRate, ProductDescription, ProductQuantity, ProductSwapped,
        CustomerSwapped, CustomerAmount, SecondProductAdded
    }

    public enum Path { SingleRow, WholeVoucher }

    [Theory]
    [InlineData(Edit.ProductRate, Path.SingleRow)]
    [InlineData(Edit.ProductDescription, Path.SingleRow)]
    [InlineData(Edit.ProductQuantity, Path.SingleRow)]
    [InlineData(Edit.ProductSwapped, Path.SingleRow)]
    [InlineData(Edit.CustomerSwapped, Path.SingleRow)]
    [InlineData(Edit.CustomerAmount, Path.SingleRow)]
    [InlineData(Edit.ProductRate, Path.WholeVoucher)]
    [InlineData(Edit.ProductDescription, Path.WholeVoucher)]
    [InlineData(Edit.ProductQuantity, Path.WholeVoucher)]
    [InlineData(Edit.ProductSwapped, Path.WholeVoucher)]
    [InlineData(Edit.CustomerSwapped, Path.WholeVoucher)]
    [InlineData(Edit.CustomerAmount, Path.WholeVoucher)]
    [InlineData(Edit.SecondProductAdded, Path.WholeVoucher)]
    public async Task EditAfterProcessing_ThenReprocessBatch_LeavesEveryProductRowOnAChallan(Edit edit, Path path)
    {
        var f = await Fixture.CreateAsync();
        await f.ProcessBatchAsync();
        await f.AssertFullyProcessedAsync();

        await f.ApplyEditAsync(edit, path);

        // A row the edit sent back to "unprocessed" must not keep pointing at its old challan row.
        await f.AssertUnprocessedRowsHaveNoChallanRowAsync();

        // Whatever the edit left unprocessed (if anything) must be processable again.
        var pending = await f.UnprocessedProductEntryCountAsync();
        if (pending > 0)
            await f.ProcessBatchAsync();

        await f.AssertFullyProcessedAsync();
    }

    [Theory]
    [InlineData(Edit.ProductQuantity, Path.SingleRow)]
    [InlineData(Edit.ProductSwapped, Path.SingleRow)]
    [InlineData(Edit.ProductQuantity, Path.WholeVoucher)]
    [InlineData(Edit.ProductSwapped, Path.WholeVoucher)]
    public async Task EditAfterProcessing_ThenReprocessSingleEntry_LeavesEveryProductRowOnAChallan(Edit edit, Path path)
    {
        var f = await Fixture.CreateAsync();
        await f.ProcessBatchAsync();
        await f.ApplyEditAsync(edit, path);

        foreach (var id in await f.UnprocessedProductEntryIdsAsync())
            await f.ProcessSingleAsync(id);

        await f.AssertFullyProcessedAsync();
    }

    [Fact]
    public async Task EditingQuantityTwice_AfterProcessing_StillReprocessesCleanly()
    {
        var f = await Fixture.CreateAsync();
        await f.ProcessBatchAsync();

        await f.ApplyEditAsync(Edit.ProductQuantity, Path.SingleRow);
        await f.ProcessBatchAsync();
        await f.AssertFullyProcessedAsync();

        await f.ApplyEditAsync(Edit.ProductQuantity, Path.WholeVoucher, quantity: 3);
        await f.ProcessBatchAsync();
        await f.AssertFullyProcessedAsync();
    }

    [Fact]
    public async Task RowsLeftHalfProcessedByTheOldBug_AreRepairedByReprocessing()
    {
        var f = await Fixture.CreateAsync();
        await f.ProcessBatchAsync();
        // State the old bug produced: challan row still present, journal row unprocessed, voucher reopened.
        await f.MakeProductRowUnprocessedKeepingChallanRowAsync();
        Assert.Equal(1, await f.UnprocessedProductEntryCountAsync());

        await f.ProcessBatchAsync();

        await f.AssertFullyProcessedAsync();
    }

    [Fact]
    public async Task SingleRowEdit_WithUnchangedQuantity_StillUpdatesTheChallanLocation()
    {
        var f = await Fixture.CreateAsync();
        await f.ProcessBatchAsync();
        var newLocation = await f.AddLocationAsync("Second store");

        await f.ApplyEditAsync(Edit.ProductRate, Path.SingleRow, locationId: newLocation);

        await f.AssertFullyProcessedAsync();
        Assert.Equal(newLocation, await f.ChallanLocationAsync());
    }

    private sealed class Fixture
    {
        private readonly DbContextOptions<LedgerDbContext> options;
        public int VoucherId, ProductA, ProductB, CustomerA, CustomerB, CategoryId, LocationId;
        public int ProductEntryId, CustomerEntryId;

        private Fixture(DbContextOptions<LedgerDbContext> options) => this.options = options;

        private LedgerDbContext Context() => new(options);

        public static async Task<Fixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<LedgerDbContext>()
                .UseInMemoryDatabase("dc-reprocess-" + Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
            var f = new Fixture(options);
            await using var db = f.Context();
            await db.Database.EnsureCreatedAsync();

            var project = new Project { ProjectName = "DC reprocess" };
            var location = new Location { LocationName = "Main store" };
            db.AddRange(project, location);
            await db.SaveChangesAsync();
            var category = new Category { ProjectId = project.ProjectId, CategoryName = "Mixed", AccountTypeId = 1 };
            db.Add(category);
            await db.SaveChangesAsync();

            var productA = new Account { CategoryId = category.CategoryId, AccountGroupId = 2, AccountName = "Product A", IsEnabled = true };
            var productB = new Account { CategoryId = category.CategoryId, AccountGroupId = 2, AccountName = "Product B", IsEnabled = true };
            var customerA = new Account { CategoryId = category.CategoryId, AccountGroupId = 1, AccountName = "Customer A", IsEnabled = true };
            var customerB = new Account { CategoryId = category.CategoryId, AccountGroupId = 1, AccountName = "Customer B", IsEnabled = true };
            db.AddRange(productA, productB, customerA, customerB);
            await db.SaveChangesAsync();

            // A sale: customer debited, product credited with a quantity.
            var voucher = new JournalVoucher
            {
                ProjectId = project.ProjectId, EntryDate = Date, CreditableDate = Date,
                RowVersion = Guid.NewGuid().ToByteArray(), IsCompleted = false,
                JournalEntries =
                {
                    new JournalEntry { AccountId = customerA.AccountId, CategoryId = category.CategoryId, Description = "sale",
                        Debit = 1000, Rbp = "N", IsProcessed = true },
                    new JournalEntry { AccountId = productA.AccountId, CategoryId = category.CategoryId, Description = "goods",
                        Quantity = 10, Rate = 100, Credit = 1000, Rbp = "N", IsProcessed = false }
                }
            };
            db.Add(voucher);
            await db.SaveChangesAsync();

            f.VoucherId = voucher.JournalVoucherId;
            f.CategoryId = category.CategoryId; f.LocationId = location.LocationId;
            f.ProductA = productA.AccountId; f.ProductB = productB.AccountId;
            f.CustomerA = customerA.AccountId; f.CustomerB = customerB.AccountId;
            f.CustomerEntryId = voucher.JournalEntries.Single(e => e.AccountId == customerA.AccountId).JournalEntryId;
            f.ProductEntryId = voucher.JournalEntries.Single(e => e.AccountId == productA.AccountId).JournalEntryId;
            return f;
        }

        private (UnitOfWork Unit, DeliveryChallanService Dc, JournalEntryService Journal) Services(LedgerDbContext db)
        {
            var unit = new UnitOfWork(db);
            var classifier = new PaymentReceiptClassifier(db);
            var guard = new ApprovalGuardService(db);
            var shared = new SharedProcessingService(unit, NullLogger<SharedProcessingService>.Instance, guard, classifier);
            return (unit,
                new DeliveryChallanService(unit, NullLogger<DeliveryChallanService>.Instance, shared, guard, classifier),
                new JournalEntryService(unit, NullLogger<JournalEntryService>.Instance, shared, guard, classifier));
        }

        public async Task ProcessBatchAsync()
        {
            await using var db = Context();
            await Services(db).Dc.ProcessBatchDCAsync(VoucherId, Date, null, LocationId, null, false, 0);
        }

        public async Task ProcessSingleAsync(int entryId)
        {
            await using var db = Context();
            await Services(db).Dc.CreateSingleDCAsync(entryId, Date, null, LocationId, null, false, 0);
        }

        public async Task ApplyEditAsync(Edit edit, Path path, int quantity = 5, int? locationId = null)
        {
            if (path == Path.SingleRow) await ApplySingleRowEditAsync(edit, quantity, locationId);
            else await ApplyWholeVoucherEditAsync(edit, quantity);
        }

        public async Task<int> AddLocationAsync(string name)
        {
            await using var db = Context();
            var location = new Location { LocationName = name };
            db.Add(location);
            await db.SaveChangesAsync();
            return location.LocationId;
        }

        public async Task<int> ChallanLocationAsync()
        {
            await using var db = Context();
            return (await db.DeliveryChallanEntries.SingleAsync(d => d.JournalEntryId == ProductEntryId)).LocationId;
        }

        public async Task MakeProductRowUnprocessedKeepingChallanRowAsync()
        {
            await using var db = Context();
            (await db.JournalEntries.SingleAsync(e => e.JournalEntryId == ProductEntryId)).IsProcessed = false;
            (await db.JournalVouchers.SingleAsync(v => v.JournalVoucherId == VoucherId)).IsCompleted = false;
            await db.SaveChangesAsync();
        }

        private async Task ApplySingleRowEditAsync(Edit edit, int quantity, int? locationId)
        {
            await using var db = Context();
            var journal = Services(db).Journal;
            var isCustomerEdit = edit is Edit.CustomerSwapped or Edit.CustomerAmount;
            var id = isCustomerEdit ? CustomerEntryId : ProductEntryId;
            var model = await journal.GetEntryForEditAsync(id) ?? throw new InvalidOperationException("entry not found");
            var update = new JournalEntryUpdateViewModel
            {
                EntryId = id, AccountId = model.AccountId, CategoryId = model.CategoryId, Description = model.Description,
                Quantity = model.Quantity, Rate = model.Rate, Discount = model.Discount,
                DiscountPercent = model.DiscountPercent, Debit = model.Debit, Credit = model.Credit,
                DeliveryChallanEntryId = model.DeliveryChallanEntryId, LocationId = locationId ?? model.LocationId
            };
            switch (edit)
            {
                case Edit.ProductRate: update.Rate = 110; update.Credit = update.Quantity * 110; break;
                case Edit.ProductDescription: update.Description = "goods (edited)"; break;
                case Edit.ProductQuantity: update.Quantity = quantity; update.Credit = quantity * update.Rate; break;
                case Edit.ProductSwapped: update.AccountId = ProductB; break;
                case Edit.CustomerSwapped: update.AccountId = CustomerB; break;
                case Edit.CustomerAmount: update.Debit = 1200; break;
                default: throw new NotSupportedException(edit.ToString());
            }
            var (success, error) = await journal.UpdateEntryAsync(update);
            Assert.True(success, error);
        }

        private async Task ApplyWholeVoucherEditAsync(Edit edit, int quantity)
        {
            await using var db = Context();
            var journal = Services(db).Journal;
            var voucher = await db.JournalVouchers.AsNoTracking().SingleAsync(v => v.JournalVoucherId == VoucherId);
            var rows = await db.JournalEntries.AsNoTracking().Where(e => e.JournalVoucherId == VoucherId)
                .OrderBy(e => e.JournalEntryId).ToListAsync();
            var model = new JournalEntryViewModel
            {
                ProjectId = voucher.ProjectId, EntryDate = voucher.EntryDate, DueDate = voucher.CreditableDate,
                BookerId = voucher.BookerId, VoucherVersion = voucher.RowVersion,
                Entries = rows.Select(e => new JournalEntryRowModel
                {
                    EntryId = e.JournalEntryId, AccountId = e.AccountId, CategoryId = e.CategoryId, Rbp = e.Rbp,
                    Des = e.Description, Qty = e.Quantity, Rate = e.Rate, Percent = e.DiscountPercent,
                    Disc = e.Discount, Debit = e.Debit, Credit = e.Credit
                }).ToList()
            };
            var product = model.Entries.Single(r => r.EntryId == ProductEntryId);
            var customer = model.Entries.Single(r => r.EntryId == CustomerEntryId);
            switch (edit)
            {
                case Edit.ProductRate: product.Rate = 110; product.Credit = product.Qty * 110; break;
                case Edit.ProductDescription: product.Des = "goods (edited)"; break;
                case Edit.ProductQuantity: product.Qty = quantity; product.Credit = quantity * product.Rate; break;
                case Edit.ProductSwapped: product.AccountId = ProductB; break;
                case Edit.CustomerSwapped: customer.AccountId = CustomerB; break;
                case Edit.CustomerAmount: customer.Debit = 1200; break;
                case Edit.SecondProductAdded:
                    model.Entries.Add(new JournalEntryRowModel
                    {
                        AccountId = ProductB, CategoryId = CategoryId, Rbp = "N", Des = "more goods",
                        Qty = 2, Rate = 50, Credit = 100, Percent = 0, Disc = 0, Debit = 0
                    });
                    break;
            }
            await journal.UpdateVoucherAsync(VoucherId, model);
        }

        public async Task<int> UnprocessedProductEntryCountAsync() => (await UnprocessedProductEntryIdsAsync()).Count;

        public async Task<List<int>> UnprocessedProductEntryIdsAsync()
        {
            await using var db = Context();
            return await db.JournalEntries
                .Where(e => e.JournalVoucherId == VoucherId && !e.IsProcessed && e.Quantity > 0 && e.Account!.AccountGroupId == 2)
                .Select(e => e.JournalEntryId).ToListAsync();
        }

        public async Task AssertUnprocessedRowsHaveNoChallanRowAsync()
        {
            await using var db = Context();
            var unprocessed = await UnprocessedProductEntryIdsAsync();
            var linked = await db.DeliveryChallanEntries.Where(d => unprocessed.Contains(d.JournalEntryId))
                .Select(d => d.JournalEntryId).ToListAsync();
            Assert.Empty(linked);
        }

        public async Task AssertFullyProcessedAsync()
        {
            await using var db = Context();
            var entries = await db.JournalEntries.Include(e => e.Account)
                .Where(e => e.JournalVoucherId == VoucherId).ToListAsync();
            var challans = await db.DeliveryChallanVouchers.Where(d => d.JournalVoucherId == VoucherId).ToListAsync();
            var dcEntries = await db.DeliveryChallanEntries
                .Where(d => d.DeliveryChallanVoucher.JournalVoucherId == VoucherId).ToListAsync();

            Assert.Single(challans);
            foreach (var product in entries.Where(e => e.Account!.AccountGroupId == 2 && e.Quantity > 0))
            {
                Assert.True(product.IsProcessed, $"product row {product.JournalEntryId} is still unprocessed");
                Assert.Single(dcEntries, d => d.JournalEntryId == product.JournalEntryId);
            }
            // No DC row may point at a journal row that is not a quantity-carrying product any more.
            foreach (var dc in dcEntries)
            {
                var row = entries.SingleOrDefault(e => e.JournalEntryId == dc.JournalEntryId);
                Assert.NotNull(row);
                Assert.Equal(2, row!.Account!.AccountGroupId);
                Assert.True(row.Quantity > 0);
            }
            Assert.All(entries, e => Assert.True(e.IsProcessed, $"entry {e.JournalEntryId} is unprocessed"));
            Assert.True((await db.JournalVouchers.SingleAsync(v => v.JournalVoucherId == VoucherId)).IsCompleted,
                "voucher should be completed once every product row has a challan");
        }
    }
}
