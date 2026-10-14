using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories;
using PureLedger.Services;
using PureLedger.ViewModels.Reporting;
using Xunit;

namespace PureLedger.Backend.Tests;

public sealed class AgingFifoReportTests
{
    [Fact]
    public async Task BothScopes_ReconcileWithExistingColumns_WithoutChangingDateSemantics()
    {
        await using var db = CreateContext();
        Seed(db);
        Add(db, 1, debit: 200, due: new(2026, 11, 1));
        Add(db, 2, debit: 150, due: new(2026, 10, 15));
        Add(db, 3, credit: 100, due: new(2026, 10, 16));
        // Preserve the existing absence of an entry-date cutoff.
        Add(db, 4, debit: 20, date: new(2026, 12, 1), due: new(2026, 12, 1));
        await db.SaveChangesAsync();
        var service = Service(db);
        var report = await service.GetAgingReportAsync(new() { ProjectId = "1", CategoryId = "10", ReferenceDate = new(2026, 10, 20) });
        var full = await service.GetAgingFifoAsync(Request("full", "history"));
        var due = await service.GetAgingFifoAsync(Request("due", "history"));
        var account = Assert.Single(report.Entries);
        Assert.Equal(270m, full.Summary.NetBalance);
        Assert.Equal(account.TotalDue, full.Summary.NetBalance);
        Assert.Equal(50m, due.Summary.NetBalance);
        Assert.Equal(account.DueTillDate, due.Summary.NetBalance);
        Assert.Equal(100m, full.Rows.Single(x => x.JournalEntryId == 1).SettledDebit);
        Assert.Equal(100m, due.Rows.Single(x => x.JournalEntryId == 2).SettledDebit);
        Assert.Equal(4, full.TotalRecords);
        Assert.Equal(2, due.TotalRecords);
    }

    [Fact]
    public async Task Eligibility_ExcludesOtherAccountsCategoriesProjectsIncompleteAndBaseVouchers()
    {
        await using var db = CreateContext(); Seed(db);
        Add(db, 1, debit: 50); Add(db, 2, credit: 10);
        Add(db, 3, debit: 999, account: 200);
        Add(db, 4, debit: 999, category: 20);
        Add(db, 5, debit: 999, project: 2);
        Add(db, 6, debit: 999, completed: false);
        Add(db, 7, debit: 999, isBase: true);
        await db.SaveChangesAsync();
        var result = await Service(db).GetAgingFifoAsync(Request());
        Assert.Equal(40m, result.Summary.NetBalance);
        Assert.Single(result.Rows);
        Assert.Equal(40m, result.Rows[0].RemainingDebit);
        // Like the summary report, disabled accounts and unprocessed lines on completed vouchers remain included.
        Assert.False((await db.Accounts.FindAsync(100))!.IsEnabled);
    }

    [Fact]
    public async Task SummaryPaging_PreservesZeroCreditAndFutureOnlyAccountSelection()
    {
        await using var db = CreateContext(); Seed(db);
        db.Accounts.AddRange(
            new Account { AccountId = 400, CategoryId = 10, AccountGroupId = 1, AccountName = "C zero" },
            new Account { AccountId = 500, CategoryId = 10, AccountGroupId = 1, AccountName = "D credit" });
        Add(db, 1, debit: 50);
        Add(db, 2, account: 200, debit: 777, due: new(2027, 1, 1));
        Add(db, 3, account: 400, debit: 10); Add(db, 4, account: 400, credit: 10);
        Add(db, 5, account: 500, credit: 20);
        await db.SaveChangesAsync();
        var service = Service(db);
        var request = new AgingReportRequest { ProjectId = "1", CategoryId = "10", ReferenceDate = new(2026, 10, 20), PageSize = 1 };
        var ids = new List<string>(); var balances = new List<decimal>();
        for (int page = 0; page < 3; page++)
        {
            request.Page = page;
            var result = await service.GetAgingReportAsync(request);
            Assert.Equal(3, result.TotalRecords);
            Assert.Equal(page < 2, result.HasMoreData);
            var account = Assert.Single(result.Entries);
            ids.Add(account.AccountId); balances.Add(account.DueTillDate);
        }
        Assert.Equal(new[] { "100", "400", "500" }, ids);
        Assert.Equal(new decimal[] { 50, 0, -20 }, balances);
    }

    [Fact]
    public async Task Pagination_HappensAfterCompleteReplay_AndAllocationsArePagedSeparately()
    {
        await using var db = CreateContext(); Seed(db);
        Add(db, 1, debit: 100); Add(db, 2, debit: 100); Add(db, 3, debit: 100); Add(db, 4, credit: 250);
        await db.SaveChangesAsync();
        var request = Request("full", "history"); request.PageSize = 1;
        var first = await Service(db).GetAgingFifoAsync(request);
        Assert.Equal(0m, Assert.Single(first.Rows).RemainingDebit);
        Assert.Equal(50m, first.Summary.NetBalance);
        Assert.True(first.HasMoreData);
        request.Page = 2;
        var third = await Service(db).GetAgingFifoAsync(request);
        Assert.Equal(50m, Assert.Single(third.Rows).RemainingDebit);
        Assert.Equal(first.LedgerVersion, third.LedgerVersion);
        request.View = "allocations"; request.EntryId = 4; request.Page = 1;
        var allocations = await Service(db).GetAgingFifoAsync(request);
        Assert.Equal(3, allocations.TotalRecords);
        Assert.Equal((2, 4, 100m), (allocations.Allocations[0].DebitEntryId, allocations.Allocations[0].CreditEntryId, allocations.Allocations[0].Amount));
        Assert.Empty(allocations.Rows);
        request.View = "outstanding"; request.EntryId = null; request.Page = 0;
        Assert.Equal(3, Assert.Single((await Service(db).GetAgingFifoAsync(request)).Rows).JournalEntryId);
    }

