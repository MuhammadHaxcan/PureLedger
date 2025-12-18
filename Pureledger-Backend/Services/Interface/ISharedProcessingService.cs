using PureLedger.Models;

namespace PureLedger.Services.Interface
{
    public interface ISharedProcessingService
    {
        #region Entry Processing Status

        Task<bool> DetermineEntryProcessedStatusAsync(JournalEntry entry);
        Task<bool> IsEligibleForDCAsync(JournalEntry entry);

        #endregion

        #region Voucher Completion Recalculation

        Task<bool> RecalculateVoucherCompletionAsync(int journalVoucherId);

        #endregion

        #region Reference Voucher Management

        Task CompleteReferenceVouchersAsync(int journalVoucherId);
        Task ReopenReferenceVouchersAsync(int journalVoucherId);
        Task<JournalVoucherReference> CreateVoucherReferenceAsync(
            int mainVoucherId,
            int referenceVoucherId,
            string referenceType);
        Task<bool> DeleteReferenceVoucherIfEmptyAsync(int journalVoucherId);

        #endregion

        #region DC Entry Linkage Operations

        Task<DeliveryChallanEntry?> GetLinkedDCEntryAsync(int journalEntryId);
        Task DeleteLinkedDCEntryAsync(int journalEntryId);
        Task<(bool CanModify, bool HasLinkedDC, string? Warning)> ValidateEntryModificationAsync(int entryId);

        #endregion

        #region Voucher Cleanup On All Entries Deleted

        Task HandleAllEntriesDeletedAsync(int journalVoucherId);

        #endregion
    }
}
