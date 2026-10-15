using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories;
using PureLedger.Services;
using PureLedger.Services.Auth;
using PureLedger.ViewModels.Home;
using Xunit;
using Fixture = PureLedger.Backend.Tests.DeliveryChallanReprocessAfterEditTests.Fixture;

namespace PureLedger.Backend.Tests;

// Since 3c0935a repositories only stage changes and every service must call SaveChanges itself. These tests run each
// edit/delete/toggle operation through a fresh DbContext (one per call, like one HTTP request) and then assert on what
// a *different* context reads back, so an operation that stages but never saves - or saves something other than what
// it reported - fails here.
public sealed class StagedWriteOperationConsistencyTests
{
    private static readonly DateOnly Date = new(2026, 10, 14);

    // ---------- metadata (project / category / account / location / booker) ----------

    private sealed class MetaEnv
    {
        public readonly DbContextOptions<LedgerDbContext> Options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase("meta-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;

        public LedgerDbContext Db() => new(Options);

        public MetaDataService Service(LedgerDbContext db) =>
            new(new UnitOfWork(db), NullLogger<MetaDataService>.Instance, new MemoryCache(new MemoryCacheOptions()));

        public async Task SeedAsync()
        {
            await using var db = Db();
            db.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "t" });
            db.AccountGroups.AddRange(
                new AccountGroup { AccountGroupId = 1, AccountGroupName = "Account" },
                new AccountGroup { AccountGroupId = 2, AccountGroupName = "Product" },
                new AccountGroup { AccountGroupId = 3, AccountGroupName = "Expense" });
            db.Projects.AddRange(new Project { ProjectId = 1, ProjectName = "P1" }, new Project { ProjectId = 2, ProjectName = "P2" });
            db.Categories.Add(new Category { CategoryId = 1, ProjectId = 1, AccountTypeId = 1, CategoryName = "C1" });
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task ProjectCategoryLocationBooker_UpdateToggleDelete_AreAllPersisted()
    {
        var env = new MetaEnv();
        await env.SeedAsync();

        await using (var db = env.Db()) Assert.Empty(await env.Service(db).UpdateProjectAsync(new Project { ProjectId = 1, ProjectName = "P1 renamed" }));
        await using (var db = env.Db()) Assert.Equal("P1 renamed", (await db.Projects.FindAsync(1))!.ProjectName);

        await using (var db = env.Db()) Assert.Empty(await env.Service(db).UpdateCategoryAsync(
            new Category { CategoryId = 1, ProjectId = 2, AccountTypeId = 1, CategoryName = "C1 moved" }));
        await using (var db = env.Db())
        {
            var c = (await db.Categories.FindAsync(1))!;
            Assert.Equal("C1 moved", c.CategoryName);
            Assert.Equal(2, c.ProjectId);
        }

        await using (var db = env.Db()) Assert.Empty(await env.Service(db).CreateLocationAsync(new Location { LocationName = "L1" }));
        int locationId;
        await using (var db = env.Db()) locationId = (await db.Locations.SingleAsync()).LocationId;
        await using (var db = env.Db()) Assert.Empty(await env.Service(db).UpdateLocationAsync(
            new Location { LocationId = locationId, LocationName = "L1 renamed", Phone = "123", Region = "North" }));
        await using (var db = env.Db())
        {
            var l = await db.Locations.SingleAsync();
            Assert.Equal(("L1 renamed", "123", "North"), (l.LocationName, l.Phone, l.Region));
        }
        await using (var db = env.Db()) Assert.Empty(await env.Service(db).DeleteLocationAsync(locationId));
        await using (var db = env.Db()) Assert.Empty(await db.Locations.ToListAsync());

        await using (var db = env.Db()) Assert.Empty(await env.Service(db).CreateBookerAsync(new Booker { BookerName = "B1" }));
        int bookerId;
        await using (var db = env.Db()) bookerId = (await db.Bookers.SingleAsync()).BookerID;
        await using (var db = env.Db()) Assert.Empty(await env.Service(db).UpdateBookerAsync(new Booker { BookerID = bookerId, BookerName = "B1 renamed" }));
        await using (var db = env.Db()) Assert.Equal("B1 renamed", (await db.Bookers.SingleAsync()).BookerName);
        await using (var db = env.Db()) Assert.Empty(await env.Service(db).ToggleBookerStatusAsync(bookerId));
        await using (var db = env.Db()) Assert.False((await db.Bookers.SingleAsync()).IsEnabled);
        await using (var db = env.Db()) Assert.Empty(await env.Service(db).DeleteBookerAsync(bookerId));
        await using (var db = env.Db()) Assert.Empty(await db.Bookers.ToListAsync());
    }

