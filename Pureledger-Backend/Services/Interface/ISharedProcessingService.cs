using PureLedger.Models;

namespace PureLedger.Services.Interface
{
    public interface ISharedProcessingService
    {
        // Mutation methods participate in the caller's unit of work.
        // The owning service must call SaveChangesAsync after the workflow is complete.

        #region Entry Processing Status

        Task<bool> DetermineEntryProcessedStatusAsync(JournalEntry entry);

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

        Task DeleteLinkedDCEntryAsync(int journalEntryId);

        #endregion

        #region Voucher Cleanup On All Entries Deleted

        Task HandleAllEntriesDeletedAsync(int journalVoucherId);

        #endregion
    }
}
