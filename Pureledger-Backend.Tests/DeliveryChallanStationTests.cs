using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using PureLedger.Controllers.Api;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories;
using PureLedger.Services;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.DeliveryChallan;
using Xunit;

namespace PureLedger.Backend.Tests;

public class DeliveryChallanStationTests
{
    [Theory]
    [InlineData("single")]
    [InlineData("batch")]
    [InlineData("cartage")]
    public async Task AllPaths_PersistReuseEditPreserveAndClearStation(string path)
    {
        await using var fixture = await StationFixture.CreateAsync();
        await fixture.VerifyStationLifecycleAsync(path);
    }

    [Theory]
    [InlineData("single")]
    [InlineData("batch")]
    [InlineData("cartage")]
    public async Task Service_RejectsOverlongStation(string path)
    {
        await using var fixture = await StationFixture.CreateAsync();
        var entry = await fixture.AddEntryAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.ProcessAsync(path, entry, new string('x', 251)));
    }

    [Theory]
    [InlineData(typeof(CreateDeliveryChallanEntryViewModel), "Station")]
    [InlineData(typeof(DeliveryChallanBatchViewModel), "Station")]
    [InlineData(typeof(CartageViewModel), "DcStation")]
    public void RequestValidation_AllowsNullAnd250CharactersRejects251(Type type, string property)
    {
        var attribute = type.GetProperty(property)!.GetCustomAttributes(typeof(StringLengthAttribute), false)
            .Cast<StringLengthAttribute>().Single();
        Assert.True(attribute.IsValid(null));
        Assert.True(attribute.IsValid(""));
        Assert.True(attribute.IsValid(new string('x', 250)));
        Assert.False(attribute.IsValid(new string('x', 251)));
        var omitted = JsonSerializer.Deserialize("{}", type)!;
        Assert.Null(type.GetProperty(property)!.GetValue(omitted));
    }

    [Fact]
    public async Task Lookup_ReadsHeaderWithoutAnyChallanEntries_AndReportMapsHeader()
    {
        await using var fixture = await StationFixture.CreateAsync();
        await using (var setup = fixture.Context())
        {
            setup.DeliveryChallanVouchers.Add(new()
            {
                JournalVoucherId = fixture.VoucherId, DCDate = StationFixture.Date, Station = "Header station"
            });
            await setup.SaveChangesAsync();
        }
        await using var db = fixture.Context();
        var controller = new DeliveryChallanApiController(StationFixture.Service(db),
            new CurrentUserService(new HttpContextAccessor()), NullLogger<DeliveryChallanApiController>.Instance, null!);
        var response = Assert.IsType<OkObjectResult>(await controller.CheckExistingChallan(fixture.VoucherId));
        var json = JsonSerializer.SerializeToElement(response.Value);
        Assert.Equal("Header station", json.GetProperty("challan").GetProperty("station").GetString());
        var reporting = new ReportingService(new UnitOfWork(db), new ApprovalGuardService(db), new PaymentReceiptClassifier(db), db);
        var report = await reporting.GetSingleDocReportAsync(fixture.VoucherId, VoucherType.DC);
        Assert.Equal("Header station", report!.Station);
        Assert.Null(db.Model.FindEntityType(typeof(DeliveryChallanEntry))!.FindProperty("Station"));
        Assert.Equal(250, db.Model.FindEntityType(typeof(DeliveryChallanVoucher))!.FindProperty("Station")!.GetMaxLength());
    }
}

