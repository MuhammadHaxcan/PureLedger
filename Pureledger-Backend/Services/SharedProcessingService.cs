using PureLedger.Models;
using PureLedger.Repositories.Interfaces;
using PureLedger.Services.Interface;

namespace PureLedger.Services
{
    /// <summary>
    /// Shared processing service for voucher completion, entry status, and reference management.
    /// IMPORTANT: This service does NOT call SaveChangesAsync. Caller is responsible for saving changes.
    /// </summary>
    public class SharedProcessingService : ISharedProcessingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<SharedProcessingService> _logger;

        // Reference type constants
        private const string REFERENCE_TYPE_FOR_CHARGES = "ForCharges";
        private const string REFERENCE_TYPE_CARTAGE = "Cartage";
        private const string REFERENCE_TYPE_DERIVED_VOUCHER = "DerivedVoucher";

        // Account group constants
        private const int ACCOUNT_GROUP_ACCOUNT = 1;
        private const int ACCOUNT_GROUP_PRODUCT = 2;

        public SharedProcessingService(
            IUnitOfWork unitOfWork,
            ILogger<SharedProcessingService> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Entry Processing State Determination

        public async Task<bool> DetermineEntryProcessedStatusAsync(JournalEntry entry)
        {
            var accountGroupId = await GetAccountGroupIdAsync(entry.AccountId);

            if (accountGroupId == null)
            {
                _logger.LogWarning("Entry {EntryId} has invalid AccountId {AccountId}, defaulting to processed",
                    entry.JournalEntryId, entry.AccountId);
                return true;
            }

            // Rule 1: Account entries are always processed
            if (accountGroupId == ACCOUNT_GROUP_ACCOUNT)
            {
                return true;
            }

            // Rule 2: Product entries depend on quantity (processed if quantity <= 0)
            if (accountGroupId == ACCOUNT_GROUP_PRODUCT)
            {
                return entry.Quantity <= 0;
            }

            // Fallback for unknown account groups
            _logger.LogWarning("Entry {EntryId} has unknown AccountGroupId {AccountGroupId}, defaulting to processed",
                entry.JournalEntryId, accountGroupId);
            return true;
        }

        public async Task<bool> IsEligibleForDCAsync(JournalEntry entry)
        {
            var accountGroupId = await GetAccountGroupIdAsync(entry.AccountId);

            // Must be Product account group, have positive quantity, and not be processed
            return accountGroupId == ACCOUNT_GROUP_PRODUCT
                && entry.Quantity > 0
                && !entry.IsProcessed;
        }

        #endregion

        #region Voucher Completion Recalculation

        public async Task<bool> RecalculateVoucherCompletionAsync(int journalVoucherId)
        {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null)
            {
                _logger.LogWarning("Voucher {VoucherId} not found for completion recalculation", journalVoucherId);
                return false;
            }

            var entries = voucher.JournalEntries?.ToList() ?? new List<JournalEntry>();
            var previousState = voucher.IsCompleted;

            // Handle empty voucher edge case
            if (!entries.Any())
            {
                _logger.LogWarning("Voucher {VoucherId} has no entries - checking if reference voucher", journalVoucherId);

                // First check if this is a reference voucher (ForCharges or Cartage)
                // If yes, delete it and its reference records
                bool wasReferenceVoucher = await DeleteReferenceVoucherIfEmptyAsync(journalVoucherId);

                if (wasReferenceVoucher)
                {
                    // Reference voucher deleted - no further cleanup needed
                    return true;
                }

                // Not a reference voucher - handle as main voucher
                // Cleanup all related entities (DC voucher, reference vouchers)
                await HandleAllEntriesDeletedAsync(journalVoucherId);
                // Delete the journal voucher itself after cleanup
                await _unitOfWork.JournalVouchers.DeleteAsync(journalVoucherId);

                return true;
            }

            // Core rule: Completed only if ALL entries are processed
            var newState = entries.All(e => e.IsProcessed);

            voucher.IsCompleted = newState;

            // Handle state transitions
            if (previousState != newState)
            {
                _logger.LogInformation("Voucher {VoucherId} completion state changed: {PreviousState} → {NewState}",
                    journalVoucherId, previousState, newState);

                if (newState)
                {
                    // Voucher became completed - complete reference vouchers
                    await CompleteReferenceVouchersAsync(journalVoucherId);
                }
                else
                {
                    // Voucher became incomplete (reopened) - reopen reference vouchers
                    await ReopenReferenceVouchersAsync(journalVoucherId);
                }
            }

            await _unitOfWork.JournalVouchers.UpdateAsync(voucher);

            return newState;
        }

