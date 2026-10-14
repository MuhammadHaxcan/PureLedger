using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using PureLedger.Controllers.Api;
using PureLedger.Data;
using PureLedger.Helpers;
using PureLedger.Models;
using PureLedger.Repositories;
using PureLedger.Services;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.MetaData;
using Xunit;

namespace PureLedger.Backend.Tests;

public sealed class AccountLinkedBookerTests
{
    [Theory]
    [InlineData("{}", false, null)]
    [InlineData("{\"linkedBookerId\":null}", true, null)]
    [InlineData("{\"linkedBookerId\":11}", true, 11)]
    public void Request_DistinguishesOmittedAndExplicitNull(string json, bool provided, int? id)
    {
        var request = JsonSerializer.Deserialize<AccountRequestDto>(json)!;
        Assert.Equal(provided, request.HasLinkedBookerId);
        Assert.Equal(id, request.LinkedBookerId);
        Assert.DoesNotContain("HasLinkedBookerId", JsonSerializer.Serialize(request));
    }

    [Fact]
    public async Task Metadata_CreateEditClearPreserveAndValidateAcrossRequests()
    {
        await using var fixture = await BookerFixture.CreateAsync(false);
        await fixture.VerifyMetadataAsync();
    }

    [Fact]
    public async Task Report_TenAccountsThreeAssignedFullBalancesAndReassignment()
    {
        await using var fixture = await BookerFixture.CreateAsync(false);
        await fixture.VerifyReportAsync();
    }
}

[Collection("PostgreSQL integration")]
public sealed class AccountLinkedBookerPostgresTests
{
    [PostgresFact]
    public async Task Metadata_PersistsAcrossRequestsAndRejectsInvalidAssignments()
    {
        await using var fixture = await BookerFixture.CreateAsync(true);
        await fixture.VerifyMetadataAsync();
    }

    [PostgresFact]
    public async Task Report_QueriesFullBalancesCountsPaginationAndReassignment()
    {
        await using var fixture = await BookerFixture.CreateAsync(true);
        await fixture.VerifyReportAsync();
    }

    [PostgresFact]
    public async Task Database_EnforcesGroupForeignKeyAndRestrictedDeletion()
    {
        await using var fixture = await BookerFixture.CreateAsync(true);
        foreach (var failure in new[] { "foreign-key", "group", "delete" })
        {
            await using var db = fixture.Context();
            if (failure == "delete") db.Bookers.Remove((await db.Bookers.FindAsync(fixture.BookerA))!);
            else db.Accounts.Add(new Account { CategoryId = fixture.CategoryId, AccountName = failure,
                AccountGroupId = failure == "group" ? 2 : 1,
                LinkedBookerId = failure == "group" ? fixture.BookerA : int.MaxValue });
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(failure == "group" ? PostgresErrorCodes.CheckViolation : PostgresErrorCodes.ForeignKeyViolation,
                Assert.IsType<PostgresException>(error.InnerException).SqlState);
        }
    }

    [PostgresFact]
    public async Task Migration_PreservesExistingAccountsAsUnassigned()
    {
        var cs = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PURELEDGER_TEST_POSTGRES"));
        var database = "pureledger_booker_migration_test_" + Guid.NewGuid().ToString("N");
        cs.Database = "postgres";
        await using var admin = new NpgsqlConnection(cs.ConnectionString); await admin.OpenAsync();
        var created = false;
        try
        {
            await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin))
            { await create.ExecuteNonQueryAsync(); created = true; }
            cs.Database = database;
            await using var db = new LedgerDbContext(new DbContextOptionsBuilder<LedgerDbContext>().UseNpgsql(cs.ConnectionString).Options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261010150600_AddDeliveryChallanStation");
            var project = new Project { ProjectName = "Booker migration" };
            db.Projects.Add(project); await db.SaveChangesAsync();
            var category = new Category { ProjectId = project.ProjectId, AccountTypeId = 1, CategoryName = "Customers" };
            db.Categories.Add(category); await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Accounts\" (\"AccountName\", \"CategoryId\", \"AccountGroupId\", \"IsEnabled\") VALUES ({"Legacy customer"}, {category.CategoryId}, 1, true)");
            await migrator.MigrateAsync();
            var account = await db.Accounts.SingleAsync();
            Assert.Equal("Legacy customer", account.AccountName); Assert.Null(account.LinkedBookerId);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            if (created)
            {
                Assert.StartsWith("pureledger_booker_migration_test_", database);
                await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin);
                await drop.ExecuteNonQueryAsync();
            }
        }
    }
}