[Collection("PostgreSQL integration")]
public class DeliveryChallanStationPostgresTests
{
    [PostgresFact]
    public async Task AdditiveMigration_PreservesExistingChallansAndLimitsOnlyHeaderStation()
    {
        var cs = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PURELEDGER_TEST_POSTGRES"));
        // Migration compatibility needs its own disposable database so it cannot
        // downgrade the database used by other integration tests.
        Assert.True(cs.Host is "localhost" or "127.0.0.1");
        var database = "pureledger_station_migration_test_" + Guid.NewGuid().ToString("N");
        cs.Database = "postgres";
        await using var admin = new NpgsqlConnection(cs.ConnectionString);
        await admin.OpenAsync();
        var created = false;
        try
        {
            await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin))
            { await create.ExecuteNonQueryAsync(); created = true; }
            cs.Database = database;
            await using var db = new LedgerDbContext(new DbContextOptionsBuilder<LedgerDbContext>().UseNpgsql(cs.ConnectionString).Options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261003102308_NewFuncFinalChanges");
            var project = new Project { ProjectName = "Migration compatibility" };
            db.Projects.Add(project); await db.SaveChangesAsync();
            var voucher = new JournalVoucher { ProjectId = project.ProjectId, EntryDate = StationFixture.Date, CreditableDate = StationFixture.Date };
            db.JournalVouchers.Add(voucher); await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"DeliveryChallanVouchers\" (\"JournalVoucherId\", \"DCDate\", \"IsCompleted\") VALUES ({voucher.JournalVoucherId}, {StationFixture.Date}, false)");
            await migrator.MigrateAsync();
            var header = await db.DeliveryChallanVouchers.SingleAsync();
            Assert.Null(header.Station);
            Assert.Equal(voucher.JournalVoucherId, header.JournalVoucherId);
            await using var connection = new NpgsqlConnection(cs.ConnectionString);
            await connection.OpenAsync();
            await using var columns = new NpgsqlCommand("SELECT table_name, is_nullable, character_maximum_length FROM information_schema.columns WHERE table_schema='public' AND column_name='Station'", connection);
            await using var reader = await columns.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("DeliveryChallanVouchers", reader.GetString(0));
            Assert.Equal("YES", reader.GetString(1));
            Assert.Equal(250, reader.GetInt32(2));
            Assert.False(await reader.ReadAsync());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            if (created)
            {
                Assert.StartsWith("pureledger_station_migration_test_", database);
                await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin);
                await drop.ExecuteNonQueryAsync();
            }
        }
    }

    [PostgresTheory]
    [InlineData("single")]
    [InlineData("batch")]
    [InlineData("cartage")]
    public async Task AllPaths_PersistAcrossSeparateRequests(string path)
    {
        await using var fixture = await StationFixture.CreateAsync(postgres: true);
        await fixture.VerifyStationLifecycleAsync(path);
    }

    [PostgresTheory]
    [InlineData("single", false)]
    [InlineData("single", true)]
    [InlineData("batch", false)]
    [InlineData("batch", true)]
    [InlineData("cartage", false)]
    [InlineData("cartage", true)]
    public async Task FailedProcessing_RollsBackStationAndEntryGraph(string path, bool existing)
    {
        await using var fixture = await StationFixture.CreateAsync(postgres: true);
        if (existing) await fixture.ProcessAsync("single", await fixture.AddEntryAsync(), "Lahore");
        var entryId = await fixture.AddEntryAsync();
        await using var before = fixture.Context();
        var vouchersBefore = await before.JournalVouchers.CountAsync(x => x.ProjectId == fixture.ProjectId);
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.ProcessAsync(path, entryId, "Karachi", invalid: true));
        await using var after = fixture.Context();
        var header = await after.DeliveryChallanVouchers.SingleOrDefaultAsync(x => x.JournalVoucherId == fixture.VoucherId);
        if (existing) Assert.Equal("Lahore", header!.Station);
        else Assert.Null(header);
        Assert.False((await after.JournalEntries.FindAsync(entryId))!.IsProcessed);
        Assert.False(await after.DeliveryChallanEntries.AnyAsync(x => x.JournalEntryId == entryId));
        Assert.Equal(vouchersBefore, await after.JournalVouchers.CountAsync(x => x.ProjectId == fixture.ProjectId));
    }

    [PostgresTheory]
    [InlineData("Karachi", "Karachi")]
    [InlineData(null, "Lahore")]
    public async Task ConcurrentProcessing_SerializesHeaderCreationAndLastCommittedValue(string? secondStation, string expected)
    {
        await using var fixture = await StationFixture.CreateAsync(postgres: true);
        var firstId = await fixture.AddEntryAsync();
        var secondId = await fixture.AddEntryAsync();
        await using var firstDb = fixture.Context();
        await using var secondDb = fixture.Context();
        var acquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstService = StationFixture.Service(firstDb, new GatedGuard(firstDb, acquired, release));
        var first = firstService.CreateSingleDCAsync(firstId, StationFixture.Date, null, fixture.LocationId, null, false, 0, station: "Lahore");
        await acquired.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = StationFixture.Service(secondDb).CreateSingleDCAsync(secondId, StationFixture.Date, null, fixture.LocationId, null, false, 0, station: secondStation);
        try
        {
            await Task.Delay(150);
            Assert.False(second.IsCompleted);
        }
        finally { release.TrySetResult(); }
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(20));
        await using var verify = fixture.Context();
        var header = await verify.DeliveryChallanVouchers.Include(x => x.Entries)
            .SingleAsync(x => x.JournalVoucherId == fixture.VoucherId);
        Assert.Equal(expected, header.Station);
        Assert.Equal(2, header.Entries.Count);
    }

    private sealed class GatedGuard(LedgerDbContext db, TaskCompletionSource acquired, TaskCompletionSource release) : IApprovalGuardService
    {
        private readonly ApprovalGuardService inner = new(db);
        public Task<bool> IsVoucherPendingAsync(int id) => inner.IsVoucherPendingAsync(id);
        public async Task EnsureVoucherNotPendingAsync(int id, bool lockVoucher = false)
        {
            await inner.EnsureVoucherNotPendingAsync(id, lockVoucher);
            acquired.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
    }
}

