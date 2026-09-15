using PureLedger.ViewModels.Approval;
using PureLedger.ViewModels.Home;

namespace PureLedger.Services.Interface;

public interface IApprovalGuardService
{
    Task<bool> IsVoucherPendingAsync(int journalVoucherId);
    Task EnsureVoucherNotPendingAsync(int journalVoucherId, bool lockVoucher = false);
}

public interface IApprovalService
{
    Task<bool> ContainsExpenseAsync(IEnumerable<JournalEntryRowModel> entries);
    Task<ApprovalSubmissionResult?> SubmitManualExpenseAsync(
        JournalEntryViewModel model, int? editingVoucherId, int userId);
    Task<object> GetPendingAsync(int userId, bool isAdmin);
    Task<object> GetHistoryAsync(int userId, bool isAdmin);
    Task<object?> GetAsync(int approvalVoucherId, int userId, bool isAdmin);
    Task<object> EditAsync(int approvalVoucherId, ApprovalEditRequest request, int userId, bool isAdmin);
    Task<object> ApproveAsync(int approvalVoucherId, Guid? snapshotVersion, int userId, bool isAdmin);
    Task<object> RejectAsync(int approvalVoucherId, string reason, Guid? snapshotVersion, int userId, bool isAdmin);
}

public sealed class ApprovalConflictException : InvalidOperationException
{
    public ApprovalConflictException(string message) : base(message) { }
}
