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

// A charge posted against a product account (cartage, freight, colour used...) has no quantity and
// often no rate. The Entry page lets the user untick "Require Quantity for Product Accounts" for it,
// and the backend must accept exactly what the page accepts.
public sealed class ProductWithoutQuantityPostingTests
{
    private const int CustomerId = 1;
    private const int ProductId = 2;

    [Theory]
    [InlineData(null, null)]
    [InlineData(0, 0.0)]
    [InlineData(0, 153.0)]
    [InlineData(null, 0.0)]
    public async Task ChargeOnProduct_WithoutQuantity_IsAccepted_WhenQuantityNotRequired(int? qty, double? rate)
    {
        await using var db = await CreateContextAsync();
        var errors = await Validator(db).ValidateAsync(Model(qty, rate.HasValue ? (decimal)rate.Value : null, requireQuantity: false));
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task ProductRow_WithoutQuantity_IsRejected_WhenQuantityRequired(int? qty)
    {
        await using var db = await CreateContextAsync();
        var errors = await Validator(db).ValidateAsync(Model(qty, 10, requireQuantity: true));
        Assert.Contains(errors, e => e.Contains("Quantity must be greater than zero"));
    }

    [Fact]
    public async Task ProductRow_WithQuantity_IsAccepted_WhenQuantityRequired()
    {
        await using var db = await CreateContextAsync();
        Assert.Empty(await Validator(db).ValidateAsync(Model(5, 10, requireQuantity: true)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NegativeRate_IsAlwaysRejected(bool requireQuantity)
    {
        await using var db = await CreateContextAsync();
        var errors = await Validator(db).ValidateAsync(Model(5, -1, requireQuantity));
        Assert.Contains(errors, e => e.Contains("Rate must be greater than or equal to zero"));
    }

    [Fact]
    public async Task NegativeQuantity_IsAlwaysRejected()
    {
        await using var db = await CreateContextAsync();
        var errors = await Validator(db).ValidateAsync(Model(-3, 10, requireQuantity: false));
        Assert.Contains(errors, e => e.Contains("Quantity cannot be negative"));
    }

    [Fact]
    public async Task PostingTheEntryPageScenario_CreatesACompletedVoucher()
    {
        // Customer credit 20,000 / product debit 20,000, empty quantity and rate, "Require Quantity" unticked.
        await using var db = await CreateContextAsync();
        var model = Model(qty: 0, rate: 0, requireQuantity: false, amount: 20000m);
        Assert.Empty(await Validator(db).ValidateAsync(model));

        var unitOfWork = new UnitOfWork(db);
        var classifier = new PaymentReceiptClassifier(db);
        var guard = new ApprovalGuardService(db);
        var processing = new SharedProcessingService(unitOfWork, NullLogger<SharedProcessingService>.Instance, guard, classifier);
        var journal = new JournalEntryService(unitOfWork, NullLogger<JournalEntryService>.Instance, processing, guard, classifier);

        var voucher = await journal.CreateVoucherAsync(model);

        db.ChangeTracker.Clear();
        var saved = await db.JournalVouchers.Include(v => v.JournalEntries).SingleAsync(v => v.JournalVoucherId == voucher.JournalVoucherId);
        Assert.True(saved.IsCompleted);
        Assert.Equal(2, saved.JournalEntries.Count);
        var product = saved.JournalEntries.Single(e => e.AccountId == ProductId);
        Assert.Equal((0, 20000m, 0m), (product.Quantity, product.Debit, product.Credit));
        Assert.True(product.IsProcessed);   // no quantity, so no delivery challan is needed
    }

    private static JournalEntryViewModel Model(int? qty, decimal? rate, bool requireQuantity, decimal amount = 100m) => new()
    {
        ProjectId = 1,
        EntryDate = new DateOnly(2026, 10, 14),
        DueDate = new DateOnly(2026, 10, 14),
        RequireQuantity = requireQuantity,
        Entries =
        [
            new JournalEntryRowModel { CategoryId = 1, AccountId = ProductId, Qty = qty, Rate = rate, Debit = amount, Rbp = "Y" },
            new JournalEntryRowModel { CategoryId = 1, AccountId = CustomerId, Credit = amount, Rbp = "Y" }
        ]
    };

    private static JournalEntryValidationService Validator(LedgerDbContext db) =>
        new(db, NullLogger<JournalEntryValidationService>.Instance);

    private static async Task<LedgerDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase("product-without-qty-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        var db = new LedgerDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Projects.Add(new Project { ProjectId = 1, ProjectName = "Main" });
        db.AccountTypes.Add(new AccountType { AccountTypeId = 100, AccountTypeName = "General" });
        db.Categories.Add(new Category { CategoryId = 1, ProjectId = 1, AccountTypeId = 100, CategoryName = "Cat" });
        db.Accounts.AddRange(
            new Account { AccountId = CustomerId, CategoryId = 1, AccountGroupId = 1, AccountName = "Customer", IsEnabled = true },
            new Account { AccountId = ProductId, CategoryId = 1, AccountGroupId = 2, AccountName = "Product", IsEnabled = true });
        await db.SaveChangesAsync();
        return db;
    }
}