internal sealed class StationFixture : IAsyncDisposable
{
    private readonly DbContextOptions<LedgerDbContext> options;
    public static readonly DateOnly Date = new(2026, 10, 10);
    public int ProjectId, VoucherId, ProductId, CategoryId, LocationId, TransporterId;
    private StationFixture(DbContextOptions<LedgerDbContext> options) => this.options = options;
    public LedgerDbContext Context() => new(options);

    public static DeliveryChallanService Service(LedgerDbContext db, IApprovalGuardService? guard = null)
    {
        var unit = new UnitOfWork(db);
        guard ??= new ApprovalGuardService(db);
        var classifier = new PaymentReceiptClassifier(db);
        return new(unit, NullLogger<DeliveryChallanService>.Instance,
            new SharedProcessingService(unit, NullLogger<SharedProcessingService>.Instance, guard, classifier), guard, classifier);
    }

    public static async Task<StationFixture> CreateAsync(bool postgres = false)
    {
        var builder = new DbContextOptionsBuilder<LedgerDbContext>();
        if (postgres) builder.UseNpgsql(Environment.GetEnvironmentVariable("PURELEDGER_TEST_POSTGRES"));
        else builder.UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        var fixture = new StationFixture(builder.Options);
        await using var db = fixture.Context();
        if (!postgres) await db.Database.EnsureCreatedAsync();
        var project = new Project { ProjectName = "Station test " + Guid.NewGuid().ToString("N")[..8] };
        var location = new Location { LocationName = "Station test " + Guid.NewGuid().ToString("N")[..8] };
        db.AddRange(project, location);
        await db.SaveChangesAsync();
        var category = new Category { ProjectId = project.ProjectId, CategoryName = "Station products", AccountTypeId = 1 };
        var transporters = new Category { ProjectId = project.ProjectId, CategoryName = "Station transporters", AccountTypeId = 1 };
        db.AddRange(category, transporters);
        await db.SaveChangesAsync();
        var product = new Account { CategoryId = category.CategoryId, AccountGroupId = 2, AccountName = "Station product" };
        var transporter = new Account { CategoryId = transporters.CategoryId, AccountGroupId = 1, AccountName = "Station transporter" };
        var voucher = new JournalVoucher { ProjectId = project.ProjectId, EntryDate = Date, CreditableDate = Date, RowVersion = Guid.NewGuid().ToByteArray() };
        db.AddRange(product, transporter, voucher, new ProjectCartageSettings { ProjectId = project.ProjectId, CartageTransporterCategoryId = transporters.CategoryId });
        await db.SaveChangesAsync();
        fixture.ProjectId = project.ProjectId; fixture.VoucherId = voucher.JournalVoucherId;
        fixture.ProductId = product.AccountId; fixture.CategoryId = category.CategoryId;
        fixture.LocationId = location.LocationId; fixture.TransporterId = transporter.AccountId;
        return fixture;
    }

