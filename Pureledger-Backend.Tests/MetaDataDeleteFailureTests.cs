using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories;
using PureLedger.Services;
using Xunit;

namespace PureLedger.Backend.Tests;

// Regression: repositories used to save (and swallow a foreign-key failure as "false") inside Delete. Since they only
// stage changes, the failure surfaced from SaveChanges as an unhandled exception => HTTP 500 instead of a clean refusal.
public sealed class MetaDataDeleteFailureTests
{
    private sealed class FailingSaveInterceptor : SaveChangesInterceptor
    {
        public bool Fail;
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
            => Fail ? throw new DbUpdateException("FK violation") : result;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => Fail ? throw new DbUpdateException("FK violation") : ValueTask.FromResult(result);
    }

    private static async Task<(MetaDataService Service, FailingSaveInterceptor Interceptor, int AccountId, int CategoryId)> SetupAsync()
    {
        var interceptor = new FailingSaveInterceptor();
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        var db = new LedgerDbContext(options);
        db.Projects.Add(new Project { ProjectId = 1, ProjectName = "p" });
        db.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "t" });
        db.AccountGroups.Add(new AccountGroup { AccountGroupId = 1, AccountGroupName = "Account" });
        db.Categories.Add(new Category { CategoryId = 1, ProjectId = 1, AccountTypeId = 1, CategoryName = "empty" });
        db.Categories.Add(new Category { CategoryId = 2, ProjectId = 1, AccountTypeId = 1, CategoryName = "with account" });
        db.Accounts.Add(new Account { AccountId = 1, CategoryId = 2, AccountGroupId = 1, AccountName = "a", IsEnabled = true });
        await db.SaveChangesAsync();
        var service = new MetaDataService(new UnitOfWork(db), NullLogger<MetaDataService>.Instance, new MemoryCache(new MemoryCacheOptions()));
        return (service, interceptor, 1, 1);
    }

    [Fact]
    public async Task DeleteAccount_WhenTheDatabaseRefusesIt_ReportsFailureInsteadOfThrowing()
    {
        var (service, interceptor, accountId, _) = await SetupAsync();
        interceptor.Fail = true;

        var (success, _) = await service.DeleteAccountAsync(accountId);

        Assert.False(success);
    }

    [Fact]
    public async Task DeleteCategory_WhenTheDatabaseRefusesIt_ReportsFailureInsteadOfThrowing()
    {
        var (service, interceptor, _, categoryId) = await SetupAsync();
        interceptor.Fail = true;

        var (success, _) = await service.DeleteCategoryAsync(categoryId);

        Assert.False(success);
    }

    [Fact]
    public async Task DeleteAccountAndCategory_WhenAllowed_StillSucceed()
    {
        var (service, _, accountId, categoryId) = await SetupAsync();

        Assert.True((await service.DeleteAccountAsync(accountId)).Success);
        Assert.True((await service.DeleteCategoryAsync(categoryId)).Success);
    }
}
