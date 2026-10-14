using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.ViewModels.Reporting;
using Xunit;

namespace PureLedger.Backend.Tests;

[Collection("PostgreSQL integration")]
public sealed class AgingFifoPostgresTests
{
    [PostgresFact]
    public async Task RealQueries_ReconcileScopesAndAllocateBeforePaging_WithoutCounterpartDuplication()
    {
        await using var db = new LedgerDbContext(new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("PURELEDGER_TEST_POSTGRES")!).Options);
        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (!await db.AccountGroups.AnyAsync(x => x.AccountGroupId == 1))
            db.AccountGroups.Add(new AccountGroup { AccountGroupId = 1, AccountGroupName = "Account" });
        if (!await db.AccountTypes.AnyAsync(x => x.AccountTypeId == 1))
            db.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "General" });
        var project = new Project { ProjectName = $"FIFO {Guid.NewGuid():N}" };
        db.Projects.Add(project); await db.SaveChangesAsync();
        var category = new Category { ProjectId = project.ProjectId, AccountTypeId = 1, CategoryName = "FIFO customers" };
        db.Categories.Add(category); await db.SaveChangesAsync();
        var account = new Account { CategoryId = category.CategoryId, AccountGroupId = 1, AccountName = "FIFO customer" };
        var bank = new Account { CategoryId = category.CategoryId, AccountGroupId = 1, AccountName = "FIFO counterpart" };
        db.Accounts.AddRange(account, bank); await db.SaveChangesAsync();
        async Task<int> Add(int day, decimal debit, decimal credit, DateOnly due)
        {
            var voucher = new JournalVoucher { ProjectId = project.ProjectId, EntryDate = new(2026, 10, day), CreditableDate = due, IsCompleted = true };
            db.JournalVouchers.Add(voucher); await db.SaveChangesAsync();
            var entry = new JournalEntry { JournalVoucherId = voucher.JournalVoucherId, AccountId = account.AccountId,
                CategoryId = category.CategoryId, Debit = debit, Credit = credit, IsProcessed = true };
            db.JournalEntries.AddRange(entry, new JournalEntry { JournalVoucherId = voucher.JournalVoucherId,
                AccountId = bank.AccountId, CategoryId = category.CategoryId, Debit = credit, Credit = debit, IsProcessed = true });
            await db.SaveChangesAsync(); return entry.JournalEntryId;
        }
        var oldest = await Add(1, 200.0003m, 0, new(2026, 11, 1));
        var newer = await Add(2, 150.0004m, 0, new(2026, 10, 15));
        var payment = await Add(3, 0, 100.0005m, new(2026, 10, 16));
        await Add(25, 20, 0, new(2026, 10, 25));
        var request = new AgingFifoRequest { ProjectId = project.ProjectId, CategoryId = category.CategoryId,
            AccountId = account.AccountId, ReferenceDate = new(2026, 10, 20), View = "history", PageSize = 1 };
        var service = AgingFifoReportTests.Service(db);
        var full = await service.GetAgingFifoAsync(request);
        Assert.Equal(oldest, Assert.Single(full.Rows).JournalEntryId);
        Assert.Equal(99.9998m, full.Rows[0].RemainingDebit);
        request.Scope = "due";
        var due = await service.GetAgingFifoAsync(request);
        Assert.Equal(newer, Assert.Single(due.Rows).JournalEntryId);
        Assert.Equal(49.9999m, due.Rows[0].RemainingDebit);
        var report = await service.GetAgingReportAsync(new() { ProjectId = project.ProjectId.ToString(),
            CategoryId = category.CategoryId.ToString(), ReferenceDate = request.ReferenceDate });
        var summary = report.Entries.Single(x => x.AccountId == account.AccountId.ToString());
        Assert.Equal(summary.TotalDue, full.Summary.NetBalance);
        Assert.Equal(summary.DueTillDate, due.Summary.NetBalance);
        request.Scope = "full"; request.View = "allocations"; request.EntryId = payment;
        var allocations = await service.GetAgingFifoAsync(request);
        Assert.Equal(oldest, Assert.Single(allocations.Allocations).DebitEntryId);
        Assert.Equal(100.0005m, allocations.Allocations[0].Amount);
        await transaction.RollbackAsync();
    }
}
