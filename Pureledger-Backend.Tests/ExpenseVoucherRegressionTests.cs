using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories;
using PureLedger.Services;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.Home;
using Xunit;

namespace PureLedger.Backend.Tests;

public sealed class ExpenseVoucherRegressionTests
{
    [Fact]
    public async Task ApprovedExpenseRevision_CannotRemoveItsFinalExpenseAccount()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        var voucher = await AddVoucherAsync(context, VoucherKind.Expense);
        var originalEntryIds = voucher.JournalEntries.Select(x => x.JournalEntryId).Order().ToArray();
        var model = BalancedModel(accountIds: [1, 2]);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateApprovalService(context).SubmitManualExpenseAsync(
                model, voucher.JournalVoucherId, userId: 1));

        Assert.Contains("retain at least one Expense account", error.Message);
        Assert.True((await context.JournalVouchers.FindAsync(voucher.JournalVoucherId))!.IsCompleted);
        Assert.Equal(originalEntryIds, await context.JournalEntries
            .Where(x => x.JournalVoucherId == voucher.JournalVoucherId)
            .OrderBy(x => x.JournalEntryId)
            .Select(x => x.JournalEntryId)
            .ToArrayAsync());
        Assert.Empty(context.ApprovalVouchers);
    }

    [Fact]
    public async Task OrdinaryVoucherWithoutExpense_StillFallsThroughToOrdinaryFlow()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        var voucher = await AddVoucherAsync(context, VoucherKind.Ordinary);

        var result = await CreateApprovalService(context).SubmitManualExpenseAsync(
            BalancedModel(accountIds: [1, 2]), voucher.JournalVoucherId, userId: 1);

        Assert.Null(result);
        Assert.Empty(context.ApprovalVouchers);
        Assert.True(voucher.IsCompleted);
    }

    [Fact]
    public async Task FullVoucherEditor_AllowsAndClassifiesExpenseVoucher()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        var voucher = await AddVoucherAsync(context, VoucherKind.Expense);
        var service = CreateJournalService(context);

        var payload = await service.GetVoucherForEditAsync(voucher.JournalVoucherId, userId: 1);
        var json = JsonSerializer.Serialize(payload);

        Assert.Contains("\"isManualExpenseVoucher\":true", json);
        Assert.Contains("\"editingVoucherId\":" + voucher.JournalVoucherId, json);
    }

    [Fact]
    public async Task OrdinaryVoucher_RetainsIndividualEntryEditing()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        var voucher = await AddVoucherAsync(context, VoucherKind.Ordinary);

        var model = await CreateJournalService(context)
            .GetEntryForEditAsync(voucher.JournalEntries.First().JournalEntryId);

        Assert.NotNull(model);
    }

    [Theory]
    [InlineData(VoucherKind.Expense, "complete voucher")]
    [InlineData(VoucherKind.StockTransfer, "Stock Transfers")]
    [InlineData(VoucherKind.PaymentReceipt, "Payment & Receipt")]
    public async Task ProtectedAggregates_RejectIndividualEntryLoad(
        VoucherKind kind, string expectedMessage)
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        var voucher = await AddVoucherAsync(context, kind);

        var error = await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            CreateJournalService(context)
                .GetEntryForEditAsync(voucher.JournalEntries.First().JournalEntryId));

        Assert.Contains(expectedMessage, error.Message);
    }

    [Theory]
    [InlineData(VoucherKind.Expense, "Pending Approvals")]
    [InlineData(VoucherKind.StockTransfer, "Stock Transfers")]
    [InlineData(VoucherKind.PaymentReceipt, "Error deleting entry")]
    public async Task ExpenseAndStockTransfer_RejectIndividualDeleteWithoutChangingRows(
        VoucherKind kind, string expectedMessage)
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        var voucher = await AddVoucherAsync(context, kind);
        var entryId = voucher.JournalEntries.First().JournalEntryId;
        var before = await context.JournalEntries.CountAsync(x => x.JournalVoucherId == voucher.JournalVoucherId);

        var result = await CreateJournalService(context).DeleteEntryAsync(entryId);

        Assert.False(result.Success);
        Assert.Contains(expectedMessage, result.ErrorMessage);
        Assert.Equal(before, await context.JournalEntries.CountAsync(x => x.JournalVoucherId == voucher.JournalVoucherId));
    }

    [Theory]
    [InlineData(VoucherKind.Expense, "Pending Approvals")]
    [InlineData(VoucherKind.StockTransfer, "Stock Transfers")]
    [InlineData(VoucherKind.PaymentReceipt, "Error updating entry")]
    public async Task ProtectedAggregates_RejectIndividualUpdateWithoutChangingRows(
        VoucherKind kind, string expectedMessage)
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        var voucher = await AddVoucherAsync(context, kind);
        var entry = voucher.JournalEntries.First();
        var originalDescription = entry.Description;

        var result = await CreateJournalService(context).UpdateEntryAsync(
            UpdateModel(entry, "Attempted individual change"));

        Assert.False(result.Success);
        Assert.Contains(expectedMessage, result.ErrorMessage);
        context.ChangeTracker.Clear();
        Assert.Equal(originalDescription,
            (await context.JournalEntries.FindAsync(entry.JournalEntryId))!.Description);
    }

    [Fact]
    public async Task OrdinaryVoucher_RetainsExistingIndividualDeleteBehavior()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        var voucher = await AddVoucherAsync(context, VoucherKind.Ordinary);
        var entryId = voucher.JournalEntries.First().JournalEntryId;

        var result = await CreateJournalService(context).DeleteEntryAsync(entryId);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Single(await context.JournalEntries
            .Where(x => x.JournalVoucherId == voucher.JournalVoucherId).ToListAsync());
    }

    [Fact]
    public async Task OrdinaryVoucher_RetainsExistingIndividualUpdateBehavior()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        var voucher = await AddVoucherAsync(context, VoucherKind.Ordinary);
        var entry = voucher.JournalEntries.First();

        var result = await CreateJournalService(context).UpdateEntryAsync(
            UpdateModel(entry, "Ordinary row updated"));

        Assert.True(result.Success, result.ErrorMessage);
        context.ChangeTracker.Clear();
        Assert.Equal("Ordinary row updated",
            (await context.JournalEntries.FindAsync(entry.JournalEntryId))!.Description);
    }

    [Fact]
    public async Task PendingExpenseRevision_BlocksEditorAndIndividualEntryLoad()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        var voucher = await AddVoucherAsync(context, VoucherKind.Expense);
        context.ApprovalVouchers.Add(new ApprovalVoucher
        {
            VoucherType = ApprovalVoucherType.ManualExpense,
            Status = ApprovalStatuses.Pending,
            RequestKind = ApprovalRequestKinds.Revision,
            JournalVoucherId = voucher.JournalVoucherId,
            ProjectId = voucher.ProjectId,
            SubmittedDate = new DateOnly(2026, 7, 31),
            PostedByUserId = 1
        });
        await context.SaveChangesAsync();
        var service = CreateJournalService(context);

        await Assert.ThrowsAsync<ApprovalConflictException>(() =>
            service.GetVoucherForEditAsync(voucher.JournalVoucherId, userId: 1));
        await Assert.ThrowsAsync<ApprovalConflictException>(() =>
            service.GetEntryForEditAsync(voucher.JournalEntries.First().JournalEntryId));
    }

    [Fact]
    public async Task DeliveryChallanSelection_KeepsOrdinaryProductVoucherAndExcludesPaymentReceipt()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        var ordinaryProduct = await AddVoucherAsync(context, VoucherKind.Product);
        var paymentReceipt = await AddVoucherAsync(context, VoucherKind.PaymentReceipt);

        var selected = await new JournalVoucherRepository(context).GetFilteredVouchersAsync(
            sinceDate: null, toDate: null, projectId: 1, includeEntries: true);

        Assert.Contains(selected, x => x.JournalVoucherId == ordinaryProduct.JournalVoucherId);
        Assert.DoesNotContain(selected, x => x.JournalVoucherId == paymentReceipt.JournalVoucherId);
    }

    private static LedgerDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase($"expense-regression-{Guid.NewGuid():N}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new LedgerDbContext(options);
    }

    private static ApprovalService CreateApprovalService(LedgerDbContext context)
    {
        var classifier = new PaymentReceiptClassifier(context);
        return new ApprovalService(
            context,
            new ProcessingStub(),
            new PaymentReceiptService(context, classifier));
    }

    private static JournalEntryService CreateJournalService(LedgerDbContext context)
    {
        var unitOfWork = new UnitOfWork(context);
        return new JournalEntryService(
            unitOfWork,
            NullLogger<JournalEntryService>.Instance,
            new ProcessingStub(),
            new ApprovalGuardService(context),
            new PaymentReceiptClassifier(context));
    }

    private static JournalEntryViewModel BalancedModel(int[] accountIds) => new()
    {
        ProjectId = 1,
        EntryDate = new DateOnly(2026, 7, 31),
        DueDate = new DateOnly(2026, 8, 1),
        Entries =
        [
            new JournalEntryRowModel { CategoryId = 1, AccountId = accountIds[0], Debit = 100, Credit = 0 },
            new JournalEntryRowModel { CategoryId = 1, AccountId = accountIds[1], Debit = 0, Credit = 100 }
        ]
    };

    private static JournalEntryUpdateViewModel UpdateModel(
        JournalEntry entry, string description) => new()
    {
        EntryId = entry.JournalEntryId,
        ProjectId = 1,
        CategoryId = entry.CategoryId,
        AccountId = entry.AccountId,
        AccountGroupId = entry.Account?.AccountGroupId ?? 0,
        Description = description,
        Quantity = entry.Quantity,
        Rate = entry.Rate,
        Discount = entry.Discount,
        DiscountPercent = entry.DiscountPercent ?? 0,
        Debit = entry.Debit,
        Credit = entry.Credit
    };

    private static async Task SeedMastersAsync(LedgerDbContext context)
    {
        context.AccountGroups.AddRange(
            new AccountGroup { AccountGroupId = 1, AccountGroupName = "Account" },
            new AccountGroup { AccountGroupId = 2, AccountGroupName = "Product" },
            new AccountGroup { AccountGroupId = 3, AccountGroupName = "Expense" });
        context.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "General" });
        context.Projects.Add(new Project { ProjectId = 1, ProjectName = "Regression Project" });
        context.Categories.Add(new Category
        {
            CategoryId = 1,
            ProjectId = 1,
            AccountTypeId = 1,
            CategoryName = "Regression"
        });
        context.Accounts.AddRange(
            new Account { AccountId = 1, CategoryId = 1, AccountGroupId = 1, AccountName = "Cash", IsEnabled = true },
            new Account { AccountId = 2, CategoryId = 1, AccountGroupId = 1, AccountName = "Bank", IsEnabled = true },
            new Account { AccountId = 3, CategoryId = 1, AccountGroupId = 3, AccountName = "Office Expense", IsEnabled = true },
            new Account { AccountId = 4, CategoryId = 1, AccountGroupId = 2, AccountName = "Stock Item", IsEnabled = true });
        context.Users.Add(new User
        {
            UserId = 1,
            Username = "audit-admin",
            PasswordHash = "test",
            FullName = "Audit Admin",
            IsAdmin = true
        });
        await context.SaveChangesAsync();
    }

    private static async Task<JournalVoucher> AddVoucherAsync(
        LedgerDbContext context, VoucherKind kind)
    {
        var date = new DateOnly(2026, 7, 31);
        var firstAccount = kind switch
        {
            VoucherKind.Expense => 3,
            VoucherKind.Product => 4,
            _ => 1
        };
        var voucher = new JournalVoucher
        {
            ProjectId = 1,
            EntryDate = date,
            CreditableDate = date.AddDays(1),
            IsCompleted = kind != VoucherKind.Product,
            IsStockTransfer = kind == VoucherKind.StockTransfer,
            RowVersion = Guid.NewGuid().ToByteArray(),
            JournalEntries =
            [
                NewEntry(firstAccount, debit: 100, credit: 0, kind == VoucherKind.Product ? 1 : 0),
                NewEntry(2, debit: 0, credit: 100, quantity: 0)
            ]
        };
        context.JournalVouchers.Add(voucher);
        await context.SaveChangesAsync();

        if (kind == VoucherKind.PaymentReceipt)
        {
            var ordered = voucher.JournalEntries.OrderBy(x => x.JournalEntryId).ToList();
            context.JournalEntryInstrumentDetails.AddRange(
                new JournalEntryInstrumentDetail
                {
                    JournalEntryId = ordered[0].JournalEntryId,
                    InstrumentType = PaymentInstrumentType.Cash
                },
                new JournalEntryInstrumentDetail
                {
                    JournalEntryId = ordered[1].JournalEntryId,
                    InstrumentType = PaymentInstrumentType.Cash
                });
            await context.SaveChangesAsync();
        }

        return await context.JournalVouchers
            .Include(x => x.JournalEntries).ThenInclude(x => x.Account)
            .Include(x => x.JournalEntries).ThenInclude(x => x.InstrumentDetail)
            .SingleAsync(x => x.JournalVoucherId == voucher.JournalVoucherId);
    }

    private static JournalEntry NewEntry(int accountId, decimal debit, decimal credit, int quantity) => new()
    {
        AccountId = accountId,
        CategoryId = 1,
        Description = "Regression row",
        Quantity = quantity,
        Rate = quantity > 0 ? 100 : 0,
        Debit = debit,
        Credit = credit,
        Rbp = "N",
        IsProcessed = quantity == 0
    };

    public enum VoucherKind
    {
        Ordinary,
        Product,
        Expense,
        StockTransfer,
        PaymentReceipt
    }

    private sealed class ProcessingStub : ISharedProcessingService
    {
        public Task<bool> DetermineEntryProcessedStatusAsync(JournalEntry entry) => Task.FromResult(entry.IsProcessed);
        public Task<bool> RecalculateVoucherCompletionAsync(int journalVoucherId) => Task.FromResult(true);
        public Task CompleteReferenceVouchersAsync(int journalVoucherId) => Task.CompletedTask;
        public Task ReopenReferenceVouchersAsync(int journalVoucherId) => Task.CompletedTask;
        public Task<JournalVoucherReference> CreateVoucherReferenceAsync(int mainVoucherId, int referenceVoucherId, string referenceType) =>
            Task.FromResult(new JournalVoucherReference
            {
                MainVoucherId = mainVoucherId,
                ReferencedVoucherId = referenceVoucherId,
                ReferenceType = referenceType
            });
        public Task<bool> DeleteReferenceVoucherIfEmptyAsync(int journalVoucherId) => Task.FromResult(false);
        public Task DeleteLinkedDCEntryAsync(int journalEntryId) => Task.CompletedTask;
        public Task HandleAllEntriesDeletedAsync(int journalVoucherId) => Task.CompletedTask;
    }
}
