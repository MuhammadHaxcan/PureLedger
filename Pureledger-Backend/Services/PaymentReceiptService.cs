using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.PaymentReceipt;

namespace PureLedger.Services;

public sealed class PaymentReceiptService : IPaymentReceiptService
{
    private const int MonetaryAccountGroup = 1;
    private readonly LedgerDbContext _context;
    private readonly IPaymentReceiptClassifier _classifier;
    private readonly IPaymentReceiptApprovalCheckpoint _approvalCheckpoint;

    public PaymentReceiptService(
        LedgerDbContext context,
        IPaymentReceiptClassifier classifier,
        IPaymentReceiptApprovalCheckpoint? approvalCheckpoint = null)
    {
        _context = context;
        _classifier = classifier;
        _approvalCheckpoint = approvalCheckpoint ?? new NoOpPaymentReceiptApprovalCheckpoint();
    }

    public async Task<object> GetInitAsync(int userId, bool isAdmin)
    {
        var projects = isAdmin
            ? await _context.Projects.AsNoTracking().OrderBy(x => x.ProjectName).ToListAsync()
            : await _context.UserProjects.AsNoTracking().Where(x => x.UserId == userId)
                .Select(x => x.Project).OrderBy(x => x.ProjectName).ToListAsync();
        return new
        {
            projects = projects.Select(x => new { id = x.ProjectId, name = x.ProjectName }),
            instrumentTypes = Enum.GetNames<PaymentInstrumentType>(),
            defaultEntryDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
    }

    public async Task<object> GetCategoriesAsync(int projectId, int userId, bool isAdmin)
    {
        await EnsureProjectAccessAsync(projectId, userId, isAdmin);
        var hidden = await HiddenCategoriesAsync(userId, isAdmin);
        return await _context.Categories.AsNoTracking()
            .Where(x => x.ProjectId == projectId && !hidden.Contains(x.CategoryId) &&
                        x.Accounts.Any(a => a.AccountGroupId == MonetaryAccountGroup && a.IsEnabled))
            .OrderBy(x => x.CategoryName)
            .Select(x => new { id = x.CategoryId, name = x.CategoryName }).ToListAsync();
    }

    public async Task<object> GetAccountsAsync(int categoryId, int userId, bool isAdmin)
    {
        var category = await _context.Categories.AsNoTracking().SingleOrDefaultAsync(x => x.CategoryId == categoryId)
            ?? throw new InvalidOperationException("Category was not found.");
        await EnsureProjectAccessAsync(category.ProjectId, userId, isAdmin);
        if ((await HiddenCategoriesAsync(userId, isAdmin)).Contains(categoryId))
            throw new UnauthorizedAccessException("This category is not accessible.");
        return await _context.Accounts.AsNoTracking()
            .Where(x => x.CategoryId == categoryId && x.AccountGroupId == MonetaryAccountGroup && x.IsEnabled)
            .OrderBy(x => x.AccountName)
            .Select(x => new { id = x.AccountId, name = x.AccountName, categoryId = x.CategoryId }).ToListAsync();
    }

    public async Task<IReadOnlyList<PaymentReceiptDuplicateWarning>> FindDuplicateWarningsAsync(
        IReadOnlyList<PaymentReceiptLineRequest> entries, int? approvalVoucherId, int? journalVoucherId)
    {
        var rows = entries.Select(row => new
        {
            Row = row,
            Normalized = Normalize(row.ReferenceNumber)
        }).Where(x => x.Row.InstrumentType != PaymentInstrumentType.Cash && x.Normalized != null).ToList();
        if (rows.Count == 0) return Array.Empty<PaymentReceiptDuplicateWarning>();

        var types = rows.Select(x => x.Row.InstrumentType).Distinct().ToList();
        var refs = rows.Select(x => x.Normalized!).Distinct().ToList();
        var posted = await _context.JournalEntryInstrumentDetails.AsNoTracking()
            .Where(x => types.Contains(x.InstrumentType) && refs.Contains(x.NormalizedReferenceNumber!) &&
                        x.JournalEntry.JournalVoucher.IsCompleted &&
                        (!journalVoucherId.HasValue || x.JournalEntry.JournalVoucherId != journalVoucherId.Value) &&
                        _context.ApprovalVouchers.Any(a =>
                            a.JournalVoucherId == x.JournalEntry.JournalVoucherId &&
                            a.VoucherType == ApprovalVoucherType.PaymentReceipt &&
                            a.Status == ApprovalStatuses.Approved))
            .Select(x => new { x.InstrumentType, x.NormalizedReferenceNumber }).Distinct().ToListAsync();
        var pending = await _context.ApprovalVoucherEntries.AsNoTracking()
            .Where(x => x.ApprovalVoucher.VoucherType == ApprovalVoucherType.PaymentReceipt &&
                        x.ApprovalVoucher.Status == ApprovalStatuses.Pending &&
                        (!approvalVoucherId.HasValue || x.ApprovalVoucherId != approvalVoucherId.Value) &&
                        (!journalVoucherId.HasValue || x.ApprovalVoucher.JournalVoucherId != journalVoucherId.Value) &&
                        x.InstrumentType.HasValue && types.Contains(x.InstrumentType.Value) &&
                        refs.Contains(x.NormalizedReferenceNumber!))
            .Select(x => new { Type = x.InstrumentType!.Value, x.NormalizedReferenceNumber }).Distinct().ToListAsync();
        var requestCounts = rows.GroupBy(x => new { x.Row.InstrumentType, x.Normalized })
            .ToDictionary(x => x.Key, x => x.Count());

        return rows.Select(x =>
        {
            var same = requestCounts[new { x.Row.InstrumentType, x.Normalized }] > 1;
            var existing = posted.Any(p => p.InstrumentType == x.Row.InstrumentType && p.NormalizedReferenceNumber == x.Normalized);
            var inPending = pending.Any(p => p.Type == x.Row.InstrumentType && p.NormalizedReferenceNumber == x.Normalized);
            return new PaymentReceiptDuplicateWarning
            {
                InstrumentType = x.Row.InstrumentType,
                ReferenceNumber = x.Row.ReferenceNumber!.Trim(),
                MatchesCurrentRequest = same,
                MatchesExistingVoucher = existing,
                MatchesPendingRequest = inPending,
                Message = existing || inPending
                    ? "A matching instrument reference exists in another Payment & Receipt voucher or request."
                    : "This instrument reference is repeated in the current request."
            };
        }).Where(x => x.MatchesCurrentRequest || x.MatchesExistingVoucher || x.MatchesPendingRequest)
          .ToList();
    }

    public async Task<object> SubmitAsync(PaymentReceiptSubmissionRequest request, int userId, bool isAdmin)
    {
        await ValidateAsync(request, userId, isAdmin, null);
        if (request.Entries.Any(x => x.OriginalJournalEntryId.HasValue))
            throw new InvalidOperationException("A new request cannot reference posted journal entries.");
        var warnings = await FindDuplicateWarningsAsync(request.Entries, null, null);
        var approval = new ApprovalVoucher
        {
            VoucherType = ApprovalVoucherType.PaymentReceipt,
            Status = ApprovalStatuses.Pending,
            RequestKind = ApprovalRequestKinds.New,
            ProjectId = request.ProjectId,
            BookerId = null,
            CreditDays = 0,
            SubmittedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            RequestedEntryDate = request.EntryDate,
            SnapshotVersion = Guid.NewGuid(),
            HadDuplicateWarningsAtSubmission = warnings.Count > 0,
            PostedByUserId = userId
        };
        _context.ApprovalVouchers.Add(approval);
        ReplaceSnapshot(approval, request.Entries);
        await _context.SaveChangesAsync();
        return SubmissionResponse(approval, warnings);
    }

    public async Task<object?> GetPostedAsync(int journalVoucherId, int userId, bool isAdmin)
    {
        if (!await _classifier.HasAnyInstrumentDetailsAsync(journalVoucherId)) return null;
        if (!await _classifier.IsStructurallyValidAsync(journalVoucherId))
            throw new PaymentReceiptIntegrityException("The voucher has partial or invalid Payment & Receipt data.");
        var voucher = await _context.JournalVouchers.AsNoTracking().AsSplitQuery()
            .Include(x => x.Project)
            .Include(x => x.JournalEntries).ThenInclude(x => x.Account)
            .Include(x => x.JournalEntries).ThenInclude(x => x.Category)
            .Include(x => x.JournalEntries).ThenInclude(x => x.InstrumentDetail)
            .SingleAsync(x => x.JournalVoucherId == journalVoucherId);
        var hasApprovedProvenance = await _context.ApprovalVouchers.AsNoTracking().AnyAsync(x =>
            x.JournalVoucherId == journalVoucherId &&
            x.VoucherType == ApprovalVoucherType.PaymentReceipt &&
            x.Status == ApprovalStatuses.Approved && x.ProjectId == voucher.ProjectId && x.Entries.Count >= 2);
        var pendingIds = await _context.ApprovalVouchers.AsNoTracking()
            .Where(x => x.JournalVoucherId == journalVoucherId &&
                        x.VoucherType == ApprovalVoucherType.PaymentReceipt &&
                        x.RequestKind == ApprovalRequestKinds.Revision &&
                        x.Status == ApprovalStatuses.Pending)
            .Select(x => x.ApprovalVoucherId).ToListAsync();
        if (!hasApprovedProvenance || voucher.IsCompleted && pendingIds.Count != 0 ||
            !voucher.IsCompleted && pendingIds.Count != 1)
            throw new PaymentReceiptIntegrityException(
                "The Payment & Receipt voucher has invalid approval provenance or pending state.");
        await EnsureProjectAccessAsync(voucher.ProjectId, userId, isAdmin);
        var hidden = await HiddenCategoriesAsync(userId, isAdmin);
        if (voucher.JournalEntries.Any(x => hidden.Contains(x.CategoryId)))
            throw new UnauthorizedAccessException("The voucher contains a category you cannot access.");
        int? pendingId = pendingIds.Count == 1 ? pendingIds[0] : null;
        return new
        {
            journalVoucherId,
            voucherType = ApprovalVoucherType.PaymentReceipt.ToString(),
            projectId = voucher.ProjectId,
            projectName = voucher.Project?.ProjectName,
            entryDate = voucher.EntryDate,
            voucherVersion = voucher.RowVersion,
            isCompleted = voucher.IsCompleted,
            pendingApprovalId = pendingId,
            isReadOnly = pendingId.HasValue,
            entries = voucher.JournalEntries.OrderBy(x => x.JournalEntryId).Select(x => new
            {
                originalJournalEntryId = x.JournalEntryId,
                x.CategoryId,
                categoryName = x.Category?.CategoryName,
                x.AccountId,
                accountName = x.Account?.AccountName,
                description = x.Description,
                instrumentType = x.InstrumentDetail.InstrumentType.ToString(),
                referenceNumber = x.InstrumentDetail.ReferenceNumber,
                instrumentDate = x.InstrumentDetail.InstrumentDate,
                x.Debit,
                x.Credit
            }),
            totalDebit = voucher.JournalEntries.Sum(x => x.Debit),
            totalCredit = voucher.JournalEntries.Sum(x => x.Credit)
        };
    }

    public async Task EnsureVoucherAccessAsync(int journalVoucherId, int userId, bool isAdmin)
    {
        var scope = await _context.JournalVouchers.AsNoTracking()
            .Where(x => x.JournalVoucherId == journalVoucherId)
            .Select(x => new
            {
                x.ProjectId,
                Categories = x.JournalEntries.Select(e => e.CategoryId).Distinct().ToList()
            }).SingleOrDefaultAsync() ?? throw new InvalidOperationException("Voucher was not found.");
        await EnsureProjectAccessAsync(scope.ProjectId, userId, isAdmin);
        var hidden = await HiddenCategoriesAsync(userId, isAdmin);
        if (scope.Categories.Any(hidden.Contains))
            throw new UnauthorizedAccessException("The voucher contains a category you cannot access.");
    }

    public async Task<object> SubmitRevisionAsync(int journalVoucherId, PaymentReceiptSubmissionRequest request, int userId, bool isAdmin)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var voucher = await LockVoucherAsync(journalVoucherId);
            if (!await _classifier.IsApprovedAsync(journalVoucherId))
                throw new PaymentReceiptIntegrityException("Only completed, approved Payment & Receipt vouchers can be revised.");
            await EnsureProjectAccessAsync(voucher.ProjectId, userId, isAdmin);
            if (request.ProjectId != voucher.ProjectId)
                throw new InvalidOperationException("A Payment & Receipt revision cannot change project.");
            if (request.VoucherVersion == null || voucher.RowVersion == null ||
                !CryptographicOperations.FixedTimeEquals(request.VoucherVersion, voucher.RowVersion))
                throw new ApprovalConflictException("The voucher changed after it was loaded. Reload and try again.");
            if (await _context.ApprovalVouchers.AnyAsync(x => x.JournalVoucherId == journalVoucherId && x.Status == ApprovalStatuses.Pending))
                throw new ApprovalConflictException("This voucher already has a pending approval request.");
            await ValidateAsync(request, userId, isAdmin, voucher);
            var warnings = await FindDuplicateWarningsAsync(request.Entries, null, journalVoucherId);
            var approval = new ApprovalVoucher
            {
                VoucherType = ApprovalVoucherType.PaymentReceipt,
                Status = ApprovalStatuses.Pending,
                RequestKind = ApprovalRequestKinds.Revision,
                JournalVoucherId = journalVoucherId,
                ProjectId = voucher.ProjectId,
                BookerId = null,
                CreditDays = 0,
                SubmittedDate = DateOnly.FromDateTime(DateTime.UtcNow),
                RequestedEntryDate = request.EntryDate,
                SnapshotVersion = Guid.NewGuid(),
                HadDuplicateWarningsAtSubmission = warnings.Count > 0,
                PostedByUserId = userId
            };
            _context.ApprovalVouchers.Add(approval);
            ReplaceSnapshot(approval, request.Entries);
            voucher.IsCompleted = false;
            voucher.RowVersion = NewVersion();
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return SubmissionResponse(approval, warnings);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<object> EditPendingAsync(int approvalVoucherId, PaymentReceiptApprovalEditRequest request, int userId, bool isAdmin)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var approval = await LockApprovalAsync(approvalVoucherId);
            if (approval.VoucherType != ApprovalVoucherType.PaymentReceipt)
                throw new InvalidOperationException("This is not a Payment & Receipt request.");
            EnsureSnapshot(approval, request.SnapshotVersion);
            await EnsureApprovalAccessAsync(approval, userId, isAdmin);
            JournalVoucher? voucher = null;
            if (approval.RequestKind == ApprovalRequestKinds.Revision)
            {
                voucher = await LockVoucherAsync(approval.JournalVoucherId!.Value);
                if (request.ProjectId != voucher.ProjectId)
                    throw new InvalidOperationException("A revision cannot change project.");
            }
            else if (request.ProjectId != approval.ProjectId)
                throw new InvalidOperationException("A pending request cannot change project.");
            await ValidateAsync(request, userId, isAdmin, voucher);
            var warnings = await FindDuplicateWarningsAsync(request.Entries, approvalVoucherId, approval.JournalVoucherId);
            approval.RequestedEntryDate = request.EntryDate;
            approval.SnapshotVersion = Guid.NewGuid();
            approval.HadDuplicateWarningsAtSubmission |= warnings.Count > 0;
            _context.ApprovalVoucherEntries.RemoveRange(approval.Entries);
            ReplaceSnapshot(approval, request.Entries);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return SubmissionResponse(approval, warnings);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<object> ApproveAsync(ApprovalVoucher approval, Guid? reviewedSnapshotVersion, int userId, bool isAdmin)
    {
        EnsureSnapshot(approval, reviewedSnapshotVersion);
        await EnsureApprovalAccessAsync(approval, userId, isAdmin);
        ValidateApprovalSnapshot(approval);
        var request = ToRequest(approval);
        JournalVoucher? existing = null;
        if (approval.RequestKind == ApprovalRequestKinds.Revision)
        {
            if (!approval.JournalVoucherId.HasValue)
                throw new ApprovalConflictException("The revision is no longer linked to its voucher.");
            existing = await LockVoucherAsync(approval.JournalVoucherId.Value);
        }
        await ValidateAsync(request, userId, isAdmin, existing);
        var warnings = await FindDuplicateWarningsAsync(approval.Entries.OrderBy(x => x.ApprovalVoucherEntryId)
            .Select(ToLineRequest).ToList(), approval.ApprovalVoucherId, approval.JournalVoucherId);

        JournalVoucher voucher;
        if (existing == null)
        {
            voucher = new JournalVoucher
            {
                ProjectId = approval.ProjectId,
                EntryDate = approval.RequestedEntryDate!.Value,
                CreditableDate = approval.RequestedEntryDate.Value,
                IsCompleted = false,
                IsBaseVoucher = false,
                IsStockTransfer = false,
                PendingForCharges = false,
                BookerId = null,
                RowVersion = NewVersion()
            };
            _context.JournalVouchers.Add(voucher);
            await _context.SaveChangesAsync();
            await _approvalCheckpoint.ReachedAsync(PaymentReceiptApprovalCheckpoint.VoucherSaved);
        }
        else
        {
            voucher = existing;
            if (!await _classifier.IsStructurallyValidAsync(voucher.JournalVoucherId))
                throw new PaymentReceiptIntegrityException("The posted Payment & Receipt graph changed while approval was pending.");
        }

        var detailChanges = await ApplySnapshotEntriesAsync(voucher, approval, existing != null);
        voucher.EntryDate = approval.RequestedEntryDate!.Value;
        voucher.CreditableDate = approval.RequestedEntryDate.Value;
        await _context.SaveChangesAsync();
        await _approvalCheckpoint.ReachedAsync(PaymentReceiptApprovalCheckpoint.EntriesSaved);
        ApplySnapshotInstrumentDetails(detailChanges);
        await _context.SaveChangesAsync();
        await _approvalCheckpoint.ReachedAsync(PaymentReceiptApprovalCheckpoint.InstrumentDetailsSaved);
        if (!await _classifier.IsStructurallyValidAsync(voucher.JournalVoucherId))
            throw new PaymentReceiptIntegrityException("The approved Payment & Receipt aggregate failed its integrity validation.");

        voucher.IsCompleted = true;
        voucher.RowVersion = NewVersion();
        approval.JournalVoucherId = voucher.JournalVoucherId;
        approval.Status = ApprovalStatuses.Approved;
        approval.DecisionDate = DateOnly.FromDateTime(DateTime.UtcNow);
        approval.DecisionByUserId = userId;
        approval.HadDuplicateWarningsAtDecision = warnings.Count > 0;
        approval.SnapshotVersion = Guid.NewGuid();
        await _context.SaveChangesAsync();
        return new
        {
            status = approval.Status,
            journalVoucherId = voucher.JournalVoucherId,
            duplicateWarnings = warnings,
            resolvedVoucherType = VoucherType.PaymentReceipt.ToString()
        };
    }

    public async Task<object> RejectAsync(ApprovalVoucher approval, string reason, Guid? reviewedSnapshotVersion, int userId, bool isAdmin)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("A rejection reason is required.");
        EnsureSnapshot(approval, reviewedSnapshotVersion);
        await EnsureApprovalAccessAsync(approval, userId, isAdmin);
        if (approval.RequestKind == ApprovalRequestKinds.Revision)
        {
            var voucher = await LockVoucherAsync(approval.JournalVoucherId ?? 0);
            if (!await _classifier.IsStructurallyValidAsync(voucher.JournalVoucherId))
                throw new PaymentReceiptIntegrityException("The posted Payment & Receipt graph changed while approval was pending.");
            voucher.IsCompleted = true;
            voucher.RowVersion = NewVersion();
        }
        approval.Status = ApprovalStatuses.Rejected;
        approval.DecisionDate = DateOnly.FromDateTime(DateTime.UtcNow);
        approval.DecisionByUserId = userId;
        approval.RejectionReason = reason.Trim();
        approval.SnapshotVersion = Guid.NewGuid();
        await _context.SaveChangesAsync();
        return new { status = approval.Status, journalVoucherId = approval.JournalVoucherId };
    }