    [Fact]
    public async Task Account_CreateToggleAndSwitchingGroup_SwapsTheDetailRowsAndPersists()
    {
        var env = new MetaEnv();
        await env.SeedAsync();

        await using (var db = env.Db())
            Assert.Empty(await env.Service(db).CreateAccountAsync(new Account
            {
                CategoryId = 1, AccountGroupId = 2, AccountName = "Widget", IsEnabled = true,
                ProductData = new ProductData { PkgForm = "Box", Pkg = 12, SaleRate = 5 }
            }));
        int accountId;
        await using (var db = env.Db()) accountId = (await db.Accounts.SingleAsync()).AccountId;
        await using (var db = env.Db()) Assert.Equal(1, await db.ProductDatas.CountAsync());

        // product -> customer: ProductData must go, AccountData must appear
        await using (var db = env.Db())
            Assert.Empty(await env.Service(db).UpdateAccountAsync(new Account
            {
                AccountId = accountId, CategoryId = 1, AccountGroupId = 1, AccountName = "Widget Co",
                AccountData = new AccountData { PhoneNumber = "555", City = "Lahore" }
            }));
        await using (var db = env.Db())
        {
            var a = await db.Accounts.Include(x => x.ProductData).Include(x => x.AccountData).SingleAsync();
            Assert.Equal(("Widget Co", 1), (a.AccountName, a.AccountGroupId));
            Assert.Null(a.ProductData);
            Assert.Equal("Lahore", a.AccountData!.City);
            Assert.Equal(0, await db.ProductDatas.CountAsync());
        }

        // customer -> product: the reverse
        await using (var db = env.Db())
            Assert.Empty(await env.Service(db).UpdateAccountAsync(new Account
            {
                AccountId = accountId, CategoryId = 1, AccountGroupId = 2, AccountName = "Widget",
                ProductData = new ProductData { PkgForm = "Carton", Pkg = 6, SaleRate = 9 }
            }));
        await using (var db = env.Db())
        {
            var a = await db.Accounts.Include(x => x.ProductData).Include(x => x.AccountData).SingleAsync();
            Assert.Null(a.AccountData);
            Assert.Equal("Carton", a.ProductData!.PkgForm);
            Assert.Equal(0, await db.AccountDatas.CountAsync());
        }

        await using (var db = env.Db()) Assert.Empty(await env.Service(db).ToggleAccountStatusAsync(accountId));
        await using (var db = env.Db()) Assert.False((await db.Accounts.SingleAsync()).IsEnabled);

        // delete removes the account and its detail row
        await using (var db = env.Db()) Assert.True((await env.Service(db).DeleteAccountAsync(accountId)).Success);
        await using (var db = env.Db())
        {
            Assert.Empty(await db.Accounts.ToListAsync());
            Assert.Empty(await db.ProductDatas.ToListAsync());
        }
    }

    // ---------- journal: missed entries, FOR charges, derived vouchers, no-op edit ----------

    [Fact]
    public async Task MarkEntriesAsMissed_PersistsMissedRowsAndZeroedQuantities()
    {
        var f = await Fixture.CreateAsync();
        int baseId;
        await using (var db = f.Db())
        {
            var baseVoucher = new JournalVoucher
            {
                ProjectId = (await db.Projects.FirstAsync()).ProjectId, EntryDate = Date, CreditableDate = Date,
                IsBaseVoucher = true, RowVersion = Guid.NewGuid().ToByteArray(),
                JournalEntries =
                {
                    new JournalEntry { AccountId = f.CustomerA, CategoryId = f.CategoryId, Debit = 500, Rbp = "N", IsProcessed = true },
                    new JournalEntry { AccountId = f.ProductA, CategoryId = f.CategoryId, Quantity = 5, Rate = 100, Credit = 500, Rbp = "N" }
                }
            };
            db.Add(baseVoucher);
            await db.SaveChangesAsync();
            baseId = baseVoucher.JournalVoucherId;
        }

        await using (var db = f.Db()) await f.NewServices(db).Journal.MarkEntriesAsMissedAsync(baseId);

        await using var verify = f.Db();
        var missed = await verify.MissedEntries.SingleAsync();
        Assert.Equal((f.CustomerA, f.ProductA, 5), (missed.CustomerAccountId, missed.ProductAccountId, (int)missed.Quantity));
        Assert.Equal(0, (await verify.JournalEntries.SingleAsync(e => e.JournalVoucherId == baseId && e.AccountId == f.ProductA)).Quantity);
    }

