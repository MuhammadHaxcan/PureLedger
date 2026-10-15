using PureLedger.Models;
using PureLedger.Helpers;
using PureLedger.Exceptions;
using PureLedger.Repositories.Interfaces;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.DeliveryChallan;

namespace PureLedger.Services
{
    public class DeliveryChallanService : IDeliveryChallanService
    {
        #region Fields and Constructor

        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DeliveryChallanService> _logger;
        private readonly ISharedProcessingService _sharedProcessingService;
        private readonly IApprovalGuardService _approvalGuard;
        private readonly IPaymentReceiptClassifier _paymentReceiptClassifier;

        private const int ACCOUNT_GROUP_ACCOUNT = 1;
        private const int ACCOUNT_GROUP_PRODUCT = 2;

        public DeliveryChallanService(
            IUnitOfWork unitOfWork,
            ILogger<DeliveryChallanService> logger,
            ISharedProcessingService sharedProcessingService,
            IApprovalGuardService approvalGuard,
            IPaymentReceiptClassifier paymentReceiptClassifier)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sharedProcessingService = sharedProcessingService ?? throw new ArgumentNullException(nameof(sharedProcessingService));
            _approvalGuard = approvalGuard;
            _paymentReceiptClassifier = paymentReceiptClassifier;
        }

        #endregion

        #region Simple Data Retrieval

        public Task<List<Project>> GetUserProjectsAsync(int userId) => _unitOfWork.Projects.GetAllForUserAsync(userId);

        public Task<List<Location>> GetLocationsAsync() => _unitOfWork.Locations.GetAllAsync();

