using System.Data;
using PureLedger.Helpers;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.Approval;
using PureLedger.ViewModels.Home;

namespace PureLedger.Services;

public sealed class ApprovalService : IApprovalService
{
    private const int AccountGroup = 1;
    private const int ProductGroup = 2;
    private const int ExpenseGroup = 3;
    private static readonly string[] EditableChildTypes = { "ForCharges", "Cartage" };
    private readonly LedgerDbContext _context;
    private readonly ISharedProcessingService _processing;
    private readonly IPaymentReceiptService _paymentReceipts;

    public ApprovalService(LedgerDbContext context, ISharedProcessingService processing, IPaymentReceiptService paymentReceipts)
        => (_context, _processing, _paymentReceipts) = (context, processing, paymentReceipts);

    public async Task<bool> ContainsExpenseAsync(IEnumerable<JournalEntryRowModel> entries)
    {
        var ids = entries.Where(x => x.AccountId.HasValue).Select(x => x.AccountId!.Value).Distinct().ToList();
        return ids.Count > 0 && await _context.Accounts.AsNoTracking()
            .AnyAsync(x => ids.Contains(x.AccountId) && x.AccountGroupId == ExpenseGroup);
    }

    public async Task<ApprovalSubmissionResult?> SubmitManualExpenseAsync(
        JournalEntryViewModel model, int? editingVoucherId, int userId)
    {
        var containsExpense = await ContainsExpenseAsync(model.Entries);
        if (!containsExpense)
        {
            if (editingVoucherId.HasValue &&
                await IsManualExpenseVoucherAsync(editingVoucherId.Value))
            {
                throw new InvalidOperationException(
                    "An Expense voucher must retain at least one Expense account. " +
                    "Restore an Expense line, or cancel this edit and delete the voucher through its authorized workflow.");
            }

            // Preserve the ordinary-JV dispatch contract for new vouchers and for
            // existing vouchers that were never manual Expense vouchers.
            return null;
        }
        if (model.IsBaseVoucher || model.SaveAsRecipe)
            throw new InvalidOperationException("Expense vouchers cannot be base vouchers or recipes.");

        if (editingVoucherId.HasValue)
        {
            var origin = await _context.JournalVouchers.AsNoTracking()
                .Where(x => x.JournalVoucherId == editingVoucherId.Value)
                .Select(x => new
                {
                    x.IsStockTransfer,
                    x.IsBaseVoucher,
                    HasIncomingReference = x.IncomingReferences.Any(),
                    HasInstrumentDetails = x.JournalEntries.Any(e => e.InstrumentDetail != null)
                }).SingleOrDefaultAsync();
            if (origin == null) throw new InvalidOperationException("Voucher was not found.");
            if (origin.IsBaseVoucher)
                throw new InvalidOperationException("Expense accounts cannot be introduced into a base voucher.");
            if (origin.HasInstrumentDetails)
                throw new InvalidOperationException(
                    "Payment & Receipt vouchers must be revised through their dedicated approval flow.");
            if (origin.IsStockTransfer || origin.HasIncomingReference)
                return null;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var isAdmin = await ApprovalPostingUser.IsAdminAsync(_context, userId);
            await EnsureProjectAccessAsync(model.ProjectId, userId, isAdmin);
            var hidden = await HiddenCategoriesAsync(userId, isAdmin);
            if (model.Entries.Any(x => x.CategoryId.HasValue && hidden.Contains(x.CategoryId.Value)))
                throw new UnauthorizedAccessException("The voucher contains a category you cannot access.");
            JournalVoucher? voucher = null;
            if (editingVoucherId.HasValue)
            {
                voucher = await LockVoucherAsync(editingVoucherId.Value);
                // Legacy vouchers may have a null token. The first mutation rotates it,
                // so a second editor holding that null version is then rejected.
                if (!VersionsMatch(model.VoucherVersion, voucher.RowVersion))
                    throw new ApprovalConflictException("The voucher changed after it was loaded. Reload and try again.");
                // The requested project was authorized above; the stored voucher must live in that same project.
                if (voucher.ProjectId != model.ProjectId)
                    throw new InvalidOperationException("An Expense revision cannot change the voucher's project.");
                if (voucher.JournalEntries.Any(x => hidden.Contains(x.CategoryId)))
                    throw new UnauthorizedAccessException("The voucher contains a category you cannot access.");
                await EnsureManualRevisionEligibleAsync(voucher);
                if (await _context.ApprovalVouchers.AnyAsync(x =>
                        x.JournalVoucherId == voucher.JournalVoucherId && x.Status == ApprovalStatuses.Pending))
                    throw new ApprovalConflictException("This voucher already has a pending approval request.");
            }

            await ValidateModelAsync(model, requireExpense: true);
            ValidateOriginalEntryMappings(voucher, model.Entries);
            var approval = new ApprovalVoucher
            {
                Status = ApprovalStatuses.Pending,
                RequestKind = voucher == null ? ApprovalRequestKinds.New : ApprovalRequestKinds.Revision,
                JournalVoucherId = voucher?.JournalVoucherId,
                ProjectId = model.ProjectId,
                BookerId = model.BookerId,
                CreditDays = model.CreditDays,
                SubmittedDate = BusinessClock.Today,
                PostedByUserId = userId
            };
            // Admin requests are approved in this same transaction, so only requests that
            // actually wait for a decision reserve a JV number.
            if (voucher == null && !isAdmin)
                approval.ReservedJournalVoucherId = await JournalVoucherNumbers.ReserveAsync(_context);
            _context.ApprovalVouchers.Add(approval);
            await _context.SaveChangesAsync();
            ReplaceSnapshot(approval, model.Entries);

            if (voucher != null)
            {
                voucher.IsCompleted = false;
                voucher.RowVersion = RandomNumberGenerator.GetBytes(16);
                var children = await GetEditableChildrenAsync(voucher.JournalVoucherId);
                foreach (var child in children) child.IsCompleted = false;
            }

            await _context.SaveChangesAsync();
            if (isAdmin)
                voucher = await ApproveExpenseCoreAsync(approval, userId, isAdmin);
            await transaction.CommitAsync();
            return new ApprovalSubmissionResult
            {
                IsPending = !isAdmin,
                AutoApproved = isAdmin,
                ApprovalVoucherId = approval.ApprovalVoucherId,
                JournalVoucherId = approval.JournalVoucherId,
                ReservedJournalVoucherId = approval.ReservedJournalVoucherId,
                ProjectId = approval.ProjectId
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private Task<bool> IsManualExpenseVoucherAsync(int journalVoucherId) =>
        _context.JournalVouchers.AsNoTracking().AnyAsync(voucher =>
            voucher.JournalVoucherId == journalVoucherId &&
            !voucher.IsStockTransfer &&
            !voucher.IsBaseVoucher &&
            !voucher.IncomingReferences.Any() &&
            !voucher.JournalEntries.Any(entry => entry.InstrumentDetail != null) &&
            voucher.JournalEntries.Any(entry => entry.Account!.AccountGroupId == ExpenseGroup));

    // Explains a JV number that was reserved at submission but has no voucher row (yet, or ever).
    public async Task<string?> DescribeReservedVoucherNumberAsync(int journalVoucherId)
    {
        var status = await _context.ApprovalVouchers.AsNoTracking()
            .Where(x => x.ReservedJournalVoucherId == journalVoucherId && x.JournalVoucherId == null)
            .Select(x => x.Status)
            .SingleOrDefaultAsync();
        return status switch
        {
            ApprovalStatuses.Pending => $"JV-{journalVoucherId} is pending approval. It becomes available once it is approved.",
            ApprovalStatuses.Rejected => $"JV-{journalVoucherId} was rejected and was never posted.",
            _ => null
        };
    }

    public async Task<object> GetPendingAsync(int userId, bool isAdmin)
    {
        var projectIds = await AccessibleProjectsAsync(userId, isAdmin);
        var hidden = await HiddenCategoriesAsync(userId, isAdmin);
        var rows = await _context.ApprovalVouchers.AsNoTracking()
            .Where(x => x.Status == ApprovalStatuses.Pending && projectIds.Contains(x.ProjectId) &&
                        !x.Entries.Any(e => hidden.Contains(e.CategoryId)))
            .OrderBy(x => x.SubmittedDate).ThenBy(x => x.ApprovalVoucherId)
            .Select(x => new
            {
                approvalVoucherId = x.ApprovalVoucherId,
                voucherType = x.VoucherType == ApprovalVoucherType.PaymentReceipt ? "PaymentReceipt" : "ManualExpense",
                journalVoucherId = x.JournalVoucherId,
                reservedJournalVoucherId = x.ReservedJournalVoucherId,
                displayVoucherId = x.JournalVoucherId ?? x.ReservedJournalVoucherId,
                requestKind = x.RequestKind,
                projectId = x.ProjectId,
                projectName = _context.Projects.Where(p => p.ProjectId == x.ProjectId).Select(p => p.ProjectName).FirstOrDefault(),
                submittedDate = x.SubmittedDate,
                requestedEntryDate = x.RequestedEntryDate,
                snapshotVersion = x.SnapshotVersion,
                hasDuplicateWarnings = x.HadDuplicateWarningsAtSubmission,
                postedByUserId = x.PostedByUserId,
                postedByUsername = _context.Users.Where(u => u.UserId == x.PostedByUserId).Select(u => u.Username).FirstOrDefault(),
                totalAmount = x.Entries.Sum(e => e.Debit),
                totalDebit = x.Entries.Sum(e => e.Debit),
                totalCredit = x.Entries.Sum(e => e.Credit),
                balance = x.Entries.Sum(e => e.Debit) - x.Entries.Sum(e => e.Credit)
            }).ToListAsync();
        return new { items = rows };
    }

    public async Task<object> GetHistoryAsync(int userId, bool isAdmin)
    {
        var projectIds = await AccessibleProjectsAsync(userId, isAdmin);
        var hidden = await HiddenCategoriesAsync(userId, isAdmin);
        var rows = await _context.ApprovalVouchers.AsNoTracking()
            .Where(x => x.Status != ApprovalStatuses.Pending && projectIds.Contains(x.ProjectId) &&
                        !x.Entries.Any(e => hidden.Contains(e.CategoryId)))
            .OrderByDescending(x => x.DecisionDate).ThenByDescending(x => x.ApprovalVoucherId)
            .Select(x => new
            {
                approvalVoucherId = x.ApprovalVoucherId,
                voucherType = x.VoucherType == ApprovalVoucherType.PaymentReceipt ? "PaymentReceipt" : "ManualExpense",
                journalVoucherId = x.JournalVoucherId,
                reservedJournalVoucherId = x.ReservedJournalVoucherId,
                displayVoucherId = x.JournalVoucherId ?? x.ReservedJournalVoucherId,
                requestKind = x.RequestKind,
                status = x.Status,
                projectId = x.ProjectId,
                projectName = _context.Projects.Where(p => p.ProjectId == x.ProjectId).Select(p => p.ProjectName).FirstOrDefault(),
                submittedDate = x.SubmittedDate,
                requestedEntryDate = x.RequestedEntryDate,
                snapshotVersion = x.SnapshotVersion,
                hadDuplicateWarningsAtSubmission = x.HadDuplicateWarningsAtSubmission,
                hadDuplicateWarningsAtDecision = x.HadDuplicateWarningsAtDecision,
                postedByUserId = x.PostedByUserId,
                postedByUsername = _context.Users.Where(u => u.UserId == x.PostedByUserId).Select(u => u.Username).FirstOrDefault(),
                decisionDate = x.DecisionDate,
                decisionByUserId = x.DecisionByUserId,
                decisionByUsername = _context.Users.Where(u => u.UserId == x.DecisionByUserId).Select(u => u.Username).FirstOrDefault(),
                rejectionReason = x.RejectionReason,
                totalAmount = x.Entries.Sum(e => e.Debit),
                totalDebit = x.Entries.Sum(e => e.Debit),
                totalCredit = x.Entries.Sum(e => e.Credit),
                balance = x.Entries.Sum(e => e.Debit) - x.Entries.Sum(e => e.Credit),
                canPrint = x.VoucherType == ApprovalVoucherType.PaymentReceipt && x.JournalVoucherId.HasValue &&
                    _context.JournalVouchers.Any(v =>
                        v.JournalVoucherId == x.JournalVoucherId.Value && v.IsCompleted)
            }).ToListAsync();
        return new { items = rows };
    }

    public async Task<object?> GetAsync(int approvalVoucherId, int userId, bool isAdmin)
    {
        var approval = await LoadApprovalAsync(approvalVoucherId, tracking: false);
        if (approval == null) return null;
        await EnsureApprovalAccessAsync(approval, userId, isAdmin);
        var accountIds = approval.Entries.Select(x => x.AccountId).Distinct().ToList();
        var categoryIds = approval.Entries.Select(x => x.CategoryId).Distinct().ToList();
        var accounts = await _context.Accounts.AsNoTracking().Where(x => accountIds.Contains(x.AccountId))
            .ToDictionaryAsync(x => x.AccountId, x => x.AccountName);
        var categories = await _context.Categories.AsNoTracking().Where(x => categoryIds.Contains(x.CategoryId))
            .ToDictionaryAsync(x => x.CategoryId, x => x.CategoryName);
        var poster = await _context.Users.AsNoTracking().Where(x => x.UserId == approval.PostedByUserId)
            .Select(x => x.Username).SingleOrDefaultAsync();
        var projectName = await _context.Projects.AsNoTracking().Where(x => x.ProjectId == approval.ProjectId)
            .Select(x => x.ProjectName).SingleOrDefaultAsync();
        var bookerName = approval.BookerId.HasValue
            ? await _context.Bookers.AsNoTracking().Where(x => x.BookerID == approval.BookerId.Value)
                .Select(x => x.BookerName).SingleOrDefaultAsync()
            : null;
        var decisionBy = approval.DecisionByUserId.HasValue
            ? await _context.Users.AsNoTracking().Where(x => x.UserId == approval.DecisionByUserId.Value)
                .Select(x => x.Username).SingleOrDefaultAsync()
            : null;
        var duplicateWarnings = approval.VoucherType == ApprovalVoucherType.PaymentReceipt
            ? await _paymentReceipts.FindDuplicateWarningsAsync(
                approval.Entries.Where(x => x.InstrumentType.HasValue && Enum.IsDefined(x.InstrumentType.Value))
                .OrderBy(x => x.ApprovalVoucherEntryId).Select(x => new PureLedger.ViewModels.PaymentReceipt.PaymentReceiptLineRequest
                {
                    OriginalJournalEntryId = x.OriginalJournalEntryId,
                    CategoryId = x.CategoryId,
                    AccountId = x.AccountId,
                    Description = x.Description,
                    InstrumentType = x.InstrumentType!.Value,
                    ReferenceNumber = x.ReferenceNumber,
                    InstrumentDate = x.InstrumentDate,
                    Debit = x.Debit,
                    Credit = x.Credit
                }).ToList(), approval.ApprovalVoucherId, approval.JournalVoucherId)
            : Array.Empty<PureLedger.ViewModels.PaymentReceipt.PaymentReceiptDuplicateWarning>();
        var canPrint = approval.VoucherType == ApprovalVoucherType.PaymentReceipt &&
                       approval.JournalVoucherId.HasValue &&
                       await _context.JournalVouchers.AsNoTracking().AnyAsync(x =>
                           x.JournalVoucherId == approval.JournalVoucherId.Value && x.IsCompleted);
        var entriesForResponse = approval.VoucherType == ApprovalVoucherType.PaymentReceipt
            ? approval.Entries.OrderBy(x => x.ApprovalVoucherEntryId).Select(x => (object)new
            {
                approvalVoucherEntryId = x.ApprovalVoucherEntryId,
                originalJournalEntryId = x.OriginalJournalEntryId,
                accountId = x.AccountId,
                accountName = accounts.GetValueOrDefault(x.AccountId, $"Account #{x.AccountId} (deleted)"),
                categoryId = x.CategoryId,
                categoryName = categories.GetValueOrDefault(x.CategoryId, $"Category #{x.CategoryId} (deleted)"),
                description = x.Description,
                instrumentType = x.InstrumentType?.ToString(),
                referenceNumber = x.ReferenceNumber,
                instrumentDate = x.InstrumentDate,
                debit = x.Debit,
                credit = x.Credit
            }).ToList()
            : approval.Entries.OrderBy(x => x.LineNumber).Select(x => (object)new
            {
                approvalVoucherEntryId = x.ApprovalVoucherEntryId,
                originalJournalEntryId = x.OriginalJournalEntryId,
                lineNumber = x.LineNumber,
                accountId = x.AccountId,
                accountName = accounts.GetValueOrDefault(x.AccountId, $"Account #{x.AccountId} (deleted)"),
                categoryId = x.CategoryId,
                categoryName = categories.GetValueOrDefault(x.CategoryId, $"Category #{x.CategoryId} (deleted)"),
                description = x.Description,
                instrumentType = x.InstrumentType?.ToString(),
                referenceNumber = x.ReferenceNumber,
                instrumentDate = x.InstrumentDate,
                debit = x.Debit,
                credit = x.Credit
            }).ToList();
        return new
        {
            approvalVoucherId = approval.ApprovalVoucherId,
            journalVoucherId = approval.JournalVoucherId,
            reservedJournalVoucherId = approval.ReservedJournalVoucherId,
            displayVoucherId = approval.JournalVoucherId ?? approval.ReservedJournalVoucherId,
            status = approval.Status,
            voucherType = approval.VoucherType.ToString(),
            requestKind = approval.RequestKind,
            projectId = approval.ProjectId,
            projectName,
            bookerId = approval.BookerId,
            bookerName,
            creditDays = approval.CreditDays,
            submittedDate = approval.SubmittedDate,
            requestedEntryDate = approval.RequestedEntryDate,
            snapshotVersion = approval.SnapshotVersion,
            hadDuplicateWarningsAtSubmission = approval.HadDuplicateWarningsAtSubmission,
            hadDuplicateWarningsAtDecision = approval.HadDuplicateWarningsAtDecision,
            duplicateWarnings,
            postedByUserId = approval.PostedByUserId,
            postedByUsername = poster ?? $"User #{approval.PostedByUserId} (deleted)",
            decisionDate = approval.DecisionDate,
            decisionByUserId = approval.DecisionByUserId,
            decisionByUsername = decisionBy,
            rejectionReason = approval.RejectionReason,
            totalDebit = approval.Entries.Sum(x => x.Debit),
            totalCredit = approval.Entries.Sum(x => x.Credit),
            balance = approval.Entries.Sum(x => x.Debit) - approval.Entries.Sum(x => x.Credit),
            canPrint,
            entries = entriesForResponse
        };
    }

    public async Task<object> EditAsync(int approvalVoucherId, ApprovalEditRequest request, int userId, bool isAdmin)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var approval = await LockApprovalAsync(approvalVoucherId);
            if (approval.VoucherType == ApprovalVoucherType.PaymentReceipt)
                throw new InvalidOperationException("Use the Payment & Receipt pending editor for this request.");
            await EnsureApprovalAccessAsync(approval, userId, isAdmin);
            var model = request.ToJournalEntryViewModel();
            if (model.ProjectId != approval.ProjectId)
                throw new InvalidOperationException("A pending revision cannot change project.");
            if (model.IsBaseVoucher || model.SaveAsRecipe)
                throw new InvalidOperationException("Expense approval drafts cannot be base vouchers or recipes.");

            var hasExpense = await ContainsExpenseAsync(model.Entries);
            await ValidateModelAsync(model, requireExpense: hasExpense);
            var hidden = await HiddenCategoriesAsync(userId, isAdmin);
            if (model.Entries.Any(x => x.CategoryId.HasValue && hidden.Contains(x.CategoryId.Value)))
                throw new UnauthorizedAccessException("The approval draft contains a category you cannot access.");
            JournalVoucher? mappedVoucher = null;
            if (approval.RequestKind == ApprovalRequestKinds.Revision)
                mappedVoucher = await LockVoucherAsync(approval.JournalVoucherId!.Value);
            ValidateOriginalEntryMappings(mappedVoucher, model.Entries);
            if (hasExpense)
            {
                approval.BookerId = model.BookerId;
                approval.CreditDays = model.CreditDays;
                ReplaceSnapshot(approval, model.Entries);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return new { status = ApprovalStatuses.Pending, approvalVoucherId };
            }

            var posted = await ApplyNormalAsync(approval, model, userId);
            await _context.SaveChangesAsync();
            if (posted.JournalVoucherId > 0)
                await _processing.RecalculateVoucherCompletionAsync(posted.JournalVoucherId);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return new { status = ApprovalStatuses.PostedNormally, journalVoucherId = posted.JournalVoucherId };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<object> ApproveAsync(int approvalVoucherId, Guid? snapshotVersion, int userId, bool isAdmin)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var approval = await LockApprovalAsync(approvalVoucherId);
            if (approval.VoucherType == ApprovalVoucherType.PaymentReceipt)
            {
                var result = await _paymentReceipts.ApproveAsync(approval, snapshotVersion, userId, isAdmin);
                await tx.CommitAsync();
                return result;
            }
            var voucher = await ApproveExpenseCoreAsync(approval, userId, isAdmin);
            await tx.CommitAsync();
            return new { status = approval.Status, journalVoucherId = voucher.JournalVoucherId };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<object> RejectAsync(int approvalVoucherId, string reason, Guid? snapshotVersion, int userId, bool isAdmin)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A rejection reason is required.");
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var approval = await LockApprovalAsync(approvalVoucherId);
            if (approval.VoucherType == ApprovalVoucherType.PaymentReceipt)
            {
                var result = await _paymentReceipts.RejectAsync(approval, reason, snapshotVersion, userId, isAdmin);
                await tx.CommitAsync();
                return result;
            }
            await EnsureApprovalAccessAsync(approval, userId, isAdmin);
            if (approval.RequestKind == ApprovalRequestKinds.Revision)
            {
                var voucher = await LockVoucherAsync(approval.JournalVoucherId!.Value);
                await EnsureRevisionGraphStillValidAsync(voucher);
                await LockEditableChildrenAsync(voucher.JournalVoucherId);
                voucher.IsCompleted = true;
                voucher.RowVersion = RandomNumberGenerator.GetBytes(16);
                foreach (var child in await GetEditableChildrenAsync(voucher.JournalVoucherId))
                    child.IsCompleted = true;
            }
            approval.Status = ApprovalStatuses.Rejected;
            approval.DecisionDate = BusinessClock.Today;
            approval.DecisionByUserId = userId;
            approval.RejectionReason = reason.Trim();
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return new { status = approval.Status, journalVoucherId = approval.JournalVoucherId };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // Both submission and explicit approval own their transaction. Keep the accounting
    // operation shared without starting a nested transaction here.
    private async Task<JournalVoucher> ApproveExpenseCoreAsync(ApprovalVoucher approval, int userId, bool isAdmin)
    {
        if (approval.Status != ApprovalStatuses.Pending || approval.VoucherType != ApprovalVoucherType.ManualExpense)
            throw new ApprovalConflictException("This Expense request cannot be approved.");
        await EnsureApprovalAccessAsync(approval, userId, isAdmin);
        await ValidateSnapshotAsync(approval);
        var today = BusinessClock.Today;
        JournalVoucher voucher;
        if (approval.RequestKind == ApprovalRequestKinds.New)
        {
            voucher = new JournalVoucher
            {
                ProjectId = approval.ProjectId,
                BookerId = approval.BookerId,
                EntryDate = today,
                CreditableDate = today.AddDays(approval.CreditDays),
                IsCompleted = true,
                IsBaseVoucher = false,
                PendingForCharges = false
            };
            await JournalVoucherNumbers.ApplyReservedIdAsync(_context, approval, voucher);
            _context.JournalVouchers.Add(voucher);
            await _context.SaveChangesAsync();
        }
        else
        {
            if (!approval.JournalVoucherId.HasValue)
                throw new ApprovalConflictException("The revision is no longer linked to its voucher.");
            voucher = await LockVoucherAsync(approval.JournalVoucherId.Value);
            if (voucher.ProjectId != approval.ProjectId)
                throw new ApprovalConflictException("The revision no longer matches its voucher's project.");
            await EnsureRevisionGraphStillValidAsync(voucher);
            await LockEditableChildrenAsync(voucher.JournalVoucherId);
            await DeleteEditableGraphAsync(voucher.JournalVoucherId);
        }
        ApplyExpenseSnapshot(voucher, approval, today);
        voucher.RowVersion = RandomNumberGenerator.GetBytes(16);
        approval.JournalVoucherId = voucher.JournalVoucherId;
        approval.Status = ApprovalStatuses.Approved;
        approval.DecisionDate = today;
        approval.DecisionByUserId = userId;
        await _context.SaveChangesAsync();
        return voucher;
    }

    private static bool VersionsMatch(byte[]? expected, byte[]? actual) =>
        expected == null || actual == null
            ? expected == null && actual == null
            : CryptographicOperations.FixedTimeEquals(expected, actual);

    private async Task ValidateModelAsync(JournalEntryViewModel model, bool requireExpense)
    {
        if (model.Entries.Count < 2) throw new InvalidOperationException("A voucher requires at least two entries.");
        if (model.BookerId.HasValue &&
            !await _context.Bookers.AsNoTracking()
                .AnyAsync(x => x.BookerID == model.BookerId.Value && x.IsEnabled))
            throw new InvalidOperationException("The selected Booker is invalid or disabled.");
        var accountIds = model.Entries.Select(x => x.AccountId ?? 0).Distinct().ToList();
        var accounts = await _context.Accounts.Include(x => x.Category)
            .Where(x => accountIds.Contains(x.AccountId)).ToDictionaryAsync(x => x.AccountId);
        if (accounts.Count != accountIds.Count || accounts.Values.Any(x => !x.IsEnabled || x.Category?.ProjectId != model.ProjectId))
            throw new InvalidOperationException("One or more accounts are invalid, disabled, or outside the project.");
        foreach (var row in model.Entries)
        {
            if (!row.AccountId.HasValue || !accounts.TryGetValue(row.AccountId.Value, out var account) ||
                !row.CategoryId.HasValue || account.CategoryId != row.CategoryId.Value)
                throw new InvalidOperationException("Every entry must use an account from its selected category.");
            var debit = row.Debit ?? 0;
            var credit = row.Credit ?? 0;
            if (debit < 0 || credit < 0)
                throw new InvalidOperationException("Debit and credit values cannot be negative.");
            if ((debit > 0) == (credit > 0))
                throw new InvalidOperationException("Every entry must contain either debit or credit, but not both.");
            if (requireExpense && ((row.Qty ?? 0) != 0 || (row.Rate ?? 0) != 0 ||
                                   (row.Percent ?? 0) != 0 || (row.Disc ?? 0) != 0))
                throw new InvalidOperationException("Expense vouchers are monetary-only; product fields must be zero.");
            if (!requireExpense && account.AccountGroupId == ProductGroup)
            {
                if ((row.Rate ?? 0) <= 0)
                    throw new InvalidOperationException("Rate must be greater than zero for Product entries.");
                if ((row.Qty ?? 0) <= 0)
                    throw new InvalidOperationException("Quantity must be greater than zero for Product entries.");
                if (row.Rbp is not ("Y" or "N"))
                    throw new InvalidOperationException("RBP must be Y or N for Product entries.");
            }
        }
        if (requireExpense && accounts.Values.All(x => x.AccountGroupId != ExpenseGroup))
            throw new InvalidOperationException("The approval draft no longer contains an Expense account.");
        var debitTotal = model.Entries.Sum(x => x.Debit ?? 0);
        var creditTotal = model.Entries.Sum(x => x.Credit ?? 0);
        if (Math.Abs(debitTotal - creditTotal) > 0.01m)
            throw new InvalidOperationException("Debits and credits must be equal.");
    }

    private async Task ValidateSnapshotAsync(ApprovalVoucher approval)
    {
        var model = new JournalEntryViewModel
        {
            ProjectId = approval.ProjectId,
            BookerId = approval.BookerId,
            Entries = approval.Entries.OrderBy(x => x.LineNumber).Select(x => new JournalEntryRowModel
            {
                EntryId = x.OriginalJournalEntryId,
                AccountId = x.AccountId,
                CategoryId = x.CategoryId,
                Des = x.Description,
                Debit = x.Debit,
                Credit = x.Credit,
                Qty = 0,
                Rate = 0,
                Percent = 0,
                Disc = 0,
                Rbp = "N"
            }).ToList()
        };
        await ValidateModelAsync(model, requireExpense: true);
    }

    private void ReplaceSnapshot(ApprovalVoucher approval, IReadOnlyList<JournalEntryRowModel> entries)
    {
        if (approval.Entries.Count > 0) _context.ApprovalVoucherEntries.RemoveRange(approval.Entries);
        approval.Entries = entries.Select((x, index) => new ApprovalVoucherEntry
        {
            ApprovalVoucherId = approval.ApprovalVoucherId,
            OriginalJournalEntryId = x.EntryId,
            LineNumber = index + 1,
            AccountId = x.AccountId ?? 0,
            CategoryId = x.CategoryId ?? 0,
            Description = x.Des,
            Debit = x.Debit ?? 0,
            Credit = x.Credit ?? 0
        }).ToList();
    }

    private void ApplyExpenseSnapshot(JournalVoucher voucher, ApprovalVoucher approval, DateOnly date)
    {
        var existing = voucher.JournalEntries.ToDictionary(x => x.JournalEntryId);
        var mappedIds = approval.Entries.Where(x => x.OriginalJournalEntryId.HasValue)
            .Select(x => x.OriginalJournalEntryId!.Value).ToList();
        if (mappedIds.Count != mappedIds.Distinct().Count() ||
            mappedIds.Any(id => !existing.ContainsKey(id)))
            throw new ApprovalConflictException("The approval draft contains stale or duplicate entry mappings.");
        var retained = new HashSet<int>();
        foreach (var row in approval.Entries.OrderBy(x => x.LineNumber))
        {
            JournalEntry entry;
            if (row.OriginalJournalEntryId.HasValue &&
                existing.TryGetValue(row.OriginalJournalEntryId.Value, out var mapped))
            {
                entry = mapped;
                retained.Add(entry.JournalEntryId);
                var changed = entry.AccountId != row.AccountId ||
                              entry.CategoryId != row.CategoryId ||
                              entry.Description != row.Description ||
                              entry.Debit != row.Debit ||
                              entry.Credit != row.Credit ||
                              entry.Quantity != 0 || entry.Rate != 0 ||
                              entry.Discount != 0 || entry.DiscountPercent != null ||
                              entry.Rbp != "N";
                if (changed)
                {
                    entry.IsEdited = true;
                    entry.ModifiedDateTime = date;
                }
            }
            else
            {
                entry = new JournalEntry { JournalVoucherId = voucher.JournalVoucherId, IsEdited = true, ModifiedDateTime = date };
                _context.JournalEntries.Add(entry);
            }
            entry.AccountId = row.AccountId;
            entry.CategoryId = row.CategoryId;
            entry.Description = row.Description;
            entry.Debit = row.Debit;
            entry.Credit = row.Credit;
            entry.Quantity = 0;
            entry.Rate = 0;
            entry.Discount = 0;
            entry.DiscountPercent = null;
            entry.Rbp = "N";
            entry.IsProcessed = true;
        }
        foreach (var removed in existing.Values.Where(x => !retained.Contains(x.JournalEntryId)))
            _context.JournalEntries.Remove(removed);
        voucher.ProjectId = approval.ProjectId;
        voucher.BookerId = approval.BookerId;
        voucher.EntryDate = date;
        voucher.CreditableDate = date.AddDays(approval.CreditDays);
        voucher.IsBaseVoucher = false;
        voucher.PendingForCharges = false;
        voucher.IsCompleted = true;
    }

    private async Task<JournalVoucher> ApplyNormalAsync(ApprovalVoucher approval, JournalEntryViewModel model, int userId)
    {
        JournalVoucher voucher;
        if (approval.RequestKind == ApprovalRequestKinds.New)
        {
            voucher = new JournalVoucher
            {
                ProjectId = model.ProjectId,
                BookerId = model.BookerId,
                EntryDate = model.EntryDate,
                CreditableDate = model.DueDate,
                IsBaseVoucher = false,
                PendingForCharges = false,
                IsCompleted = false
            };
            await JournalVoucherNumbers.ApplyReservedIdAsync(_context, approval, voucher);
            _context.JournalVouchers.Add(voucher);
            await _context.SaveChangesAsync();
        }
        else
        {
            voucher = await LockVoucherAsync(approval.JournalVoucherId!.Value);
            await EnsureRevisionGraphStillValidAsync(voucher);
        }
        await ApplyNormalRowsAsync(voucher, model);
        approval.JournalVoucherId = voucher.JournalVoucherId;
        approval.Status = ApprovalStatuses.PostedNormally;
        approval.DecisionDate = BusinessClock.Today;
        approval.DecisionByUserId = userId;
        return voucher;
    }

    private async Task ApplyNormalRowsAsync(JournalVoucher voucher, JournalEntryViewModel model)
    {
        var accountIds = model.Entries.Select(e => e.AccountId ?? 0).Distinct().ToList();
        var accounts = await _context.Accounts.AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId))
            .ToDictionaryAsync(x => x.AccountId);
        var groups = accounts.ToDictionary(x => x.Key, x => x.Value.AccountGroupId);
        var existing = voucher.JournalEntries.ToDictionary(x => x.JournalEntryId);
        var retained = new HashSet<int>();
        foreach (var row in model.Entries)
        {
            JournalEntry entry;
            var isExisting = false;
            if (row.EntryId.HasValue && existing.TryGetValue(row.EntryId.Value, out var mapped))
            {
                entry = mapped;
                retained.Add(entry.JournalEntryId);
                isExisting = true;
            }
            else
            {
                entry = new JournalEntry { JournalVoucherId = voucher.JournalVoucherId, IsEdited = true };
                _context.JournalEntries.Add(entry);
            }
            var oldGroup = entry.AccountId > 0
                ? await _context.Accounts.AsNoTracking().Where(x => x.AccountId == entry.AccountId).Select(x => x.AccountGroupId).SingleOrDefaultAsync()
                : 0;
            var oldQuantity = entry.Quantity;
            var oldProcessed = entry.IsProcessed;
            var newGroup = groups[row.AccountId!.Value];
            if (oldGroup == ProductGroup && newGroup != ProductGroup)
                await DeleteDceAsync(entry.JournalEntryId);
            entry.AccountId = row.AccountId.Value;
            entry.CategoryId = row.CategoryId!.Value;
            entry.Description = row.Des;
            entry.Quantity = row.Qty ?? 0;
            entry.Rate = row.Rate ?? 0;
            entry.DiscountPercent = row.Percent;
            entry.Discount = row.Disc ?? 0;
            entry.Debit = row.Debit ?? 0;
            entry.Credit = row.Credit ?? 0;
            entry.Rbp = row.Rbp ?? "Y";
            if (newGroup != ProductGroup)
                entry.IsProcessed = true;
            else if (!isExisting || oldGroup != ProductGroup)
            {
                entry.IsProcessed = entry.Quantity <= 0;
                if (isExisting) await DeleteDceAsync(entry.JournalEntryId);
            }
            else if (oldQuantity == entry.Quantity)
                entry.IsProcessed = oldProcessed;
            else
            {
                entry.IsProcessed = entry.Quantity <= 0;
                if (oldProcessed && !entry.IsProcessed || oldQuantity > 0 && entry.Quantity <= 0)
                    await DeleteDceAsync(entry.JournalEntryId);
            }
        }
        foreach (var removed in existing.Values.Where(x => !retained.Contains(x.JournalEntryId)))
        {
            await DeleteDceAsync(removed.JournalEntryId);
            _context.JournalEntries.Remove(removed);
        }
        voucher.ProjectId = model.ProjectId;
        voucher.BookerId = JournalVoucherBooker.Resolve(
            model.Entries.Select(row => accounts.GetValueOrDefault(row.AccountId ?? 0)), model.BookerId);
        voucher.EntryDate = model.EntryDate;
        voucher.CreditableDate = model.DueDate;
        voucher.IsBaseVoucher = model.IsBaseVoucher;
        voucher.RowVersion = RandomNumberGenerator.GetBytes(16);
    }

    private async Task DeleteDceAsync(int entryId)
    {
        var rows = await _context.DeliveryChallanEntries.Where(x => x.JournalEntryId == entryId).ToListAsync();
        _context.DeliveryChallanEntries.RemoveRange(rows);
    }

    private async Task EnsureManualRevisionEligibleAsync(JournalVoucher voucher)
    {
        if (!voucher.IsCompleted || voucher.IsBaseVoucher || voucher.IsStockTransfer)
            throw new InvalidOperationException("Only completed manual vouchers can be submitted for Expense reapproval.");
        if (await _context.JournalVoucherReferences.AnyAsync(x => x.ReferencedVoucherId == voucher.JournalVoucherId))
            throw new InvalidOperationException("Generated/reference vouchers bypass approval and must be edited through their owning workflow.");
        await EnsureRevisionGraphStillValidAsync(voucher);
        var childIds = await _context.JournalVoucherReferences.AsNoTracking()
            .Where(x => x.MainVoucherId == voucher.JournalVoucherId &&
                        EditableChildTypes.Contains(x.ReferenceType!))
            .Select(x => x.ReferencedVoucherId).ToListAsync();
        foreach (var childId in childIds)
        {
            var child = await LockVoucherAsync(childId);
            if (!child.IsCompleted)
                throw new InvalidOperationException(
                    "All linked FOR/cartage vouchers must be completed before reapproval.");
        }
    }

    private static void ValidateOriginalEntryMappings(
        JournalVoucher? voucher, IReadOnlyCollection<JournalEntryRowModel> rows)
    {
        var ids = rows.Where(x => x.EntryId.HasValue).Select(x => x.EntryId!.Value).ToList();
        if (ids.Count != ids.Distinct().Count())
            throw new InvalidOperationException("An existing journal entry can appear only once in an approval draft.");
        if (voucher == null)
        {
            if (ids.Count > 0)
                throw new InvalidOperationException("A new approval request cannot reference existing journal entries.");
            return;
        }
        var valid = voucher.JournalEntries.Select(x => x.JournalEntryId).ToHashSet();
        if (ids.Any(id => !valid.Contains(id)))
            throw new ApprovalConflictException("One or more approval rows no longer belong to this voucher.");
    }

    private async Task EnsureRevisionGraphStillValidAsync(JournalVoucher voucher)
    {
        var refs = await _context.JournalVoucherReferences
            .Where(x => x.MainVoucherId == voucher.JournalVoucherId).ToListAsync();
        if (refs.Any(x => !EditableChildTypes.Contains(x.ReferenceType!)) ||
            refs.GroupBy(x => new { x.ReferenceType, x.ReferencedVoucherId }).Any(x => x.Count() > 1) ||
            refs.GroupBy(x => x.ReferencedVoucherId).Any(x => x.Count() > 1))
            throw new ApprovalConflictException("The linked voucher graph is not supported for Expense reapproval.");
        var childIds = refs.Select(x => x.ReferencedVoucherId).ToList();
        if (childIds.Count > 0)
        {
            var children = await _context.JournalVouchers.AsNoTracking()
                .Where(x => childIds.Contains(x.JournalVoucherId))
                .Select(x => new
                {
                    x.JournalVoucherId,
                    x.ProjectId,
                    x.IsStockTransfer,
                    x.IsBaseVoucher
                })
                .ToListAsync();
            if (children.Count != childIds.Count || children.Any(x => x.ProjectId != voucher.ProjectId))
                throw new ApprovalConflictException("The linked voucher graph contains a missing or cross-project child.");
            if (children.Any(x => x.IsStockTransfer || x.IsBaseVoucher))
                throw new ApprovalConflictException(
                    "Stock-transfer and base vouchers cannot be approval-owned FOR/cartage children.");
        }
        if (childIds.Count > 0 && await _context.JournalVoucherReferences.AnyAsync(x =>
                childIds.Contains(x.MainVoucherId) ||
                childIds.Contains(x.ReferencedVoucherId) && x.MainVoucherId != voucher.JournalVoucherId))
            throw new ApprovalConflictException("The linked voucher graph changed while approval was pending.");
        var graphIds = childIds.Append(voucher.JournalVoucherId).ToList();
        if (await _context.DeliveryChallanVouchers
                .Where(x => graphIds.Contains(x.JournalVoucherId))
                .GroupBy(x => x.JournalVoucherId).AnyAsync(x => x.Count() > 1))
            throw new ApprovalConflictException("The linked graph contains duplicate Delivery Challan containers.");
        if (await _context.DeliveryChallanEntries
                .Where(x => graphIds.Contains(x.JournalEntry.JournalVoucherId))
                .GroupBy(x => x.JournalEntryId).AnyAsync(x => x.Count() > 1))
            throw new ApprovalConflictException("The linked graph contains duplicate Delivery Challan entry links.");
    }

    private async Task DeleteEditableGraphAsync(int parentId)
    {
        var refs = await _context.JournalVoucherReferences
            .Where(x => x.MainVoucherId == parentId && EditableChildTypes.Contains(x.ReferenceType!)).ToListAsync();
        var childIds = refs.Select(x => x.ReferencedVoucherId).ToList();
        var graphIds = childIds.Append(parentId).ToList();
        var dcs = await _context.DeliveryChallanVouchers.Where(x => graphIds.Contains(x.JournalVoucherId))
            .Include(x => x.Entries).ToListAsync();
        _context.DeliveryChallanEntries.RemoveRange(dcs.SelectMany(x => x.Entries));
        _context.DeliveryChallanVouchers.RemoveRange(dcs);
        _context.JournalVoucherReferences.RemoveRange(refs);
        if (childIds.Count > 0)
            _context.JournalVouchers.RemoveRange(await _context.JournalVouchers.Where(x => childIds.Contains(x.JournalVoucherId)).ToListAsync());
        await _context.SaveChangesAsync();
    }

    private Task<List<JournalVoucher>> GetEditableChildrenAsync(int parentId) =>
        _context.JournalVoucherReferences.Where(x => x.MainVoucherId == parentId &&
                EditableChildTypes.Contains(x.ReferenceType!))
            .Select(x => x.ReferencedVoucher).ToListAsync();

    private async Task LockEditableChildrenAsync(int parentId)
    {
        var ids = await _context.JournalVoucherReferences.AsNoTracking()
            .Where(x => x.MainVoucherId == parentId && EditableChildTypes.Contains(x.ReferenceType!))
            .Select(x => x.ReferencedVoucherId).OrderBy(x => x).ToListAsync();
        foreach (var id in ids) _ = await LockVoucherAsync(id);
    }

    private async Task<ApprovalVoucher> LockApprovalAsync(int id)
    {
        var approval = await RowLocks.LockApprovalVoucherAsync(_context, id) ?? throw new InvalidOperationException("Approval request was not found.");
        if (approval.Status != ApprovalStatuses.Pending)
            throw new ApprovalConflictException("This approval request has already been decided.");
        await _context.Entry(approval).Collection(x => x.Entries).LoadAsync();
        return approval;
    }

    private async Task<JournalVoucher> LockVoucherAsync(int id)
    {
        var voucher = await RowLocks.LockJournalVoucherAsync(_context, id) ?? throw new InvalidOperationException("Voucher was not found.");
        await _context.Entry(voucher).Collection(x => x.JournalEntries).LoadAsync();
        return voucher;
    }

    private Task<ApprovalVoucher?> LoadApprovalAsync(int id, bool tracking)
    {
        var query = _context.ApprovalVouchers.Include(x => x.Entries).AsQueryable();
        if (!tracking) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.ApprovalVoucherId == id);
    }

