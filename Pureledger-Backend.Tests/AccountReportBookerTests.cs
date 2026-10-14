using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories;
using Xunit;

namespace PureLedger.Backend.Tests;

public sealed class AccountReportBookerTests
{
    [Fact]
    public async Task AccountReport_AssignedAccountsKeepFullBalances()
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase($"account-report-booker-{Guid.NewGuid():N}")
            .Options;
        await using var context = new LedgerDbContext(options);
        context.Projects.Add(new Project { ProjectId = 1, ProjectName = "Main" });
        context.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "General" });
        context.AccountGroups.Add(new AccountGroup { AccountGroupId = 1, AccountGroupName = "Account" });
        context.Categories.Add(new Category
        {
            CategoryId = 10, ProjectId = 1, AccountTypeId = 1, CategoryName = "Customers"
        });
        context.Accounts.AddRange(
            new Account { AccountId = 100, CategoryId = 10, AccountGroupId = 1, AccountName = "A", LinkedBookerId = 11 },
            new Account { AccountId = 200, CategoryId = 10, AccountGroupId = 1, AccountName = "B", LinkedBookerId = 11 });
        AddEntry(context, 1, 100, 11, debit: 100);
        AddEntry(context, 2, 100, 12, credit: 40);
        AddEntry(context, 3, 100, null, debit: 30);
        AddEntry(context, 4, 200, 11, debit: 50);
        await context.SaveChangesAsync();

        var repository = new JournalEntryRepository(context);
        var (all, allCount) = await repository.GetEntriesForAccountReportAsync(1, null, null, null, 0, 20);
        var (booker, bookerCount) = await repository.GetEntriesForAccountReportAsync(1, null, null, 11, 0, 20);
        var (page, pagedCount) = await repository.GetEntriesForAccountReportAsync(1, null, null, 11, 1, 1);

        Assert.Equal(2, allCount);
        Assert.Equal(90m, all.Single(x => x.AccountId == "100").NetBalance);
        Assert.Equal(2, bookerCount);
        Assert.Equal(90m, booker.Single(x => x.AccountId == "100").NetBalance);
        Assert.Equal(50m, booker.Single(x => x.AccountId == "200").NetBalance);
        Assert.Equal(2, pagedCount);
        Assert.Single(page);
        Assert.Equal("200", page[0].AccountId);
    }

    private static void AddEntry(LedgerDbContext context, int id, int accountId, int? bookerId,
        decimal debit = 0, decimal credit = 0)
    {
        context.JournalVouchers.Add(new JournalVoucher
        {
            JournalVoucherId = id,
            ProjectId = 1,
            BookerId = bookerId,
            EntryDate = new DateOnly(2026, 1, id),
            CreditableDate = new DateOnly(2026, 1, id + 1),
            IsCompleted = true
        });
        context.JournalEntries.Add(new JournalEntry
        {
            JournalEntryId = id,
            JournalVoucherId = id,
            AccountId = accountId,
            CategoryId = 10,
            Debit = debit,
            Credit = credit
        });
    }
}
