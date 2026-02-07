using PureLedger.Models;
using PureLedger.Repositories.Interfaces;
using PureLedger.Services.Interface;

namespace PureLedger.Services
{
    public class SharedProcessingService : ISharedProcessingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<SharedProcessingService> _logger;

        private const string REFERENCE_TYPE_FOR_CHARGES = "ForCharges";
        private const string REFERENCE_TYPE_CARTAGE = "Cartage";
        private const string REFERENCE_TYPE_DERIVED_VOUCHER = "DerivedVoucher";

        private const int ACCOUNT_GROUP_ACCOUNT = 1;
        private const int ACCOUNT_GROUP_PRODUCT = 2;

        public SharedProcessingService(
            IUnitOfWork unitOfWork,
            ILogger<SharedProcessingService> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Entry Processing Status

        public async Task<bool> DetermineEntryProcessedStatusAsync(JournalEntry entry)
        {
            var accountGroupId = await GetAccountGroupIdAsync(entry.AccountId);

            if (accountGroupId == null)
            {
                _logger.LogWarning("Entry {EntryId} has invalid AccountId {AccountId}, defaulting to processed",
                    entry.JournalEntryId, entry.AccountId);
                return true;
            }

            if (accountGroupId == ACCOUNT_GROUP_ACCOUNT)
                return true;

            if (accountGroupId == ACCOUNT_GROUP_PRODUCT)
                return entry.Quantity <= 0;

            _logger.LogWarning("Entry {EntryId} has unknown AccountGroupId {AccountGroupId}, defaulting to processed",
                entry.JournalEntryId, accountGroupId);
            return true;
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

            if (!entries.Any())
            {
                _logger.LogWarning("Voucher {VoucherId} has no entries - checking if reference voucher", journalVoucherId);

                bool wasReferenceVoucher = await DeleteReferenceVoucherIfEmptyAsync(journalVoucherId);

                if (wasReferenceVoucher)
                    return true;

                await HandleAllEntriesDeletedAsync(journalVoucherId);
                await _unitOfWork.JournalVouchers.DeleteAsync(journalVoucherId);

                return true;
            }

            var newState = entries.All(e => e.IsProcessed);
            voucher.IsCompleted = newState;

            if (previousState != newState)
            {
                _logger.LogInformation("Voucher {VoucherId} completion state changed: {PreviousState} → {NewState}",
                    journalVoucherId, previousState, newState);

                if (newState)
                    await CompleteReferenceVouchersAsync(journalVoucherId);
                else
                    await ReopenReferenceVouchersAsync(journalVoucherId);
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

        public async Task<JournalVoucherReference> CreateVoucherReferenceAsync(
            int mainVoucherId,
            int referenceVoucherId,
            string referenceType)
        {
            var validTypes = new[] { REFERENCE_TYPE_FOR_CHARGES, REFERENCE_TYPE_CARTAGE, REFERENCE_TYPE_DERIVED_VOUCHER };
            if (!validTypes.Contains(referenceType))
                throw new ArgumentException($"Invalid reference type: {referenceType}. Must be one of: {string.Join(", ", validTypes)}");

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

        public async Task<bool> DeleteReferenceVoucherIfEmptyAsync(int journalVoucherId)
        {
            var references = await _unitOfWork.JournalVoucherReferences.GetByReferencedVoucherIdAsync(journalVoucherId);

            var refVoucherReferences = references
                .Where(r => r.ReferenceType == REFERENCE_TYPE_FOR_CHARGES ||
                            r.ReferenceType == REFERENCE_TYPE_CARTAGE ||
                            r.ReferenceType == REFERENCE_TYPE_DERIVED_VOUCHER)
                .ToList();

            if (!refVoucherReferences.Any())
                return false;

            _logger.LogInformation("Reference voucher {VoucherId} is empty - deleting voucher and reference records", journalVoucherId);

            // Clean up any DeliveryChallanVoucher referencing this JV before deleting (FK is NoAction)
            var dcVoucher = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(journalVoucherId);
            if (dcVoucher != null)
            {
                var dcEntries = await _unitOfWork.DeliveryChallanEntries.GetByDeliveryChallanIdAsync(dcVoucher.DeliveryChallanVoucherId);
                foreach (var dcEntry in dcEntries)
                {
                    await _unitOfWork.DeliveryChallanEntries.DeleteAsync(dcEntry.DeliveryChallanEntryId);
                    _logger.LogDebug("Deleted DC entry: {DCEntryId}", dcEntry.DeliveryChallanEntryId);
                }
                await _unitOfWork.DeliveryChallanVouchers.DeleteAsync(dcVoucher.DeliveryChallanVoucherId);
                _logger.LogInformation("Deleted DC voucher: {DCVoucherId} for reference voucher: {VoucherId}", dcVoucher.DeliveryChallanVoucherId, journalVoucherId);
            }

            foreach (var reference in refVoucherReferences)
            {
                await _unitOfWork.JournalVoucherReferences.DeleteAsync(reference.JournalVoucherReferenceId);
                _logger.LogInformation("Deleted reference record {ReferenceId} (Type: {ReferenceType}, MainVoucher: {MainVoucherId})",
                    reference.JournalVoucherReferenceId, reference.ReferenceType, reference.MainVoucherId);
            }

            await _unitOfWork.JournalVouchers.DeleteAsync(journalVoucherId);
            _logger.LogInformation("Deleted reference voucher {VoucherId} (Types: {ReferenceTypes})",
                journalVoucherId, string.Join(", ", refVoucherReferences.Select(r => r.ReferenceType).Distinct()));

            return true;
        }

        #endregion

        #region DC Entry Linkage Operations

        public async Task DeleteLinkedDCEntryAsync(int journalEntryId)
        {
            var dcEntry = await _unitOfWork.DeliveryChallanEntries.GetByJournalEntryIdAsync(journalEntryId);

            if (dcEntry == null)
            {
                _logger.LogDebug("No DC entry linked to JV entry: {JournalEntryId}", journalEntryId);
                return;
            }

            await _unitOfWork.DeliveryChallanEntries.DeleteAsync(dcEntry.DeliveryChallanEntryId);

            _logger.LogInformation("Deleted DC entry {DCEntryId} linked to JV entry: {JournalEntryId}",
                dcEntry.DeliveryChallanEntryId, journalEntryId);
        }

        #endregion

        #region Voucher Cleanup On All Entries Deleted

        public async Task HandleAllEntriesDeletedAsync(int journalVoucherId)
        {
            _logger.LogInformation("All entries deleted from voucher: {VoucherId} - starting cleanup", journalVoucherId);

            var dcVoucher = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(journalVoucherId);

            if (dcVoucher != null)
            {
                var dcEntries = await _unitOfWork.DeliveryChallanEntries.GetByDeliveryChallanIdAsync(dcVoucher.DeliveryChallanVoucherId);
                foreach (var dcEntry in dcEntries)
                {
                    await _unitOfWork.DeliveryChallanEntries.DeleteAsync(dcEntry.DeliveryChallanEntryId);
                    _logger.LogDebug("Deleted DC entry: {DCEntryId}", dcEntry.DeliveryChallanEntryId);
                }

                await _unitOfWork.DeliveryChallanVouchers.DeleteAsync(dcVoucher.DeliveryChallanVoucherId);
                _logger.LogInformation("Deleted DC voucher: {DCVoucherId}", dcVoucher.DeliveryChallanVoucherId);
            }

            await DeleteOrUnlinkReferenceVouchersAsync(journalVoucherId);

            _logger.LogInformation("Cleanup completed for voucher: {VoucherId}", journalVoucherId);
        }

        private async Task DeleteOrUnlinkReferenceVouchersAsync(int journalVoucherId)
        {
            var allReferences = await _unitOfWork.JournalVoucherReferences.GetByMainVoucherIdAsync(journalVoucherId);

            foreach (var reference in allReferences)
            {
                if (reference.ReferenceType == REFERENCE_TYPE_DERIVED_VOUCHER)
                {
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
                    var refVoucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(reference.ReferencedVoucherId);

                    if (refVoucher != null)
                    {
                        var refEntries = refVoucher.JournalEntries?.ToList() ?? new List<JournalEntry>();
                        foreach (var entry in refEntries)
                        {
                            await _unitOfWork.JournalEntries.DeleteAsync(entry.JournalEntryId);
                        }

                        await _unitOfWork.JournalVouchers.DeleteAsync(refVoucher.JournalVoucherId);
                        _logger.LogInformation("Deleted reference voucher: {VoucherId} (Type: {ReferenceType})",
                            refVoucher.JournalVoucherId, reference.ReferenceType);
                    }

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

        #region Private Helpers

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
