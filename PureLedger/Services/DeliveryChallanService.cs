using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;
using PureLedger.ViewModels;

namespace PureLedger.Services
{
    public class DeliveryChallanService : IDeliveryChallanService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DeliveryChallanService> _logger;

        public DeliveryChallanService(
            IUnitOfWork unitOfWork,
            ILogger<DeliveryChallanService> logger
            )
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        #region Entity Retrieval

        public async Task<List<Project>> GetAllProjectsAsync() =>
            await _unitOfWork.Projects.GetAllAsync();

        public async Task<List<Project>> GetAllProjectsForUserAsync(int userId) =>
            await _unitOfWork.Projects.GetAllForUserAsync(userId);

        public async Task<List<Location>> GetAllLocationsAsync() =>
            await _unitOfWork.Locations.GetAllAsync();

        public async Task<JournalEntry> GetJournalEntryByIdAsync(int journalEntryId) =>
            await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(journalEntryId);

        public async Task<DeliveryChallanVoucher> GetDeliveryChallanVoucherByIdAsync(int deliveryChallanId) =>
            await _unitOfWork.DeliveryChallanVouchers.GetByIdWithEntriesAsync(deliveryChallanId);

        public async Task<DeliveryChallanVoucher> GetDeliveryChallanVoucherByJournalVoucherIdAsync(int journalVoucherId)
        {
            try
            {
                return await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(journalVoucherId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting delivery challan voucher for journal voucher ID {JournalVoucherId}", journalVoucherId);
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
                _logger.LogError(ex, "Error getting delivery challan vouchers for date range {StartDate} to {EndDate}", startDate, endDate);
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
                _logger.LogError(ex, "Error getting delivery challan entries for voucher ID {DeliveryChallanId}", deliveryChallanId);
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
                _logger.LogError(ex, "Error getting unprocessed entries for voucher {VoucherId}", voucherId);
                throw;
            }
        }

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

                var vouchers = await _unitOfWork.JournalVouchers.GetFilteredVouchersAsync(
                    sinceDate, toDate, projectId, !accountId.HasValue && !categoryId.HasValue);

                var unprocessedVouchers = vouchers.Where(v => !v.IsCompleted && !v.IsBaseVoucher).ToList();

                _logger.LogInformation("Found {TotalCount} vouchers, {UnprocessedCount} unprocessed non-base vouchers",
                    vouchers.Count, unprocessedVouchers.Count);

                foreach (var voucher in unprocessedVouchers)
                {
                    var voucherViewModel = MapToJournalVoucherViewModel(voucher);

                    var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(
                        voucher.JournalVoucherId, categoryId, accountId);

                    foreach (var entry in entries.Where(e => !e.IsProcessed && e.Account.AccountGroupId != 1))
                    {
                        voucherViewModel.Entries.Add(MapToJournalEntryDetailViewModel(entry));
                    }

                    if (voucherViewModel.Entries.Any())
                    {
                        result.Vouchers.Add(voucherViewModel);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting filtered entries for delivery challan");
            }

            return result;
        }

        #endregion

        #region Create & Update Operations

        public async Task<DeliveryChallanVoucher> CreateDeliveryChallanVoucherAsync(int journalVoucherId, DateOnly dcDate)
        {
            return await CreateDeliveryChallanVoucherAsync(journalVoucherId, dcDate, null);
        }

        public async Task<DeliveryChallanVoucher> CreateDeliveryChallanVoucherAsync(int journalVoucherId, DateOnly dcDate, string? reference)
        {
            try
            {

                var journalVoucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
                if (journalVoucher == null)
                {
                    _logger.LogError("Journal voucher with ID {JournalVoucherId} not found", journalVoucherId);
                    throw new ArgumentException($"Journal voucher with ID {journalVoucherId} not found");
                }

                var existingDC = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(journalVoucherId);
                if (existingDC != null)
                {
                    _logger.LogInformation("Delivery challan voucher already exists for journal voucher ID {JournalVoucherId}", journalVoucherId);

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

                var result = await _unitOfWork.DeliveryChallanVouchers.AddAsync(deliveryChallanVoucher);
                await _unitOfWork.SaveChangesAsync();

                var savedVoucher = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(journalVoucherId);
                if (savedVoucher == null)
                {
                    _logger.LogError("Failed to retrieve saved delivery challan voucher for journal voucher ID {JournalVoucherId}", journalVoucherId);
                    throw new InvalidOperationException($"Failed to save delivery challan voucher for journal voucher ID {journalVoucherId}");
                }

                _logger.LogInformation("Created new delivery challan voucher with ID {DeliveryChallanId} for journal voucher ID {JournalVoucherId}",
                    savedVoucher.DeliveryChallanVoucherId, journalVoucherId);

                return savedVoucher;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating delivery challan voucher for journal voucher ID {JournalVoucherId}", journalVoucherId);
                throw;
            }
        }

        public async Task<DeliveryChallanEntry> CreateDeliveryChallanEntryAsync(
            int deliveryChallanId, int journalEntryId, int locationId, string? remarks, string? otherReference)
        {

            return await CreateDeliveryChallanEntryAsync(
                deliveryChallanId, journalEntryId, locationId, remarks, otherReference, DateOnly.FromDateTime(DateTime.Now));
        }

        public async Task<DeliveryChallanEntry> CreateDeliveryChallanEntryAsync(
            int deliveryChallanId, int journalEntryId, int locationId, string? remarks, string? otherReference, DateOnly entryDate)
        {
            try
            {

                var (deliveryChallan, journalEntry, location, existingEntry) = await ValidateDeliveryChallanEntryDataAsync(
                    deliveryChallanId, journalEntryId, locationId);

                if (existingEntry != null)
                {

                    existingEntry.Remarks = remarks;
                    existingEntry.OtherReference = otherReference;
                    existingEntry.LocationId = locationId;
                    existingEntry.EntryDate = entryDate;

                    await _unitOfWork.DeliveryChallanEntries.UpdateAsync(existingEntry);
                    await _unitOfWork.SaveChangesAsync();

                    _logger.LogInformation("Updated delivery challan entry with ID {DCEntryId}", existingEntry.DeliveryChallanEntryId);
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

                _logger.LogInformation("Created new delivery challan entry with ID {DCEntryId} with entry date {EntryDate}",
                    result.DeliveryChallanEntryId, entryDate);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating delivery challan entry for delivery challan ID {DeliveryChallanId} and journal entry ID {JournalEntryId}",
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
                    _logger.LogError("Delivery challan voucher with ID {DeliveryChallanId} not found", deliveryChallanId);
                    return false;
                }

                if (!deliveryChallan.Entries.Any())
                {
                    _logger.LogError("Cannot complete delivery challan with ID {DeliveryChallanId} as it has no entries", deliveryChallanId);
                    return false;
                }

                deliveryChallan.IsCompleted = true;
                deliveryChallan.FinalDeliveryDate = finalDeliveryDate;

                await _unitOfWork.DeliveryChallanVouchers.UpdateAsync(deliveryChallan);

                await UpdateJournalVoucherProcessedStatusAsync(deliveryChallan.JournalVoucherId);

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Completed delivery challan voucher with ID {DeliveryChallanId}", deliveryChallanId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing delivery challan voucher with ID {DeliveryChallanId}", deliveryChallanId);
                return false;
            }
        }

        #endregion

        #region Journal Voucher Processing

        public async Task UpdateJournalVoucherProcessedStatusAsync(int journalVoucherId)
        {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null || voucher.JournalEntries == null || !voucher.JournalEntries.Any())
                return;

            var accountIds = voucher.JournalEntries.Select(e => e.AccountId).Distinct().ToList();
            var accounts = new Dictionary<int, int>();
            foreach (var accountId in accountIds)
            {
                var account = await _unitOfWork.Accounts.GetByIdWithDetailsAsync(accountId);
                if (account != null && account.AccountGroupId != null)
                    accounts[accountId] = account.AccountGroupId;
            }

            var group2Entries = voucher.JournalEntries
                .Where(e => accounts.TryGetValue(e.AccountId, out var groupId) && groupId == 2)
                .ToList();

            if (group2Entries.Count == 0)
                return;

            bool allGroup2Processed = group2Entries.All(e => e.IsProcessed);

            if (allGroup2Processed)
            {

                var group1Entries = voucher.JournalEntries
                    .Where(e => accounts.TryGetValue(e.AccountId, out var groupId) && groupId == 1 && !e.IsProcessed)
                    .ToList();

                foreach (var entry in group1Entries)
                {
                    entry.IsProcessed = true;
                    await _unitOfWork.JournalEntries.UpdateAsync(entry);
                }

                if (!voucher.IsCompleted)
                {
                    voucher.IsCompleted = true;
                    await _unitOfWork.JournalVouchers.UpdateAsync(voucher);

                    await ProcessCartageVoucherAsync(voucher.JournalVoucherId);
                    await ProcessForChargesVoucherAsync(voucher.JournalVoucherId);
                }

                try
                {
                    var deliveryChallanVoucher = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(journalVoucherId);
                    if (deliveryChallanVoucher != null && !deliveryChallanVoucher.IsCompleted)
                    {
                        _logger.LogInformation("Marking delivery challan voucher with ID {DeliveryChallanId} as completed because all group 2 entries are processed",
                            deliveryChallanVoucher.DeliveryChallanVoucherId);

                        deliveryChallanVoucher.IsCompleted = true;
                        deliveryChallanVoucher.FinalDeliveryDate = DateOnly.FromDateTime(DateTime.Now);
                        await _unitOfWork.DeliveryChallanVouchers.UpdateAsync(deliveryChallanVoucher);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error marking delivery challan voucher as completed for journal voucher ID {JournalVoucherId}",
                        journalVoucherId);

                }

                await _unitOfWork.SaveChangesAsync();
            }
            else
            {

                if (voucher.IsCompleted)
                {
                    voucher.IsCompleted = false;
                    await _unitOfWork.JournalVouchers.UpdateAsync(voucher);
                    await _unitOfWork.SaveChangesAsync();
                }
            }
        }