    public async Task<int> AddEntryAsync()
    {
        await using var db = Context();
        var entry = new JournalEntry { JournalVoucherId = VoucherId, AccountId = ProductId, CategoryId = CategoryId,
            Description = "Station acceptance entry", Quantity = 1, Rate = 100, Credit = 100, Rbp = "N" };
        db.Add(entry);
        (await db.JournalVouchers.FindAsync(VoucherId))!.IsCompleted = false;
        await db.SaveChangesAsync();
        return entry.JournalEntryId;
    }

    public async Task ProcessAsync(string path, int entryId, string? station, bool invalid = false)
    {
        await using var db = Context();
        var service = Service(db);
        if (path == "single") await service.CreateSingleDCAsync(entryId, Date, null, invalid ? int.MaxValue : LocationId, null, false, 0, station: station);
        else if (path == "batch") await service.ProcessBatchDCAsync(VoucherId, Date, null, invalid ? int.MaxValue : LocationId, null, false, 0, station: station);
        else await service.ProcessCartageAsync(new CartageViewModel { JournalVoucherId = VoucherId, EntryId = entryId,
            TransporterId = invalid ? int.MaxValue : TransporterId, Amount = 10, PostToCustomer = false,
            DcDate = Date, DcLocation = LocationId, DcStation = station });
    }

    public async Task VerifyStationLifecycleAsync(string path)
    {
        foreach (var (submitted, expected) in new (string?, string?)[] {
            ("  Lahore  ", "Lahore"), (null, "Lahore"), (" Karachi ", "Karachi"),
            (null, "Karachi"), (" \t ", null), (new string('x', 250), new string('x', 250)), ("", null) })
        {
            await ProcessAsync(path, await AddEntryAsync(), submitted);
            await using var verify = Context();
            var header = await verify.DeliveryChallanVouchers.SingleAsync(x => x.JournalVoucherId == VoucherId);
            Assert.Equal(expected, header.Station);
            Assert.Equal(1, await verify.DeliveryChallanVouchers.CountAsync(x => x.JournalVoucherId == VoucherId));
        }
    }

    public async ValueTask DisposeAsync()
    {
        await using var db = Context();
        if (!db.Database.IsNpgsql()) { await db.Database.EnsureDeletedAsync(); return; }
        var vouchers = db.JournalVouchers.Where(x => x.ProjectId == ProjectId);
        await db.JournalVoucherReferences.Where(x => vouchers.Any(v => v.JournalVoucherId == x.MainVoucherId || v.JournalVoucherId == x.ReferencedVoucherId)).ExecuteDeleteAsync();
        await db.DeliveryChallanVouchers.Where(x => x.JournalVoucher!.ProjectId == ProjectId).ExecuteDeleteAsync();
        await vouchers.ExecuteDeleteAsync();
        await db.ProjectCartageSettings.Where(x => x.ProjectId == ProjectId).ExecuteDeleteAsync();
        await db.Accounts.Where(x => x.Category!.ProjectId == ProjectId).ExecuteDeleteAsync();
        await db.Categories.Where(x => x.ProjectId == ProjectId).ExecuteDeleteAsync();
        await db.Projects.Where(x => x.ProjectId == ProjectId).ExecuteDeleteAsync();
        await db.Locations.Where(x => x.LocationId == LocationId).ExecuteDeleteAsync();
    }
}
