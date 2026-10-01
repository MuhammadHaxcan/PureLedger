using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories;
using PureLedger.Services;
using Xunit;

namespace PureLedger.Backend.Tests;

public sealed class ProductStockBreakdownTests
{
    [Fact]
    public async Task Breakdown_GroupsAvailableQuantityAndKeepsDataIssuesAuditable()
    {
        await using var context = CreateContext();
        SeedProduct(context);
        context.Locations.AddRange(
            new Location { LocationId = 1, LocationName = "Warehouse X" },
            new Location { LocationId = 2, LocationName = "Warehouse Y" });

        AddMovement(context, 1, new DateOnly(2026, 1, 1), quantity: 30, debit: 300, locations: [1]);
        AddMovement(context, 2, new DateOnly(2026, 1, 2), quantity: 20, debit: 400, locations: [2]);
        AddMovement(context, 3, new DateOnly(2026, 1, 3), quantity: 5, credit: 50, locations: [1]);
        AddMovement(context, 4, new DateOnly(2026, 1, 4), quantity: 7, debit: 70, locations: []);
        AddMovement(context, 5, new DateOnly(2026, 1, 5), quantity: 4, debit: 40, locations: [1, 2]);
        AddMovement(context, 7, new DateOnly(2026, 1, 6), quantity: 0, debit: 40, locations: []);
        AddMovement(context, 6, new DateOnly(2026, 2, 1), quantity: 100, debit: 1000, locations: [2]);
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetProductStockBreakdownAsync(
            projectId: 1, productId: 100, endDate: new DateOnly(2026, 1, 31));

        Assert.Equal(56, result.TotalQuantity);
        Assert.Equal(7, result.UnassignedQuantity);
        Assert.Equal(4, result.AmbiguousQuantity);
        Assert.Equal(2, result.AssignmentIssueCount);
        Assert.Equal(0, result.ReconciliationDifference);
        Assert.Collection(result.Locations,
            location =>
            {
                Assert.Equal("Warehouse X", location.LocationName);
                Assert.Equal(25, location.Quantity);
            },
            location =>
            {
                Assert.Equal("Warehouse Y", location.LocationName);
                Assert.Equal(20, location.Quantity);
            });
    }

    [Fact]
    public async Task Breakdown_RejectsAProductOutsideTheRequestedProject()
    {
        await using var context = CreateContext();
        SeedProduct(context);
        context.Projects.Add(new Project { ProjectId = 2, ProjectName = "Other" });
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            CreateService(context).GetProductStockBreakdownAsync(2, 100, null));
    }

    private static ReportingService CreateService(LedgerDbContext context) =>
        new(
            new UnitOfWork(context),
            new ApprovalGuardService(context),
            new PaymentReceiptClassifier(context),
            context);

    private static void SeedProduct(LedgerDbContext context)
    {
        context.Projects.Add(new Project { ProjectId = 1, ProjectName = "Main" });
        context.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "General" });
        context.AccountGroups.Add(new AccountGroup { AccountGroupId = 2, AccountGroupName = "Product" });
        context.Categories.Add(new Category
        {
            CategoryId = 10,
            ProjectId = 1,
            AccountTypeId = 1,
            CategoryName = "Products"
        });
        context.Accounts.Add(new Account
        {
            AccountId = 100,
            CategoryId = 10,
            AccountGroupId = 2,
            AccountName = "Product A",
            IsEnabled = true
        });
    }

    private static void AddMovement(
        LedgerDbContext context,
        int id,
        DateOnly date,
        int quantity,
        decimal debit = 0,
        decimal credit = 0,
        int[]? locations = null)
    {
        var voucherId = 1000 + id;
        var entryId = 2000 + id;
        var challanId = 3000 + id;
        context.JournalVouchers.Add(new JournalVoucher
        {
            JournalVoucherId = voucherId,
            ProjectId = 1,
            EntryDate = date,
            CreditableDate = date.AddDays(1),
            IsCompleted = true,
            IsBaseVoucher = false
        });
        context.JournalEntries.Add(new JournalEntry
        {
            JournalEntryId = entryId,
            JournalVoucherId = voucherId,
            AccountId = 100,
            CategoryId = 10,
            IsProcessed = true,
            Quantity = quantity,
            Debit = debit,
            Credit = credit
        });
        context.DeliveryChallanVouchers.Add(new DeliveryChallanVoucher
        {
            DeliveryChallanVoucherId = challanId,
            JournalVoucherId = voucherId,
            DCDate = date
        });
        foreach (var locationId in locations ?? [])
        {
            context.DeliveryChallanEntries.Add(new DeliveryChallanEntry
            {
                DeliveryChallanEntryId = challanId * 10 + locationId,
                DeliveryChallanVoucherId = challanId,
                JournalEntryId = entryId,
                LocationId = locationId,
                EntryDate = date
            });
        }
    }

    private static LedgerDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase($"product-stock-breakdown-{Guid.NewGuid():N}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new LedgerDbContext(options);
    }
}