    public async Task<object> GetReportAsync(PaymentReceiptReportRequest request, int userId, bool isAdmin)
    {
        if (request.StartDate.HasValue && request.EndDate.HasValue && request.StartDate > request.EndDate)
            throw new ArgumentException("Start date cannot be after end date.");
        if (!request.ProjectId.HasValue || request.ProjectId.Value <= 0)
            throw new ArgumentException("Project is required.");

        var projectIds = isAdmin
            ? await _context.Projects.AsNoTracking().Select(x => x.ProjectId).ToListAsync()
            : await _context.UserProjects.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.ProjectId).ToListAsync();
        var hidden = await HiddenCategoriesAsync(userId, isAdmin);
        var query = _context.JournalVouchers.AsNoTracking()
            .Where(v => v.IsCompleted && projectIds.Contains(v.ProjectId) &&
                        !v.JournalEntries.Any(e => hidden.Contains(e.CategoryId)) &&
                        v.JournalEntries.Any() &&
                        v.JournalEntries.All(e => e.InstrumentDetail != null) &&
                        _context.ApprovalVouchers.Any(a => a.JournalVoucherId == v.JournalVoucherId &&
                            a.VoucherType == ApprovalVoucherType.PaymentReceipt && a.Status == ApprovalStatuses.Approved));
        if (request.StartDate.HasValue) query = query.Where(x => x.EntryDate >= request.StartDate);
        if (request.EndDate.HasValue) query = query.Where(x => x.EntryDate <= request.EndDate);
        query = query.Where(x => x.ProjectId == request.ProjectId.Value);

