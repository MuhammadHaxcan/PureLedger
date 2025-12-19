using PureLedger.Models;
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

        private const int ACCOUNT_GROUP_ACCOUNT = 1;
        private const int ACCOUNT_GROUP_PRODUCT = 2;

        public DeliveryChallanService(
            IUnitOfWork unitOfWork,
            ILogger<DeliveryChallanService> logger,
            ISharedProcessingService sharedProcessingService)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sharedProcessingService = sharedProcessingService ?? throw new ArgumentNullException(nameof(sharedProcessingService));
        }

        #endregion

        #region Simple Data Retrieval

        public Task<List<Project>> GetAllProjectsAsync() => _unitOfWork.Projects.GetAllAsync();

        public Task<List<Project>> GetUserProjectsAsync(int userId) => _unitOfWork.Projects.GetAllForUserAsync(userId);

        public Task<List<Location>> GetLocationsAsync() => _unitOfWork.Locations.GetAllAsync();

        public Task<JournalEntry?> GetJournalEntryByIdAsync(int journalEntryId) => _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(journalEntryId);

        public Task<DeliveryChallanVoucher?> GetDCByIdAsync(int deliveryChallanId) => _unitOfWork.DeliveryChallanVouchers.GetByIdWithEntriesAsync(deliveryChallanId);

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

        public async Task<List<DeliveryChallanVoucher>> GetDCsByDateRangeAsync(DateOnly startDate, DateOnly endDate)
        {
            try
            {
                return await _unitOfWork.DeliveryChallanVouchers.GetByDateRangeAsync(startDate, endDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve delivery challans for date range {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<List<DeliveryChallanEntry>> GetDCEntriesByVoucherIdAsync(int deliveryChallanId)
        {
            try
            {
                return await _unitOfWork.DeliveryChallanEntries.GetByDeliveryChallanIdAsync(deliveryChallanId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve entries for delivery challan {DeliveryChallanId}", deliveryChallanId);
                throw;
            }
        }

        public async Task<List<JournalEntryDetailViewModel>> GetVoucherEntriesAsync(int voucherId)
        {
            var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(voucherId, null, null);
            return entries.Select(MapToDetailViewModel).ToList();
        }

        public async Task<List<JournalEntry>> GetUnprocessedEntriesAsync(int voucherId)
        {
            try
            {
                var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(voucherId, null, null);
                return entries.Where(e => !e.IsProcessed && e.Quantity > 0).ToList();
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
                    var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(voucher.JournalVoucherId, categoryId, accountId);
                    var filteredEntries = entries.Where(e => !e.IsProcessed && e.Account.AccountGroupId != ACCOUNT_GROUP_ACCOUNT);

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
            int locationId, string? remarks, bool includeForCharges, int forAmount)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var journalEntry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(journalEntryId);
                if (journalEntry == null)
                    throw new ArgumentException($"Journal entry {journalEntryId} not found");

                var deliveryChallan = await CreateDCVoucherInternalAsync(journalEntry.JournalVoucherId, dcDate, otherReference);
                var dcEntry = await CreateDCEntryInternalAsync(deliveryChallan.DeliveryChallanVoucherId, journalEntryId, locationId, remarks, otherReference, dcDate);

                if (includeForCharges && forAmount > 0)
                    await ProcessForChargesInternalAsync(journalEntry.JournalVoucherId, journalEntryId, forAmount, dcDate);

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
            int locationId, string? remarks, bool includeForCharges, int forAmount)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var unprocessedEntries = await GetUnprocessedEntriesAsync(journalVoucherId);
                if (!unprocessedEntries.Any())
                    throw new InvalidOperationException("No unprocessed entries found for this voucher");

                var deliveryChallan = await CreateDCVoucherInternalAsync(journalVoucherId, dcDate, otherReference);
                var results = new List<EntryProcessResult>();

                foreach (var entry in unprocessedEntries)
                {
                    var result = await ProcessSingleEntryInternalAsync(deliveryChallan.DeliveryChallanVoucherId, entry, locationId, remarks, otherReference, dcDate);
                    results.Add(result);
                }

                if (includeForCharges && forAmount > 0)
                    await ProcessForChargesInternalAsync(journalVoucherId, null, forAmount, dcDate);

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
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var deliveryChallan = await ProcessCartageInternalAsync(
                    model.JournalVoucherId, model.EntryId, model.TransporterId, model.Amount,
                    model.PostToCustomer, model.Reference, model.DcDate, model.DcRemarks,
                    model.DcStation, model.DcOtherRef, model.DcLocation);

                if (model.IncludeForCharges && model.ForAmount > 0)
                    await ProcessForChargesInternalAsync(model.JournalVoucherId, model.EntryId, model.ForAmount, model.DcDate);

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

        public async Task<JournalVoucher> ProcessForChargesAsync(int journalVoucherId, int? entryId, int amount, DateOnly dcDate)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var originalJV = await GetValidatedJournalVoucherAsync(journalVoucherId);
                var (forAccountId, forCategoryId) = await GetForChargesAccountAsync(originalJV.ProjectId);
                var (customerAccountId, customerCategoryId) = await GetCustomerAccountFromVoucherAsync(originalJV);

                var forJV = await CreateForChargesVoucherAsync(originalJV.ProjectId, dcDate);
                await _sharedProcessingService.CreateVoucherReferenceAsync(journalVoucherId, forJV.JournalVoucherId, "ForCharges");
                await CreateForChargesEntriesAsync(forJV.JournalVoucherId, forAccountId, forCategoryId, customerAccountId, customerCategoryId, amount, journalVoucherId, entryId);

                originalJV.PendingForCharges = false;
                await _unitOfWork.JournalVouchers.UpdateAsync(originalJV);
                await _sharedProcessingService.RecalculateVoucherCompletionAsync(forJV.JournalVoucherId);

                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Created FOR charges voucher {JvId} for amount {Amount}", forJV.JournalVoucherId, amount);
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

        #region DC Voucher CRUD Operations

        public Task<DeliveryChallanVoucher> CreateDCVoucherAsync(int journalVoucherId, DateOnly dcDate) =>
            CreateDCVoucherAsync(journalVoucherId, dcDate, null);

        public async Task<DeliveryChallanVoucher> CreateDCVoucherAsync(int journalVoucherId, DateOnly dcDate, string? reference)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var result = await CreateDCVoucherInternalAsync(journalVoucherId, dcDate, reference);
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public Task<DeliveryChallanEntry> CreateDCEntryAsync(int deliveryChallanId, int journalEntryId, int locationId, string? remarks, string? otherReference) =>
            CreateDCEntryAsync(deliveryChallanId, journalEntryId, locationId, remarks, otherReference, DateOnly.FromDateTime(DateTime.UtcNow));

        public async Task<DeliveryChallanEntry> CreateDCEntryAsync(int deliveryChallanId, int journalEntryId, int locationId, string? remarks, string? otherReference, DateOnly entryDate)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var result = await CreateDCEntryInternalAsync(deliveryChallanId, journalEntryId, locationId, remarks, otherReference, entryDate);
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<DeliveryChallanVoucher> UpdateDCVoucherAsync(DeliveryChallanVoucher voucher)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var result = await _unitOfWork.DeliveryChallanVouchers.UpdateAsync(voucher);
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region Voucher Status Management

        public async Task<bool> CompleteDCAsync(int deliveryChallanId, DateOnly finalDeliveryDate)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var deliveryChallan = await _unitOfWork.DeliveryChallanVouchers.GetByIdWithEntriesAsync(deliveryChallanId);
                if (deliveryChallan == null)
                {
                    _logger.LogError("Delivery challan {DeliveryChallanId} not found", deliveryChallanId);
                    return false;
                }

                if (!deliveryChallan.Entries.Any())
                {
                    _logger.LogError("Cannot complete delivery challan {DeliveryChallanId} - no entries found", deliveryChallanId);
                    return false;
                }

                deliveryChallan.IsCompleted = true;
                deliveryChallan.FinalDeliveryDate = finalDeliveryDate;

                await _unitOfWork.DeliveryChallanVouchers.UpdateAsync(deliveryChallan);
                await RecalculateVoucherCompletionAsync(deliveryChallan.JournalVoucherId);
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Completed delivery challan {DeliveryChallanId}", deliveryChallanId);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to complete delivery challan {DeliveryChallanId}", deliveryChallanId);
                return false;
            }
        }

        public async Task UpdateVoucherStatusAsync(int journalVoucherId)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                await RecalculateVoucherCompletionAsync(journalVoucherId);
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task MarkVoucherCompletedAsync(int journalVoucherId)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
                if (voucher == null)
                    throw new ArgumentException($"Journal voucher {journalVoucherId} not found");

                if (!voucher.IsCompleted)
                {
                    voucher.IsCompleted = true;
                    await _unitOfWork.JournalVouchers.UpdateAsync(voucher);
                }

                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
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

        private async Task<DeliveryChallanVoucher> CreateDCVoucherInternalAsync(int journalVoucherId, DateOnly dcDate, string? reference)
        {
            try
            {
                var journalVoucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
                if (journalVoucher == null)
                    throw new ArgumentException($"Journal voucher with ID {journalVoucherId} not found");

                var existingDC = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(journalVoucherId);
                if (existingDC != null)
                {
                    _logger.LogInformation("Delivery challan already exists for journal voucher {JournalVoucherId}", journalVoucherId);
                    if (!string.IsNullOrEmpty(reference))
                        await _unitOfWork.DeliveryChallanVouchers.UpdateAsync(existingDC);
                    return existingDC;
                }

                var deliveryChallanVoucher = new DeliveryChallanVoucher
                {
                    JournalVoucherId = journalVoucherId,
                    DCDate = dcDate,
                    IsCompleted = false
                };

                await _unitOfWork.DeliveryChallanVouchers.AddAsync(deliveryChallanVoucher);
                _logger.LogInformation("Created delivery challan for journal voucher {JournalVoucherId}", journalVoucherId);

                return deliveryChallanVoucher;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create delivery challan for journal voucher {JournalVoucherId}", journalVoucherId);
                throw;
            }
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
                    await _unitOfWork.DeliveryChallanEntries.UpdateAsync(existingEntry);
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

                var result = await _unitOfWork.DeliveryChallanEntries.AddAsync(deliveryChallanEntry);
                journalEntry.IsProcessed = true;
                await _unitOfWork.JournalEntries.UpdateAsync(journalEntry);

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
                await _unitOfWork.JournalEntries.UpdateAsync(entry);

                var deliveryChallanVoucher = await CreateDCVoucherInternalAsync(journalVoucherId, dcDate, reference);
                await CreateDCEntryInternalAsync(deliveryChallanVoucher.DeliveryChallanVoucherId, entry.JournalEntryId, dcLocation, dcRemarks, dcOtherRef, dcDate);

                int debitAccountId = await GetDebitAccountIdAsync(originalJV, entry, postToCustomer);
                var cartageVoucher = await CreateCartageJournalVoucherAsync(originalJV.ProjectId, debitAccountId, transporterId, entry, amount, dcOtherRef);
                await _sharedProcessingService.CreateVoucherReferenceAsync(originalJV.JournalVoucherId, cartageVoucher.JournalVoucherId, "Cartage");

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
                var cartageJV = await CreateBaseCartageVoucherAsync(projectId);
                var (debitCategoryId, cartageCategoryId) = await GetCartageAccountCategoriesAsync(debitAccountId, originalEntry.JournalVoucher.ProjectId);

                var entries = new List<JournalEntry>
                {
                    CreateCartageDebitEntry(cartageJV.JournalVoucherId, debitAccountId, debitCategoryId, originalEntry, amount, otherRef),
                    CreateCartageCreditEntry(cartageJV.JournalVoucherId, transporterId, cartageCategoryId, originalEntry, amount, otherRef)
                };

                foreach (var entry in entries)
                    await _unitOfWork.JournalEntries.AddAsync(entry);

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
                EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
                IsCompleted = false,
                CreditableDate = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            return await _unitOfWork.JournalVouchers.AddAsync(cartageJV);
        }

        private async Task<(int debitCategoryId, int cartageCategoryId)> GetCartageAccountCategoriesAsync(int debitAccountId, int projectId)
        {
            var debitAccount = await _unitOfWork.Accounts.GetByIdAsync(debitAccountId);
            if (debitAccount == null)
                throw new InvalidOperationException($"Debit account {debitAccountId} not found for cartage entry");

            var projectSettings = await _unitOfWork.ProjectCartageSettings.GetByProjectIdAsync(projectId);
            if (projectSettings?.CartageTransporterCategoryId == null)
                throw new InvalidOperationException($"Cartage transporter category not configured for project {projectId}");

            return (debitAccount.CategoryId, projectSettings.CartageTransporterCategoryId.Value);
        }

        private static JournalEntry CreateCartageDebitEntry(int journalVoucherId, int accountId, int categoryId, JournalEntry originalEntry, int amount, string? otherRef)
        {
            return new JournalEntry
            {
                JournalVoucherId = journalVoucherId,
                AccountId = accountId,
                CategoryId = categoryId,
                Description = BuildCartageDescription("Debit", originalEntry.JournalVoucherId, otherRef),
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

        private static JournalEntry CreateCartageCreditEntry(int journalVoucherId, int transporterId, int cartageCategoryId, JournalEntry originalEntry, int amount, string? otherRef)
        {
            return new JournalEntry
            {
                JournalVoucherId = journalVoucherId,
                AccountId = transporterId,
                CategoryId = cartageCategoryId,
                Description = BuildCartageDescription("Credit", originalEntry.JournalVoucherId, otherRef),
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

        private static string BuildCartageDescription(string entryType, int originalVoucherId, string? otherRef)
        {
            var description = $"Cartage {entryType} - Ref to JV-{originalVoucherId}";
            if (!string.IsNullOrWhiteSpace(otherRef))
                description += $" OtherRef-{otherRef}";
            return description;
        }

        #endregion

        #region Private Helper Methods - FOR Charges

        private async Task ProcessForChargesInternalAsync(int journalVoucherId, int? entryId, int amount, DateOnly dcDate)
        {
            try
            {
                var originalJV = await GetValidatedJournalVoucherAsync(journalVoucherId);
                var (forAccountId, forCategoryId) = await GetForChargesAccountAsync(originalJV.ProjectId);
                var (customerAccountId, customerCategoryId) = await GetCustomerAccountFromVoucherAsync(originalJV);

                var forJV = await CreateForChargesVoucherAsync(originalJV.ProjectId, dcDate);
                await _sharedProcessingService.CreateVoucherReferenceAsync(journalVoucherId, forJV.JournalVoucherId, "ForCharges");
                await CreateForChargesEntriesAsync(forJV.JournalVoucherId, forAccountId, forCategoryId, customerAccountId, customerCategoryId, amount, journalVoucherId, entryId);

                originalJV.PendingForCharges = false;
                await _unitOfWork.JournalVouchers.UpdateAsync(originalJV);
                await _sharedProcessingService.RecalculateVoucherCompletionAsync(forJV.JournalVoucherId);

                _logger.LogInformation("Created FOR charges voucher {JvId} for amount {Amount}", forJV.JournalVoucherId, amount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process FOR charges (internal) for journal voucher {JvId}", journalVoucherId);
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

            return await _unitOfWork.JournalVouchers.AddAsync(forJV);
        }

        private async Task<(int accountId, int categoryId)> GetForChargesAccountAsync(int projectId)
        {
            var projectSettings = await _unitOfWork.ProjectCartageSettings.GetByProjectIdAsync(projectId);
            if (projectSettings?.ForExpenseAccountId == null)
                throw new InvalidOperationException($"FOR expense account not configured for project {projectId}");

            var forAccount = await _unitOfWork.Accounts.GetByIdWithDetailsAsync(projectSettings.ForExpenseAccountId.Value);
            if (forAccount == null)
                throw new InvalidOperationException($"FOR account {projectSettings.ForExpenseAccountId.Value} not found");

            return (forAccount.AccountId, forAccount.CategoryId);
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

        private async Task CreateForChargesEntriesAsync(int voucherId, int forAccountId, int forCategoryId, int customerAccountId, int customerCategoryId, int amount, int originalVoucherId, int? entryId)
        {
            var debitEntry = new JournalEntry
            {
                JournalVoucherId = voucherId,
                AccountId = forAccountId,
                CategoryId = forCategoryId,
                Description = BuildForChargesDescription("Debit", originalVoucherId, entryId),
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
                Description = BuildForChargesDescription("Credit", originalVoucherId, entryId),
                Quantity = 0,
                Rate = amount,
                Debit = 0,
                Credit = amount,
                Discount = 0,
                DiscountPercent = 0,
                Rbp = "N",
                IsProcessed = true
            };

            await _unitOfWork.JournalEntries.AddAsync(debitEntry);
            await _unitOfWork.JournalEntries.AddAsync(creditEntry);
        }

        private static string BuildForChargesDescription(string entryType, int originalVoucherId, int? entryId)
        {
            var description = $"FOR Charges {entryType} - Ref to JV-{originalVoucherId}";
            if (entryId.HasValue)
                description += $" Entry-{entryId}";
            return description;
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
                return new EntryProcessResult { EntryId = entry.JournalEntryId, Success = false, Message = ex.Message };
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

            var journalEntry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(journalEntryId)
                ?? throw new ArgumentException($"Journal entry {journalEntryId} not found");

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
