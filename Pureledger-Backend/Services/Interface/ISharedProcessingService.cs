using PureLedger.Models;

namespace PureLedger.Services.Interface
{
    public interface ISharedProcessingService
    {
        /// <summary>
        /// Determines the processed status of an entry based on account group and quantity.
        /// Account entries are always processed. Product entries are processed only if quantity <= 0.
        /// </summary>
        Task<bool> DetermineEntryProcessedStatusAsync(JournalEntry entry);

        /// <summary>
        /// Recalculates voucher completion status based on all entries' processed status.
        /// Handles empty voucher edge case and triggers reference voucher cascading on state changes.
        /// Only affects ForCharges and Cartage references, not DerivedVoucher.
        /// Note: Caller is responsible for calling SaveChangesAsync.
        /// </summary>
        Task<bool> RecalculateVoucherCompletionAsync(int journalVoucherId);

        /// <summary>
        /// Completes ForCharges and Cartage reference vouchers when main voucher becomes completed.
        /// Does not affect DerivedVoucher references.
        /// Note: Caller is responsible for calling SaveChangesAsync.
        /// </summary>
        Task CompleteReferenceVouchersAsync(int journalVoucherId);

        /// <summary>
        /// Reopens ForCharges and Cartage reference vouchers when main voucher becomes incomplete.
        /// Does not affect DerivedVoucher references.
        /// Note: Caller is responsible for calling SaveChangesAsync.
        /// </summary>
        Task ReopenReferenceVouchersAsync(int journalVoucherId);

        /// <summary>
        /// Creates a journal voucher reference linking main voucher to a reference voucher.
        /// Validates reference type must be "ForCharges", "Cartage", or "DerivedVoucher".
        /// Note: Caller is responsible for calling SaveChangesAsync.
        /// </summary>
        Task<JournalVoucherReference> CreateJournalVoucherReferenceAsync(
            int mainVoucherId,
            int referenceVoucherId,
            string referenceType);

        /// <summary>
        /// Gets the delivery challan entry linked to a journal voucher entry.
        /// </summary>
        Task<DeliveryChallanEntry?> GetLinkedDCEntryAsync(int journalEntryId);

        /// <summary>
        /// Deletes the delivery challan entry linked to a journal voucher entry.
        /// Called when entry is deleted or transitions to non-DC-eligible state.
        /// Note: Caller is responsible for calling SaveChangesAsync.
        /// </summary>
        Task DeleteLinkedDCEntryAsync(int journalEntryId);

        /// <summary>
        /// Handles cleanup when all entries are deleted from a voucher.
        /// Deletes DC voucher, DC entries, and ForCharges/Cartage reference vouchers.
        /// For DerivedVoucher references, only deletes the reference record, not the derived voucher.
        /// Note: Caller is responsible for calling SaveChangesAsync.
        /// </summary>
        Task HandleAllEntriesDeletedAsync(int journalVoucherId);

        /// <summary>
        /// Checks if a voucher is a reference voucher (ForCharges or Cartage) and deletes it along with its reference records.
        /// Called when the last entry of a reference voucher is deleted.
        /// Returns true if the voucher was a reference voucher and was deleted, false otherwise.
        /// Note: Caller is responsible for calling SaveChangesAsync.
        /// </summary>
        Task<bool> DeleteReferenceVoucherIfEmptyAsync(int journalVoucherId);

        /// <summary>
        /// Validates if an entry can be modified and returns information about linked DC entries.
        /// </summary>
        Task<(bool CanModify, bool HasLinkedDC, string? Warning)> ValidateEntryCanBeModifiedAsync(int entryId);

        /// <summary>
        /// Checks if a journal entry is eligible for delivery challan processing.
        /// Entry must be Product account group, have quantity > 0, and not be processed.
        /// </summary>
        Task<bool> IsEligibleForDCAsync(JournalEntry entry);
    }
}