    [Fact]
    public async Task ForCharges_CreateAndClearPending_PersistVoucherEntriesReferenceAndFlag()
    {
        var f = await Fixture.CreateAsync();
        await f.ProcessBatchAsync();
        int expenseAccountId;
        await using (var db = f.Db())
        {
            var projectId = (await db.Projects.FirstAsync()).ProjectId;
            var cat = new Category { ProjectId = projectId, CategoryName = "FOR", AccountTypeId = 1 };
            db.Add(cat);
            await db.SaveChangesAsync();
            var expense = new Account { CategoryId = cat.CategoryId, AccountGroupId = 3, AccountName = "FOR expense", IsEnabled = true };
            db.Add(expense);
            (await db.ProjectCartageSettings.SingleAsync(s => s.ProjectId == projectId)).ForExpenseCategoryId = cat.CategoryId;
            (await db.JournalVouchers.SingleAsync(v => v.JournalVoucherId == f.VoucherId)).PendingForCharges = true;
            await db.SaveChangesAsync();
            expenseAccountId = expense.AccountId;
        }

        await using (var db = f.Db())
            await f.NewServices(db).Dc.ProcessForChargesAsync(f.VoucherId, f.ProductEntryId, 75, Date, expenseAccountId);

        await using (var verify = f.Db())
        {
            var reference = await verify.JournalVoucherReferences.SingleAsync(r => r.MainVoucherId == f.VoucherId);
            Assert.Equal("ForCharges", reference.ReferenceType);
            var forVoucher = await verify.JournalVouchers.Include(v => v.JournalEntries)
                .SingleAsync(v => v.JournalVoucherId == reference.ReferencedVoucherId);
            Assert.True(forVoucher.IsCompleted, "FOR voucher follows its completed parent");
            Assert.Equal(2, forVoucher.JournalEntries.Count);
            Assert.Equal(75, forVoucher.JournalEntries.Sum(e => e.Debit));
            Assert.Equal(75, forVoucher.JournalEntries.Sum(e => e.Credit));
            Assert.False((await verify.JournalVouchers.SingleAsync(v => v.JournalVoucherId == f.VoucherId)).PendingForCharges);
        }

        // ClearPendingForCharges persists the flag on its own
        await using (var db = f.Db())
        {
            (await db.JournalVouchers.SingleAsync(v => v.JournalVoucherId == f.VoucherId)).PendingForCharges = true;
            await db.SaveChangesAsync();
        }
        await using (var db = f.Db()) await f.NewServices(db).Dc.ClearPendingForChargesAsync(f.VoucherId);
        await using (var verify = f.Db())
            Assert.False((await verify.JournalVouchers.SingleAsync(v => v.JournalVoucherId == f.VoucherId)).PendingForCharges);
    }

    [Fact]
    public async Task DerivedVoucher_PartialThenRemainder_ReducesBaseQuantityAndCompletesTheBase()
    {
        var f = await Fixture.CreateAsync();
        int baseId, baseProductEntryId, baseCustomerEntryId;
        await using (var db = f.Db())
        {
            var baseVoucher = new JournalVoucher
            {
                ProjectId = (await db.Projects.FirstAsync()).ProjectId, EntryDate = Date, CreditableDate = Date,
                IsBaseVoucher = true, PendingForCharges = true, RowVersion = Guid.NewGuid().ToByteArray(),
                JournalEntries =
                {
                    new JournalEntry { AccountId = f.CustomerA, CategoryId = f.CategoryId, Debit = 1000, Rbp = "N", IsProcessed = true },
                    new JournalEntry { AccountId = f.ProductA, CategoryId = f.CategoryId, Quantity = 10, Rate = 100, Credit = 1000, Rbp = "N" }
                }
            };
            db.Add(baseVoucher);
            await db.SaveChangesAsync();
            baseId = baseVoucher.JournalVoucherId;
            baseProductEntryId = baseVoucher.JournalEntries.Single(e => e.AccountId == f.ProductA).JournalEntryId;
            baseCustomerEntryId = baseVoucher.JournalEntries.Single(e => e.AccountId == f.CustomerA).JournalEntryId;
        }

        List<BaseVoucherEntryViewModel> Rows(int qty) => new()
        {
            new() { EntryId = baseCustomerEntryId, AccountId = f.CustomerA },
            new() { EntryId = baseProductEntryId, AccountId = f.ProductA, ProcessQuantity = qty, Rate = 100, Rbp = "N" }
        };

        int firstDerived, secondDerived;
        await using (var db = f.Db())
            firstDerived = await f.NewServices(db).Journal.CreateDerivedVoucherAsync(baseId, Rows(4), 400, true, Date, Date);

        await using (var verify = f.Db())
        {
            Assert.NotEqual(baseId, firstDerived);
            var product = await verify.JournalEntries.SingleAsync(e => e.JournalEntryId == baseProductEntryId);
            Assert.Equal(6, product.Quantity);
            Assert.False(product.IsProcessed);
            Assert.False((await verify.JournalVouchers.SingleAsync(v => v.JournalVoucherId == baseId)).IsCompleted);
            Assert.Single(verify.JournalVoucherReferences, r => r.MainVoucherId == baseId && r.ReferencedVoucherId == firstDerived);
            var derivedRows = await verify.JournalEntries.Where(e => e.JournalVoucherId == firstDerived).ToListAsync();
            Assert.Equal(4, derivedRows.Single(e => e.AccountId == f.ProductA).Quantity);
            Assert.Contains(derivedRows, e => e.AccountId == f.CustomerA && e.Debit == 400);
        }

        await using (var db = f.Db())
            secondDerived = await f.NewServices(db).Journal.CreateDerivedVoucherAsync(baseId, Rows(6), 600, true, Date, Date);

        await using (var verify = f.Db())
        {
            var product = await verify.JournalEntries.SingleAsync(e => e.JournalEntryId == baseProductEntryId);
            Assert.Equal(0, product.Quantity);
            Assert.True(product.IsProcessed);
            Assert.True((await verify.JournalVouchers.SingleAsync(v => v.JournalVoucherId == baseId)).IsCompleted);
            Assert.Equal(2, await verify.JournalVoucherReferences.CountAsync(r => r.MainVoucherId == baseId));
            Assert.NotEqual(firstDerived, secondDerived);
        }
    }

