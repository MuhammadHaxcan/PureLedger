using PureLedger.Models;
using PureLedger.Repositories.Interfaces;
using PureLedger.Services.Interface;
using PureLedger.ViewModels;

namespace PureLedger.Services
{
    public class DeliveryChallanService : IDeliveryChallanService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DeliveryChallanService> _logger;

        public DeliveryChallanService(
            IUnitOfWork unitOfWork,
            ILogger<DeliveryChallanService> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Entity Retrieval

        public Task<List<Project>> GetAllProjectsAsync() =>
            _unitOfWork.Projects.GetAllAsync();

        public Task<List<Project>> GetAllProjectsForUserAsync(int userId) =>
            _unitOfWork.Projects.GetAllForUserAsync(userId);

        public Task<List<Location>> GetAllLocationsAsync() =>
            _unitOfWork.Locations.GetAllAsync();

        // Return nullable to match repository and actual semantics
        public Task<JournalEntry?> GetJournalEntryByIdAsync(int journalEntryId) =>
            _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(journalEntryId);

        // Return nullable to match repository
        public Task<DeliveryChallanVoucher?> GetDeliveryChallanVoucherByIdAsync(int deliveryChallanId) =>
            _unitOfWork.DeliveryChallanVouchers.GetByIdWithEntriesAsync(deliveryChallanId);

        // Return nullable to match repository
        public async Task<DeliveryChallanVoucher?> GetDeliveryChallanVoucherByJournalVoucherIdAsync(int journalVoucherId)
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

        public async Task<List<DeliveryChallanVoucher>> GetDeliveryChallanVouchersByDateRangeAsync(DateOnly startDate, DateOnly endDate)
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

        public async Task<List<DeliveryChallanEntry>> GetDeliveryChallanEntriesByVoucherIdAsync(int deliveryChallanId)
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

        public async Task<List<JournalEntryDetailViewModel>> GetEntriesByVoucherIdAsync(int voucherId)
        {
            var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(voucherId, null, null);
            return entries.Select(MapToJournalEntryDetailViewModel).ToList();
        }

        public async Task<List<JournalEntry>> GetUnprocessedEntriesByVoucherIdAsync(int voucherId)
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

        public async Task<IEnumerable<Account>> GetAccountsByCategoryAsync(int categoryId) =>
            await _unitOfWork.Accounts.GetByCategoryIdAsync(categoryId);

        public async Task<IEnumerable<Category>> GetCategoriesByProjectAsync(int projectId)
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

        public Task<ProjectCartageSettings?> GetProjectCartageSettingsAsync(int projectId) =>
            _unitOfWork.ProjectCartageSettings.GetByProjectIdAsync(projectId);

        #endregion

        #region Filtered Entries

        public async Task<DeliveryChallanFilterViewModel> GetFilteredEntriesForDeliveryChallanAsync(
            DateOnly? sinceDate,
            DateOnly? toDate,
            int? projectId = null,
            int? categoryId = null,
            int? accountId = null)
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
                var includeEntries = !accountId.HasValue && !categoryId.HasValue;
                var vouchers = await _unitOfWork.JournalVouchers.GetFilteredVouchersAsync(sinceDate, toDate, projectId, includeEntries);
                var unprocessedVouchers = vouchers.Where(v => !v.IsCompleted && !v.IsBaseVoucher).ToList();

                _logger.LogInformation("Found {UnprocessedCount} unprocessed non-base vouchers out of {TotalCount} total vouchers",
                    unprocessedVouchers.Count, vouchers.Count);

                foreach (var voucher in unprocessedVouchers)
                {
                    var voucherViewModel = MapToJournalVoucherViewModel(voucher);
                    var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(voucher.JournalVoucherId, categoryId, accountId);

                    var processableEntries = entries
                        .Where(e => !e.IsProcessed && e.Account.AccountGroupId != 1)
                        .Select(MapToJournalEntryDetailViewModel);

                    voucherViewModel.Entries.AddRange(processableEntries);

                    if (voucherViewModel.Entries.Any())
                    {
                        result.Vouchers.Add(voucherViewModel);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve filtered entries for delivery challan");
            }

            return result;
        }

        #endregion

        #region Create & Update Operations

        public Task<DeliveryChallanVoucher> CreateDeliveryChallanVoucherAsync(int journalVoucherId, DateOnly dcDate) =>
            CreateDeliveryChallanVoucherAsync(journalVoucherId, dcDate, null);

        public async Task<DeliveryChallanVoucher> CreateDeliveryChallanVoucherAsync(int journalVoucherId, DateOnly dcDate, string? reference)
        {
            try
            {
                var journalVoucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
                if (journalVoucher == null)
                {
                    throw new ArgumentException($"Journal voucher with ID {journalVoucherId} not found");
                }

                var existingDC = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(journalVoucherId);
                if (existingDC != null)
                {
                    _logger.LogInformation("Delivery challan already exists for journal voucher {JournalVoucherId}", journalVoucherId);

                    if (!string.IsNullOrEmpty(reference))
                    {
                        await _unitOfWork.DeliveryChallanVouchers.UpdateAsync(existingDC);
                        await _unitOfWork.SaveChangesAsync();
                    }

                    return existingDC;
                }

                var deliveryChallanVoucher = new DeliveryChallanVoucher
                {
                    JournalVoucherId = journalVoucherId,
                    DCDate = dcDate,
                    IsCompleted = false
                };

                await _unitOfWork.DeliveryChallanVouchers.AddAsync(deliveryChallanVoucher);
                await _unitOfWork.SaveChangesAsync();

                var savedVoucher = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(journalVoucherId);
                if (savedVoucher == null)
                {
                    throw new InvalidOperationException($"Failed to save delivery challan voucher for journal voucher {journalVoucherId}");
                }

                _logger.LogInformation("Created delivery challan {DeliveryChallanId} for journal voucher {JournalVoucherId}",
                    savedVoucher.DeliveryChallanVoucherId, journalVoucherId);

                return savedVoucher;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create delivery challan for journal voucher {JournalVoucherId}", journalVoucherId);
                throw;
            }
        }

        public Task<DeliveryChallanEntry> CreateDeliveryChallanEntryAsync(
            int deliveryChallanId, int journalEntryId, int locationId, string? remarks, string? otherReference) =>
            CreateDeliveryChallanEntryAsync(deliveryChallanId, journalEntryId, locationId, remarks, otherReference, DateOnly.FromDateTime(DateTime.Now));

        public async Task<DeliveryChallanEntry> CreateDeliveryChallanEntryAsync(
            int deliveryChallanId, int journalEntryId, int locationId, string? remarks, string? otherReference, DateOnly entryDate)
        {
            try
            {
                var (deliveryChallan, journalEntry, _, existingEntry) = await ValidateDeliveryChallanEntryDataAsync(
                    deliveryChallanId, journalEntryId, locationId);

                if (existingEntry != null)
                {
                    existingEntry.Remarks = remarks;
                    existingEntry.OtherReference = otherReference;
                    existingEntry.LocationId = locationId;
                    existingEntry.EntryDate = entryDate;

                    await _unitOfWork.DeliveryChallanEntries.UpdateAsync(existingEntry);
                    await _unitOfWork.SaveChangesAsync();

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
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Created delivery challan entry {DCEntryId} for date {EntryDate}",
                    result.DeliveryChallanEntryId, entryDate);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create delivery challan entry for DC {DeliveryChallanId}, entry {JournalEntryId}",
                    deliveryChallanId, journalEntryId);
                throw;
            }
        }