        private async Task ProcessCartageVoucherAsync(int mainVoucherId)
        {
            try
            {

                var references = await _unitOfWork.JournalVoucherReferences.GetByMainVoucherIdAndTypeAsync(mainVoucherId, "Cartage");

                foreach (var reference in references)
                {
                    var cartageVoucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(reference.ReferencedVoucherId);
                    if (cartageVoucher == null)
                    {
                        _logger.LogError("Referenced cartage voucher with ID {CartageVoucherId} not found", reference.ReferencedVoucherId);
                        continue;
                    }

                    foreach (var entry in cartageVoucher.JournalEntries)
                    {
                        entry.IsProcessed = true;
                        await _unitOfWork.JournalEntries.UpdateAsync(entry);
                    }

                    cartageVoucher.IsCompleted = true;
                    await _unitOfWork.JournalVouchers.UpdateAsync(cartageVoucher);

                    _logger.LogInformation("Processed cartage voucher with ID {CartageVoucherId}", reference.ReferencedVoucherId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing cartage vouchers for main voucher with ID {MainVoucherId}", mainVoucherId);
            }
        }

        private async Task ProcessForChargesVoucherAsync(int mainVoucherId)
        {
            try
            {

                var references = await _unitOfWork.JournalVoucherReferences.GetByMainVoucherIdAndTypeAsync(mainVoucherId, "ForCharges");

                foreach (var reference in references)
                {
                    var forChargesVoucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(reference.ReferencedVoucherId);
                    if (forChargesVoucher == null)
                    {
                        _logger.LogError("Referenced FOR charges voucher with ID {ForChargesVoucherId} not found", reference.ReferencedVoucherId);
                        continue;
                    }

                    foreach (var entry in forChargesVoucher.JournalEntries)
                    {
                        entry.IsProcessed = true;
                        await _unitOfWork.JournalEntries.UpdateAsync(entry);
                    }

                    forChargesVoucher.IsCompleted = true;
                    await _unitOfWork.JournalVouchers.UpdateAsync(forChargesVoucher);

                    _logger.LogInformation("Processed FOR charges voucher with ID {ForChargesVoucherId}", reference.ReferencedVoucherId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing FOR charges vouchers for main voucher with ID {MainVoucherId}", mainVoucherId);

            }
        }

        #endregion

        #region Cartage Processing

        public async Task<DeliveryChallanVoucher> ProcessCartageAndDeliveryAsync(CartageViewModel model)
        {
            return await ProcessCartageAndDeliveryAsync(
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
        }

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

                var originalJV = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
                if (originalJV == null) throw new ArgumentException("Original JV not found");

                var entry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(entryId);
                if (entry == null) throw new ArgumentException("Journal entry not found");

                int debitAccountId = await GetDebitAccountIdAsync(originalJV, entry, postToCustomer);

                entry.IsProcessed = true;
                await _unitOfWork.JournalEntries.UpdateAsync(entry);

                var deliveryChallanVoucher = await CreateDeliveryChallanVoucherAsync(journalVoucherId, dcDate, reference);

                await CreateDeliveryChallanEntryAsync(
                    deliveryChallanVoucher.DeliveryChallanVoucherId,
                    entry.JournalEntryId,
                    dcLocation,
                    dcRemarks,
                    dcOtherRef,
                    dcDate);

                var cartageVoucher = await CreateCartageJournalVoucherAsync(
                    originalJV.ProjectId, debitAccountId, transporterId, entry, amount, dcOtherRef);

                await CreateJournalVoucherReferenceAsync(originalJV.JournalVoucherId, cartageVoucher.JournalVoucherId, "Cartage");

                await transaction.CommitAsync();

                return deliveryChallanVoucher;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error processing cartage and delivery for journal voucher {JournalVoucherId}, entry {EntryId}",
                    journalVoucherId, entryId);
                throw;
            }
        }

        private async Task<JournalVoucher> CreateCartageJournalVoucherAsync(
            int projectId, int debitAccountId, int transporterId, JournalEntry originalEntry, int amount, string? otherref)
        {
            try
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

                var debitAccount = await _unitOfWork.Accounts.GetByIdAsync(debitAccountId);
                if (debitAccount == null)
                    throw new InvalidOperationException("Debit account not found for cartage entry.");

                int debitCategoryId = debitAccount.CategoryId;

                var projectSettings = await _unitOfWork.ProjectCartageSettings.GetByProjectIdAsync(originalEntry.JournalVoucher.ProjectId);
                if (projectSettings == null)
                    throw new InvalidOperationException("Project cartage settings not found for this project");

                int CategoryId = projectSettings.CartageTransporterCategoryId ?? 0;
                if (CategoryId == 0)
                    throw new InvalidOperationException("FOR Account ID is not set for this project");

                var entries = new List<JournalEntry>
                {
                    CreateCartageDebitEntry(cartageJV.JournalVoucherId, debitAccountId, debitCategoryId, originalEntry, amount,otherref),
                    CreateCartageCreditEntry(cartageJV.JournalVoucherId, transporterId, originalEntry, amount, CategoryId,otherref)
                };

                foreach (var entry in entries)
                {

                    entry.IsProcessed = false;
                    await _unitOfWork.JournalEntries.AddAsync(entry);
                }

                await _unitOfWork.SaveChangesAsync();
                return cartageJV;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating cartage journal voucher for project ID {ProjectId}", projectId);
                throw;
            }
        }

        private static JournalEntry CreateCartageDebitEntry(int journalVoucherId, int accountId, int categoryId, JournalEntry originalEntry, int amount, string? otherref)
        {
            return new JournalEntry
            {
                JournalVoucherId = journalVoucherId,
                AccountId = accountId,
                CategoryId = categoryId,
                Description = $"Cartage Debit - Ref to JV-{originalEntry.JournalVoucherId}"
                    + (!string.IsNullOrWhiteSpace(otherref) ? $" OtherRef-{otherref}" : ""),
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

        private JournalEntry CreateCartageCreditEntry(int journalVoucherId, int transporterId, JournalEntry originalEntry, int amount, int cartageCategoryId ,string? otherref)
        {
            return new JournalEntry
            {
                JournalVoucherId = journalVoucherId,
                AccountId = transporterId,
                CategoryId = cartageCategoryId,
                Description = $"Cartage Credit - Ref to JV-{originalEntry.JournalVoucherId}"
                    + (!string.IsNullOrWhiteSpace(otherref) ? $" OtherRef-{otherref}" : ""),
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

        #endregion

        #region Helper Methods

        private async Task<(DeliveryChallanVoucher, JournalEntry, Models.Location, DeliveryChallanEntry?)>
            ValidateDeliveryChallanEntryDataAsync(int deliveryChallanId, int journalEntryId, int locationId)
        {

            var deliveryChallan = await _unitOfWork.DeliveryChallanVouchers.GetByIdAsync(deliveryChallanId);
            var journalEntry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(journalEntryId);
            var location = await _unitOfWork.Locations.GetByIdAsync(locationId);
            var existingEntry = await _unitOfWork.DeliveryChallanEntries.GetByDeliveryChallanIdAndJournalEntryIdAsync(
                deliveryChallanId, journalEntryId);

            if (deliveryChallan == null)
            {
                throw new ArgumentException($"Delivery challan voucher with ID {deliveryChallanId} not found");
            }

            if (journalEntry == null)
            {
                throw new ArgumentException($"Journal entry with ID {journalEntryId} not found");
            }

            if (location == null)
            {
                throw new ArgumentException($"Location with ID {locationId} not found");
            }

            return (deliveryChallan, journalEntry, location, existingEntry);
        }

        private async Task<int> GetDebitAccountIdAsync(JournalVoucher originalJV, JournalEntry entry, bool postToCustomer)
        {
            if (postToCustomer)
            {

                foreach (var je in originalJV.JournalEntries)
                {
                    var account = await _unitOfWork.Accounts.GetByIdAsync(je.AccountId);
                    if (account != null && account.AccountGroupId == 1)
                    {
                        return account.AccountId;
                    }
                }
                throw new InvalidOperationException("No customer account found in the journal voucher");
            }
            else
            {

                return entry.AccountId;
            }
        }

        private static JournalEntryDetailViewModel MapToJournalEntryDetailViewModel(JournalEntry entry)
        {
            return new JournalEntryDetailViewModel
            {
                EntryId = entry.JournalEntryId,
                JournalVoucherId = entry.JournalVoucherId,
                AccountId = entry.AccountId,
                AccountName = entry.Account?.AccountName,
                Description = entry.Description,
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
                ProjectName = voucher.Project?.ProjectName,
                EntryDate = voucher.EntryDate,
                IsProcessed = voucher.IsCompleted,
                Entries = new List<JournalEntryDetailViewModel>()
            };
        }

        #endregion

        public async Task<IEnumerable<Account>> GetAccountsByCategoryAsync(int categoryId)
        {
            return await _unitOfWork.Accounts.GetByCategoryIdAsync(categoryId);
        }

        public async Task<IEnumerable<Category>> GetCategoriesByProjectAsync(int projectId)
        {
            try
            {

                return await _unitOfWork.Categories.GetByProjectIdAsync(projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categories for project {ProjectId}", projectId);
                throw;
            }
        }

        private async Task<JournalVoucherReference> CreateJournalVoucherReferenceAsync(int mainVoucherId, int referencedVoucherId, string referenceType)
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

        public async Task<JournalVoucher> ProcessForChargesAsync(
            int journalVoucherId,
            int? entryId,
            int amount,
            DateOnly dcDate)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var originalJV = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
                if (originalJV == null)
                    throw new ArgumentException($"Original journal voucher with ID {journalVoucherId} not found");

                var projectSettings = await _unitOfWork.ProjectCartageSettings.GetByProjectIdAsync(originalJV.ProjectId);
                if (projectSettings == null)
                    throw new InvalidOperationException("Project cartage settings not found for this project");

                int forAccountId = projectSettings.ForExpenseAccountId ?? 0;
                if (forAccountId == 0)
                    throw new InvalidOperationException("FOR Account ID is not set for this project");

                var forAccount = await _unitOfWork.Accounts.GetByIdWithDetailsAsync(forAccountId);
                if (forAccount == null)
                    throw new InvalidOperationException($"FOR account with ID {forAccountId} not found");

                int customerAccountId = 0;
                int customerCategoryId = 0;
                foreach (var je in originalJV.JournalEntries)
                {
                    var account = await _unitOfWork.Accounts.GetByIdWithDetailsAsync(je.AccountId);
                    if (account != null && account.AccountGroupId == 1)
                    {
                        customerAccountId = account.AccountId;
                        customerCategoryId = je.CategoryId;
                        break;
                    }
                }

                if (customerAccountId == 0)
                    throw new InvalidOperationException("No customer account found in the journal voucher");

                var forJV = new JournalVoucher
                {
                    ProjectId = originalJV.ProjectId,
                    EntryDate = dcDate,
                    CreditableDate = dcDate,
                    IsCompleted = false
                };

                forJV = await _unitOfWork.JournalVouchers.AddAsync(forJV);
                await _unitOfWork.SaveChangesAsync();

                await _unitOfWork.JournalVoucherReferences.AddAsync(new JournalVoucherReference
                {
                    MainVoucherId = journalVoucherId,
                    ReferencedVoucherId = forJV.JournalVoucherId,
                    ReferenceType = "ForCharges"
                });

                var debitEntry = new JournalEntry
                {
                    JournalVoucherId = forJV.JournalVoucherId,
                    AccountId = forAccountId,
                    CategoryId = forAccount.CategoryId,
                    Description = $"FOR Charges Debit - Ref to JV-{journalVoucherId}" +
                                  (entryId.HasValue ? $" Entry-{entryId}" : ""),
                    Quantity = 1,
                    Rate = amount,
                    Debit = amount,
                    Credit = 0,
                    Discount = 0,
                    DiscountPercent = 0,
                    Rbp = "N",
                    IsProcessed = false
                };
                await _unitOfWork.JournalEntries.AddAsync(debitEntry);

                var creditEntry = new JournalEntry
                {
                    JournalVoucherId = forJV.JournalVoucherId,
                    AccountId = customerAccountId,
                    CategoryId = customerCategoryId,
                    Description = $"FOR Charges Credit - Ref to JV-{journalVoucherId}" +
                                  (entryId.HasValue ? $" Entry-{entryId}" : ""),
                    Quantity = 0,
                    Rate = amount,
                    Debit = 0,
                    Credit = amount,
                    Discount = 0,
                    DiscountPercent = 0,
                    Rbp = "N",
                    IsProcessed = false
                };
                await _unitOfWork.JournalEntries.AddAsync(creditEntry);

                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Created FOR charges journal voucher {JvId} for amount {Amount}",
                    forJV.JournalVoucherId, amount);

                return forJV;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error processing FOR charges for journal voucher {JvId}", journalVoucherId);
                throw;
            }
        }

        public async Task<ProjectCartageSettings?> GetProjectCartageSettingsAsync(int projectId)
        {
            return await _unitOfWork.ProjectCartageSettings.GetByProjectIdAsync(projectId);
        }
    }
}