        #endregion

        #region Reference Voucher Management

        public async Task CompleteReferenceVouchersAsync(int journalVoucherId)
        {
            await UpdateReferenceVouchersStatusAsync(journalVoucherId, isCompleted: true);
        }

        public async Task ReopenReferenceVouchersAsync(int journalVoucherId)
        {
            await UpdateReferenceVouchersStatusAsync(journalVoucherId, isCompleted: false);
        }

        private async Task UpdateReferenceVouchersStatusAsync(int journalVoucherId, bool isCompleted)
        {
            var allRefs = await GetReferenceVouchersForCompletionAsync(journalVoucherId);

            foreach (var reference in allRefs)
            {
                var refVoucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(reference.ReferencedVoucherId);
                if (refVoucher == null)
                {
                    _logger.LogError("Reference voucher {VoucherId} not found", reference.ReferencedVoucherId);
                    continue;
                }

                refVoucher.IsCompleted = isCompleted;
                await _unitOfWork.JournalVouchers.UpdateAsync(refVoucher);

                var action = isCompleted ? "completed" : "reopened";
                _logger.LogInformation("Reference voucher {VoucherId} {Action} (Type: {ReferenceType})",
                    refVoucher.JournalVoucherId, action, reference.ReferenceType);
            }
        }

        public async Task<JournalVoucherReference> CreateJournalVoucherReferenceAsync(
            int mainVoucherId,
            int referenceVoucherId,
            string referenceType)
        {
            // Validate reference type
            var validTypes = new[] { REFERENCE_TYPE_FOR_CHARGES, REFERENCE_TYPE_CARTAGE, REFERENCE_TYPE_DERIVED_VOUCHER };
            if (!validTypes.Contains(referenceType))
            {
                throw new ArgumentException($"Invalid reference type: {referenceType}. Must be one of: {string.Join(", ", validTypes)}");
            }

            var reference = new JournalVoucherReference
            {
                MainVoucherId = mainVoucherId,
                ReferencedVoucherId = referenceVoucherId,
                ReferenceType = referenceType
            };

            var result = await _unitOfWork.JournalVoucherReferences.AddAsync(reference);

            _logger.LogInformation("Created reference: MainVoucher={MainVoucherId}, RefVoucher={ReferenceVoucherId}, Type={ReferenceType}",
                mainVoucherId, referenceVoucherId, referenceType);

            return result;
        }

        #endregion

        #region DC Entry Linkage Operations

        public async Task<DeliveryChallanEntry?> GetLinkedDCEntryAsync(int journalEntryId)
        {
            return await _unitOfWork.DeliveryChallanEntries.GetByJournalEntryIdAsync(journalEntryId);
        }

        public async Task DeleteLinkedDCEntryAsync(int journalEntryId)
        {
            var dcEntry = await GetLinkedDCEntryAsync(journalEntryId);

            if (dcEntry == null)
            {
                _logger.LogDebug("No DC entry linked to JV entry: {JournalEntryId}", journalEntryId);
                return;
            }

            await _unitOfWork.DeliveryChallanEntries.DeleteAsync(dcEntry.DeliveryChallanEntryId);

            _logger.LogInformation("Deleted DC entry {DCEntryId} linked to JV entry: {JournalEntryId}",
                dcEntry.DeliveryChallanEntryId, journalEntryId);
        }

