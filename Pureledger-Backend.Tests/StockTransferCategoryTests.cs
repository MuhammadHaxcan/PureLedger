using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Services;
using Xunit;

namespace PureLedger.Backend.Tests;

public sealed class StockTransferCategoryTests
{
    [Fact]
    public async Task Categories_AreFilteredToEnabledAccountsInTheRequestedGroup()
    {
        await using var context = CreateContext();
        context.Projects.Add(new Project { ProjectId = 1, ProjectName = "Main" });
        context.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "General" });
        context.AccountGroups.AddRange(
            new AccountGroup { AccountGroupId = 1, AccountGroupName = "Account" },
            new AccountGroup { AccountGroupId = 2, AccountGroupName = "Product" });
        context.Categories.AddRange(
            new Category { CategoryId = 10, ProjectId = 1, AccountTypeId = 1, CategoryName = "Products" },
            new Category { CategoryId = 20, ProjectId = 1, AccountTypeId = 1, CategoryName = "Transit" },
            new Category { CategoryId = 30, ProjectId = 1, AccountTypeId = 1, CategoryName = "Disabled Products" },
            new Category { CategoryId = 40, ProjectId = 1, AccountTypeId = 1, CategoryName = "Empty" });
        context.Accounts.AddRange(
            new Account { AccountId = 100, CategoryId = 10, AccountGroupId = 2, AccountName = "Product A", IsEnabled = true },
            new Account { AccountId = 200, CategoryId = 20, AccountGroupId = 1, AccountName = "Stock In Transit", IsEnabled = true },
            new Account { AccountId = 300, CategoryId = 30, AccountGroupId = 2, AccountName = "Disabled Product", IsEnabled = false });
        await context.SaveChangesAsync();

        var service = new StockTransferService(context, NullLogger<StockTransferService>.Instance);

        var productNames = Names(await service.GetCategoriesAsync(1, 2, userId: 1, isAdmin: true));
        var normalNames = Names(await service.GetCategoriesAsync(1, 1, userId: 1, isAdmin: true));

        Assert.Equal(["Products"], productNames);
        Assert.Equal(["Transit"], normalNames);
    }

    private static string[] Names(object result)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result));
        return json.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("name").GetString()!)
            .ToArray();
    }

    private static LedgerDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase($"stock-transfer-categories-{Guid.NewGuid():N}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new LedgerDbContext(options);
    }
}