    [Fact]
    public async Task WholeVoucherEdit_WithNothingChanged_LeavesChallanCompletionAndEditFlagsAlone()
    {
        var f = await Fixture.CreateAsync();
        await f.ProcessBatchAsync();

        await f.ApplyEditAsync(DeliveryChallanReprocessAfterEditTests.Edit.SecondProductAdded,
            DeliveryChallanReprocessAfterEditTests.Path.WholeVoucher); // forces a real edit once...
        await f.ProcessBatchAsync();
        List<(int Id, bool Edited, bool Processed)> before;
        await using (var db = f.Db())
            before = (await db.JournalEntries.Where(e => e.JournalVoucherId == f.VoucherId).OrderBy(e => e.JournalEntryId).ToListAsync())
                .Select(e => (e.JournalEntryId, e.IsEdited, e.IsProcessed)).ToList();

        await f.RemoveRowViaVoucherEditAsync(-1); // ...then resubmit the identical rows (nothing removed, nothing changed)

        await using var verify = f.Db();
        var after = (await verify.JournalEntries.Where(e => e.JournalVoucherId == f.VoucherId).OrderBy(e => e.JournalEntryId).ToListAsync())
            .Select(e => (e.JournalEntryId, e.IsEdited, e.IsProcessed)).ToList();
        Assert.Equal(before, after);
        Assert.True((await verify.JournalVouchers.SingleAsync(v => v.JournalVoucherId == f.VoucherId)).IsCompleted);
        Assert.Equal(2, await verify.DeliveryChallanEntries.CountAsync(d => d.DeliveryChallanVoucher.JournalVoucherId == f.VoucherId));
    }

    // ---------- auth / settings ----------