        public async Task<(bool CanModify, bool HasLinkedDC, string? Warning)> ValidateEntryCanBeModifiedAsync(int entryId)
        {
            var entry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(entryId);
            if (entry == null)
            {
                return (false, false, "Entry not found");
            }

            var hasLinkedDC = await GetLinkedDCEntryAsync(entryId) != null;

            var warning = hasLinkedDC ? "Entry has linked DC which will be deleted" : null;

            return (true, hasLinkedDC, warning);
        }

        #endregion

        #region All Entries Deleted Cleanup

        /// <summary>
        /// Handles cleanup when all entries are deleted from a voucher.
        /// - Deletes DC voucher and DC entries
        /// - Deletes ForCharges and Cartage reference vouchers (and their references)
        /// - For DerivedVoucher references, ONLY deletes the reference record, NOT the derived voucher
        /// </summary>
        public async Task HandleAllEntriesDeletedAsync(int journalVoucherId)
        {
            _logger.LogInformation("All entries deleted from voucher: {VoucherId} - starting cleanup", journalVoucherId);

            // Step 1: Get and delete DC voucher
            var dcVoucher = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(journalVoucherId);

            if (dcVoucher != null)
            {
                // Delete all DC entries
                var dcEntries = await _unitOfWork.DeliveryChallanEntries.GetByDeliveryChallanIdAsync(dcVoucher.DeliveryChallanVoucherId);
                foreach (var dcEntry in dcEntries)
                {
                    await _unitOfWork.DeliveryChallanEntries.DeleteAsync(dcEntry.DeliveryChallanEntryId);
                    _logger.LogDebug("Deleted DC entry: {DCEntryId}", dcEntry.DeliveryChallanEntryId);
                }

                // Delete DC voucher
                await _unitOfWork.DeliveryChallanVouchers.DeleteAsync(dcVoucher.DeliveryChallanVoucherId);
                _logger.LogInformation("Deleted DC voucher: {DCVoucherId}", dcVoucher.DeliveryChallanVoucherId);
            }

            // Step 2: Handle reference vouchers based on type
            await DeleteOrUnlinkReferenceVouchersAsync(journalVoucherId);

            _logger.LogInformation("Cleanup completed for voucher: {VoucherId}", journalVoucherId);
        }

        /// <summary>
        /// Handles reference vouchers when main voucher is deleted:
        /// - ForCharges and Cartage: Delete the reference voucher AND the reference record
        /// - DerivedVoucher: ONLY delete the reference record, keep the derived voucher intact
        /// </summary>
        private async Task DeleteOrUnlinkReferenceVouchersAsync(int journalVoucherId)
        {
            // Get all outgoing references from this voucher
            var allReferences = await _unitOfWork.JournalVoucherReferences.GetByMainVoucherIdAsync(journalVoucherId);

            foreach (var reference in allReferences)
            {
                if (reference.ReferenceType == REFERENCE_TYPE_DERIVED_VOUCHER)
                {
                    // For DerivedVoucher: Only delete the reference record, keep the voucher
                    await _unitOfWork.JournalVoucherReferences.DeleteAsync(reference.JournalVoucherReferenceId);
                    _logger.LogInformation(
                        "Unlinked DerivedVoucher reference {ReferenceId}: MainVoucher={MainVoucherId}, DerivedVoucher={DerivedVoucherId} (derived voucher preserved)",
                        reference.JournalVoucherReferenceId,
                        reference.MainVoucherId,
                        reference.ReferencedVoucherId);
                }
                else if (reference.ReferenceType == REFERENCE_TYPE_FOR_CHARGES ||
                         reference.ReferenceType == REFERENCE_TYPE_CARTAGE)
                {
                    // For ForCharges and Cartage: Delete the reference voucher AND the reference record
                    var refVoucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(reference.ReferencedVoucherId);

                    if (refVoucher != null)
                    {
                        // Delete entries of reference voucher
                        var refEntries = refVoucher.JournalEntries?.ToList() ?? new List<JournalEntry>();
                        foreach (var entry in refEntries)
                        {
                            await _unitOfWork.JournalEntries.DeleteAsync(entry.JournalEntryId);
                        }

                        // Delete reference voucher
                        await _unitOfWork.JournalVouchers.DeleteAsync(refVoucher.JournalVoucherId);
                        _logger.LogInformation("Deleted reference voucher: {VoucherId} (Type: {ReferenceType})",
                            refVoucher.JournalVoucherId, reference.ReferenceType);
                    }

                    // Delete reference record
                    await _unitOfWork.JournalVoucherReferences.DeleteAsync(reference.JournalVoucherReferenceId);
                }
                else
                {
                    _logger.LogWarning("Unknown reference type {ReferenceType} for reference {ReferenceId}, deleting reference only",
                        reference.ReferenceType, reference.JournalVoucherReferenceId);
                    await _unitOfWork.JournalVoucherReferences.DeleteAsync(reference.JournalVoucherReferenceId);
                }
            }

            if (allReferences.Any())
            {
                _logger.LogInformation("Processed {Count} reference vouchers for JV: {VoucherId}",
                    allReferences.Count, journalVoucherId);
            }
        }

