using PureLedger.Models;
using PureLedger.ViewModels.PaymentReceipt;

namespace PureLedger.Services.Interface;

public interface IPaymentReceiptService
{
    Task<object> GetInitAsync(int userId, bool isAdmin);
    Task<object> GetCategoriesAsync(int projectId, int userId, bool isAdmin);
    Task<object> GetAccountsAsync(int categoryId, int userId, bool isAdmin);
    Task<IReadOnlyList<PaymentReceiptDuplicateWarning>> FindDuplicateWarningsAsync(
        IReadOnlyList<PaymentReceiptLineRequest> entries, int? approvalVoucherId, int? journalVoucherId);
    Task<object> SubmitAsync(PaymentReceiptSubmissionRequest request, int userId, bool isAdmin);
    Task<object?> GetPostedAsync(int journalVoucherId, int userId, bool isAdmin);
    Task<object> SubmitRevisionAsync(int journalVoucherId, PaymentReceiptSubmissionRequest request, int userId, bool isAdmin);
    Task<object> EditPendingAsync(int approvalVoucherId, PaymentReceiptApprovalEditRequest request, int userId, bool isAdmin);
    Task<object> ApproveAsync(ApprovalVoucher approval, Guid? reviewedSnapshotVersion, int userId, bool isAdmin);
    Task<object> RejectAsync(ApprovalVoucher approval, string reason, Guid? reviewedSnapshotVersion, int userId, bool isAdmin);
    Task<object> GetReportAsync(PaymentReceiptReportRequest request, int userId, bool isAdmin);
    Task<object> GetReportInitAsync(int userId, bool isAdmin);
    Task EnsureVoucherAccessAsync(int journalVoucherId, int userId, bool isAdmin);
}

public interface IPaymentReceiptClassifier
{
    Task<bool> HasAnyInstrumentDetailsAsync(int journalVoucherId);
    Task<bool> IsStructurallyValidAsync(int journalVoucherId);
    Task<bool> IsApprovedAsync(int journalVoucherId);
    /// <summary>Set-based form of <see cref="IsStructurallyValidAsync"/>, for filtering and paging in the database.</summary>
    Task<IQueryable<JournalVoucher>> WhereStructurallyValidAsync(IQueryable<JournalVoucher> vouchers);
    /// <summary>Set-based form of <see cref="IsApprovedAsync"/>.</summary>
    Task<IQueryable<JournalVoucher>> WhereApprovedAsync(IQueryable<JournalVoucher> vouchers);
    Task EnsureNotPaymentReceiptAsync(int journalVoucherId, string operation);
    Task<VoucherType> ResolvePrintableTypeAsync(int journalVoucherId, VoucherType requestedType);
}

public sealed class PaymentReceiptIntegrityException : InvalidOperationException
{
    public PaymentReceiptIntegrityException(string message) : base(message) { }
}
