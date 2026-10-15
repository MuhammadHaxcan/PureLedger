using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories;
using PureLedger.Services;
using Xunit;

namespace PureLedger.Backend.Tests;

// Regression: DELETE journal-entry/{id} used to report success while keeping the row. The repositories no longer
// save on their own, so the "mark remaining row as edited" step re-queried the row staged for deletion, took it as
// the first remaining row and Update() turned it back into a modified entity.
public sealed class JournalEntryDeleteTests
{
    private static readonly DateOnly Date = new(2026, 7, 31);

    [Theory]
    [InlineData(0)] // first row: the one an unordered query returns first
    [InlineData(1)]
    [InlineData(2)]
    public async Task DeletingAnyRow_RemovesThatRowAndMarksOnlyASurvivorAsEdited(int index)
    {
        var db = $"journal-delete-{Guid.NewGuid():N}";
        var (voucherId, entryIds) = await SeedVoucherAsync(db, rows: 3);
        var target = entryIds[index];

        var result = await RunDeleteAsync(db, target);

        Assert.True(result.Success, result.ErrorMessage);
        await using var verify = CreateContext(db);
        var remaining = await verify.JournalEntries.Where(x => x.JournalVoucherId == voucherId)
            .OrderBy(x => x.JournalEntryId).ToListAsync();
        Assert.DoesNotContain(remaining, x => x.JournalEntryId == target);
        Assert.Equal(entryIds.Where(x => x != target), remaining.Select(x => x.JournalEntryId));
        Assert.Single(remaining, x => x.IsEdited);
        Assert.True(await verify.JournalVouchers.AnyAsync(x => x.JournalVoucherId == voucherId));
    }

    [Fact]
    public async Task DeletingRowsOneByOne_RemovesEveryRowAndThenTheEmptyVoucher()
    {
        var db = $"journal-delete-{Guid.NewGuid():N}";
        var (voucherId, entryIds) = await SeedVoucherAsync(db, rows: 3);

        foreach (var id in entryIds) // always the first-created row, i.e. the worst case for the old code
        {
            var result = await RunDeleteAsync(db, id);
            Assert.True(result.Success, result.ErrorMessage);
            await using var check = CreateContext(db);
            Assert.False(await check.JournalEntries.AnyAsync(x => x.JournalEntryId == id),
                $"entry {id} was reported deleted but still exists");
        }

        await using var verify = CreateContext(db);
        Assert.Empty(await verify.JournalEntries.Where(x => x.JournalVoucherId == voucherId).ToListAsync());
        Assert.False(await verify.JournalVouchers.AnyAsync(x => x.JournalVoucherId == voucherId));
    }

    [Fact]
    public async Task DeletingTheOnlyRemainingNeverEditedRow_RemovesItAndTheVoucher()
    {
        var db = $"journal-delete-{Guid.NewGuid():N}";
        var (voucherId, entryIds) = await SeedVoucherAsync(db, rows: 1);

        var result = await RunDeleteAsync(db, entryIds[0]);

        Assert.True(result.Success, result.ErrorMessage);
        await using var verify = CreateContext(db);
        Assert.Empty(await verify.JournalEntries.Where(x => x.JournalVoucherId == voucherId).ToListAsync());
        Assert.False(await verify.JournalVouchers.AnyAsync(x => x.JournalVoucherId == voucherId));
    }

    private static async Task<(bool Success, string? ErrorMessage)> RunDeleteAsync(string db, int entryId)
    {
        // A fresh context per call mirrors one HTTP request: nothing is pre-tracked.
        await using var context = CreateContext(db);
        var unitOfWork = new UnitOfWork(context);
        var classifier = new PaymentReceiptClassifier(context);
        var guard = new ApprovalGuardService(context);
        var shared = new SharedProcessingService(unitOfWork, NullLogger<SharedProcessingService>.Instance, guard, classifier);
        var service = new JournalEntryService(unitOfWork, NullLogger<JournalEntryService>.Instance, shared, guard, classifier);
        return await service.DeleteEntryAsync(entryId);
    }

    private static async Task<(int VoucherId, int[] EntryIds)> SeedVoucherAsync(string db, int rows)
    {
        await using var context = CreateContext(db);
        context.AccountGroups.Add(new AccountGroup { AccountGroupId = 1, AccountGroupName = "Account" });
        context.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "General" });
        context.Projects.Add(new Project { ProjectId = 1, ProjectName = "Delete Regression" });
        context.Categories.Add(new Category { CategoryId = 1, ProjectId = 1, AccountTypeId = 1, CategoryName = "Cat" });
        context.Accounts.AddRange(
            new Account { AccountId = 1, CategoryId = 1, AccountGroupId = 1, AccountName = "Cash", IsEnabled = true },
            new Account { AccountId = 2, CategoryId = 1, AccountGroupId = 1, AccountName = "Bank", IsEnabled = true },
            new Account { AccountId = 3, CategoryId = 1, AccountGroupId = 1, AccountName = "Other", IsEnabled = true });
        await context.SaveChangesAsync();

        var voucher = new JournalVoucher
        {
            ProjectId = 1, EntryDate = Date, CreditableDate = Date.AddDays(1), IsCompleted = true,
            RowVersion = Guid.NewGuid().ToByteArray(),
            JournalEntries = Enumerable.Range(1, rows).Select(i => new JournalEntry
            {
                AccountId = i, CategoryId = 1, Description = $"row {i}", Debit = i == 1 ? 100 : 0, Credit = i == 1 ? 0 : 50,
                Rbp = "N", IsProcessed = true
            }).ToList()
        };
        context.JournalVouchers.Add(voucher);
        await context.SaveChangesAsync();
        var ids = await context.JournalEntries.Where(x => x.JournalVoucherId == voucher.JournalVoucherId)
            .OrderBy(x => x.JournalEntryId).Select(x => x.JournalEntryId).ToArrayAsync();
        return (voucher.JournalVoucherId, ids);
    }

    private static LedgerDbContext CreateContext(string db) => new(new DbContextOptionsBuilder<LedgerDbContext>()
        .UseInMemoryDatabase(db)
        .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);
}