        var searchText = request.Search?.Trim();
        int? searchJvId = null;
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var numeric = searchText.StartsWith("JV-", StringComparison.OrdinalIgnoreCase)
                ? searchText[3..]
                : searchText;
            if (int.TryParse(numeric, out var parsedJvId)) searchJvId = parsedJvId;
        }
        PaymentInstrumentType? searchInstrument = null;
        if (!string.IsNullOrWhiteSpace(searchText) && Enum.TryParse<PaymentInstrumentType>(
                searchText.Replace(" ", string.Empty), true, out var parsedInstrument))
            searchInstrument = parsedInstrument;

        var matching = query.Where(v => v.JournalEntries.Any(e =>
            (!request.CategoryId.HasValue || e.CategoryId == request.CategoryId) &&
            (!request.AccountId.HasValue || e.AccountId == request.AccountId) &&
            (!request.InstrumentType.HasValue || e.InstrumentDetail!.InstrumentType == request.InstrumentType) &&
            (string.IsNullOrWhiteSpace(request.ReferenceNumber) ||
                e.InstrumentDetail!.NormalizedReferenceNumber!.Contains(Normalize(request.ReferenceNumber)!)) &&
            (string.IsNullOrWhiteSpace(searchText) ||
                (searchJvId.HasValue && v.JournalVoucherId == searchJvId.Value) ||
                (e.Description != null && EF.Functions.ILike(e.Description, $"%{searchText}%")) ||
                EF.Functions.ILike(e.Account!.AccountName, $"%{searchText}%") ||
                (searchInstrument.HasValue && e.InstrumentDetail!.InstrumentType == searchInstrument.Value) ||
                (e.InstrumentDetail!.ReferenceNumber != null && EF.Functions.ILike(e.InstrumentDetail.ReferenceNumber, $"%{searchText}%")))));
        var ids = await matching.OrderByDescending(x => x.EntryDate).ThenByDescending(x => x.JournalVoucherId)
            .Select(x => x.JournalVoucherId).ToListAsync();
        var validIds = new List<int>();
        foreach (var id in ids)
            if (await _classifier.IsApprovedAsync(id)) validIds.Add(id);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 500);
        var pageIds = validIds.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var vouchers = await _context.JournalVouchers.AsNoTracking().AsSplitQuery()
            .Where(x => pageIds.Contains(x.JournalVoucherId))
            .Include(x => x.Project)
            .Include(x => x.JournalEntries).ThenInclude(x => x.Account)
            .Include(x => x.JournalEntries).ThenInclude(x => x.Category)
            .Include(x => x.JournalEntries).ThenInclude(x => x.InstrumentDetail)
            .OrderByDescending(x => x.EntryDate).ThenByDescending(x => x.JournalVoucherId).ToListAsync();
        var allTotals = await _context.JournalEntries.AsNoTracking().Where(x => validIds.Contains(x.JournalVoucherId))
            .GroupBy(_ => 1).Select(g => new { debit = g.Sum(x => x.Debit), credit = g.Sum(x => x.Credit) })
            .SingleOrDefaultAsync();
        return new
        {
            page,
            pageSize,
            totalVouchers = validIds.Count,
            totalDebit = allTotals?.debit ?? 0,
            totalCredit = allTotals?.credit ?? 0,
            vouchers = vouchers.Select(v => new
            {
                journalVoucherId = v.JournalVoucherId,
                v.EntryDate,
                v.ProjectId,
                projectName = v.Project?.ProjectName,
                entries = v.JournalEntries.OrderBy(e => e.JournalEntryId).Select(e => new
                {
                    journalEntryId = e.JournalEntryId,
                    e.CategoryId,
                    categoryName = e.Category?.CategoryName,
                    e.AccountId,
                    accountName = e.Account?.AccountName,
                    description = e.Description,
                    instrumentType = e.InstrumentDetail.InstrumentType.ToString(),
                    referenceNumber = e.InstrumentDetail.ReferenceNumber,
                    instrumentDate = e.InstrumentDetail.InstrumentDate,
                    e.Debit,
                    e.Credit,
                    isFilterMatch = Matches(e, request)
                })
            })
        };
    }

    public async Task<object> GetReportInitAsync(int userId, bool isAdmin)
    {
        var projectIds = isAdmin
            ? await _context.Projects.AsNoTracking().Select(x => x.ProjectId).ToListAsync()
            : await _context.UserProjects.AsNoTracking().Where(x => x.UserId == userId)
                .Select(x => x.ProjectId).ToListAsync();
        var hidden = await HiddenCategoriesAsync(userId, isAdmin);
        var projects = await _context.Projects.AsNoTracking().Where(x => projectIds.Contains(x.ProjectId))
            .OrderBy(x => x.ProjectName).Select(x => new { id = x.ProjectId, name = x.ProjectName }).ToListAsync();
        var categories = await _context.Categories.AsNoTracking()
            .Where(x => projectIds.Contains(x.ProjectId) && !hidden.Contains(x.CategoryId) &&
                        x.Accounts.Any(a => a.IsEnabled && a.AccountGroupId == MonetaryAccountGroup))
            .OrderBy(x => x.CategoryName)
            .Select(x => new { id = x.CategoryId, name = x.CategoryName, projectId = x.ProjectId }).ToListAsync();
        var categoryIds = categories.Select(x => x.id).ToList();
        var accounts = await _context.Accounts.AsNoTracking()
            .Where(x => categoryIds.Contains(x.CategoryId) && x.IsEnabled && x.AccountGroupId == MonetaryAccountGroup)
            .OrderBy(x => x.AccountName)
            .Select(x => new
            {
                id = x.AccountId,
                name = x.AccountName,
                categoryId = x.CategoryId,
                projectId = x.Category!.ProjectId
            }).ToListAsync();
        return new { projects, categories, accounts, instrumentTypes = Enum.GetNames<PaymentInstrumentType>() };
    }

    private async Task ValidateAsync(PaymentReceiptSubmissionRequest request, int userId, bool isAdmin, JournalVoucher? voucher)
    {
        if (request.ProjectId <= 0) throw new InvalidOperationException("Project is required.");
        if (request.EntryDate == default) throw new InvalidOperationException("Entry date is required.");
        await EnsureProjectAccessAsync(request.ProjectId, userId, isAdmin);
        if (request.Entries.Count < 2) throw new InvalidOperationException("At least two entries are required.");
        var hidden = await HiddenCategoriesAsync(userId, isAdmin);
        if (request.Entries.Any(x => hidden.Contains(x.CategoryId)))
            throw new UnauthorizedAccessException("The request contains a category you cannot access.");
        var accountIds = request.Entries.Select(x => x.AccountId).Distinct().ToList();
        var accounts = await _context.Accounts.AsNoTracking().Where(x => accountIds.Contains(x.AccountId))
            .Select(x => new { x.AccountId, x.CategoryId, x.AccountGroupId, x.IsEnabled, ProjectId = x.Category!.ProjectId })
            .ToListAsync();
        if (accounts.Count != accountIds.Count || accounts.Any(x => !x.IsEnabled || x.AccountGroupId != MonetaryAccountGroup || x.ProjectId != request.ProjectId))
            throw new InvalidOperationException("Every row must use an enabled monetary account in the selected project.");
        foreach (var (row, index) in request.Entries.Select((x, i) => (x, i)))
        {
            if (!Enum.IsDefined(row.InstrumentType))
                throw new InvalidOperationException($"Entry {index + 1}: instrument type is invalid.");
            if (row.CategoryId <= 0 || !accounts.Any(x => x.AccountId == row.AccountId && x.CategoryId == row.CategoryId))
                throw new InvalidOperationException($"Entry {index + 1}: category and account do not match.");
            if (row.Debit < 0 || row.Credit < 0)
                throw new InvalidOperationException($"Entry {index + 1}: debit and credit cannot be negative.");
            if ((row.Debit > 0) == (row.Credit > 0))
                throw new InvalidOperationException($"Entry {index + 1}: enter either debit or credit, but not both.");
            NormalizeInstrument(row, index + 1);
        }
        if (Math.Abs(request.Entries.Sum(x => x.Debit) - request.Entries.Sum(x => x.Credit)) > 0.01m)
            throw new InvalidOperationException("Debit and credit totals must balance.");
        var mapped = request.Entries.Where(x => x.OriginalJournalEntryId.HasValue)
            .Select(x => x.OriginalJournalEntryId!.Value).ToList();
        if (mapped.Count != mapped.Distinct().Count())
            throw new InvalidOperationException("A posted entry may appear only once in the revision.");
        if (voucher == null && mapped.Count > 0)
            throw new InvalidOperationException("A new request cannot reference posted journal entries.");
        if (voucher != null && mapped.Any(x => voucher.JournalEntries.All(e => e.JournalEntryId != x)))
            throw new ApprovalConflictException("One or more rows no longer belong to this voucher.");
    }

    private async Task<List<InstrumentDetailChange>> ApplySnapshotEntriesAsync(
        JournalVoucher voucher,
        ApprovalVoucher approval,
        bool revision)
    {
        var existing = voucher.JournalEntries.ToDictionary(x => x.JournalEntryId);
        var retained = new HashSet<int>();
        var detailChanges = new List<InstrumentDetailChange>();
        foreach (var row in approval.Entries.OrderBy(x => x.ApprovalVoucherEntryId))
        {
            JournalEntry entry;
            var changed = false;
            var isNew = false;
            if (row.OriginalJournalEntryId.HasValue && existing.TryGetValue(row.OriginalJournalEntryId.Value, out var mapped))
            {
                entry = mapped;
                retained.Add(entry.JournalEntryId);
                await _context.Entry(entry).Reference(x => x.InstrumentDetail).LoadAsync();
                changed = entry.AccountId != row.AccountId || entry.CategoryId != row.CategoryId ||
                          entry.Description != row.Description || entry.Debit != row.Debit || entry.Credit != row.Credit ||
                          entry.InstrumentDetail?.InstrumentType != row.InstrumentType ||
                          entry.InstrumentDetail?.ReferenceNumber != row.ReferenceNumber ||
                          entry.InstrumentDetail?.InstrumentDate != row.InstrumentDate;
            }
            else
            {
                entry = new JournalEntry { JournalVoucherId = voucher.JournalVoucherId };
                isNew = true;
                _context.JournalEntries.Add(entry);
                changed = revision;
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
            if (!revision)
            {
                entry.IsEdited = false;
                entry.ModifiedDateTime = null;
            }
            else if (isNew || changed)
            {
                entry.IsEdited = true;
                entry.ModifiedDateTime = DateOnly.FromDateTime(DateTime.UtcNow);
            }
            detailChanges.Add(new InstrumentDetailChange(entry, entry.InstrumentDetail, row));
        }
        foreach (var removed in existing.Values.Where(x => !retained.Contains(x.JournalEntryId)))
            _context.JournalEntries.Remove(removed);
        return detailChanges;
    }

    private void ApplySnapshotInstrumentDetails(IEnumerable<InstrumentDetailChange> changes)
    {
        foreach (var change in changes)
        {
            var detail = change.Detail;
            if (detail == null)
            {
                detail = new JournalEntryInstrumentDetail { JournalEntryId = change.Entry.JournalEntryId };
                _context.JournalEntryInstrumentDetails.Add(detail);
                change.Entry.InstrumentDetail = detail;
            }
            detail.InstrumentType = change.Row.InstrumentType!.Value;
            detail.ReferenceNumber = change.Row.ReferenceNumber;
            detail.NormalizedReferenceNumber = change.Row.NormalizedReferenceNumber;
            detail.InstrumentDate = change.Row.InstrumentDate;
        }
    }

    private sealed record InstrumentDetailChange(
        JournalEntry Entry,
        JournalEntryInstrumentDetail? Detail,
        ApprovalVoucherEntry Row);

    private static void ReplaceSnapshot(ApprovalVoucher approval, IReadOnlyList<PaymentReceiptLineRequest> rows)
    {
        approval.Entries = rows.Select((x, index) => new ApprovalVoucherEntry
        {
            ApprovalVoucherId = approval.ApprovalVoucherId,
            OriginalJournalEntryId = x.OriginalJournalEntryId,
            LineNumber = null,
            CategoryId = x.CategoryId,
            AccountId = x.AccountId,
            Description = x.Description,
            InstrumentType = x.InstrumentType,
            ReferenceNumber = x.InstrumentType == PaymentInstrumentType.Cash ? null : x.ReferenceNumber!.Trim(),
            NormalizedReferenceNumber = x.InstrumentType == PaymentInstrumentType.Cash ? null : Normalize(x.ReferenceNumber),
            InstrumentDate = x.InstrumentType == PaymentInstrumentType.Cash ? null : x.InstrumentDate,
            Debit = x.Debit,
            Credit = x.Credit
        }).ToList();
    }

    private static void NormalizeInstrument(PaymentReceiptLineRequest row, int entryNumber)
    {
        if (row.InstrumentType == PaymentInstrumentType.Cash)
        {
            row.ReferenceNumber = null;
            row.InstrumentDate = null;
            return;
        }
        row.ReferenceNumber = row.ReferenceNumber?.Trim();
        if (string.IsNullOrWhiteSpace(row.ReferenceNumber))
            throw new InvalidOperationException($"Entry {entryNumber}: reference or cheque number is required.");
        if (!row.InstrumentDate.HasValue)
            throw new InvalidOperationException($"Entry {entryNumber}: instrument date is required.");
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static byte[] NewVersion() => RandomNumberGenerator.GetBytes(16);

    private static void EnsureSnapshot(ApprovalVoucher approval, Guid? supplied)
    {
        if (!supplied.HasValue || approval.SnapshotVersion != supplied)
            throw new ApprovalConflictException("The approval draft changed after it was reviewed. Reload and try again.");
    }

    private async Task<ApprovalVoucher> LockApprovalAsync(int id)
    {
        var approval = _context.Database.ProviderName?.StartsWith("Npgsql", StringComparison.Ordinal) == true
            ? await _context.ApprovalVouchers
                .FromSqlInterpolated($"SELECT * FROM \"ApprovalVouchers\" WHERE \"ApprovalVoucherId\" = {id} FOR UPDATE")
                .SingleOrDefaultAsync()
            : await _context.ApprovalVouchers.SingleOrDefaultAsync(x => x.ApprovalVoucherId == id);
        if (approval == null)
            throw new InvalidOperationException("Approval request was not found.");
        if (approval.Status != ApprovalStatuses.Pending)
            throw new ApprovalConflictException("This approval request has already been decided.");
        await _context.Entry(approval).Collection(x => x.Entries).LoadAsync();
        return approval;
    }

    private async Task<JournalVoucher> LockVoucherAsync(int id)
    {
        var voucher = _context.Database.ProviderName?.StartsWith("Npgsql", StringComparison.Ordinal) == true
            ? await _context.JournalVouchers
                .FromSqlInterpolated($"SELECT * FROM \"JournalVouchers\" WHERE \"JournalVoucherId\" = {id} FOR UPDATE")
                .SingleOrDefaultAsync()
            : await _context.JournalVouchers.SingleOrDefaultAsync(x => x.JournalVoucherId == id);
        if (voucher == null)
            throw new InvalidOperationException("Voucher was not found.");
        await _context.Entry(voucher).Collection(x => x.JournalEntries).LoadAsync();
        return voucher;
    }

    private async Task EnsureProjectAccessAsync(int projectId, int userId, bool isAdmin)
    {
        if (!await _context.Projects.AsNoTracking().AnyAsync(x => x.ProjectId == projectId))
            throw new InvalidOperationException("Project was not found.");
        if (!isAdmin && !await _context.UserProjects.AsNoTracking().AnyAsync(x => x.UserId == userId && x.ProjectId == projectId))
            throw new UnauthorizedAccessException("You do not have access to this project.");
    }

    private Task<List<int>> HiddenCategoriesAsync(int userId, bool isAdmin) => isAdmin
        ? Task.FromResult(new List<int>())
        : _context.UserCategories.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.CategoryId).ToListAsync();

    private async Task EnsureApprovalAccessAsync(ApprovalVoucher approval, int userId, bool isAdmin)
    {
        await EnsureProjectAccessAsync(approval.ProjectId, userId, isAdmin);
        var hidden = await HiddenCategoriesAsync(userId, isAdmin);
        if (approval.Entries.Any(x => hidden.Contains(x.CategoryId)))
            throw new UnauthorizedAccessException("This approval contains a category you cannot access.");
    }

    private static PaymentReceiptSubmissionRequest ToRequest(ApprovalVoucher approval) => new()
    {
        ProjectId = approval.ProjectId,
        EntryDate = approval.RequestedEntryDate ?? default,
        Entries = approval.Entries.OrderBy(x => x.ApprovalVoucherEntryId).Select(ToLineRequest).ToList()
    };

    private static PaymentReceiptLineRequest ToLineRequest(ApprovalVoucherEntry x) => new()
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
    };

    // Approval snapshots are immutable accounting inputs. Reject malformed persisted
    // snapshots instead of applying client-side defaults that could silently heal data.
    private static void ValidateApprovalSnapshot(ApprovalVoucher approval)
    {
        if (!approval.RequestedEntryDate.HasValue || !approval.SnapshotVersion.HasValue)
            throw new PaymentReceiptIntegrityException("The Payment & Receipt approval header is incomplete.");
        var rows = approval.Entries.OrderBy(x => x.ApprovalVoucherEntryId).ToList();
        if (rows.Count < 2 || rows.Any(x => x.LineNumber.HasValue))
            throw new PaymentReceiptIntegrityException("The Payment & Receipt approval snapshot has invalid row metadata.");
        foreach (var (row, index) in rows.Select((value, rowIndex) => (value, rowIndex)))
        {
            if (!row.InstrumentType.HasValue || !Enum.IsDefined(row.InstrumentType.Value))
                throw new PaymentReceiptIntegrityException($"Approval row {index + 1} has an invalid instrument type.");
            if (row.InstrumentType == PaymentInstrumentType.Cash)
            {
                if (row.ReferenceNumber != null || row.NormalizedReferenceNumber != null || row.InstrumentDate.HasValue)
                    throw new PaymentReceiptIntegrityException($"Approval row {index + 1} has invalid Cash instrument data.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(row.ReferenceNumber) ||
                row.ReferenceNumber != row.ReferenceNumber.Trim() ||
                row.NormalizedReferenceNumber != Normalize(row.ReferenceNumber) ||
                !row.InstrumentDate.HasValue)
                throw new PaymentReceiptIntegrityException($"Approval row {index + 1} has incomplete instrument data.");
        }
    }

    private static object SubmissionResponse(ApprovalVoucher approval, IReadOnlyList<PaymentReceiptDuplicateWarning> warnings) => new
    {
        status = "PendingApproval",
        approvalVoucherId = approval.ApprovalVoucherId,
        journalVoucherId = approval.JournalVoucherId,
        snapshotVersion = approval.SnapshotVersion,
        duplicateWarnings = warnings,
        shouldPrint = false
    };

    private static bool Matches(JournalEntry entry, PaymentReceiptReportRequest request)
    {
        var hasLineFilter = request.CategoryId.HasValue || request.AccountId.HasValue ||
                            request.InstrumentType.HasValue || !string.IsNullOrWhiteSpace(request.ReferenceNumber) ||
                            !string.IsNullOrWhiteSpace(request.Search);
        if (!hasLineFilter) return false;

        var detail = entry.InstrumentDetail!;
        if (request.CategoryId.HasValue && entry.CategoryId != request.CategoryId) return false;
        if (request.AccountId.HasValue && entry.AccountId != request.AccountId) return false;
        if (request.InstrumentType.HasValue && detail.InstrumentType != request.InstrumentType) return false;
        if (!string.IsNullOrWhiteSpace(request.ReferenceNumber) &&
            !(detail.NormalizedReferenceNumber?.Contains(Normalize(request.ReferenceNumber)!) ?? false)) return false;
        if (string.IsNullOrWhiteSpace(request.Search)) return true;
        var search = request.Search.Trim();
        var numeric = search.StartsWith("JV-", StringComparison.OrdinalIgnoreCase) ? search[3..] : search;
        if (int.TryParse(numeric, out var jvId) && entry.JournalVoucherId == jvId) return true;
        return (entry.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
               (entry.Account?.AccountName.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
               detail.InstrumentType.ToString().Replace(" ", string.Empty)
                   .Contains(search.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase) ||
               (detail.ReferenceNumber?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
