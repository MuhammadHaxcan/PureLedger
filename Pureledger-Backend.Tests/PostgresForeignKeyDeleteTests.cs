using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PureLedger.Models;
using PureLedger.Repositories;
using PureLedger.Services;
using Xunit;
using Fixture = PureLedger.Backend.Tests.DeliveryChallanReprocessAfterEditTests.Fixture;

namespace PureLedger.Backend.Tests;

// The in-memory provider does not enforce foreign keys, so MetaDataDeleteFailureTests can only simulate the refusal.
// Here PostgreSQL itself refuses the delete (set PURELEDGER_TEST_POSTGRES to a disposable, migrated database).
[Collection("PostgreSQL integration")]
public sealed class PostgresForeignKeyDeleteTests
{
    private static MetaDataService Service(PureLedger.Data.LedgerDbContext db) =>
        new(new UnitOfWork(db), NullLogger<MetaDataService>.Instance, new MemoryCache(new MemoryCacheOptions()));

    [PostgresFact]
    public async Task DeleteAccount_ReferencedByJournalEntries_IsRefusedByThePostgresForeignKey_AndNothingIsLost()
    {
        var f = await Fixture.CreateAsync();

        await using (var db = f.Db())
            Assert.False((await Service(db).DeleteAccountAsync(f.ProductA)).Success);

        await using var verify = f.Db();
        Assert.True(await verify.Accounts.AnyAsync(a => a.AccountId == f.ProductA));
        Assert.True(await verify.JournalEntries.AnyAsync(e => e.AccountId == f.ProductA));
    }

    [PostgresFact]
    public async Task DeleteCategory_StillReferencedByAJournalEntry_IsRefusedByThePostgresForeignKey()
    {
        var f = await Fixture.CreateAsync();
        int categoryId;
        await using (var db = f.Db())
        {
            var category = new Category { ProjectId = f.ProjectId, CategoryName = "Referenced only by an entry", AccountTypeId = 1 };
            db.Add(category);
            await db.SaveChangesAsync();
            db.Add(new JournalEntry { JournalVoucherId = f.VoucherId, AccountId = f.CustomerA, CategoryId = category.CategoryId, Debit = 1, Rbp = "N", IsProcessed = true });
            await db.SaveChangesAsync();
            categoryId = category.CategoryId;
        }

        await using (var db = f.Db())
            Assert.False((await Service(db).DeleteCategoryAsync(categoryId)).Success);

        await using var verify = f.Db();
        Assert.True(await verify.Categories.AnyAsync(c => c.CategoryId == categoryId));
    }

    [PostgresFact]
    public async Task DeleteAccountAndCategory_WhenUnreferenced_StillSucceedOnPostgres()
    {
        var f = await Fixture.CreateAsync();
        int categoryId, accountId;
        await using (var db = f.Db())
        {
            var category = new Category { ProjectId = f.ProjectId, CategoryName = "Unused", AccountTypeId = 1 };
            db.Add(category);
            await db.SaveChangesAsync();
            var account = new Account { CategoryId = category.CategoryId, AccountGroupId = 1, AccountName = "Unused account", IsEnabled = true };
            db.Add(account);
            await db.SaveChangesAsync();
            (categoryId, accountId) = (category.CategoryId, account.AccountId);
        }

        await using (var db = f.Db()) Assert.True((await Service(db).DeleteAccountAsync(accountId)).Success);
        await using (var db = f.Db()) Assert.True((await Service(db).DeleteCategoryAsync(categoryId)).Success);

        await using var verify = f.Db();
        Assert.False(await verify.Accounts.AnyAsync(a => a.AccountId == accountId));
        Assert.False(await verify.Categories.AnyAsync(c => c.CategoryId == categoryId));
    }
}