    private async Task<List<int>> AccessibleProjectsAsync(int userId, bool isAdmin) =>
        isAdmin
            ? await _context.Projects.AsNoTracking().Select(x => x.ProjectId).ToListAsync()
            : await _context.UserProjects.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.ProjectId).ToListAsync();

    private async Task EnsureProjectAccessAsync(int projectId, int userId, bool isAdmin)
    {
        if (!isAdmin && !await _context.UserProjects.AsNoTracking()
                .AnyAsync(x => x.UserId == userId && x.ProjectId == projectId))
            throw new UnauthorizedAccessException("You do not have access to this project.");
    }

    private async Task<List<int>> HiddenCategoriesAsync(int userId, bool isAdmin) =>
        isAdmin ? new List<int>() : await _context.UserCategories.AsNoTracking()
            .Where(x => x.UserId == userId).Select(x => x.CategoryId).ToListAsync();

    private async Task EnsureApprovalAccessAsync(ApprovalVoucher approval, int userId, bool isAdmin)
    {
        await EnsureProjectAccessAsync(approval.ProjectId, userId, isAdmin);
        var hidden = await HiddenCategoriesAsync(userId, isAdmin);
        if (approval.Entries.Any(x => hidden.Contains(x.CategoryId)))
            throw new UnauthorizedAccessException("This approval contains a category you cannot access.");
    }
}