    [Fact]
    public async Task CreditsView_ReportsUnappliedCreditAndEmptyOutstandingView()
    {
        await using var db = CreateContext(); Seed(db);
        Add(db, 1, debit: 10); Add(db, 2, credit: 15); await db.SaveChangesAsync();
        var result = await Service(db).GetAgingFifoAsync(Request("full", "credits"));
        Assert.Equal(-5m, result.Summary.NetBalance);
        Assert.Equal(5m, Assert.Single(result.Rows).UnappliedCredit);
        Assert.Empty((await Service(db).GetAgingFifoAsync(Request())).Rows);
    }

    [Fact]
    public async Task AccountMustBelongToRequestedCategoryAndProject()
    {
        await using var db = CreateContext(); Seed(db); await db.SaveChangesAsync();
        var request = Request(); request.ProjectId = 2;
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(db).GetAgingFifoAsync(request));
        request.ProjectId = 1; request.CategoryId = 20;
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(db).GetAgingFifoAsync(request));
    }

    [Theory]
    [InlineData("other", "history", 0)]
    [InlineData("full", "unknown", 0)]
    [InlineData("full", "allocations", 0)]
    [InlineData("full", "history", -1)]
    public async Task InvalidRequestsAreRejected(string scope, string view, int page)
    {
        await using var db = CreateContext(); Seed(db); await db.SaveChangesAsync();
        var request = Request(scope, view); request.Page = page;
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).GetAgingFifoAsync(request));
    }

    [Fact]
    public async Task ChangedAmountsChangeLedgerVersion_AndReadingDoesNotWriteAllocations()
    {
        await using var db = CreateContext(); Seed(db); Add(db, 1, debit: 100); await db.SaveChangesAsync();
        var before = await Service(db).GetAgingFifoAsync(Request());
        (await db.JournalEntries.FindAsync(1))!.Debit = 101; await db.SaveChangesAsync();
        var after = await Service(db).GetAgingFifoAsync(Request());
        Assert.NotEqual(before.LedgerVersion, after.LedgerVersion);
        Assert.Empty(db.ChangeTracker.Entries().Where(x => x.State != EntityState.Unchanged));
        Assert.Equal(1, await db.JournalEntries.CountAsync());
    }

    internal static LedgerDbContext CreateContext() => new(new DbContextOptionsBuilder<LedgerDbContext>()
        .UseInMemoryDatabase($"aging-fifo-{Guid.NewGuid():N}").Options);
    internal static ReportingService Service(LedgerDbContext db) => new(new UnitOfWork(db), new ApprovalGuardService(db), new PaymentReceiptClassifier(db), db);
    internal static AgingFifoRequest Request(string scope = "full", string view = "outstanding") => new()
        { ProjectId = 1, CategoryId = 10, AccountId = 100, ReferenceDate = new(2026, 10, 20), Scope = scope, View = view };
    internal static void Seed(LedgerDbContext db)
    {
        db.Projects.AddRange(new Project { ProjectId = 1, ProjectName = "Main" }, new Project { ProjectId = 2, ProjectName = "Other" });
        db.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "General" });
        db.AccountGroups.Add(new AccountGroup { AccountGroupId = 1, AccountGroupName = "Account" });
        db.Categories.AddRange(new Category { CategoryId = 10, ProjectId = 1, AccountTypeId = 1, CategoryName = "Customers" },
            new Category { CategoryId = 20, ProjectId = 2, AccountTypeId = 1, CategoryName = "Other" });
        db.Accounts.AddRange(new Account { AccountId = 100, CategoryId = 10, AccountGroupId = 1, AccountName = "A", IsEnabled = false },
            new Account { AccountId = 200, CategoryId = 10, AccountGroupId = 1, AccountName = "B" });
    }
    internal static void Add(LedgerDbContext db, int id, decimal debit = 0, decimal credit = 0,
        DateOnly? date = null, DateOnly? due = null, int account = 100, int category = 10, int project = 1,
        bool completed = true, bool isBase = false)
    {
        db.JournalVouchers.Add(new JournalVoucher { JournalVoucherId = id, ProjectId = project,
            EntryDate = date ?? new DateOnly(2026, 10, 1).AddDays(id - 1),
            CreditableDate = due ?? new DateOnly(2026, 10, 1).AddDays(id - 1), IsCompleted = completed, IsBaseVoucher = isBase });
        db.JournalEntries.Add(new JournalEntry { JournalEntryId = id, JournalVoucherId = id,
            AccountId = account, CategoryId = category, Description = $"Movement {id}", Debit = debit, Credit = credit });
    }
}
