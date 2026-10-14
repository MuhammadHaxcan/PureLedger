using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories;
using PureLedger.Services;
using PureLedger.ViewModels.Approval;
using PureLedger.ViewModels.Home;
using Xunit;

namespace PureLedger.Backend.Tests;

public sealed class JournalVoucherLinkedBookerTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 10)]
    [InlineData(true, 10)]
    public async Task Posting_UsesCustomerBookerRegardlessOfBaseFlag(bool isBase, int products)
    {
        await using var fixture = await JournalBookerFixture.CreateAsync();
        await using var db = fixture.Context();
        var model = fixture.Model(Enumerable.Repeat(fixture.ProductId, products)
            .Prepend(fixture.CustomerId).ToArray());
        model.IsBaseVoucher = isBase;
        var voucher = await fixture.Journal(db).CreateVoucherAsync(model);
        await fixture.AssertVoucherAsync(voucher.JournalVoucherId, fixture.LinkedBookerId, isBase, products);
    }

    [Fact]
    public async Task Posting_TenDifferentProducts_UsesCustomerBooker()
    {
        await using var fixture = await JournalBookerFixture.CreateAsync();
        await using var db = fixture.Context();
        var products = Enumerable.Range(1, 9).Select(i => new Account
        {
            CategoryId = fixture.CategoryId, AccountGroupId = 2, AccountName = "Item " + i
        }).ToList();
        db.Accounts.AddRange(products); await db.SaveChangesAsync();
        var productIds = products.Select(x => x.AccountId).Prepend(fixture.ProductId).ToHashSet();
        var model = fixture.Model(productIds.Prepend(fixture.CustomerId).ToArray());
        foreach (var row in model.Entries.Where(x => productIds.Contains(x.AccountId!.Value)))
        { row.Qty = 10; row.Rate = 10; }
        var voucher = await fixture.Journal(db).CreateVoucherAsync(model);
        Assert.Equal(fixture.LinkedBookerId, await fixture.SavedBookerAsync(voucher.JournalVoucherId));
        var rows = await db.JournalEntries.AsNoTracking().Where(x => x.JournalVoucherId == voucher.JournalVoucherId).ToListAsync();
        Assert.Equal(11, rows.Count);
        Assert.Equal(10, rows.Count(x => productIds.Contains(x.AccountId)));
        Assert.Equal(1000, rows.Sum(x => x.Debit));
        Assert.Equal(1000, rows.Sum(x => x.Credit));
    }

    [Fact]
    public async Task QualifyingPosting_ResolvesBookerBeforeFirstDatabaseSave()
    {
        await using var fixture = await JournalBookerFixture.CreateAsync();
        await using var db = fixture.Context();
        var model = fixture.Model(); model.BookerId = null;
        var voucher = await fixture.Journal(db).CreateVoucherAsync(model);
        Assert.Equal(fixture.LinkedBookerId, await fixture.SavedBookerAsync(voucher.JournalVoucherId));
    }

    [Theory]
    [InlineData("two-customers")]
    [InlineData("duplicate-customer")]
    [InlineData("products-only")]
    [InlineData("accounts-only")]
    [InlineData("expense")]
    [InlineData("unassigned")]
    public async Task NonqualifyingPosting_RetainsSelectedBooker(string kind)
    {
        await using var fixture = await JournalBookerFixture.CreateAsync();
        await using var db = fixture.Context();
        var ids = kind switch
        {
            "two-customers" => new[] { fixture.CustomerId, fixture.OtherCustomerId, fixture.ProductId },
            "duplicate-customer" => new[] { fixture.CustomerId, fixture.CustomerId, fixture.ProductId },
            "products-only" => new[] { fixture.ProductId, fixture.ProductId },
            "accounts-only" => new[] { fixture.CustomerId, fixture.OtherCustomerId },
            "expense" => new[] { fixture.CustomerId, fixture.ProductId, fixture.ExpenseId },
            _ => new[] { fixture.UnassignedCustomerId, fixture.ProductId }
        };
        var voucher = await fixture.Journal(db).CreateVoucherAsync(fixture.Model(ids));
        Assert.Equal(fixture.SelectedBookerId, await fixture.SavedBookerAsync(voucher.JournalVoucherId));
    }

    [Fact]
    public async Task RetainedDisabledAssignment_StillOverridesSelectedBooker()
    {
        await using var fixture = await JournalBookerFixture.CreateAsync();
        await using var db = fixture.Context();
        (await db.Bookers.FindAsync(fixture.LinkedBookerId))!.IsEnabled = false;
        await db.SaveChangesAsync();
        var voucher = await fixture.Journal(db).CreateVoucherAsync(fixture.Model());
        Assert.Equal(fixture.LinkedBookerId, await fixture.SavedBookerAsync(voucher.JournalVoucherId));
    }

    [Fact]
    public async Task FullVoucherEdit_UsesFinalCustomerAndRetainsExistingRowIds()
    {
        await using var fixture = await JournalBookerFixture.CreateAsync();
        await VerifyEditingAsync(fixture);
    }

    internal static async Task VerifyEditingAsync(JournalBookerFixture fixture)
    {
        await using var db = fixture.Context();
        var service = fixture.Journal(db);
        var voucher = await service.CreateVoucherAsync(fixture.Model());
        var model = await fixture.EditModelAsync(db, voucher.JournalVoucherId);
        var ids = model.Entries.Select(x => x.EntryId).ToArray();
        model.Entries.Single(x => x.AccountId == fixture.CustomerId).AccountId = fixture.OtherCustomerId;
        await service.UpdateVoucherAsync(voucher.JournalVoucherId, model);
        Assert.Equal(fixture.OtherCustomerId, await db.JournalEntries.AsNoTracking()
            .Where(x => x.JournalEntryId == ids[1]).Select(x => x.AccountId).SingleAsync());
        Assert.Equal(fixture.OtherBookerId, await fixture.SavedBookerAsync(voucher.JournalVoucherId));

        // AccountId can be omitted for a retained entry; classify the actual saved rows.
        model.Entries.Single(x => x.AccountId == fixture.OtherCustomerId).AccountId = null;
        await service.UpdateVoucherAsync(voucher.JournalVoucherId, model);
        Assert.Equal(fixture.OtherBookerId, await fixture.SavedBookerAsync(voucher.JournalVoucherId));
        Assert.Equal(ids.Order(), await db.JournalEntries.Where(x => x.JournalVoucherId == voucher.JournalVoucherId)
            .OrderBy(x => x.JournalEntryId).Select(x => (int?)x.JournalEntryId).ToArrayAsync());

        // Adding another Account row removes eligibility and restores the submitted fallback.
        model.Entries.Add(fixture.Row(fixture.CustomerId, debit: 10, credit: 0));
        model.Entries.First().Credit = 110;
        await service.UpdateVoucherAsync(voucher.JournalVoucherId, model);
        Assert.Equal(fixture.SelectedBookerId, await fixture.SavedBookerAsync(voucher.JournalVoucherId));

        model.Entries.RemoveAt(model.Entries.Count - 1);
        model.Entries.First().Credit = 100;
        await service.UpdateVoucherAsync(voucher.JournalVoucherId, model);
        Assert.Equal(fixture.OtherBookerId, await fixture.SavedBookerAsync(voucher.JournalVoucherId));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task LegacyBaseProcessing_UsesCurrentAssignmentAndPreservesProcessing(bool full, bool unassigned)
    {
        await using var fixture = await JournalBookerFixture.CreateAsync();
        await VerifyProcessingAsync(fixture, full, unassigned);
    }

    internal static async Task VerifyProcessingAsync(JournalBookerFixture fixture, bool full, bool unassigned)
    {
        var baseId = await fixture.LegacyBaseAsync();
        await using (var db = fixture.Context())
        {
            (await db.Accounts.FindAsync(fixture.CustomerId))!.LinkedBookerId = unassigned ? null : fixture.OtherBookerId;
            await db.SaveChangesAsync();
        }
        var resultId = await fixture.ProcessAsync(baseId, full ? 10 : 4);
        var expected = unassigned ? fixture.SelectedBookerId : fixture.OtherBookerId;
        await fixture.AssertVoucherAsync(resultId, expected, false, 1, full ? 10 : 4, processing: true);
        await using var verify = fixture.Context();
        if (full)
        {
            Assert.Equal(baseId, resultId);
            Assert.Empty(await verify.JournalVoucherReferences.Where(x => x.MainVoucherId == baseId).ToListAsync());
        }
        else
        {
            Assert.NotEqual(baseId, resultId);
            var source = await verify.JournalVouchers.Include(x => x.JournalEntries).SingleAsync(x => x.JournalVoucherId == baseId);
            Assert.True(source.IsBaseVoucher);
            Assert.Equal(fixture.SelectedBookerId, source.BookerId);
            Assert.Equal(6, source.JournalEntries.Single(x => x.AccountId == fixture.ProductId).Quantity);
            Assert.Single(await verify.JournalVoucherReferences.Where(x => x.MainVoucherId == baseId &&
                x.ReferencedVoucherId == resultId && x.ReferenceType == "DerivedVoucher").ToListAsync());

            // A later processing operation uses a new assignment without changing its earlier sibling.
            (await verify.Accounts.FindAsync(fixture.CustomerId))!.LinkedBookerId = fixture.LinkedBookerId;
            await verify.SaveChangesAsync();
            var secondId = await fixture.ProcessAsync(baseId, 2);
            Assert.Equal(fixture.LinkedBookerId, await fixture.SavedBookerAsync(secondId));
            Assert.Equal(expected, await fixture.SavedBookerAsync(resultId));
        }
    }

    [Fact]
    public async Task PartialProcessing_WithoutCustomerRowRetainsInheritedBooker()
    {
        await using var fixture = await JournalBookerFixture.CreateAsync();
        var baseId = await fixture.LegacyBaseAsync();
        var derivedId = await fixture.ProcessAsync(baseId, 4, includeCustomer: false);
        Assert.Equal(fixture.SelectedBookerId, await fixture.SavedBookerAsync(derivedId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonqualifyingLegacyBase_DoesNotOverrideBookerOnProcessing(bool full)
    {
        await using var fixture = await JournalBookerFixture.CreateAsync();
        var baseId = await fixture.LegacyBaseAsync();
        await using (var db = fixture.Context())
        {
            db.JournalEntries.Add(new JournalEntry { JournalVoucherId = baseId, CategoryId = fixture.CategoryId,
                AccountId = fixture.OtherCustomerId, Debit = 50, IsProcessed = true, Description = "Additional Account", Rbp = "N" });
            (await db.JournalEntries.SingleAsync(x => x.JournalVoucherId == baseId && x.AccountId == fixture.CustomerId)).Debit = 50;
            await db.SaveChangesAsync();
        }
        var derivedId = await fixture.ProcessAsync(baseId, full ? 10 : 4);
        Assert.Equal(fixture.SelectedBookerId, await fixture.SavedBookerAsync(derivedId));
    }

    [Fact]
    public async Task ExpenseDraftConvertedToOrdinaryVoucher_UsesLinkedBooker()
    {
        await using var fixture = await JournalBookerFixture.CreateAsync();
        await VerifyApprovalConversionAsync(fixture);
    }

    internal static async Task VerifyApprovalConversionAsync(JournalBookerFixture fixture)
    {
        await using var db = fixture.Context();
        var pending = new ApprovalVoucher { ProjectId = fixture.ProjectId, PostedByUserId = 1,
            BookerId = fixture.SelectedBookerId, SubmittedDate = fixture.Date,
            Entries = [new ApprovalVoucherEntry { AccountId = fixture.ExpenseId,
                CategoryId = fixture.CategoryId, Debit = 100, LineNumber = 1 }] };
        db.ApprovalVouchers.Add(pending);
        await db.SaveChangesAsync();
        var model = fixture.Model();
        var classifier = new PaymentReceiptClassifier(db);
        var approvals = new ApprovalService(db, fixture.Processing(db), new PaymentReceiptService(db, classifier));
        var result = await approvals.EditAsync(pending.ApprovalVoucherId, new ApprovalEditRequest
        {
            ProjectId = model.ProjectId, BookerId = model.BookerId, EntryDate = model.EntryDate,
            DueDate = model.DueDate, Entries = model.Entries
        }, userId: 1, isAdmin: true);
        var json = JsonSerializer.SerializeToElement(result);
        Assert.Equal(ApprovalStatuses.PostedNormally, json.GetProperty("status").GetString());
        Assert.Equal(fixture.LinkedBookerId, await fixture.SavedBookerAsync(json.GetProperty("journalVoucherId").GetInt32()));
    }
}

[Collection("PostgreSQL integration")]
public sealed class JournalVoucherLinkedBookerPostgresTests
{
    [PostgresFact]
    public async Task PostingAndEditing_PersistLinkedBookerAcrossRequests()
    {
        await using var fixture = await JournalBookerFixture.CreateAsync(postgres: true);
        await JournalVoucherLinkedBookerTests.VerifyEditingAsync(fixture);
    }

    [PostgresTheory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task Processing_PersistsBookerAndPreservesQuantitiesReferencesAndFallback(bool full, bool unassigned)
    {
        await using var fixture = await JournalBookerFixture.CreateAsync(postgres: true);
        await JournalVoucherLinkedBookerTests.VerifyProcessingAsync(fixture, full, unassigned);
    }

    [PostgresFact]
    public async Task OrdinaryPostingFromApproval_PersistsLinkedBooker()
    {
        await using var fixture = await JournalBookerFixture.CreateAsync(postgres: true);
        await JournalVoucherLinkedBookerTests.VerifyApprovalConversionAsync(fixture);
    }
}

internal sealed class JournalBookerFixture : IAsyncDisposable
{
    private readonly DbContextOptions<LedgerDbContext> _options;
    private readonly string? _isolatedDatabase;
    private JournalBookerFixture(DbContextOptions<LedgerDbContext> options, string? database)
        => (_options, _isolatedDatabase) = (options, database);
    public int ProjectId, CategoryId, CustomerId, OtherCustomerId, UnassignedCustomerId, ProductId, ExpenseId;
    public int SelectedBookerId, LinkedBookerId, OtherBookerId;
    public DateOnly Date = new(2026, 10, 12);
    public LedgerDbContext Context() => new(_options);
    public SharedProcessingService Processing(LedgerDbContext db) => new(new UnitOfWork(db),
        NullLogger<SharedProcessingService>.Instance, new ApprovalGuardService(db), new PaymentReceiptClassifier(db));
    public JournalEntryService Journal(LedgerDbContext db) => new(new UnitOfWork(db),
        NullLogger<JournalEntryService>.Instance, Processing(db), new ApprovalGuardService(db), new PaymentReceiptClassifier(db));

    public static async Task<JournalBookerFixture> CreateAsync(bool postgres = false)
    {
        var builder = new DbContextOptionsBuilder<LedgerDbContext>();
        string? database = null;
        if (postgres)
        {
            database = "pureledger_jv_booker_test_" + Guid.NewGuid().ToString("N");
            var cs = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PURELEDGER_TEST_POSTGRES"))
                { Database = database };
            builder.UseNpgsql(cs.ConnectionString);
        }
        else
            builder.UseInMemoryDatabase("journal-booker-" + Guid.NewGuid())
                .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        var fixture = new JournalBookerFixture(builder.Options, database);
        try
        {
            await using var db = fixture.Context();
            await db.Database.EnsureCreatedAsync();
            var project = new Project { ProjectName = "Journal Booker Test" };
            var selected = new Booker { BookerName = "UI selection" };
            var linked = new Booker { BookerName = "Ahsan" };
            var other = new Booker { BookerName = "New assignment" };
            db.Projects.Add(project); db.Bookers.AddRange(selected, linked, other);
            await db.SaveChangesAsync();
            fixture.ProjectId = project.ProjectId;
            (fixture.SelectedBookerId, fixture.LinkedBookerId, fixture.OtherBookerId) = (selected.BookerID, linked.BookerID, other.BookerID);
            var category = new Category { ProjectId = project.ProjectId, AccountTypeId = 1, CategoryName = "Journal Test" };
            db.Categories.Add(category); await db.SaveChangesAsync(); fixture.CategoryId = category.CategoryId;
            var accounts = new[]
            {
                new Account { AccountName = "Tahir", AccountGroupId = 1, LinkedBookerId = linked.BookerID },
                new Account { AccountName = "Other customer", AccountGroupId = 1, LinkedBookerId = other.BookerID },
                new Account { AccountName = "Unassigned", AccountGroupId = 1 },
                new Account { AccountName = "Product", AccountGroupId = 2 },
                new Account { AccountName = "Expense", AccountGroupId = 3 }
            };
            foreach (var account in accounts) account.CategoryId = category.CategoryId;
            db.Accounts.AddRange(accounts); await db.SaveChangesAsync();
            (fixture.CustomerId, fixture.OtherCustomerId, fixture.UnassignedCustomerId, fixture.ProductId, fixture.ExpenseId) =
                (accounts[0].AccountId, accounts[1].AccountId, accounts[2].AccountId, accounts[3].AccountId, accounts[4].AccountId);
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    public JournalEntryRowModel Row(int account, decimal debit, decimal credit) => new()
    {
        AccountId = account, CategoryId = CategoryId, Des = "Booker test", Rbp = "N",
        Qty = account == ProductId ? 10 : 0, Rate = account == ProductId ? 10 : 0,
        Debit = debit, Credit = credit
    };
    public JournalEntryViewModel Model(params int[] ids)
    {
        if (ids.Length == 0) ids = [ProductId, CustomerId];
        return new JournalEntryViewModel
        {
            ProjectId = ProjectId, EntryDate = Date, DueDate = Date.AddDays(3), CreditDays = 3,
            BookerId = SelectedBookerId,
            Entries = ids.Select((id, index) => Row(id, index == 0 ? 0 : 100, index == 0 ? 100 * (ids.Length - 1) : 0)).ToList()
        };
    }
    public async Task<JournalEntryViewModel> EditModelAsync(LedgerDbContext db, int id)
    {
        var entries = await db.JournalEntries.Where(x => x.JournalVoucherId == id).OrderBy(x => x.JournalEntryId).ToListAsync();
        var model = Model();
        model.Entries = entries.Select(e => new JournalEntryRowModel
        {
            EntryId = e.JournalEntryId, AccountId = e.AccountId, CategoryId = e.CategoryId, Des = e.Description,
            Qty = e.Quantity, Rate = e.Rate, Rbp = e.Rbp, Percent = e.DiscountPercent,
            Disc = e.Discount, Debit = e.Debit, Credit = e.Credit
        }).ToList();
        return model;
    }
    public async Task<int> LegacyBaseAsync()
    {
        await using var db = Context();
        var model = Model(); model.IsBaseVoucher = true;
        var voucher = await Journal(db).CreateVoucherAsync(model);
        voucher.BookerId = SelectedBookerId; // Simulate a base voucher saved before this feature.
        await db.SaveChangesAsync();
        return voucher.JournalVoucherId;
    }
    public async Task<int> ProcessAsync(int baseId, int quantity, bool includeCustomer = true)
    {
        await using var db = Context();
        var rows = await db.JournalEntries.Where(x => x.JournalVoucherId == baseId).ToListAsync();
        var selected = rows.Where(x => x.AccountId == ProductId || includeCustomer && x.AccountId == CustomerId)
            .Select(x => new BaseVoucherEntryViewModel { EntryId = x.JournalEntryId,
                ProcessQuantity = x.AccountId == ProductId ? quantity : 0, Rate = 10, Rbp = "N", Percent = 0 }).ToList();
        return await Journal(db).CreateDerivedVoucherAsync(baseId, selected, quantity * 10, true, Date.AddDays(1), Date.AddDays(4));
    }
    public async Task<int?> SavedBookerAsync(int id)
    {
        await using var db = Context();
        return (await db.JournalVouchers.AsNoTracking().SingleAsync(x => x.JournalVoucherId == id)).BookerId;
    }
    public async Task AssertVoucherAsync(int id, int booker, bool isBase, int products, int quantity = 10, bool processing = false)
    {
        await using var db = Context();
        var voucher = await db.JournalVouchers.AsNoTracking().Include(x => x.JournalEntries).SingleAsync(x => x.JournalVoucherId == id);
        Assert.Equal(booker, voucher.BookerId); Assert.Equal(isBase, voucher.IsBaseVoucher);
        Assert.False(voucher.IsCompleted);
        Assert.Equal(products + 1, voucher.JournalEntries.Count);
        Assert.All(voucher.JournalEntries.Where(x => x.AccountId == ProductId), x =>
        {
            Assert.Equal(quantity, x.Quantity); Assert.False(x.IsProcessed); Assert.Equal(10, x.Rate);
        });
        Assert.True(voucher.JournalEntries.Single(x => x.AccountId != ProductId).IsProcessed);
        Assert.Equal(voucher.TotalDebit, voucher.TotalCredit);
        Assert.Equal(processing ? Date.AddDays(1) : Date, voucher.EntryDate);
    }
    public async ValueTask DisposeAsync()
    {
        await using var db = Context();
        if (_isolatedDatabase != null)
        {
            Assert.StartsWith("pureledger_jv_booker_test_", _isolatedDatabase);
            Assert.Equal(_isolatedDatabase, db.Database.GetDbConnection().Database);
        }
        await db.Database.EnsureDeletedAsync();
    }
}
