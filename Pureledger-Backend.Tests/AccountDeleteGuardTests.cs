using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PureLedger.Controllers.Api;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories;
using PureLedger.Services;
using PureLedger.Services.Interface;
using Xunit;

namespace PureLedger.Backend.Tests;

public sealed class AccountDeleteGuardTests
{
    private static LedgerDbContext CreateContext(string name) => new(new DbContextOptionsBuilder<LedgerDbContext>()
        .UseInMemoryDatabase(name).Options);

    private static MetaDataApiController Controller(LedgerDbContext db)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new MetaDataService(new UnitOfWork(db), NullLogger<MetaDataService>.Instance, cache);
        return new MetaDataApiController(
            new MetaDataValidationService(new UnitOfWork(db), NullLogger<MetaDataValidationService>.Instance), service,
            new CurrentUserService(new HttpContextAccessor()),
            new CacheInvalidationService(cache, NullLogger<CacheInvalidationService>.Instance),
            NullLogger<MetaDataApiController>.Instance);
    }

    private static async Task<(int AccountId, int ApprovalId)> SeedAsync(LedgerDbContext db, string status)
    {
        var project = new Project { ProjectName = "Guard project" };
        db.Add(project);
        await db.SaveChangesAsync();
        var category = new Category { ProjectId = project.ProjectId, AccountTypeId = 1, CategoryName = "Cash" };
        db.Add(category);
        await db.SaveChangesAsync();
        var account = new Account { CategoryId = category.CategoryId, AccountGroupId = 1, AccountName = "Petty cash" };
        db.Add(account);
        await db.SaveChangesAsync();
        var approval = new ApprovalVoucher
        {
            Status = status, ProjectId = project.ProjectId, PostedByUserId = 1,
            Entries = { new ApprovalVoucherEntry { AccountId = account.AccountId, CategoryId = category.CategoryId, Debit = 10 } }
        };
        db.Add(approval);
        await db.SaveChangesAsync();
        return (account.AccountId, approval.ApprovalVoucherId);
    }

    [Fact]
    public async Task AccountUsedByPendingApproval_CannotBeDeleted_AndTheMessageNamesTheRequest()
    {
        await using var db = CreateContext("delete-guard-" + Guid.NewGuid());
        var (accountId, approvalId) = await SeedAsync(db, ApprovalStatuses.Pending);

        var check = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await Controller(db).CanDeleteAccount(accountId)).Value);
        var delete = Assert.IsType<BadRequestObjectResult>(await Controller(db).DeleteAccount(accountId));

        Assert.False(check.GetProperty("canDelete").GetBoolean());
        Assert.Contains($"#{approvalId}", check.GetProperty("message").GetString());
        Assert.Contains("pending approval", JsonSerializer.Serialize(delete.Value));
        Assert.NotNull(await db.Accounts.FindAsync(accountId));
    }

    [Fact]
    public async Task ServiceRefusesToDelete_EvenWhenCalledDirectly()
    {
        await using var db = CreateContext("delete-guard-" + Guid.NewGuid());
        var (accountId, _) = await SeedAsync(db, ApprovalStatuses.Pending);
        var service = new MetaDataService(new UnitOfWork(db), NullLogger<MetaDataService>.Instance,
            new MemoryCache(new MemoryCacheOptions()));

        var (success, _) = await service.DeleteAccountAsync(accountId);

        Assert.False(success);
        Assert.NotNull(await db.Accounts.FindAsync(accountId));
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    public async Task DecidedApprovals_NoLongerBlockDeletion(string status)
    {
        await using var db = CreateContext("delete-guard-" + Guid.NewGuid());
        var (accountId, _) = await SeedAsync(db, status);

        var check = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await Controller(db).CanDeleteAccount(accountId)).Value);
        Assert.True(check.GetProperty("canDelete").GetBoolean());
        Assert.IsType<OkObjectResult>(await Controller(db).DeleteAccount(accountId));
        Assert.Null(await db.Accounts.FindAsync(accountId));
    }
}
