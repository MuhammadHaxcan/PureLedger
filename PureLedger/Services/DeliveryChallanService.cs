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
        private readonly int _transporterCategoryId;

        public DeliveryChallanService(
            IUnitOfWork unitOfWork,
            ILogger<DeliveryChallanService> logger,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            
            // Get the transporter category ID from configuration
            _transporterCategoryId = configuration.GetValue<int>("AppSettings:TransporterCategoryId");
            _logger.LogInformation("Transporter category ID set to {TransporterCategoryId}", _transporterCategoryId);
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
                // Get journal vouchers based on filters
                var vouchers = await _unitOfWork.JournalVouchers.GetFilteredVouchersAsync(
                    sinceDate, toDate, projectId, !accountId.HasValue && !categoryId.HasValue);

                // Process only unprocessed vouchers that are NOT base vouchers
                var unprocessedVouchers = vouchers.Where(v => !v.IsCompleted && !v.IsBaseVoucher).ToList();

                _logger.LogInformation("Found {TotalCount} vouchers, {UnprocessedCount} unprocessed non-base vouchers", 
                    vouchers.Count, unprocessedVouchers.Count);

                // Process each voucher sequentially to avoid DbContext concurrency issues
                foreach (var voucher in unprocessedVouchers)
                {
                    var voucherViewModel = MapToJournalVoucherViewModel(voucher);

                    // Get entries for this voucher
                    var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(
                        voucher.JournalVoucherId, categoryId, accountId);

                    // Add only unprocessed entries with quantity > 0
                    foreach (var entry in entries.Where(e => !e.IsProcessed && e.Account.AccountGroupId != 1))
                    {
                        voucherViewModel.Entries.Add(MapToJournalEntryDetailViewModel(entry));
                    }

                    // Add voucher to results if it has any entries
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
                // Check if journal voucher exists
                var journalVoucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
                if (journalVoucher == null)
                {
                    _logger.LogError("Journal voucher with ID {JournalVoucherId} not found", journalVoucherId);
                    throw new ArgumentException($"Journal voucher with ID {journalVoucherId} not found");
                }

                // Check if delivery challan voucher already exists for this journal voucher
                var existingDC = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(journalVoucherId);
                if (existingDC != null)
                {
                    _logger.LogInformation("Delivery challan voucher already exists for journal voucher ID {JournalVoucherId}", journalVoucherId);

                    // Update reference if provided and different
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

                // **CRITICAL FIX**: Refresh the entity from the database to get the proper ID
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
            // Use current date as default if no specific date is provided
            return await CreateDeliveryChallanEntryAsync(
                deliveryChallanId, journalEntryId, locationId, remarks, otherReference, DateOnly.FromDateTime(DateTime.Now));
        }

        public async Task<DeliveryChallanEntry> CreateDeliveryChallanEntryAsync(
            int deliveryChallanId, int journalEntryId, int locationId, string? remarks, string? otherReference, DateOnly entryDate)
        {
            try
            {
                // Validate all required entities in a single operation to reduce DB calls
                var (deliveryChallan, journalEntry, location, existingEntry) = await ValidateDeliveryChallanEntryDataAsync(
                    deliveryChallanId, journalEntryId, locationId);

                if (existingEntry != null)
                {
                    // Update existing entry
                    existingEntry.Remarks = remarks;
                    existingEntry.OtherReference = otherReference;
                    existingEntry.LocationId = locationId;
                    existingEntry.EntryDate = entryDate; // Use the provided date

                    await _unitOfWork.DeliveryChallanEntries.UpdateAsync(existingEntry);
                    await _unitOfWork.SaveChangesAsync();

                    _logger.LogInformation("Updated delivery challan entry with ID {DCEntryId}", existingEntry.DeliveryChallanEntryId);
                    return existingEntry;
                }

                // Create new entry
                var deliveryChallanEntry = new DeliveryChallanEntry
                {
                    DeliveryChallanVoucherId = deliveryChallanId,
                    JournalEntryId = journalEntryId,
                    LocationId = locationId,
                    Remarks = remarks,
                    OtherReference = otherReference,
                    EntryDate = entryDate // Use the provided date
                };

                var result = await _unitOfWork.DeliveryChallanEntries.AddAsync(deliveryChallanEntry);

                // Update journal entry status
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

                // Check if there are any entries
                if (!deliveryChallan.Entries.Any())
                {
                    _logger.LogError("Cannot complete delivery challan with ID {DeliveryChallanId} as it has no entries", deliveryChallanId);
                    return false;
                }

                deliveryChallan.IsCompleted = true;
                deliveryChallan.FinalDeliveryDate = finalDeliveryDate;

                await _unitOfWork.DeliveryChallanVouchers.UpdateAsync(deliveryChallan);

                // Check if all journal entries in the voucher are processed
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

            // Load accounts for all entries
            var accountIds = voucher.JournalEntries.Select(e => e.AccountId).Distinct().ToList();
            var accounts = new Dictionary<int, int>(); // AccountId -> AccountGroupId
            foreach (var accountId in accountIds)
            {
                var account = await _unitOfWork.Accounts.GetByIdWithDetailsAsync(accountId);
                if (account != null && account.AccountGroupId != null)
                    accounts[accountId] = account.AccountGroupId;
            }

            // Get all group 2 entries
            var group2Entries = voucher.JournalEntries
                .Where(e => accounts.TryGetValue(e.AccountId, out var groupId) && groupId == 2)
                .ToList();

            // Only proceed if there is at least one group 2 entry
            if (group2Entries.Count == 0)
                return;

            // Check if all group 2 entries are processed
            bool allGroup2Processed = group2Entries.All(e => e.IsProcessed);

            // Only if all group 2 entries are processed, process group 1 entries and mark voucher as completed
            if (allGroup2Processed)
            {
                // Set all AccountGroupId == 1 entries to processed
                var group1Entries = voucher.JournalEntries
                    .Where(e => accounts.TryGetValue(e.AccountId, out var groupId) && groupId == 1 && !e.IsProcessed)
                    .ToList();

                foreach (var entry in group1Entries)
                {
                    entry.IsProcessed = true;
                    await _unitOfWork.JournalEntries.UpdateAsync(entry);
                }

                // Mark voucher as completed if not already
                if (!voucher.IsCompleted)
                {
                    voucher.IsCompleted = true;
                    await _unitOfWork.JournalVouchers.UpdateAsync(voucher);

                    // NEW CODE: If this voucher has a cartage voucher reference, process that cartage voucher
                    await ProcessCartageVoucherAsync(voucher.JournalVoucherId);
                }

                // NEW CODE: Mark the corresponding delivery challan voucher as completed
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
                    // Don't rethrow - we want to continue with the rest of the process
                }

                await _unitOfWork.SaveChangesAsync();
            }
            else
            {
                // If not all group 2 entries are processed, ensure voucher is not completed
                if (voucher.IsCompleted)
                {
                    voucher.IsCompleted = false;
                    await _unitOfWork.JournalVouchers.UpdateAsync(voucher);
                    await _unitOfWork.SaveChangesAsync();
                }
            }
        }

        // Add this new method to process the cartage voucher
        private async Task ProcessCartageVoucherAsync(int mainVoucherId)
        {
            try
            {
                // Find all cartage vouchers referenced by this voucher
                var references = await _unitOfWork.JournalVoucherReferences.GetByMainVoucherIdAndTypeAsync(mainVoucherId, "Cartage");
                
                foreach (var reference in references)
                {
                    var cartageVoucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(reference.ReferencedVoucherId);
                    if (cartageVoucher == null)
                    {
                        _logger.LogError("Referenced cartage voucher with ID {CartageVoucherId} not found", reference.ReferencedVoucherId);
                        continue;
                    }

                    // Mark all entries as processed
                    foreach (var entry in cartageVoucher.JournalEntries)
                    {
                        entry.IsProcessed = true;
                        await _unitOfWork.JournalEntries.UpdateAsync(entry);
                    }

                    // Mark the cartage voucher as completed
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
            // Begin a transaction with default isolation level
            using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                // Get required data
                var originalJV = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
                if (originalJV == null) throw new ArgumentException("Original JV not found");

                var entry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(entryId);
                if (entry == null) throw new ArgumentException("Journal entry not found");

                // Get debit account ID based on postToCustomer flag
                int debitAccountId = await GetDebitAccountIdAsync(originalJV, entry, postToCustomer);

                // Update the original entry first
                entry.IsProcessed = true;
                await _unitOfWork.JournalEntries.UpdateAsync(entry);

                // Create or get delivery challan voucher
                var deliveryChallanVoucher = await CreateDeliveryChallanVoucherAsync(journalVoucherId, dcDate, reference);

                // Create delivery challan entry with correct date
                await CreateDeliveryChallanEntryAsync(
                    deliveryChallanVoucher.DeliveryChallanVoucherId,
                    entry.JournalEntryId,
                    dcLocation,
                    dcRemarks,
                    dcOtherRef,
                    dcDate);

                // Create cartage journal voucher and entries as the last step
                var cartageVoucher = await CreateCartageJournalVoucherAsync(
                    originalJV.ProjectId, debitAccountId, transporterId, entry, amount, dcOtherRef);
                    
                // Set the reference in the original voucher
                await CreateJournalVoucherReferenceAsync(originalJV.JournalVoucherId, cartageVoucher.JournalVoucherId, "Cartage");

                // Update journal voucher processed status
                await UpdateJournalVoucherProcessedStatusAsync(journalVoucherId);

                // Commit transaction
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
            int projectId, int debitAccountId, int transporterId, JournalEntry originalEntry, int amount, string otherref)
        {
            try
            {
                // Create cartage journal voucher
                var cartageJV = new JournalVoucher
                {
                    ProjectId = projectId,
                    EntryDate = DateOnly.FromDateTime(DateTime.Now),
                    IsCompleted = false, // Changed from true to false - cartage voucher not processed by default
                    CreditableDate = DateOnly.FromDateTime(DateTime.Now)
                };

                // Save the voucher first
                cartageJV = await _unitOfWork.JournalVouchers.AddAsync(cartageJV);
                await _unitOfWork.SaveChangesAsync();

                // Fetch the correct category for the debit account
                var debitAccount = await _unitOfWork.Accounts.GetByIdAsync(debitAccountId);
                if (debitAccount == null)
                    throw new InvalidOperationException("Debit account not found for cartage entry.");

                int debitCategoryId = debitAccount.CategoryId;

                // Create both entries at once to avoid gaps
                var entries = new List<JournalEntry>
                {
                    CreateCartageDebitEntry(cartageJV.JournalVoucherId, debitAccountId, debitCategoryId, originalEntry, amount,otherref),
                    CreateCartageCreditEntry(cartageJV.JournalVoucherId, transporterId, originalEntry, amount,otherref)
                };

                // Add entries in batch
                foreach (var entry in entries)
                {
                    // Entries should not be marked as processed initially
                    entry.IsProcessed = false; // Changed from true to false
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

        private static JournalEntry CreateCartageDebitEntry(int journalVoucherId, int accountId, int categoryId, JournalEntry originalEntry, int amount,string otherref)
        {
            return new JournalEntry
            {
                JournalVoucherId = journalVoucherId,
                AccountId = accountId,
                CategoryId = categoryId,
                Description = $"Cartage Debit - Ref to JV-{originalEntry.JournalVoucherId} OtherRef-{otherref}",
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

        private JournalEntry CreateCartageCreditEntry(int journalVoucherId, int transporterId, JournalEntry originalEntry, int amount, string otherref)
        {
            return new JournalEntry
            {
                JournalVoucherId = journalVoucherId,
                AccountId = transporterId,
                CategoryId = _transporterCategoryId, // Use the instance field
                Description = $"Cartage Credit - Ref to JV-{originalEntry.JournalVoucherId} OtherRef-{otherref}",
                Quantity = 1,
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
            // Get all required entities sequentially instead of in parallel
            var deliveryChallan = await _unitOfWork.DeliveryChallanVouchers.GetByIdAsync(deliveryChallanId);
            var journalEntry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(journalEntryId);
            var location = await _unitOfWork.Locations.GetByIdAsync(locationId);
            var existingEntry = await _unitOfWork.DeliveryChallanEntries.GetByDeliveryChallanIdAndJournalEntryIdAsync(
                deliveryChallanId, journalEntryId);

            // Check results
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
                // Find customer account (AccountGroupId == 1)
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
                // Always use the account of the entry being processed
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

        private async Task CompleteVoucherIfAllGroup2ProcessedAsync(int journalVoucherId)
        {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null || voucher.JournalEntries == null || !voucher.JournalEntries.Any())
                return;

            // Load accounts for all entries
            var accountIds = voucher.JournalEntries.Select(e => e.AccountId).Distinct().ToList();
            var accounts = new Dictionary<int, int>(); // AccountId -> AccountGroupId
            foreach (var accountId in accountIds)
            {
                var account = await _unitOfWork.Accounts.GetByIdWithDetailsAsync(accountId);
                if (account != null && account.AccountGroup != null)
                    accounts[accountId] = account.AccountGroup.AccountGroupId;
            }

            // Check if all AccountGroupId == 2 entries are processed
            var group2Entries = voucher.JournalEntries.Where(e => accounts.TryGetValue(e.AccountId, out var groupId) && groupId == 2).ToList();
            bool allGroup2Processed = group2Entries.All(e => e.IsProcessed);

            if (allGroup2Processed)
            {
                // Set all AccountGroupId == 1 entries to processed
                var group1Entries = voucher.JournalEntries.Where(e => accounts.TryGetValue(e.AccountId, out var groupId) && groupId == 1 && !e.IsProcessed).ToList();
                foreach (var entry in group1Entries)
                {
                    entry.IsProcessed = true;
                    await _unitOfWork.JournalEntries.UpdateAsync(entry);
                }

                // Mark voucher as completed if not already
                if (!voucher.IsCompleted)
                {
                    voucher.IsCompleted = true;
                    await _unitOfWork.JournalVouchers.UpdateAsync(voucher);
                }

                await _unitOfWork.SaveChangesAsync();
            }
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
                // Access the Categories repository directly to get categories by project ID
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
    }
}