        public async Task<DeliveryChallanVoucher?> GetDCByJournalVoucherIdAsync(int journalVoucherId)
        {
            try
            {
                return await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(journalVoucherId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve delivery challan for journal voucher {JournalVoucherId}", journalVoucherId);
                throw;
            }
        }

        public async Task<List<JournalEntryDetailViewModel>> GetVoucherEntriesAsync(int voucherId)
        {
            await _paymentReceiptClassifier.EnsureNotPaymentReceiptAsync(
                voucherId, "selected for Delivery Challan processing");
            var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(voucherId, null, null);
            return entries.Select(MapToDetailViewModel).ToList();
        }

        public async Task<List<JournalEntry>> GetUnprocessedEntriesAsync(int voucherId)
        {
            try
            {
                await _paymentReceiptClassifier.EnsureNotPaymentReceiptAsync(
                    voucherId, "selected for Delivery Challan processing");
                var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(voucherId, null, null);
                return entries.Where(e => !e.IsProcessed && e.Quantity > 0 &&
                                          e.Account?.AccountGroupId == ACCOUNT_GROUP_PRODUCT).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve unprocessed entries for voucher {VoucherId}", voucherId);
                throw;
            }
        }

        public async Task<IEnumerable<Account>> GetAccountsAsync(int categoryId) => await _unitOfWork.Accounts.GetByCategoryIdAsync(categoryId);

        public async Task<IEnumerable<Category>> GetCategoriesAsync(int projectId)
        {
            try
            {
                return await _unitOfWork.Categories.GetByProjectIdAsync(projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve categories for project {ProjectId}", projectId);
                throw;
            }
        }

        public async Task<IEnumerable<Category>> GetCategoriesForUserAsync(int projectId, int userId)
        {
            try
            {
                return await _unitOfWork.Categories.GetByProjectIdForUserAsync(projectId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve categories for project {ProjectId} and user {UserId}", projectId, userId);
                throw;
            }
        }

        public Task<ProjectCartageSettings?> GetCartageSettingsAsync(int projectId) => _unitOfWork.ProjectCartageSettings.GetByProjectIdAsync(projectId);

        #endregion

        #region Filtered Entries

        public async Task<DeliveryChallanFilterViewModel> GetFilteredEntriesAsync(
            DateOnly? sinceDate, DateOnly? toDate, int? projectId = null,
            int? categoryId = null, int? accountId = null, int? userId = null)
        {
            var result = new DeliveryChallanFilterViewModel
            {
                SinceDate = sinceDate,
                ToDate = toDate,
                ProjectId = projectId,
                CategoryId = categoryId,
                AccountId = accountId,
                Vouchers = new List<JournalVoucherViewModel>()
            };

            try
            {
                var hiddenCategoryIds = await GetHiddenCategoryIdsAsync(userId);
                var includeEntries = !accountId.HasValue && !categoryId.HasValue;
                var vouchers = await _unitOfWork.JournalVouchers.GetFilteredVouchersAsync(sinceDate, toDate, projectId, includeEntries);
                var unprocessedVouchers = vouchers.Where(v => !v.IsCompleted && !v.IsBaseVoucher).ToList();

                _logger.LogInformation("Found {UnprocessedCount} unprocessed non-base vouchers out of {TotalCount} total vouchers", unprocessedVouchers.Count, vouchers.Count);

                foreach (var voucher in unprocessedVouchers)
                {
                    if (await _approvalGuard.IsVoucherPendingAsync(voucher.JournalVoucherId))
                        continue;

                    var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(voucher.JournalVoucherId, categoryId, accountId);
                    var filteredEntries = entries.Where(e => !e.IsProcessed &&
                                                             e.Account?.AccountGroupId == ACCOUNT_GROUP_PRODUCT);

                    if (userId.HasValue && hiddenCategoryIds.Any())
                    {
                        filteredEntries = filteredEntries.Where(e => !hiddenCategoryIds.Contains(e.CategoryId));
                        if (entries.Any(e => hiddenCategoryIds.Contains(e.CategoryId))) continue;
                    }

                    var voucherViewModel = MapToVoucherViewModel(voucher);
                    voucherViewModel.Entries.AddRange(filteredEntries.Select(MapToDetailViewModel));

                    if (voucherViewModel.Entries.Any())
                        result.Vouchers.Add(voucherViewModel);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve filtered entries for delivery challan");
            }

            return result;
        }

        public async Task<DeliveryChallanFilterViewModel> GetPendingForChargesAsync(
            DateOnly? sinceDate, DateOnly? toDate, int? projectId = null,
            int? categoryId = null, int? accountId = null, int? userId = null)
        {
            var result = new DeliveryChallanFilterViewModel
            {
                SinceDate = sinceDate,
                ToDate = toDate,
                ProjectId = projectId,
                CategoryId = categoryId,
                AccountId = accountId,
                Vouchers = new List<JournalVoucherViewModel>()
            };

            try
            {
                var hiddenCategoryIds = await GetHiddenCategoryIdsAsync(userId);
                var includeEntries = !accountId.HasValue && !categoryId.HasValue;
                var vouchers = await _unitOfWork.JournalVouchers.GetFilteredVouchersAsync(sinceDate, toDate, projectId, includeEntries);
                var pendingForChargesVouchers = vouchers.Where(v => v.IsCompleted && v.PendingForCharges && !v.IsBaseVoucher).ToList();

                _logger.LogInformation("Found {PendingCount} pending FOR charges vouchers out of {TotalCount} total vouchers", pendingForChargesVouchers.Count, vouchers.Count);

                foreach (var voucher in pendingForChargesVouchers)
                {
                    if (await _approvalGuard.IsVoucherPendingAsync(voucher.JournalVoucherId))
                        continue;

                    var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(voucher.JournalVoucherId, categoryId, accountId);
                    var voucherViewModel = MapToVoucherViewModel(voucher);

                    var customerEntry = entries.FirstOrDefault(e => e.Account?.AccountGroupId == ACCOUNT_GROUP_ACCOUNT);
                    if (customerEntry != null)
                        voucherViewModel.CustomerName = customerEntry.Account?.AccountName ?? "";

                    var productEntries = entries.Where(e => e.Account?.AccountGroupId == ACCOUNT_GROUP_PRODUCT);

                    if (userId.HasValue && hiddenCategoryIds.Any())
                    {
                        productEntries = productEntries.Where(e => !hiddenCategoryIds.Contains(e.CategoryId));
                        if (entries.Any(e => hiddenCategoryIds.Contains(e.CategoryId))) continue;
                    }

                    voucherViewModel.Entries.AddRange(productEntries.Select(MapToDetailViewModel));

                    if (voucherViewModel.Entries.Any())
                        result.Vouchers.Add(voucherViewModel);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve pending FOR charges vouchers");
            }

            return result;
        }

        #endregion

        #region Delivery Challan Processing

        public async Task<DeliveryChallanEntry> CreateSingleDCAsync(
            int journalEntryId, DateOnly dcDate, string? otherReference,
            int locationId, string? remarks, bool includeForCharges, int forAmount, int? forAccountId = null, string? station = null)
        {
            var sourceEntry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(journalEntryId)
                ?? throw new InvalidOperationException("Journal entry was not found.");
            await _paymentReceiptClassifier.EnsureNotPaymentReceiptAsync(
                sourceEntry.JournalVoucherId, "processed into a Delivery Challan");
            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var journalEntry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(journalEntryId);
                if (journalEntry == null)
                    throw new ArgumentException($"Journal entry {journalEntryId} not found");
                await _approvalGuard.EnsureVoucherNotPendingAsync(journalEntry.JournalVoucherId, lockVoucher: true);
                await EnsureNotStockTransferAsync(journalEntry.JournalVoucherId);
                if (journalEntry.Account?.AccountGroupId != ACCOUNT_GROUP_PRODUCT)
                    throw new InvalidOperationException("Only Product entries can be processed through a Delivery Challan.");

                var deliveryChallan = await CreateDCVoucherInternalAsync(journalEntry.JournalVoucherId, dcDate, otherReference, station);
                var dcEntry = await CreateDCEntryInternalAsync(deliveryChallan.DeliveryChallanVoucherId, journalEntryId, locationId, remarks, otherReference, dcDate);

                if (includeForCharges && forAmount > 0)
                    _ = await ProcessForChargesInternalAsync(journalEntry.JournalVoucherId, journalEntryId, forAmount, dcDate, forAccountId);

                // Persist new DC/FOR rows before completion recalculation queries the voucher graph.
                await _unitOfWork.SaveChangesAsync();
                await RecalculateVoucherCompletionAsync(journalEntry.JournalVoucherId);
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Created delivery challan entry {DCEntryId} for journal entry {EntryId}", dcEntry.DeliveryChallanEntryId, journalEntryId);
                return dcEntry;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to create single delivery challan for entry {EntryId}", journalEntryId);
                throw;
            }
        }

        public async Task<(int deliveryChallanId, List<EntryProcessResult> results)> ProcessBatchDCAsync(
            int journalVoucherId, DateOnly dcDate, string? otherReference,
            int locationId, string? remarks, bool includeForCharges, int forAmount, int? forAccountId = null, string? station = null)
        {
            await _paymentReceiptClassifier.EnsureNotPaymentReceiptAsync(
                journalVoucherId, "processed into a Delivery Challan");
            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                await _approvalGuard.EnsureVoucherNotPendingAsync(journalVoucherId, lockVoucher: true);
                await EnsureNotStockTransferAsync(journalVoucherId);
                var unprocessedEntries = await GetUnprocessedEntriesAsync(journalVoucherId);
                if (!unprocessedEntries.Any())
                    throw new InvalidOperationException("No unprocessed entries found for this voucher");

                var deliveryChallan = await CreateDCVoucherInternalAsync(journalVoucherId, dcDate, otherReference, station);
                var results = new List<EntryProcessResult>();

                foreach (var entry in unprocessedEntries)
                {
                    var result = await ProcessSingleEntryInternalAsync(deliveryChallan.DeliveryChallanVoucherId, entry, locationId, remarks, otherReference, dcDate);
                    results.Add(result);
                }

                if (!results.Any(result => result.Success))
                    throw new InvalidOperationException("No entries were processed successfully; the delivery challan was not changed.");

                if (includeForCharges && forAmount > 0)
                    _ = await ProcessForChargesInternalAsync(journalVoucherId, null, forAmount, dcDate, forAccountId);

                // Persist batched DC/FOR rows before completion recalculation queries the voucher graph.
                await _unitOfWork.SaveChangesAsync();
                await RecalculateVoucherCompletionAsync(journalVoucherId);
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                var successCount = results.Count(r => r.Success);
                _logger.LogInformation("Batch processing completed - {SuccessCount}/{TotalCount} entries processed successfully", successCount, unprocessedEntries.Count);

                return (deliveryChallan.DeliveryChallanVoucherId, results);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to process batch for voucher {VoucherId}", journalVoucherId);
                throw;
            }
        }

        #endregion

        #region Cartage Processing

        public async Task<DeliveryChallanVoucher> ProcessCartageAsync(CartageViewModel model)
        {
            await _paymentReceiptClassifier.EnsureNotPaymentReceiptAsync(
                model.JournalVoucherId, "processed for cartage");
            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                await _approvalGuard.EnsureVoucherNotPendingAsync(model.JournalVoucherId, lockVoucher: true);
                await EnsureNotStockTransferAsync(model.JournalVoucherId);
                var deliveryChallan = await ProcessCartageInternalAsync(
                    model.JournalVoucherId, model.EntryId, model.TransporterId, model.Amount,
                    model.PostToCustomer, model.Reference, model.DcDate, model.DcRemarks,
                    model.DcStation, model.DcOtherRef, model.DcLocation);

                if (model.IncludeForCharges && model.ForAmount > 0)
                    _ = await ProcessForChargesInternalAsync(model.JournalVoucherId, model.EntryId, model.ForAmount, model.DcDate, model.ForAccountId);

                // Persist cartage/DC/FOR rows before completion recalculation queries the voucher graph.
                await _unitOfWork.SaveChangesAsync();
                await RecalculateVoucherCompletionAsync(model.JournalVoucherId);
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Processed cartage for voucher {VoucherId}, entry {EntryId}", model.JournalVoucherId, model.EntryId);
                return deliveryChallan;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to process cartage for voucher {VoucherId}, entry {EntryId}", model.JournalVoucherId, model.EntryId);
                throw;
            }
        }

        #endregion

        #region FOR Charges Processing

        public async Task<JournalVoucher> ProcessForChargesAsync(int journalVoucherId, int? entryId, int amount, DateOnly dcDate, int? forAccountId = null, string? otherRef = null)
        {
            await _paymentReceiptClassifier.EnsureNotPaymentReceiptAsync(
                journalVoucherId, "processed for FOR charges");
            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                await _approvalGuard.EnsureVoucherNotPendingAsync(journalVoucherId, lockVoucher: true);
                await EnsureNotStockTransferAsync(journalVoucherId);
                var forJV = await ProcessForChargesInternalAsync(journalVoucherId, entryId, amount, dcDate, forAccountId, otherRef);

                await _unitOfWork.SaveChangesAsync();

                var mainVoucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
                if (mainVoucher?.IsCompleted == true)
                {
                    await _sharedProcessingService.CompleteReferenceVouchersAsync(journalVoucherId);
                    await _unitOfWork.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                _logger.LogInformation("Processed FOR charges for journal voucher {JvId}", journalVoucherId);
                return forJV;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to process FOR charges for journal voucher {JvId}", journalVoucherId);
                throw;
            }
        }

        #endregion

        #region Cartage Overload

        public Task<DeliveryChallanVoucher> ProcessCartageAsync(
            int journalVoucherId, int entryId, int transporterId, int amount,
            bool postToCustomer, string? reference, DateOnly dcDate, string? dcRemarks,
            string? dcStation, string? dcOtherRef, int dcLocation)
        {
            var model = new CartageViewModel
            {
                JournalVoucherId = journalVoucherId,
                EntryId = entryId,
                TransporterId = transporterId,
                Amount = amount,
                PostToCustomer = postToCustomer,
                Reference = reference,
                DcDate = dcDate,
                DcRemarks = dcRemarks,
                DcStation = dcStation,
                DcOtherRef = dcOtherRef,
                DcLocation = dcLocation
            };
            return ProcessCartageAsync(model);
        }

        #endregion

        #region Private Helper Methods - DC Creation

        private async Task<DeliveryChallanVoucher> CreateDCVoucherInternalAsync(int journalVoucherId, DateOnly dcDate, string? reference, string? station)
        {
            try
            {
                var journalVoucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
                if (journalVoucher == null)
                    throw new ArgumentException($"Journal voucher with ID {journalVoucherId} not found");
                if (await _unitOfWork.IsStockTransferVoucherAsync(journalVoucherId))
                    throw new InvalidOperationException("Stock-transfer delivery challans are immutable here. Use the Stock Transfers screen for cancellation, Closed Short, or complete deletion.");

                var normalizedStation = NormalizeStation(station);
                var existingDC = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(journalVoucherId);
                if (existingDC != null)
                {
                    _logger.LogInformation("Delivery challan already exists for journal voucher {JournalVoucherId}", journalVoucherId);
                    // Missing/null fields from older clients preserve the shared value.
                    // An explicitly blank station clears it.
                    if (station != null) existingDC.Station = normalizedStation;
                    if (station != null || !string.IsNullOrEmpty(reference))
                        _unitOfWork.DeliveryChallanVouchers.Update(existingDC);
                    return existingDC;
                }

                var deliveryChallanVoucher = new DeliveryChallanVoucher
                {
                    JournalVoucherId = journalVoucherId,
                    DCDate = dcDate,
                    Station = normalizedStation,
                    IsCompleted = false
                };

                _unitOfWork.DeliveryChallanVouchers.Add(deliveryChallanVoucher);
                // Materialize the generated DC voucher ID before creating DC entry children.
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Created delivery challan for journal voucher {JournalVoucherId}", journalVoucherId);

                return deliveryChallanVoucher;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create delivery challan for journal voucher {JournalVoucherId}", journalVoucherId);
                throw;
            }
        }

        private static string? NormalizeStation(string? station)
        {
            if (station == null) return null;
            // Measure what is stored, so padding whitespace cannot cause a spurious rejection.
            var trimmed = station.Trim();
            if (trimmed.Length > DeliveryChallanVoucher.StationMaxLength)
                throw new ArgumentException($"Station must be {DeliveryChallanVoucher.StationMaxLength} characters or fewer.", nameof(station));
            return trimmed.Length == 0 ? null : trimmed;
        }

        private async Task<DeliveryChallanEntry> CreateDCEntryInternalAsync(int deliveryChallanId, int journalEntryId, int locationId, string? remarks, string? otherReference, DateOnly entryDate)
        {
            try
            {
                var (deliveryChallan, journalEntry, _, existingEntry) = await ValidateDCEntryDataAsync(deliveryChallanId, journalEntryId, locationId);

                if (existingEntry != null)
                {
                    existingEntry.Remarks = remarks;
                    existingEntry.OtherReference = otherReference;
                    existingEntry.LocationId = locationId;
                    existingEntry.EntryDate = entryDate;
                    _unitOfWork.DeliveryChallanEntries.Update(existingEntry);
                    // Self-heal rows left half-processed (DC row present, journal row still unprocessed), so
                    // re-processing completes them instead of silently leaving them off the challan/invoice.
                    if (!journalEntry.IsProcessed)
                    {
                        journalEntry.IsProcessed = true;
                        _unitOfWork.JournalEntries.Update(journalEntry);
                    }
                    _logger.LogInformation("Updated delivery challan entry {DCEntryId}", existingEntry.DeliveryChallanEntryId);
                    return existingEntry;
                }

                var deliveryChallanEntry = new DeliveryChallanEntry
                {
                    DeliveryChallanVoucherId = deliveryChallanId,
                    JournalEntryId = journalEntryId,
                    LocationId = locationId,
                    Remarks = remarks,
                    OtherReference = otherReference,
                    EntryDate = entryDate
                };

                var result = _unitOfWork.DeliveryChallanEntries.Add(deliveryChallanEntry);
                journalEntry.IsProcessed = true;
                _unitOfWork.JournalEntries.Update(journalEntry);

                _logger.LogInformation("Created delivery challan entry for journal entry {JournalEntryId}", journalEntryId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create delivery challan entry for DC {DeliveryChallanId}, entry {JournalEntryId}", deliveryChallanId, journalEntryId);
                throw;
            }
        }

        private async Task RecalculateVoucherCompletionAsync(int journalVoucherId) =>
            await _sharedProcessingService.RecalculateVoucherCompletionAsync(journalVoucherId);

        #endregion

        #region Private Helper Methods - Cartage

        private async Task<DeliveryChallanVoucher> ProcessCartageInternalAsync(
            int journalVoucherId, int entryId, int transporterId, int amount,
            bool postToCustomer, string? reference, DateOnly dcDate, string? dcRemarks,
            string? dcStation, string? dcOtherRef, int dcLocation)
        {
            try
            {
                var originalJV = await GetValidatedJournalVoucherAsync(journalVoucherId);
                var entry = await GetValidatedJournalEntryAsync(entryId);

                entry.IsProcessed = true;
                _unitOfWork.JournalEntries.Update(entry);

                var deliveryChallanVoucher = await CreateDCVoucherInternalAsync(journalVoucherId, dcDate, reference, dcStation);
                await CreateDCEntryInternalAsync(deliveryChallanVoucher.DeliveryChallanVoucherId, entry.JournalEntryId, dcLocation, dcRemarks, dcOtherRef, dcDate);

                int debitAccountId = await GetDebitAccountIdAsync(originalJV, entry, postToCustomer);
                var cartageVoucher = await CreateCartageJournalVoucherAsync(originalJV.ProjectId, debitAccountId, transporterId, entry, amount, dcOtherRef);
                await _sharedProcessingService.CreateVoucherReferenceAsync(originalJV.JournalVoucherId, cartageVoucher.JournalVoucherId, "Cartage");
                // Note: Cartage voucher IsCompleted stays false; CompleteReferenceVouchersAsync handles it when main voucher completes

                _logger.LogInformation("Processed cartage and delivery (internal) for journal voucher {JournalVoucherId}, entry {EntryId}", journalVoucherId, entryId);
                return deliveryChallanVoucher;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process cartage and delivery (internal) for journal voucher {JournalVoucherId}, entry {EntryId}", journalVoucherId, entryId);
                throw;
            }
        }

        private async Task<JournalVoucher> CreateCartageJournalVoucherAsync(int projectId, int debitAccountId, int transporterId, JournalEntry originalEntry, int amount, string? otherRef)
        {
            try
            {
                var (debitCategoryId, cartageCategoryId, debitAccountName, transporterAccountName) =
                    await GetValidatedCartageAccountsAsync(debitAccountId, transporterId, projectId);
                var cartageJV = await CreateBaseCartageVoucherAsync(projectId);

                var entries = new List<JournalEntry>
                {
                    CreateCartageDebitEntry(cartageJV.JournalVoucherId, debitAccountId, debitCategoryId, originalEntry, amount, otherRef, debitAccountName),
                    CreateCartageCreditEntry(cartageJV.JournalVoucherId, transporterId, cartageCategoryId, originalEntry, amount, otherRef, transporterAccountName)
                };

                foreach (var entry in entries)
                    _unitOfWork.JournalEntries.Add(entry);

                return cartageJV;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create cartage journal voucher for project {ProjectId}", projectId);
                throw;
            }
        }

        private async Task<JournalVoucher> CreateBaseCartageVoucherAsync(int projectId)
        {
            var cartageJV = new JournalVoucher
            {
                ProjectId = projectId,
                EntryDate = BusinessClock.Today,
                IsCompleted = false,
                CreditableDate = BusinessClock.Today
            };

            _unitOfWork.JournalVouchers.Add(cartageJV);
            // Materialize the generated cartage JV ID before adding entries and references.
            await _unitOfWork.SaveChangesAsync();
            return cartageJV;
        }

        private async Task<(int debitCategoryId, int cartageCategoryId, string debitAccountName, string transporterAccountName)>
            GetValidatedCartageAccountsAsync(int debitAccountId, int transporterId, int projectId)
        {
            var debitAccount = await _unitOfWork.Accounts.GetByIdAsync(debitAccountId);
            if (debitAccount == null)
                throw new InvalidOperationException($"Debit account {debitAccountId} not found for cartage entry");

            var projectSettings = await _unitOfWork.ProjectCartageSettings.GetByProjectIdAsync(projectId);
            if (projectSettings?.CartageTransporterCategoryId == null)
                throw new InvalidOperationException($"Cartage transporter category not configured for project {projectId}");

            var cartageCategoryId = projectSettings.CartageTransporterCategoryId.Value;
            var cartageCategory = await _unitOfWork.Categories.GetByIdWithDetailsAsync(cartageCategoryId);
            if (cartageCategory == null || cartageCategory.ProjectId != projectId)
                throw new InvalidOperationException(
                    $"Configured cartage transporter category {cartageCategoryId} does not belong to project {projectId}");

            var transporterAccount = await _unitOfWork.Accounts.GetByIdWithCategoryAsync(transporterId);
            if (transporterAccount == null ||
                !transporterAccount.IsEnabled ||
                transporterAccount.CategoryId != cartageCategoryId ||
                transporterAccount.Category?.ProjectId != projectId)
            {
                throw new InvalidOperationException(
                    $"Transporter account {transporterId} must be enabled and belong to the configured cartage category for project {projectId}");
            }

            return (
                debitAccount.CategoryId,
                cartageCategoryId,
                debitAccount.AccountName,
                transporterAccount.AccountName);
        }

        private static JournalEntry CreateCartageDebitEntry(int journalVoucherId, int accountId, int categoryId, JournalEntry originalEntry, int amount, string? otherRef, string accountName)
        {
            return new JournalEntry
            {
                JournalVoucherId = journalVoucherId,
                AccountId = accountId,
                CategoryId = categoryId,
                Description = BuildCartageDescription("Debit", originalEntry.JournalVoucherId, otherRef, accountName),
                Quantity = 0,
                Rate = amount,
                Debit = amount,
                Credit = 0,
                Discount = 0,
                DiscountPercent = 0,
                Rbp = "N",
                IsProcessed = true
            };
        }

        private static JournalEntry CreateCartageCreditEntry(int journalVoucherId, int transporterId, int cartageCategoryId, JournalEntry originalEntry, int amount, string? otherRef, string accountName)
        {
            return new JournalEntry
            {
                JournalVoucherId = journalVoucherId,
                AccountId = transporterId,
                CategoryId = cartageCategoryId,
                Description = BuildCartageDescription("Credit", originalEntry.JournalVoucherId, otherRef, accountName),
                Quantity = 0,
                Rate = amount,
                Debit = 0,
                Credit = amount,
                Discount = 0,
                DiscountPercent = 0,
                Rbp = "N",
                IsProcessed = true
            };
        }

        private static string BuildCartageDescription(string entryType, int originalVoucherId, string? otherRef, string? accountName)
        {
            var description = $"Cartage {entryType} - Ref to JV-{originalVoucherId}";
            if (!string.IsNullOrWhiteSpace(otherRef))
                description += $" OtherRef-{otherRef}";
            if (!string.IsNullOrWhiteSpace(accountName))
                description += $" - {accountName}";
            return description;
        }

        #endregion

        #region Private Helper Methods - FOR Charges

        private async Task<JournalVoucher> ProcessForChargesInternalAsync(int journalVoucherId, int? entryId, int amount, DateOnly dcDate, int? forAccountId = null, string? otherRef = null)
        {
            try
            {
                var originalJV = await GetValidatedJournalVoucherAsync(journalVoucherId);
                var (customerAccountId, customerCategoryId) = await GetCustomerAccountFromVoucherAsync(originalJV);

                // Get FOR category ID
                var forCategoryId = await GetForChargesCategoryIdAsync(originalJV.ProjectId);
                if (!forCategoryId.HasValue)
                    throw new InvalidOperationException($"FOR expense category not configured for project {originalJV.ProjectId}");

                // Always use a single FOR account - get accounts from the category
                var forAccounts = await GetAccountsAsync(forCategoryId.Value);
                if (!forAccounts.Any())
                    throw new InvalidOperationException($"No FOR expense accounts found in category {forCategoryId.Value}");

                // Determine which account to use: provided account (if valid) or first account from category
                int forAccountIdToUse;
                if (forAccountId.HasValue)
                {
                    // Validate that the provided account belongs to the FOR category
                    var validAccountIds = forAccounts.Select(a => a.AccountId).ToList();
                    if (!validAccountIds.Contains(forAccountId.Value))
                        throw new InvalidOperationException($"Invalid FOR account ID: {forAccountId.Value}. Account must belong to FOR expense category {forCategoryId.Value}");
                    forAccountIdToUse = forAccountId.Value;
                }
                else
                {
                    // Default to first account from the category
                    forAccountIdToUse = forAccounts.First().AccountId;
                    _logger.LogInformation("No FOR account specified, using first account {AccountId} from category {CategoryId}", forAccountIdToUse, forCategoryId.Value);
                }

                // Get account details
                var forAccount = await _unitOfWork.Accounts.GetByIdWithDetailsAsync(forAccountIdToUse);
                if (forAccount == null)
                    throw new InvalidOperationException($"FOR account {forAccountIdToUse} not found");

                var forJV = await CreateForChargesVoucherAsync(originalJV.ProjectId, dcDate);
                await _sharedProcessingService.CreateVoucherReferenceAsync(journalVoucherId, forJV.JournalVoucherId, "ForCharges");

                // Create FOR charges entries for the single selected account
                await CreateForChargesEntriesAsync(forJV.JournalVoucherId, forAccountIdToUse, forAccount.CategoryId, customerAccountId, customerCategoryId, amount, journalVoucherId, entryId, otherRef);

                originalJV.PendingForCharges = false;
                _unitOfWork.JournalVouchers.Update(originalJV);

                // Save otherRef to the first DC entry for the original voucher
                if (!string.IsNullOrWhiteSpace(otherRef))
                {
                    await SaveOtherRefToDCEntryAsync(journalVoucherId, otherRef);
                }

                _logger.LogInformation("Created FOR charges voucher {JvId} for amount {Amount} using single FOR account {AccountId}", forJV.JournalVoucherId, amount, forAccountIdToUse);
                return forJV;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process FOR charges (internal) for journal voucher {JvId}", journalVoucherId);
                throw;
            }
        }

        public async Task ClearPendingForChargesAsync(int journalVoucherId, string? otherRef = null)
        {
            await _paymentReceiptClassifier.EnsureNotPaymentReceiptAsync(
                journalVoucherId, "mutated by pending-for-charges processing");
            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                await _approvalGuard.EnsureVoucherNotPendingAsync(journalVoucherId, lockVoucher: true);
                await EnsureNotStockTransferAsync(journalVoucherId);
                var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
                if (voucher == null)
                    throw new InvalidOperationException($"Voucher {journalVoucherId} not found");
                voucher.PendingForCharges = false;
                _unitOfWork.JournalVouchers.Update(voucher);

                if (!string.IsNullOrWhiteSpace(otherRef))
                    await SaveOtherRefToDCEntryAsync(journalVoucherId, otherRef);

                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<JournalVoucher> CreateForChargesVoucherAsync(int projectId, DateOnly dcDate)
        {
            var forJV = new JournalVoucher
            {
                ProjectId = projectId,
                EntryDate = dcDate,
                CreditableDate = dcDate,
                IsCompleted = false
            };

            _unitOfWork.JournalVouchers.Add(forJV);
            // Materialize the generated FOR JV ID before adding entries and references.
            await _unitOfWork.SaveChangesAsync();
            return forJV;
        }

        public async Task<int?> GetForChargesCategoryIdAsync(int projectId)
        {
            var projectSettings = await _unitOfWork.ProjectCartageSettings.GetByProjectIdAsync(projectId);
            return projectSettings?.ForExpenseCategoryId;
        }

        private async Task EnsureNotStockTransferAsync(int journalVoucherId)
        {
            if (await _unitOfWork.IsStockTransferVoucherAsync(journalVoucherId))
                throw new InvalidOperationException("Stock-transfer vouchers and delivery challans are read-only here. Use the Stock Transfers screen.");
        }

        private async Task<(int accountId, int categoryId)> GetCustomerAccountFromVoucherAsync(JournalVoucher voucher)
        {
            foreach (var entry in voucher.JournalEntries)
            {
                var account = await _unitOfWork.Accounts.GetByIdWithDetailsAsync(entry.AccountId);
                if (account?.AccountGroupId == ACCOUNT_GROUP_ACCOUNT)
                    return (account.AccountId, entry.CategoryId);
            }

            throw new InvalidOperationException($"No customer account found in journal voucher {voucher.JournalVoucherId}");
        }

        private async Task CreateForChargesEntriesAsync(int voucherId, int forAccountId, int forCategoryId, int customerAccountId, int customerCategoryId, int amount, int originalVoucherId, int? entryId, string? otherRef = null)
        {
            var debitEntry = new JournalEntry
            {
                JournalVoucherId = voucherId,
                AccountId = forAccountId,
                CategoryId = forCategoryId,
                Description = BuildForChargesDescription("Debit", originalVoucherId, entryId, otherRef),
                Quantity = 0,
                Rate = amount,
                Debit = amount,
                Credit = 0,
                Discount = 0,
                DiscountPercent = 0,
                Rbp = "N",
                IsProcessed = true
            };

            var creditEntry = new JournalEntry
            {
                JournalVoucherId = voucherId,
                AccountId = customerAccountId,
                CategoryId = customerCategoryId,
                Description = BuildForChargesDescription("Credit", originalVoucherId, entryId, otherRef),
                Quantity = 0,
                Rate = amount,
                Debit = 0,
                Credit = amount,
                Discount = 0,
                DiscountPercent = 0,
                Rbp = "N",
                IsProcessed = true
            };

            _unitOfWork.JournalEntries.Add(debitEntry);
            _unitOfWork.JournalEntries.Add(creditEntry);
        }

        private static string BuildForChargesDescription(string entryType, int originalVoucherId, int? entryId, string? otherRef = null)
        {
            var description = $"FOR Charges {entryType} - Ref to JV-{originalVoucherId}";
            if (entryId.HasValue)
                description += $" Entry-{entryId}";
            if (!string.IsNullOrWhiteSpace(otherRef))
                description += $" | Other Ref: {otherRef}";
            return description;
        }

        private async Task SaveOtherRefToDCEntryAsync(int journalVoucherId, string otherRef)
        {
            var dcVoucher = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(journalVoucherId);
            if (dcVoucher != null)
            {
                var dcEntries = await _unitOfWork.DeliveryChallanEntries.GetByDeliveryChallanIdAsync(dcVoucher.DeliveryChallanVoucherId);
                var firstEntry = dcEntries.FirstOrDefault();
                if (firstEntry != null)
                {
                    firstEntry.OtherReference = string.IsNullOrWhiteSpace(firstEntry.OtherReference)
                        ? otherRef
                        : $"{firstEntry.OtherReference} | {otherRef}";
                    _unitOfWork.DeliveryChallanEntries.Update(firstEntry);
                }
            }
        }

        #endregion

        #region Private Helper Methods - Validation and Processing

        private async Task<EntryProcessResult> ProcessSingleEntryInternalAsync(int deliveryChallanId, JournalEntry entry, int locationId, string? remarks, string? otherReference, DateOnly entryDate)
        {
            try
            {
                if (entry.IsProcessed)
                {
                    _logger.LogWarning("Entry {EntryId} already processed - skipping", entry.JournalEntryId);
                    return new EntryProcessResult { EntryId = entry.JournalEntryId, Success = true, Message = "Entry was already processed" };
                }

                var dcEntry = await CreateDCEntryInternalAsync(deliveryChallanId, entry.JournalEntryId, locationId, remarks, otherReference, entryDate);
                _logger.LogInformation("Successfully processed entry {EntryId}, created DC entry {DCEntryId}", entry.JournalEntryId, dcEntry.DeliveryChallanEntryId);

                return new EntryProcessResult { EntryId = entry.JournalEntryId, Success = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process entry {EntryId} for delivery challan {DeliveryChallanId}", entry.JournalEntryId, deliveryChallanId);
                return new EntryProcessResult { EntryId = entry.JournalEntryId, Success = false, Message = ExceptionClassifier.SafeMessage(ex, "This entry could not be processed.") };
            }
        }

        private async Task<JournalVoucher> GetValidatedJournalVoucherAsync(int journalVoucherId)
        {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null)
                throw new ArgumentException($"Journal voucher {journalVoucherId} not found");
            return voucher;
        }

        private async Task<JournalEntry> GetValidatedJournalEntryAsync(int entryId)
        {
            var entry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(entryId);
            if (entry == null)
                throw new ArgumentException($"Journal entry {entryId} not found");
            return entry;
        }

        private async Task<(DeliveryChallanVoucher deliveryChallan, JournalEntry journalEntry, Location location, DeliveryChallanEntry? existingEntry)> ValidateDCEntryDataAsync(int deliveryChallanId, int journalEntryId, int locationId)
        {
            var deliveryChallan = await _unitOfWork.DeliveryChallanVouchers.GetByIdAsync(deliveryChallanId)
                ?? throw new ArgumentException($"Delivery challan voucher {deliveryChallanId} not found");
            if (await _unitOfWork.IsStockTransferVoucherAsync(deliveryChallan.JournalVoucherId))
                throw new InvalidOperationException("Stock-transfer delivery challans are immutable here. Use the Stock Transfers screen for cancellation, Closed Short, or complete deletion.");

            var journalEntry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(journalEntryId)
                ?? throw new ArgumentException($"Journal entry {journalEntryId} not found");
            if (journalEntry.JournalVoucherId != deliveryChallan.JournalVoucherId)
                throw new InvalidOperationException("The journal entry does not belong to this Delivery Challan voucher.");
            if (journalEntry.Account?.AccountGroupId != ACCOUNT_GROUP_PRODUCT)
                throw new InvalidOperationException("Only Product entries can be processed through a Delivery Challan.");

            var location = await _unitOfWork.Locations.GetByIdAsync(locationId)
                ?? throw new ArgumentException($"Location {locationId} not found");

            var existingEntry = await _unitOfWork.DeliveryChallanEntries.GetByDeliveryChallanIdAndJournalEntryIdAsync(deliveryChallanId, journalEntryId);

            return (deliveryChallan, journalEntry, location, existingEntry);
        }

        private async Task<int> GetDebitAccountIdAsync(JournalVoucher originalJV, JournalEntry entry, bool postToCustomer)
        {
            if (!postToCustomer) return entry.AccountId;

            foreach (var journalEntry in originalJV.JournalEntries)
            {
                var account = await _unitOfWork.Accounts.GetByIdAsync(journalEntry.AccountId);
                if (account?.AccountGroupId == ACCOUNT_GROUP_ACCOUNT)
                    return account.AccountId;
            }

            throw new InvalidOperationException($"No customer account found in journal voucher {originalJV.JournalVoucherId}");
        }

        private async Task<List<int>> GetHiddenCategoryIdsAsync(int? userId)
        {
            if (!userId.HasValue) return new List<int>();
            var userCategories = await _unitOfWork.UserCategories.GetByUserIdAsync(userId.Value);
            return userCategories.Select(uc => uc.CategoryId).ToList();
        }

        #endregion

        #region Private Helper Methods - Mapping

        private static JournalEntryDetailViewModel MapToDetailViewModel(JournalEntry entry)
        {
            return new JournalEntryDetailViewModel
            {
                EntryId = entry.JournalEntryId,
                JournalVoucherId = entry.JournalVoucherId,
                AccountId = entry.AccountId,
                AccountName = entry.Account?.AccountName ?? "",
                Description = entry.Description ?? "",
                Quantity = entry.Quantity,
                Rate = entry.Rate,
                IsProcessed = entry.IsProcessed,
                Pkg = entry.Account?.ProductData?.Pkg,
                DiscountPercent = entry.DiscountPercent
            };
        }

        private static JournalVoucherViewModel MapToVoucherViewModel(JournalVoucher voucher)
        {
            return new JournalVoucherViewModel
            {
                JvId = voucher.JournalVoucherId,
                ProjectName = voucher.Project?.ProjectName ?? "",
                EntryDate = voucher.EntryDate,
                IsProcessed = voucher.IsCompleted,
                Entries = new List<JournalEntryDetailViewModel>()
            };
        }

        #endregion
    }

    public class EntryProcessResult
    {
        public int EntryId { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
