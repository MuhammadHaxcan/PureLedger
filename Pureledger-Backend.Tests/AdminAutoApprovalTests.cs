using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PureLedger.Controllers.Api;
using PureLedger.Data;
using PureLedger.Exceptions;
using PureLedger.Models;
using PureLedger.Repositories;
using PureLedger.Services;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.Approval;
using PureLedger.ViewModels.Home;
using PureLedger.ViewModels.PaymentReceipt;
using Xunit;

namespace PureLedger.Backend.Tests;

public sealed class AdminAutoApprovalTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminSubmission_PostsCompleteVoucherAndAudit(bool paymentReceipt)
    {
        await using var data = await AutoApprovalTestData.CreateAsync();
        await CheckAdminPostingAsync(data, paymentReceipt);
    }

    internal static async Task CheckAdminPostingAsync(AutoApprovalTestData data, bool paymentReceipt)
    {
        var result = await data.SubmitAsync(paymentReceipt, data.AdminId);
        Assert.Equal("Approved", result.GetProperty("status").GetString());
        Assert.False(result.GetProperty("isPending").GetBoolean());
        Assert.True(result.GetProperty("autoApproved").GetBoolean());
        var approval = await data.Context.ApprovalVouchers.Include(x => x.Entries)
            .SingleAsync(x => x.ProjectId == data.ProjectId);
        Assert.Equal(ApprovalStatuses.Approved, approval.Status);
        Assert.Equal(data.AdminId, approval.PostedByUserId);
        Assert.Equal(data.AdminId, approval.DecisionByUserId);
        Assert.NotNull(approval.DecisionDate);
        Assert.Equal(2, approval.Entries.Count);
        var voucher = await data.Context.JournalVouchers.Include(x => x.JournalEntries)
            .ThenInclude(x => x.InstrumentDetail).SingleAsync(x => x.ProjectId == data.ProjectId);
        Assert.Equal(voucher.JournalVoucherId, approval.JournalVoucherId);
        Assert.True(voucher.IsCompleted);
        Assert.NotNull(voucher.RowVersion);
        Assert.All(voucher.JournalEntries, x => Assert.True(x.IsProcessed));
        Assert.False(await new ApprovalGuardService(data.Context).IsVoucherPendingAsync(voucher.JournalVoucherId));
        if (paymentReceipt)
        {
            Assert.Equal(data.EntryDate, voucher.EntryDate);
            Assert.Equal(data.EntryDate, voucher.CreditableDate);
            Assert.All(voucher.JournalEntries, x => Assert.NotNull(x.InstrumentDetail));
            Assert.Equal(VoucherType.PaymentReceipt,
                await data.Classifier.ResolvePrintableTypeAsync(voucher.JournalVoucherId, VoucherType.JV));
            using var report = JsonDocument.Parse(JsonSerializer.Serialize(await data.Payments.GetReportAsync(
                new PaymentReceiptReportRequest { ProjectId = data.ProjectId }, data.AdminId, true)));
            Assert.Equal(1, report.RootElement.GetProperty("totalVouchers").GetInt32());
            Assert.Null(result.GetProperty("pendingApprovalId").GetInt32OrNull());
        }
        else
        {
            Assert.True(result.GetProperty("shouldPrint").GetBoolean());
            Assert.Equal(voucher.EntryDate.AddDays(3), voucher.CreditableDate);
            Assert.All(voucher.JournalEntries, x => Assert.Null(x.InstrumentDetail));
        }
        var history = AutoApprovalTestData.Json(await data.Approvals.GetHistoryAsync(data.AdminId, true));
        Assert.Equal("Approved", history.GetProperty("items")[0].GetProperty("status").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryUser_WithStaleAdminRole_RemainsPending(bool paymentReceipt)
    {
        await using var data = await AutoApprovalTestData.CreateAsync();
        var result = await data.SubmitAsync(paymentReceipt, data.UserId, claimedAdmin: true);
        Assert.Equal("PendingApproval", result.GetProperty("status").GetString());
        Assert.True(result.GetProperty("isPending").GetBoolean());
        Assert.False(result.GetProperty("autoApproved").GetBoolean());
        Assert.False(result.GetProperty("shouldPrint").GetBoolean());
        Assert.Empty(await data.Context.JournalVouchers.Where(x => x.ProjectId == data.ProjectId).ToListAsync());
        var approval = await data.Context.ApprovalVouchers.SingleAsync(x => x.ProjectId == data.ProjectId);
        Assert.Equal(ApprovalStatuses.Pending, approval.Status);
        Assert.Null(approval.DecisionByUserId);
        Assert.Null(approval.JournalVoucherId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminRevision_AppliesRowsAndRejectsStaleVersion(bool paymentReceipt)
    {
        await using var data = await AutoApprovalTestData.CreateAsync();
        await CheckRevisionAsync(data, paymentReceipt);
    }

    internal static async Task CheckRevisionAsync(AutoApprovalTestData data, bool paymentReceipt)
    {
        await data.SubmitAsync(paymentReceipt, data.AdminId);
        var voucher = await data.Context.JournalVouchers.Include(x => x.JournalEntries)
            .SingleAsync(x => x.ProjectId == data.ProjectId);
        var oldVersion = voucher.RowVersion!.ToArray();
        var retained = voucher.JournalEntries.Single(x => x.Debit > 0).JournalEntryId;
        var omitted = voucher.JournalEntries.Single(x => x.Credit > 0).JournalEntryId;
        if (paymentReceipt)
        {
            var request = data.PaymentRequest();
            request.VoucherVersion = oldVersion;
            request.Entries[0].OriginalJournalEntryId = retained;
            request.Entries[0].Description = "Revised debit";
            await data.Payments.SubmitRevisionAsync(voucher.JournalVoucherId, request, data.AdminId, false);
            await Assert.ThrowsAsync<ApprovalConflictException>(() => data.Payments.SubmitRevisionAsync(
                voucher.JournalVoucherId, request, data.AdminId, false));
        }
        else
        {
            var model = data.ExpenseModel();
            model.VoucherVersion = oldVersion;
            model.Entries[0].EntryId = retained;
            model.Entries[0].Des = "Revised debit";
            var result = await data.Approvals.SubmitManualExpenseAsync(model, voucher.JournalVoucherId, data.AdminId);
            Assert.False(result!.IsPending);
            await Assert.ThrowsAsync<ApprovalConflictException>(() => data.Approvals.SubmitManualExpenseAsync(
                model, voucher.JournalVoucherId, data.AdminId));
        }
        data.Context.ChangeTracker.Clear();
        var revised = await data.Context.JournalVouchers.Include(x => x.JournalEntries)
            .ThenInclude(x => x.InstrumentDetail).SingleAsync(x => x.ProjectId == data.ProjectId);
        Assert.Equal(voucher.JournalVoucherId, revised.JournalVoucherId);
        Assert.True(revised.IsCompleted);
        Assert.NotEqual(oldVersion, revised.RowVersion);
        Assert.Contains(revised.JournalEntries, x => x.JournalEntryId == retained && x.Description == "Revised debit" && x.IsEdited);
        Assert.DoesNotContain(revised.JournalEntries, x => x.JournalEntryId == omitted);
        Assert.Equal(2, revised.JournalEntries.Count);
        var approvals = await data.Context.ApprovalVouchers.Where(x => x.ProjectId == data.ProjectId).ToListAsync();
        Assert.Equal(2, approvals.Count);
        Assert.All(approvals, x => Assert.Equal(ApprovalStatuses.Approved, x.Status));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminEditingOrdinaryUsersPendingDraft_DoesNotApprove(bool paymentReceipt)
    {
        await using var data = await AutoApprovalTestData.CreateAsync();
        await data.SubmitAsync(paymentReceipt, data.UserId);
        var approval = await data.Context.ApprovalVouchers.Include(x => x.Entries).SingleAsync();
        if (paymentReceipt)
        {
            var request = data.PaymentRequest();
            await data.Payments.EditPendingAsync(approval.ApprovalVoucherId, new PaymentReceiptApprovalEditRequest
            {
                ProjectId = request.ProjectId, EntryDate = request.EntryDate, Entries = request.Entries,
                SnapshotVersion = approval.SnapshotVersion!.Value
            }, data.AdminId, true);
        }
        else
        {
            var model = data.ExpenseModel();
            await data.Approvals.EditAsync(approval.ApprovalVoucherId,
                new ApprovalEditRequest { ProjectId = model.ProjectId, Entries = model.Entries }, data.AdminId, true);
        }
        Assert.Equal(ApprovalStatuses.Pending, approval.Status);
        Assert.Empty(await data.Context.JournalVouchers.ToListAsync());
        await data.Approvals.ApproveAsync(approval.ApprovalVoucherId, approval.SnapshotVersion, data.AdminId, true);
        Assert.Equal(ApprovalStatuses.Approved, approval.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidAndInactiveAdminSubmissions_ChangeNothing(bool paymentReceipt)
    {
        await using var data = await AutoApprovalTestData.CreateAsync();
        if (paymentReceipt)
        {
            var request = data.PaymentRequest();
            request.Entries[0].Debit = 101;
            await Assert.ThrowsAsync<InvalidOperationException>(() => data.Payments.SubmitAsync(request, data.AdminId, true));
        }
        else
        {
            var model = data.ExpenseModel();
            model.Entries[0].Debit = 101;
            await Assert.ThrowsAsync<InvalidOperationException>(() => data.Approvals.SubmitManualExpenseAsync(model, null, data.AdminId));
        }
        var admin = await data.Context.Users.SingleAsync(x => x.UserId == data.AdminId);
        admin.IsActive = false;
        await data.Context.SaveChangesAsync();
        if (paymentReceipt)
            await Assert.ThrowsAsync<NotAuthenticatedException>(() => data.Payments.SubmitAsync(data.PaymentRequest(), data.AdminId, true));
        else
            await Assert.ThrowsAsync<NotAuthenticatedException>(() => data.Approvals.SubmitManualExpenseAsync(data.ExpenseModel(), null, data.AdminId));
        Assert.Empty(await data.Context.ApprovalVouchers.ToListAsync());
        Assert.Empty(await data.Context.JournalVouchers.ToListAsync());
    }

    [Fact]
    public async Task ExpenseSubmission_WithoutProjectAccess_Returns403NotSessionExpiredStatus()
    {
        await using var data = await AutoApprovalTestData.CreateAsync();
        data.Context.UserProjects.RemoveRange(data.Context.UserProjects.Where(x => x.UserId == data.UserId));
        await data.Context.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new HomeApiController(null!, null!, new AutoApprovalTestData.CurrentUserStub(data.UserId, false), cache,
            NullLogger<HomeApiController>.Instance, null!, data.Approvals);

        var response = await controller.CreateOrUpdateEntry(data.ExpenseModel(), "submit");

        // 401 would make the client refresh its token and replay the POST as if the session had expired.
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(response).StatusCode);
        Assert.Empty(await data.Context.ApprovalVouchers.ToListAsync());
    }

    [Fact]
    public async Task LegacyExpenseVersion_IsAcceptedOnceThenRejectsTheOldEditor()
    {
        await using var data = await AutoApprovalTestData.CreateAsync();
        await data.SubmitAsync(false, data.AdminId);
        var voucher = await data.Context.JournalVouchers.SingleAsync();
        voucher.RowVersion = null;
        await data.Context.SaveChangesAsync();
        var model = data.ExpenseModel();
        await data.Approvals.SubmitManualExpenseAsync(model, voucher.JournalVoucherId, data.AdminId);
        await Assert.ThrowsAsync<ApprovalConflictException>(() => data.Approvals.SubmitManualExpenseAsync(model, voucher.JournalVoucherId, data.AdminId));
        Assert.Equal(2, await data.Context.ApprovalVouchers.CountAsync());
    }

    [Fact]
    public async Task AdminPaymentReceipt_DuplicateWarningsRemainAdvisoryAndAudited()
    {
        await using var data = await AutoApprovalTestData.CreateAsync();
        var request = data.PaymentRequest();
        foreach (var entry in request.Entries)
        {
            entry.InstrumentType = PaymentInstrumentType.Cheque;
            entry.ReferenceNumber = "  auto-123  ";
            entry.InstrumentDate = data.EntryDate;
        }
        await data.Payments.SubmitAsync(request, data.AdminId, true);
        var result = AutoApprovalTestData.Json(await data.Payments.SubmitAsync(request, data.AdminId, true));
        Assert.Equal("Approved", result.GetProperty("status").GetString());
        Assert.Equal(2, result.GetProperty("duplicateWarnings").GetArrayLength());
        var latest = await data.Context.ApprovalVouchers.OrderByDescending(x => x.ApprovalVoucherId).FirstAsync();
        Assert.True(latest.HadDuplicateWarningsAtSubmission);
        Assert.True(latest.HadDuplicateWarningsAtDecision);
        Assert.All(await data.Context.JournalEntryInstrumentDetails.ToListAsync(), x =>
        {
            Assert.Equal("auto-123", x.ReferenceNumber);
            Assert.Equal("AUTO-123", x.NormalizedReferenceNumber);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryUserRevision_RemainsPendingAndCanBeRejectedWithoutChangingRows(bool paymentReceipt)
    {
        await using var data = await AutoApprovalTestData.CreateAsync();
        await data.SubmitAsync(paymentReceipt, data.AdminId);
        var voucher = await data.Context.JournalVouchers.Include(x => x.JournalEntries).SingleAsync();
        var oldRows = voucher.JournalEntries.Select(x => x.JournalEntryId).Order().ToArray();
        if (paymentReceipt)
        {
            var request = data.PaymentRequest();
            request.VoucherVersion = voucher.RowVersion;
            await data.Payments.SubmitRevisionAsync(voucher.JournalVoucherId, request, data.UserId, false);
        }
        else
        {
            var model = data.ExpenseModel();
            model.VoucherVersion = voucher.RowVersion;
            var result = await data.Approvals.SubmitManualExpenseAsync(model, voucher.JournalVoucherId, data.UserId);
            Assert.True(result!.IsPending);
        }
        var pending = await data.Context.ApprovalVouchers.SingleAsync(x => x.Status == ApprovalStatuses.Pending);
        Assert.False(voucher.IsCompleted);
        Assert.Equal(oldRows, voucher.JournalEntries.Select(x => x.JournalEntryId).Order().ToArray());
        await data.Approvals.RejectAsync(pending.ApprovalVoucherId, "Retain original", pending.SnapshotVersion, data.AdminId, true);
        Assert.True(voucher.IsCompleted);
        Assert.Equal(oldRows, voucher.JournalEntries.Select(x => x.JournalEntryId).Order().ToArray());
    }
}

internal sealed class AutoApprovalTestData : IAsyncDisposable
{
    public LedgerDbContext Context { get; }
    public PaymentReceiptClassifier Classifier { get; }
    public PaymentReceiptService Payments { get; }
    public ApprovalService Approvals { get; }
    public int AdminId { get; private set; }
    public int UserId { get; private set; }
    public int ProjectId { get; private set; }
    public int CategoryId { get; private set; }
    public int CashId { get; private set; }
    public int BankId { get; private set; }
    public int ExpenseId { get; private set; }
    public DateOnly EntryDate => new(2026, 10, 1);
    private readonly bool _postgres;

    private AutoApprovalTestData(LedgerDbContext context, bool postgres, IPaymentReceiptApprovalCheckpoint? checkpoint)
    {
        Context = context;
        _postgres = postgres;
        Classifier = new PaymentReceiptClassifier(context);
        Payments = new PaymentReceiptService(context, Classifier, checkpoint);
        Approvals = new ApprovalService(context, new SharedProcessingService(new UnitOfWork(context),
            NullLogger<SharedProcessingService>.Instance, new ApprovalGuardService(context), Classifier), Payments);
    }

    public static async Task<AutoApprovalTestData> CreateAsync(bool postgres = false,
        IPaymentReceiptApprovalCheckpoint? checkpoint = null, IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<LedgerDbContext>();
        if (postgres) builder.UseNpgsql(Environment.GetEnvironmentVariable("PURELEDGER_TEST_POSTGRES")!);
        else builder.UseInMemoryDatabase($"auto-approval-{Guid.NewGuid():N}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        if (interceptor != null) builder.AddInterceptors(interceptor);
        var data = new AutoApprovalTestData(new LedgerDbContext(builder.Options), postgres, checkpoint);
        await data.SeedAsync();
        return data;
    }

    private async Task SeedAsync()
    {
        foreach (var (id, name) in new[] { (1, "Account"), (3, "Expense") })
            if (!await Context.AccountGroups.AnyAsync(x => x.AccountGroupId == id))
                Context.AccountGroups.Add(new AccountGroup { AccountGroupId = id, AccountGroupName = name });
        if (!await Context.AccountTypes.AnyAsync(x => x.AccountTypeId == 1))
            Context.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "Assets" });
        var suffix = Guid.NewGuid().ToString("N");
        var project = new Project { ProjectName = $"Auto approval {suffix}" };
        var admin = new User { Username = $"admin-{suffix}", FullName = "Test Admin", PasswordHash = "test", IsAdmin = true };
        var user = new User { Username = $"user-{suffix}", FullName = "Test User", PasswordHash = "test" };
        Context.AddRange(project, admin, user);
        await Context.SaveChangesAsync();
        ProjectId = project.ProjectId; AdminId = admin.UserId; UserId = user.UserId;
        var category = new Category { ProjectId = ProjectId, AccountTypeId = 1, CategoryName = "Cash and Expenses" };
        Context.Categories.Add(category);
        await Context.SaveChangesAsync();
        CategoryId = category.CategoryId;
        var cash = new Account { CategoryId = CategoryId, AccountGroupId = 1, AccountName = "Cash", IsEnabled = true };
        var bank = new Account { CategoryId = CategoryId, AccountGroupId = 1, AccountName = "Bank", IsEnabled = true };
        var expense = new Account { CategoryId = CategoryId, AccountGroupId = 3, AccountName = "Office Expense", IsEnabled = true };
        Context.AddRange(cash, bank, expense);
        Context.UserProjects.Add(new UserProject { UserId = UserId, ProjectId = ProjectId });
        await Context.SaveChangesAsync();
        CashId = cash.AccountId; BankId = bank.AccountId; ExpenseId = expense.AccountId;
    }

    public JournalEntryViewModel ExpenseModel() => new()
    {
        ProjectId = ProjectId, CreditDays = 3,
        Entries = new()
        {
            new() { AccountId = ExpenseId, CategoryId = CategoryId, Des = "Expense", Debit = 100 },
            new() { AccountId = CashId, CategoryId = CategoryId, Des = "Cash", Credit = 100 }
        }
    };

    public PaymentReceiptSubmissionRequest PaymentRequest() => new()
    {
        ProjectId = ProjectId, EntryDate = EntryDate,
        Entries = new()
        {
            new() { AccountId = CashId, CategoryId = CategoryId, Description = "Cash", InstrumentType = PaymentInstrumentType.Cash, Debit = 100 },
            new() { AccountId = BankId, CategoryId = CategoryId, Description = "Bank", InstrumentType = PaymentInstrumentType.Cash, Credit = 100 }
        }
    };

    public async Task<JsonElement> SubmitAsync(bool paymentReceipt, int userId, bool claimedAdmin = false)
    {
        var current = new CurrentUserStub(userId, claimedAdmin);
        IActionResult response;
        if (paymentReceipt)
            response = await new PaymentReceiptApiController(Payments, current).Submit(PaymentRequest());
        else
        {
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var controller = new HomeApiController(null!, null!, current, cache,
                NullLogger<HomeApiController>.Instance, null!, Approvals);
            response = await controller.CreateOrUpdateEntry(ExpenseModel(), "submit_print");
        }
        return Json(Assert.IsType<OkObjectResult>(response).Value!);
    }

    public static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value,
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    public async ValueTask DisposeAsync()
    {
        if (_postgres)
        {
            Context.ChangeTracker.Clear();
            await Context.ApprovalVouchers.Where(x => x.ProjectId == ProjectId).ExecuteDeleteAsync();
            await Context.JournalVoucherReferences.Where(x => x.MainVoucher.ProjectId == ProjectId).ExecuteDeleteAsync();
            await Context.DeliveryChallanEntries.Where(x => x.JournalEntry.JournalVoucher.ProjectId == ProjectId).ExecuteDeleteAsync();
            await Context.DeliveryChallanVouchers.Where(x => x.JournalVoucher.ProjectId == ProjectId).ExecuteDeleteAsync();
            await Context.JournalVouchers.Where(x => x.ProjectId == ProjectId).ExecuteDeleteAsync();
            await Context.Accounts.Where(x => x.Category!.ProjectId == ProjectId).ExecuteDeleteAsync();
            await Context.Categories.Where(x => x.ProjectId == ProjectId).ExecuteDeleteAsync();
            await Context.UserProjects.Where(x => x.ProjectId == ProjectId).ExecuteDeleteAsync();
            await Context.Projects.Where(x => x.ProjectId == ProjectId).ExecuteDeleteAsync();
            await Context.Users.Where(x => x.UserId == AdminId || x.UserId == UserId).ExecuteDeleteAsync();
        }
        await Context.DisposeAsync();
    }

    internal sealed record CurrentUserStub(int UserId, bool Admin) : ICurrentUserService
    {
        public int? GetCurrentUserId() => UserId;
        public bool IsCurrentUserAdmin() => Admin;
        public string? GetCurrentUsername() => "test";
    }
}

internal static class JsonTestExtensions
{
    public static int? GetInt32OrNull(this JsonElement element) => element.ValueKind == JsonValueKind.Null ? null : element.GetInt32();
}