        #endregion

        #region Reference Voucher Self-Deletion

        /// <summary>
        /// Checks if a voucher is a reference voucher (ForCharges or Cartage) and deletes it along with its reference record.
        /// Called when the last entry of a reference voucher is deleted.
        /// </summary>
        public async Task<bool> DeleteReferenceVoucherIfEmptyAsync(int journalVoucherId)
        {
            // Check if this voucher is referenced as ForCharges or Cartage
            var references = await _unitOfWork.JournalVoucherReferences.GetByReferencedVoucherIdAsync(journalVoucherId);

            // Filter only ForCharges and Cartage references (exclude DerivedVoucher)
            var refVoucherReferences = references
                .Where(r => r.ReferenceType == REFERENCE_TYPE_FOR_CHARGES || r.ReferenceType == REFERENCE_TYPE_CARTAGE)
                .ToList();

            if (!refVoucherReferences.Any())
            {
                // Not a reference voucher (ForCharges or Cartage)
                return false;
            }

            _logger.LogInformation("Reference voucher {VoucherId} is empty - deleting voucher and reference records", journalVoucherId);

            // Delete all reference records (there might be multiple if voucher is referenced by multiple main vouchers)
            foreach (var reference in refVoucherReferences)
            {
                await _unitOfWork.JournalVoucherReferences.DeleteAsync(reference.JournalVoucherReferenceId);
                _logger.LogInformation("Deleted reference record {ReferenceId} (Type: {ReferenceType}, MainVoucher: {MainVoucherId})",
                    reference.JournalVoucherReferenceId, reference.ReferenceType, reference.MainVoucherId);
            }

            // Delete the reference voucher itself
            await _unitOfWork.JournalVouchers.DeleteAsync(journalVoucherId);
            _logger.LogInformation("Deleted reference voucher {VoucherId} (Types: {ReferenceTypes})",
                journalVoucherId, string.Join(", ", refVoucherReferences.Select(r => r.ReferenceType).Distinct()));

            return true;
        }

        #endregion

        #region Helper Methods

        private async Task<int?> GetAccountGroupIdAsync(int accountId)
        {
            var account = await _unitOfWork.Accounts.GetByIdAsync(accountId);
            return account?.AccountGroupId;
        }

        private async Task<List<JournalVoucherReference>> GetReferenceVouchersForCompletionAsync(int journalVoucherId)
        {
            var forChargesRefs = await _unitOfWork.JournalVoucherReferences
                .GetByMainVoucherIdAndTypeAsync(journalVoucherId, REFERENCE_TYPE_FOR_CHARGES);
            var cartageRefs = await _unitOfWork.JournalVoucherReferences
                .GetByMainVoucherIdAndTypeAsync(journalVoucherId, REFERENCE_TYPE_CARTAGE);

            return forChargesRefs.Concat(cartageRefs).ToList();
        }

        #endregion
    }
}