    private sealed class AuthEnv
    {
        public readonly DbContextOptions<LedgerDbContext> Options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase("auth-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;

        public LedgerDbContext Db() => new(Options);

        public AuthService Service(LedgerDbContext db) => new(new HttpContextAccessor(), new UnitOfWork(db),
            NullLogger<AuthService>.Instance,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:RefreshTokenExpiryDays"] = "7" }).Build());
    }

    [Fact]
    public async Task RefreshTokens_RevokeOneAndRevokeAll_ArePersisted()
    {
        var env = new AuthEnv();
        int userId;
        await using (var db = env.Db())
        {
            var user = new User { Username = "u", PasswordHash = "x", FullName = "U" };
            db.Add(user);
            await db.SaveChangesAsync();
            userId = user.UserId;
        }
        foreach (var token in new[] { "t1", "t2", "t3" })
            await using (var db = env.Db()) await env.Service(db).CreateRefreshTokenAsync(userId, token);
        await using (var db = env.Db()) Assert.Equal(3, await db.RefreshTokens.CountAsync(t => !t.IsRevoked));

        await using (var db = env.Db()) Assert.True(await env.Service(db).RevokeRefreshTokenAsync("t1"));
        await using (var db = env.Db()) Assert.Equal(2, await db.RefreshTokens.CountAsync(t => !t.IsRevoked));

        await using (var db = env.Db()) Assert.True(await env.Service(db).RevokeAllUserTokensAsync(userId));
        await using (var db = env.Db()) Assert.Equal(0, await db.RefreshTokens.CountAsync(t => !t.IsRevoked));
    }

    [Fact]
    public async Task Signatures_CreateUpdateActivateDelete_KeepExactlyOneActiveAndPersist()
    {
        var env = new AuthEnv();
        foreach (var name in new[] { "s1", "s2" })
            await using (var db = env.Db())
                await env.Service(db).CreateSignatureAsync(new Signature { Name = name, SignatureImage = "img-" + name });
        int first, second;
        await using (var db = env.Db())
        {
            var all = await db.Signatures.OrderBy(s => s.Id).ToListAsync();
            (first, second) = (all[0].Id, all[1].Id);
        }

        await using (var db = env.Db()) await env.Service(db).SetActiveSignatureAsync(first);
        await using (var db = env.Db()) await env.Service(db).SetActiveSignatureAsync(second);
        await using (var db = env.Db())
        {
            var active = await db.Signatures.Where(s => s.IsActive).ToListAsync();
            Assert.Equal(second, Assert.Single(active).Id);
        }

        await using (var db = env.Db())
            await env.Service(db).UpdateSignatureAsync(new Signature { Id = first, Name = "s1 renamed", SignatureImage = "new", IsActive = false });
        await using (var db = env.Db()) Assert.Equal("s1 renamed", (await db.Signatures.FindAsync(first))!.Name);

        await using (var db = env.Db()) Assert.True(await env.Service(db).DeleteSignatureAsync(first));
        await using (var db = env.Db()) Assert.Equal(new[] { second }, await db.Signatures.Select(s => s.Id).ToArrayAsync());
    }

    [Fact]
    public async Task CartageSettings_AddThenUpdate_PersistBoth()
    {
        var env = new AuthEnv();
        await using (var db = env.Db())
        {
            db.Projects.Add(new Project { ProjectId = 1, ProjectName = "p" });
            db.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "t" });
            db.Categories.AddRange(new Category { CategoryId = 1, ProjectId = 1, AccountTypeId = 1, CategoryName = "a" },
                new Category { CategoryId = 2, ProjectId = 1, AccountTypeId = 1, CategoryName = "b" });
            await db.SaveChangesAsync();
        }

        await using (var db = env.Db())
            Assert.True(await env.Service(db).SaveProjectCartageSettingsAsync(new ProjectCartageSettings { ProjectId = 1, CartageTransporterCategoryId = 1, ForExpenseCategoryId = 2 }));
        await using (var db = env.Db())
            Assert.True(await env.Service(db).SaveProjectCartageSettingsAsync(new ProjectCartageSettings { ProjectId = 1, CartageTransporterCategoryId = 2, ForExpenseCategoryId = 1 }));

        await using var verify = env.Db();
        var row = await verify.ProjectCartageSettings.SingleAsync();
        Assert.Equal((2, 1), (row.CartageTransporterCategoryId, row.ForExpenseCategoryId));
    }

    [Fact]
    public async Task DeleteUser_IsPersisted_AndTheLastAdminIsProtected()
    {
        var env = new AuthEnv();
        int adminId, userId;
        await using (var db = env.Db())
        {
            var admin = new User { Username = "admin", PasswordHash = "x", FullName = "A", IsAdmin = true };
            var user = new User { Username = "user", PasswordHash = "x", FullName = "U" };
            db.AddRange(admin, user);
            await db.SaveChangesAsync();
            (adminId, userId) = (admin.UserId, user.UserId);
        }

        await using (var db = env.Db()) Assert.False(await env.Service(db).DeleteUserAsync(adminId));
        await using (var db = env.Db()) Assert.True(await env.Service(db).DeleteUserAsync(userId));

        await using var verify = env.Db();
        Assert.Equal(new[] { adminId }, await verify.Users.Select(u => u.UserId).ToArrayAsync());
    }

    [Fact]
    public async Task AppSettings_SetValue_IsPersistedAndOverwritten()
    {
        var env = new AuthEnv();
        await using (var db = env.Db()) await new SettingsService(new UnitOfWork(db)).SetSettingAsync("k", "v1");
        await using (var db = env.Db()) await new SettingsService(new UnitOfWork(db)).SetSettingAsync("k", "v2");

        await using var verify = env.Db();
        Assert.Equal("v2", await new SettingsService(new UnitOfWork(verify)).GetSettingAsync("k"));
        Assert.Equal(1, await verify.AppSettings.CountAsync());
    }
}