        public async Task<DeliveryChallanVoucher> UpdateDeliveryChallanVoucherAsync(DeliveryChallanVoucher voucher)
        {
            var result = await _unitOfWork.DeliveryChallanVouchers.UpdateAsync(voucher);
            await _unitOfWork.SaveChangesAsync();
            return result;
        }

        public async Task<bool> CompleteDeliveryChallanAsync(int deliveryChallanId, DateOnly finalDeliveryDate)
        {
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
                await UpdateJournalVoucherProcessedStatusAsync(deliveryChallan.JournalVoucherId);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Completed delivery challan {DeliveryChallanId}", deliveryChallanId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete delivery challan {DeliveryChallanId}", deliveryChallanId);
                return false;
            }
        }

        #endregion

        #region Journal Voucher Processing

        public async Task UpdateJournalVoucherProcessedStatusAsync(int journalVoucherId)
        {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
            if (voucher?.JournalEntries == null || !voucher.JournalEntries.Any())
            {
                return;
            }

            var accountGroupMapping = await BuildAccountGroupMappingAsync(voucher.JournalEntries);
            var group2Entries = GetEntriesByAccountGroup(voucher.JournalEntries, accountGroupMapping, 2);

            if (!group2Entries.Any())
            {
                return;
            }

            bool allGroup2Processed = group2Entries.All(e => e.IsProcessed);

            if (allGroup2Processed)
            {
                await ProcessCompletedVoucherAsync(voucher, accountGroupMapping);
            }
            else
            {
                await MarkVoucherAsIncompleteIfNeededAsync(voucher);
            }
        }

        private async Task<Dictionary<int, int>> BuildAccountGroupMappingAsync(ICollection<JournalEntry> entries)
        {
            var accountIds = entries.Select(e => e.AccountId).Distinct();
            var mapping = new Dictionary<int, int>();

            foreach (var accountId in accountIds)
            {
                var account = await _unitOfWork.Accounts.GetByIdWithDetailsAsync(accountId);
                if (account?.AccountGroupId != null)
                {
                    mapping[accountId] = account.AccountGroupId;
                }
            }

            return mapping;
        }

        private static List<JournalEntry> GetEntriesByAccountGroup(
            ICollection<JournalEntry> entries,
            Dictionary<int, int> accountGroupMapping,
            int groupId)
        {
            return entries
                .Where(e => accountGroupMapping.TryGetValue(e.AccountId, out var gId) && gId == groupId)
                .ToList();
        }

        private async Task ProcessCompletedVoucherAsync(JournalVoucher voucher, Dictionary<int, int> accountGroupMapping)
        {
            var group1Entries = GetEntriesByAccountGroup(voucher.JournalEntries, accountGroupMapping, 1)
                .Where(e => !e.IsProcessed);

            foreach (var entry in group1Entries)
            {
                entry.IsProcessed = true;
                await _unitOfWork.JournalEntries.UpdateAsync(entry);
            }

            if (!voucher.IsCompleted)
            {
                voucher.IsCompleted = true;
                await _unitOfWork.JournalVouchers.UpdateAsync(voucher);
                await ProcessRelatedVouchersAsync(voucher.JournalVoucherId);
            }

            await CompleteAssociatedDeliveryChallanAsync(voucher.JournalVoucherId);
            await _unitOfWork.SaveChangesAsync();
        }

        private async Task CompleteAssociatedDeliveryChallanAsync(int journalVoucherId)
        {
            try
            {
                var deliveryChallan = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(journalVoucherId);
                if (deliveryChallan != null && !deliveryChallan.IsCompleted)
                {
                    deliveryChallan.IsCompleted = true;
                    deliveryChallan.FinalDeliveryDate = DateOnly.FromDateTime(DateTime.Now);
                    await _unitOfWork.DeliveryChallanVouchers.UpdateAsync(deliveryChallan);

                    _logger.LogInformation("Marked delivery challan {DeliveryChallanId} as completed", deliveryChallan.DeliveryChallanVoucherId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete delivery challan for journal voucher {JournalVoucherId}", journalVoucherId);
            }
        }

        private async Task MarkVoucherAsIncompleteIfNeededAsync(JournalVoucher voucher)
        {
            if (voucher.IsCompleted)
            {
                voucher.IsCompleted = false;
                await _unitOfWork.JournalVouchers.UpdateAsync(voucher);
                await _unitOfWork.SaveChangesAsync();
            }
        }

        private async Task ProcessRelatedVouchersAsync(int mainVoucherId)
        {
            await ProcessCartageVouchersAsync(mainVoucherId);
            await ProcessForChargesVouchersAsync(mainVoucherId);
        }

        private async Task ProcessCartageVouchersAsync(int mainVoucherId)
        {
            await ProcessReferencedVouchersAsync(mainVoucherId, "Cartage", "cartage");
        }

        private async Task ProcessForChargesVouchersAsync(int mainVoucherId)
        {
            await ProcessReferencedVouchersAsync(mainVoucherId, "ForCharges", "FOR charges");
        }

        private async Task ProcessReferencedVouchersAsync(int mainVoucherId, string referenceType, string logName)
        {
            try
            {
                var references = await _unitOfWork.JournalVoucherReferences.GetByMainVoucherIdAndTypeAsync(mainVoucherId, referenceType);

                foreach (var reference in references)
                {
                    var referencedVoucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(reference.ReferencedVoucherId);
                    if (referencedVoucher == null)
                    {
                        _logger.LogError("Referenced {VoucherType} voucher {VoucherId} not found", logName, reference.ReferencedVoucherId);
                        continue;
                    }

                    await MarkAllEntriesAsProcessedAsync(referencedVoucher.JournalEntries);

                    referencedVoucher.IsCompleted = true;
                    await _unitOfWork.JournalVouchers.UpdateAsync(referencedVoucher);

                    _logger.LogInformation("Processed {VoucherType} voucher {VoucherId}", logName, reference.ReferencedVoucherId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process {VoucherType} vouchers for main voucher {MainVoucherId}", logName, mainVoucherId);
            }
        }

        private async Task MarkAllEntriesAsProcessedAsync(ICollection<JournalEntry> entries)
        {
            foreach (var entry in entries)
            {
                entry.IsProcessed = true;
                await _unitOfWork.JournalEntries.UpdateAsync(entry);
            }
        }

        #endregion

        #region Cartage Processing

        public Task<DeliveryChallanVoucher> ProcessCartageAndDeliveryAsync(CartageViewModel model) =>
            ProcessCartageAndDeliveryAsync(
                model.JournalVoucherId,
                model.EntryId,
                model.TransporterId,
                model.Amount,
                model.PostToCustomer,
                model.Reference,
                model.DcDate,
                model.DcRemarks,
                model.DcStation,
                model.DcOtherRef,
                model.DcLocation);

        public async Task<DeliveryChallanVoucher> ProcessCartageAndDeliveryAsync(
            int journalVoucherId,
            int entryId,
            int transporterId,
            int amount,
            bool postToCustomer,
            string? reference,
            DateOnly dcDate,
            string? dcRemarks,
            string? dcStation,
            string? dcOtherRef,
            int dcLocation)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                var originalJV = await GetValidatedJournalVoucherAsync(journalVoucherId);
                var entry = await GetValidatedJournalEntryAsync(entryId);

                entry.IsProcessed = true;
                await _unitOfWork.JournalEntries.UpdateAsync(entry);

                var deliveryChallanVoucher = await CreateDeliveryChallanVoucherAsync(journalVoucherId, dcDate, reference);
                await CreateDeliveryChallanEntryAsync(deliveryChallanVoucher.DeliveryChallanVoucherId, entry.JournalEntryId,
                    dcLocation, dcRemarks, dcOtherRef, dcDate);

                int debitAccountId = await GetDebitAccountIdAsync(originalJV, entry, postToCustomer);
                var cartageVoucher = await CreateCartageJournalVoucherAsync(originalJV.ProjectId, debitAccountId,
                    transporterId, entry, amount, dcOtherRef);

                await CreateJournalVoucherReferenceAsync(originalJV.JournalVoucherId, cartageVoucher.JournalVoucherId, "Cartage");

                await transaction.CommitAsync();

                _logger.LogInformation("Processed cartage and delivery for journal voucher {JournalVoucherId}, entry {EntryId}",
                    journalVoucherId, entryId);

                return deliveryChallanVoucher;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to process cartage and delivery for journal voucher {JournalVoucherId}, entry {EntryId}",
                    journalVoucherId, entryId);
                throw;
            }
        }

        private async Task<JournalVoucher> CreateCartageJournalVoucherAsync(
            int projectId, int debitAccountId, int transporterId, JournalEntry originalEntry, int amount, string? otherRef)
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
                {
                    await _unitOfWork.JournalEntries.AddAsync(entry);
                }

                await _unitOfWork.SaveChangesAsync();
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
                EntryDate = DateOnly.FromDateTime(DateTime.Now),
                IsCompleted = false,
                CreditableDate = DateOnly.FromDateTime(DateTime.Now)
            };

            cartageJV = await _unitOfWork.JournalVouchers.AddAsync(cartageJV);
            await _unitOfWork.SaveChangesAsync();
            return cartageJV;
        }

        private async Task<(int debitCategoryId, int cartageCategoryId)> GetCartageAccountCategoriesAsync(int debitAccountId, int projectId)
        {
            var debitAccount = await _unitOfWork.Accounts.GetByIdAsync(debitAccountId);
            if (debitAccount == null)
            {
                throw new InvalidOperationException($"Debit account {debitAccountId} not found for cartage entry");
            }

            var projectSettings = await _unitOfWork.ProjectCartageSettings.GetByProjectIdAsync(projectId);
            if (projectSettings?.CartageTransporterCategoryId == null)
            {
                throw new InvalidOperationException($"Cartage transporter category not configured for project {projectId}");
            }

            return (debitAccount.CategoryId, projectSettings.CartageTransporterCategoryId.Value);
        }

        private static JournalEntry CreateCartageDebitEntry(
            int journalVoucherId, int accountId, int categoryId, JournalEntry originalEntry, int amount, string? otherRef)
        {
            return new JournalEntry
            {
                JournalVoucherId = journalVoucherId,
                AccountId = accountId,
                CategoryId = categoryId,
                Description = BuildCartageDescription("Debit", originalEntry.JournalVoucherId, otherRef),
                Quantity = 1,
                Rate = amount,
                Debit = amount,
                Credit = 0,
                Discount = 0,
                DiscountPercent = 0,
                Rbp = "N",
                IsProcessed = false
            };
        }

        private static JournalEntry CreateCartageCreditEntry(
            int journalVoucherId, int transporterId, int cartageCategoryId, JournalEntry originalEntry, int amount, string? otherRef)
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
                IsProcessed = false
            };
        }

        private static string BuildCartageDescription(string entryType, int originalVoucherId, string? otherRef)
        {
            var description = $"Cartage {entryType} - Ref to JV-{originalVoucherId}";
            if (!string.IsNullOrWhiteSpace(otherRef))
            {
                description += $" OtherRef-{otherRef}";
            }
            return description;
        }

        #endregion

        #region FOR Charges Processing

        public async Task<JournalVoucher> ProcessForChargesAsync(
            int journalVoucherId,
            int? entryId,
            int amount,
            DateOnly dcDate)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                var originalJV = await GetValidatedJournalVoucherAsync(journalVoucherId);
                var (forAccountId, forCategoryId) = await GetForChargesAccountAsync(originalJV.ProjectId);
                var (customerAccountId, customerCategoryId) = await GetCustomerAccountFromVoucherAsync(originalJV);

                var forJV = await CreateForChargesVoucherAsync(originalJV.ProjectId, dcDate);
                await CreateJournalVoucherReferenceAsync(journalVoucherId, forJV.JournalVoucherId, "ForCharges");

                await CreateForChargesEntriesAsync(forJV.JournalVoucherId, forAccountId, forCategoryId,
                    customerAccountId, customerCategoryId, amount, journalVoucherId, entryId);

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

        private async Task<JournalVoucher> CreateForChargesVoucherAsync(int projectId, DateOnly dcDate)
        {
            var forJV = new JournalVoucher
            {
                ProjectId = projectId,
                EntryDate = dcDate,
                CreditableDate = dcDate,
                IsCompleted = false
            };

            forJV = await _unitOfWork.JournalVouchers.AddAsync(forJV);
            await _unitOfWork.SaveChangesAsync();
            return forJV;
        }

        private async Task<(int accountId, int categoryId)> GetForChargesAccountAsync(int projectId)
        {
            var projectSettings = await _unitOfWork.ProjectCartageSettings.GetByProjectIdAsync(projectId);
            if (projectSettings?.ForExpenseAccountId == null)
            {
                throw new InvalidOperationException($"FOR expense account not configured for project {projectId}");
            }

            var forAccount = await _unitOfWork.Accounts.GetByIdWithDetailsAsync(projectSettings.ForExpenseAccountId.Value);
            if (forAccount == null)
            {
                throw new InvalidOperationException($"FOR account {projectSettings.ForExpenseAccountId.Value} not found");
            }

            return (forAccount.AccountId, forAccount.CategoryId);
        }

        private async Task<(int accountId, int categoryId)> GetCustomerAccountFromVoucherAsync(JournalVoucher voucher)
        {
            foreach (var entry in voucher.JournalEntries)
            {
                var account = await _unitOfWork.Accounts.GetByIdWithDetailsAsync(entry.AccountId);
                if (account?.AccountGroupId == 1)
                {
                    return (account.AccountId, entry.CategoryId);
                }
            }

            throw new InvalidOperationException($"No customer account found in journal voucher {voucher.JournalVoucherId}");
        }

        private async Task CreateForChargesEntriesAsync(
            int voucherId,
            int forAccountId,
            int forCategoryId,
            int customerAccountId,
            int customerCategoryId,
            int amount,
            int originalVoucherId,
            int? entryId)
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
                IsProcessed = false
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
                IsProcessed = false
            };

            await _unitOfWork.JournalEntries.AddAsync(debitEntry);
            await _unitOfWork.JournalEntries.AddAsync(creditEntry);
        }

        private static string BuildForChargesDescription(string entryType, int originalVoucherId, int? entryId)
        {
            var description = $"FOR Charges {entryType} - Ref to JV-{originalVoucherId}";
            if (entryId.HasValue)
            {
                description += $" Entry-{entryId}";
            }
            return description;
        }

        #endregion

        #region Validation & Mapping Helpers

        private async Task<JournalVoucher> GetValidatedJournalVoucherAsync(int journalVoucherId)
        {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null)
            {
                throw new ArgumentException($"Journal voucher {journalVoucherId} not found");
            }
            return voucher;
        }

        private async Task<JournalEntry> GetValidatedJournalEntryAsync(int entryId)
        {
            var entry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(entryId);
            if (entry == null)
            {
                throw new ArgumentException($"Journal entry {entryId} not found");
            }
            return entry;
        }

        private async Task<(DeliveryChallanVoucher deliveryChallan, JournalEntry journalEntry, Location location, DeliveryChallanEntry? existingEntry)>
            ValidateDeliveryChallanEntryDataAsync(int deliveryChallanId, int journalEntryId, int locationId)
        {
            var deliveryChallan = await _unitOfWork.DeliveryChallanVouchers.GetByIdAsync(deliveryChallanId)
                ?? throw new ArgumentException($"Delivery challan voucher {deliveryChallanId} not found");

            var journalEntry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(journalEntryId)
                ?? throw new ArgumentException($"Journal entry {journalEntryId} not found");

            var location = await _unitOfWork.Locations.GetByIdAsync(locationId)
                ?? throw new ArgumentException($"Location {locationId} not found");

            var existingEntry = await _unitOfWork.DeliveryChallanEntries.GetByDeliveryChallanIdAndJournalEntryIdAsync(
                deliveryChallanId, journalEntryId);

            return (deliveryChallan, journalEntry, location, existingEntry);
        }

        private async Task<int> GetDebitAccountIdAsync(JournalVoucher originalJV, JournalEntry entry, bool postToCustomer)
        {
            if (!postToCustomer)
            {
                return entry.AccountId;
            }

            foreach (var journalEntry in originalJV.JournalEntries)
            {
                var account = await _unitOfWork.Accounts.GetByIdAsync(journalEntry.AccountId);
                if (account?.AccountGroupId == 1)
                {
                    return account.AccountId;
                }
            }

            throw new InvalidOperationException($"No customer account found in journal voucher {originalJV.JournalVoucherId}");
        }

        private async Task<JournalVoucherReference> CreateJournalVoucherReferenceAsync(
            int mainVoucherId, int referencedVoucherId, string referenceType)
        {
            var reference = new JournalVoucherReference
            {
                MainVoucherId = mainVoucherId,
                ReferencedVoucherId = referencedVoucherId,
                ReferenceType = referenceType
            };

            var result = await _unitOfWork.JournalVoucherReferences.AddAsync(reference);
            await _unitOfWork.SaveChangesAsync();
            return result;
        }

        private static JournalEntryDetailViewModel MapToJournalEntryDetailViewModel(JournalEntry entry)
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
                IsProcessed = entry.IsProcessed
            };
        }

        private static JournalVoucherViewModel MapToJournalVoucherViewModel(JournalVoucher voucher)
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
}