internal sealed class BookerFixture : IAsyncDisposable
{
    private readonly DbContextOptions<LedgerDbContext> _options;
    private readonly bool _postgres;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    public int ProjectId { get; private set; }
    public int CategoryId { get; private set; }
    public int BookerA { get; private set; }
    public int BookerB { get; private set; }
    private int DisabledBooker { get; set; }
    private readonly List<int> _accounts = new();
    private BookerFixture(DbContextOptions<LedgerDbContext> options, bool postgres) { _options = options; _postgres = postgres; }
    public LedgerDbContext Context() => new(_options);
    private MetaDataService Service(LedgerDbContext db) => new(new UnitOfWork(db), NullLogger<MetaDataService>.Instance, _cache);
    private MetaDataApiController Controller(LedgerDbContext db) => new(
        new MetaDataValidationService(new UnitOfWork(db), NullLogger<MetaDataValidationService>.Instance), Service(db),
        new CurrentUserService(new HttpContextAccessor()), new CacheInvalidationService(_cache, NullLogger<CacheInvalidationService>.Instance),
        NullLogger<MetaDataApiController>.Instance);
    public static async Task<BookerFixture> CreateAsync(bool postgres)
    {
        var builder = new DbContextOptionsBuilder<LedgerDbContext>();
        if (postgres) builder.UseNpgsql(Environment.GetEnvironmentVariable("PURELEDGER_TEST_POSTGRES"));
        else builder.UseInMemoryDatabase("linked-booker-" + Guid.NewGuid());
        var fixture = new BookerFixture(builder.Options, postgres);
        await using var db = fixture.Context();
        if (!postgres) await db.Database.EnsureCreatedAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var project = new Project { ProjectName = "Booker test " + suffix };
        var a = new Booker { BookerName = "A " + suffix }; var b = new Booker { BookerName = "B " + suffix };
        var disabled = new Booker { BookerName = "Disabled " + suffix, IsEnabled = false };
        db.AddRange(project, a, b, disabled); await db.SaveChangesAsync();
        fixture.ProjectId = project.ProjectId; fixture.BookerA = a.BookerID; fixture.BookerB = b.BookerID; fixture.DisabledBooker = disabled.BookerID;
        var category = new Category { ProjectId = project.ProjectId, AccountTypeId = 1, CategoryName = "Customers" };
        db.Add(category); await db.SaveChangesAsync(); fixture.CategoryId = category.CategoryId;
        for (var i = 0; i < 10; i++)
        {
            var account = new Account { CategoryId = category.CategoryId, AccountGroupId = 1, AccountName = $"Customer {i:00}",
                LinkedBookerId = i < 3 ? a.BookerID : i < 5 ? b.BookerID : null, AccountData = new AccountData() };
            db.Add(account); await db.SaveChangesAsync(); fixture._accounts.Add(account.AccountId);
            foreach (var (booker, debit, credit, complete, isBase) in new (int?, decimal, decimal, bool, bool)[]
                { (a.BookerID, 100, 0, true, false), (b.BookerID, 0, 40, true, false), (null, 30, 0, true, false),
                  (a.BookerID, 999, 0, false, false), (a.BookerID, 888, 0, true, true) })
            {
                var voucher = new JournalVoucher { ProjectId = project.ProjectId, BookerId = booker,
                    EntryDate = new(2026, 10, 10), CreditableDate = new(2026, 10, 10), IsCompleted = complete, IsBaseVoucher = isBase };
                db.Add(voucher); await db.SaveChangesAsync();
                db.Add(new JournalEntry { JournalVoucherId = voucher.JournalVoucherId, CategoryId = category.CategoryId,
                    AccountId = account.AccountId, Debit = debit, Credit = credit });
            }
        }
        await db.SaveChangesAsync(); return fixture;
    }
    private AccountRequestDto Request(int id) => new() { AccountId = id, AccountName = "Customer 00", CategoryId = CategoryId,
        AccountGroupId = 1, AccountData = new AccountDataDto() };
    private async Task UpdateAsync(AccountRequestDto request, bool valid = true)
    {
        await using var db = Context(); var result = await Controller(db).UpdateAccount(request.AccountId, request);
        if (valid) Assert.IsType<OkObjectResult>(result); else Assert.IsType<BadRequestObjectResult>(result);
    }
    private async Task AssertLinkAsync(int id, int? booker)
    {
        await using var db = Context(); Assert.Equal(booker, (await db.Accounts.FindAsync(id))!.LinkedBookerId);
        var result = Assert.IsType<OkObjectResult>(await Controller(db).GetAccount(id));
        var data = JsonSerializer.SerializeToElement(result.Value).GetProperty("data");
        Assert.Equal(booker, data.GetProperty("linkedBookerId").ValueKind == JsonValueKind.Null ? null : data.GetProperty("linkedBookerId").GetInt32());
    }
    public async Task VerifyMetadataAsync()
    {
        await using (var db = Context())
        {
            var unassigned = Request(0); unassigned.AccountName = "Unassigned customer";
            Assert.IsType<OkObjectResult>(await Controller(db).CreateAccount(unassigned));
            Assert.Null((await db.Accounts.SingleAsync(a => a.CategoryId == CategoryId && a.AccountName == unassigned.AccountName)).LinkedBookerId);
        }
        foreach (var (group, booker) in new[] { (1, int.MaxValue), (1, 0), (1, -1), (1, DisabledBooker), (2, BookerA), (3, BookerA) })
        {
            await using var db = Context();
            var invalid = Request(0); invalid.AccountName = "Rejected customer";
            invalid.AccountGroupId = group; invalid.LinkedBookerId = booker;
            invalid.ProductData = new ProductDataDto { Pkg = 1, PkgForm = "Bag" };
            Assert.IsType<BadRequestObjectResult>(await Controller(db).CreateAccount(invalid));
            Assert.False(await db.Accounts.AnyAsync(a => a.CategoryId == CategoryId && a.AccountName == invalid.AccountName));
        }
        int createdId;
        await using (var db = Context())
        {
            var request = Request(0); request.AccountName = "Convertible customer"; request.LinkedBookerId = BookerA;
            Assert.IsType<OkObjectResult>(await Controller(db).CreateAccount(request));
            createdId = (await db.Accounts.SingleAsync(a => a.CategoryId == CategoryId && a.AccountName == request.AccountName)).AccountId;
        }
        await AssertLinkAsync(createdId, BookerA);
        var id = _accounts[0];
        _cache.Set(CacheKeys.AllAccounts, "stale"); _cache.Set(CacheKeys.AccountsByCategory(CategoryId), "stale");
        await UpdateAsync(Request(id)); await AssertLinkAsync(id, BookerA);
        Assert.False(_cache.TryGetValue(CacheKeys.AllAccounts, out _)); Assert.False(_cache.TryGetValue(CacheKeys.AccountsByCategory(CategoryId), out _));
        var update = Request(id); update.LinkedBookerId = BookerB;
        await UpdateAsync(update); await AssertLinkAsync(id, BookerB);
        await using (var db = Context()) { (await db.Bookers.FindAsync(BookerB))!.IsEnabled = false; await db.SaveChangesAsync(); }
        await UpdateAsync(Request(id)); await AssertLinkAsync(id, BookerB);
        update = Request(id); update.LinkedBookerId = BookerB; await UpdateAsync(update);
        await using (var db = Context())
        {
            var rows = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await Controller(db).GetAccounts(ProjectId)).Value).GetProperty("data");
            var row = rows.EnumerateArray().Single(a => a.GetProperty("AccountId").GetInt32() == id);
            Assert.Equal(BookerB, row.GetProperty("linkedBookerId").GetInt32()); Assert.NotNull(row.GetProperty("linkedBookerName").GetString());
            var report = new ReportingService(new UnitOfWork(db), new ApprovalGuardService(db), new PaymentReceiptClassifier(db), db);
            Assert.Contains(await report.GetAllBookersAsync(), b => b.BookerID == BookerB && !b.IsEnabled);
            Assert.Contains(await Service(db).DeleteBookerAsync(BookerA), e => e.Contains("assigned to accounts"));
        }
        update = Request(id); update.LinkedBookerId = null; await UpdateAsync(update); await AssertLinkAsync(id, null);
        update.LinkedBookerId = BookerA; await UpdateAsync(update); await AssertLinkAsync(id, BookerA);
        foreach (var booker in new[] { DisabledBooker, BookerB, int.MaxValue, 0 })
        {
            update = Request(id); update.LinkedBookerId = booker;
            await UpdateAsync(update, false); await AssertLinkAsync(id, BookerA);
        }
        foreach (var group in new[] { 2, 3 })
        {
            await using var db = Context();
            Assert.NotEmpty(await Service(db).CreateAccountAsync(new Account { AccountName = "Invalid", CategoryId = CategoryId,
                AccountGroupId = group, LinkedBookerId = BookerA }));
            Assert.False(await db.Accounts.AnyAsync(a => a.CategoryId == CategoryId && a.AccountName == "Invalid"));
        }
        var conversion = Request(createdId); conversion.AccountName = "Convertible customer"; conversion.AccountGroupId = 2;
        conversion.ProductData = new ProductDataDto { Pkg = 1, PkgForm = "Bag" };
        var invalidConversion = Request(createdId); invalidConversion.AccountName = conversion.AccountName;
        invalidConversion.AccountGroupId = 2; invalidConversion.ProductData = conversion.ProductData; invalidConversion.LinkedBookerId = BookerA;
        await UpdateAsync(invalidConversion, false); await AssertLinkAsync(createdId, BookerA);
        await UpdateAsync(conversion); await AssertLinkAsync(createdId, null);
        await using var verify = Context(); Assert.Equal(2, (await verify.Accounts.FindAsync(createdId))!.AccountGroupId);
        Assert.Empty(await Service(verify).DeleteBookerAsync(DisabledBooker));
    }
    public async Task VerifyReportAsync()
    {
        await using (var db = Context())
        {
            var repo = new JournalEntryRepository(db);
            var (all, count) = await repo.GetEntriesForAccountReportAsync(ProjectId, null, null, null, 0, 20);
            Assert.Equal(10, count); Assert.All(all, a => Assert.Equal(90m, a.NetBalance));
            var (assigned, assignedCount) = await repo.GetEntriesForAccountReportAsync(ProjectId, null, null, BookerA, 0, 20);
            Assert.Equal(3, assignedCount); Assert.Equal(_accounts.Take(3).Select(x => x.ToString()), assigned.Select(x => x.AccountId));
            Assert.All(assigned, a => { Assert.Equal(130m, a.TotalDebit); Assert.Equal(40m, a.TotalCredit); Assert.Equal(90m, a.NetBalance); });
            var (page, pageCount) = await repo.GetEntriesForAccountReportAsync(ProjectId, CategoryId, null, BookerA, 1, 2);
            Assert.Equal(3, pageCount); Assert.Equal(_accounts[2].ToString(), Assert.Single(page).AccountId);
            Assert.Empty((await repo.GetEntriesForAccountReportAsync(ProjectId, CategoryId, _accounts[3], BookerA, 0, 20)).Entries);
            Assert.Empty((await repo.GetEntriesForAccountReportAsync(ProjectId, int.MaxValue, null, BookerA, 0, 20)).Entries);
            Assert.Empty((await repo.GetEntriesForAccountReportAsync(int.MaxValue, null, null, BookerA, 0, 20)).Entries);
            Assert.Single((await repo.GetEntriesForAccountReportAsync(ProjectId, CategoryId, _accounts[0], BookerA, 0, 20)).Entries);
        }
        var update = Request(_accounts[0]); update.LinkedBookerId = BookerB; await UpdateAsync(update);
        await using var next = Context(); var repository = new JournalEntryRepository(next);
        Assert.Equal(2, (await repository.GetEntriesForAccountReportAsync(ProjectId, null, null, BookerA, 0, 20)).TotalCount);
        var reassigned = await repository.GetEntriesForAccountReportAsync(ProjectId, null, _accounts[0], BookerB, 0, 20);
        Assert.Equal(90m, Assert.Single(reassigned.Entries).NetBalance);
        Assert.Equal(10, (await repository.GetEntriesForAccountReportAsync(ProjectId, null, null, null, 0, 20)).TotalCount);
    }
    public async ValueTask DisposeAsync()
    {
        if (_postgres)
        {
            await using var db = Context();
            await db.JournalEntries.Where(x => x.JournalVoucher!.ProjectId == ProjectId).ExecuteDeleteAsync();
            await db.JournalVouchers.Where(x => x.ProjectId == ProjectId).ExecuteDeleteAsync();
            await db.Accounts.Where(x => x.Category!.ProjectId == ProjectId).ExecuteDeleteAsync();
            await db.Categories.Where(x => x.ProjectId == ProjectId).ExecuteDeleteAsync();
            await db.Projects.Where(x => x.ProjectId == ProjectId).ExecuteDeleteAsync();
            await db.Bookers.Where(x => x.BookerID == BookerA || x.BookerID == BookerB || x.BookerID == DisabledBooker).ExecuteDeleteAsync();
        }
        _cache.Dispose();
    }
}
