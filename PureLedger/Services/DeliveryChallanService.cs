using Microsoft.EntityFrameworkCore;
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
            ILogger<DeliveryChallanService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        #region Basic Queries

        public async Task<List<Project>> GetAllProjectsAsync() =>
            await _unitOfWork.Projects.GetAllAsync();

        public async Task<List<Models.Location>> GetAllLocationsAsync() =>
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

            return entries.Select(e => new JournalEntryDetailViewModel
            {
                EntryId = e.EntryId,
                JournalVoucherId = e.JournalVoucherId,
                AccountId = e.AccountId,
                AccountName = e.Account?.AccountName,
                Description = e.Description,
                Quantity = e.Quantity,
                Rate = e.Rate,
                IsProcessed = e.IsProcessed
            }).ToList();
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
            int? projectId,
            int? categoryId,
            int? accountId)
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

                // Process only unprocessed vouchers
                var unprocessedVouchers = vouchers.Where(v => !v.IsProcessed).ToList();

                // Process each voucher sequentially to avoid DbContext concurrency issues
                foreach (var voucher in unprocessedVouchers)
                {
                    var voucherViewModel = new JournalVoucherViewModel
                    {
                        JvId = voucher.JvId,
                        ProjectName = voucher.Project?.ProjectName,
                        EntryDate = voucher.EntryDate,
                        IsProcessed = voucher.IsProcessed,
                        Entries = new List<JournalEntryDetailViewModel>()
                    };

                    // Get entries for this voucher
                    var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(
                        voucher.JvId, categoryId, accountId);

                    // Add only unprocessed entries with quantity > 0
                    foreach (var entry in entries.Where(e => !e.IsProcessed && e.Quantity > 0))
                    {
                        voucherViewModel.Entries.Add(new JournalEntryDetailViewModel
                        {
                            EntryId = entry.EntryId,
                            JournalVoucherId = entry.JournalVoucherId,
                            AccountId = entry.AccountId,
                            AccountName = entry.Account?.AccountName,
                            Description = entry.Description,
                            Quantity = entry.Quantity,
                            Rate = entry.Rate,
                            IsProcessed = entry.IsProcessed
                        });
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

                _logger.LogInformation("Created new delivery challan voucher with ID {DeliveryChallanId} for journal voucher ID {JournalVoucherId}",
                    result.DeliveryChallanId, journalVoucherId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating delivery challan voucher for journal voucher ID {JournalVoucherId}", journalVoucherId);
                throw;
            }
        }

        public async Task<DeliveryChallanEntry> CreateDeliveryChallanEntryAsync(
            int deliveryChallanId, int journalEntryId, int locationId, string remarks, string otherReference)
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
                    existingEntry.EntryDate = DateOnly.FromDateTime(DateTime.Now);

                    await _unitOfWork.DeliveryChallanEntries.UpdateAsync(existingEntry);
                    await _unitOfWork.SaveChangesAsync();

                    _logger.LogInformation("Updated delivery challan entry with ID {DCEntryId}", existingEntry.DCEntryId);
                    return existingEntry;
                }

                // Create new entry
                var deliveryChallanEntry = new DeliveryChallanEntry
                {
                    DeliveryChallanId = deliveryChallanId,
                    JournalEntryId = journalEntryId,
                    LocationId = locationId,
                    Remarks = remarks,
                    OtherReference = otherReference,
                    EntryDate = DateOnly.FromDateTime(DateTime.Now)
                };

                var result = await _unitOfWork.DeliveryChallanEntries.AddAsync(deliveryChallanEntry);

                // Update journal entry status
                journalEntry.IsProcessed = true;
                await _unitOfWork.JournalEntries.UpdateAsync(journalEntry);

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Created new delivery challan entry with ID {DCEntryId}", result.DCEntryId);
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
                if (!deliveryChallan.DeliveryChallanEntries.Any())
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
            try
            {
                // Get the journal voucher with all its entries
                var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
                if (voucher == null)
                {
                    _logger.LogWarning("Cannot update processed status: Journal voucher {JournalVoucherId} not found", journalVoucherId);
                    return;
                }

                // Check if all entries that have quantity > 0 are processed
                bool allEntriesProcessed = voucher.JournalEntries
                    .Where(e => e.Quantity > 0)
                    .All(e => e.IsProcessed);

                // Update the voucher's IsProcessed status only if there's a change
                if (allEntriesProcessed != voucher.IsProcessed)
                {
                    voucher.IsProcessed = allEntriesProcessed;
                    await _unitOfWork.JournalVouchers.UpdateAsync(voucher);
                    await _unitOfWork.SaveChangesAsync();

                    _logger.LogInformation("Updated journal voucher {JournalVoucherId} processed status to {IsProcessed}",
                        journalVoucherId, allEntriesProcessed);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating journal voucher processed status for voucher {JournalVoucherId}", journalVoucherId);
                throw;
            }
        }

        #endregion

        #region Cartage Processing

        public async Task<DeliveryChallanVoucher> ProcessCartageAndDeliveryAsync(
            int journalVoucherId,
            int entryId,
            int transporterId,
            decimal amount,
            bool postToCustomer,
            string reference,
            DateOnly dcDate,
            string dcRemarks,
            string dcStation,
            string dcOtherRef,
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
                entry.LocationId = dcLocation;
                entry.IsProcessed = true;
                await _unitOfWork.JournalEntries.UpdateAsync(entry);
                
                // Create or get delivery challan voucher
                var deliveryChallanVoucher = await CreateDeliveryChallanVoucherAsync(journalVoucherId, dcDate);

                // Create delivery challan entry
                await CreateDeliveryChallanEntryAsync(
                    deliveryChallanVoucher.DeliveryChallanId,
                    entry.EntryId,
                    dcLocation,
                    dcRemarks,
                    dcOtherRef);

                // Create cartage journal voucher and entries as the last step
                await CreateCartageJournalVoucherAsync(
                    originalJV.ProjectId, debitAccountId, transporterId, entry, amount);

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
            int projectId, int debitAccountId, int transporterId, JournalEntry originalEntry, decimal amount)
        {
            try
            {
                // Create cartage journal voucher
                var cartageJV = new JournalVoucher
                {
                    ProjectId = projectId,
                    EntryDate = DateOnly.FromDateTime(DateTime.Now),
                    IsProcessed = true,
                    CreditableDate = DateOnly.FromDateTime(DateTime.Now)
                };
                
                // Save the voucher first
                cartageJV = await _unitOfWork.JournalVouchers.AddAsync(cartageJV);
                await _unitOfWork.SaveChangesAsync();

                // Create both entries at once to avoid gaps
                var entries = new List<JournalEntry>
                {
                    // Debit entry
                    new JournalEntry
                    {
                        JournalVoucherId = cartageJV.JvId,
                        AccountId = debitAccountId,
                        CategoryId = originalEntry.CategoryId,
                        Description = $"Cartage Debit - Ref to JV-{originalEntry.JournalVoucherId}",
                        Quantity = 1,
                        Rate = amount,
                        Debit = amount,
                        Credit = 0,
                        Discount = 0,
                        DiscountPercent = 0,
                        Rbp = "N",
                        LocationId = null,
                        IsProcessed = true
                    },
                    // Credit entry
                    new JournalEntry
                    {
                        JournalVoucherId = cartageJV.JvId,
                        AccountId = transporterId,
                        CategoryId = 2, // Transporter category
                        Description = $"Cartage Credit - Ref to JV-{originalEntry.JournalVoucherId}",
                        Quantity = 1,
                        Rate = amount,
                        Debit = 0,
                        Credit = amount,
                        Discount = 0,
                        DiscountPercent = 0,
                        Rbp = "N",
                        LocationId = null,
                        IsProcessed = true
                    }
                };

                // Add entries in batch
                foreach (var entry in entries)
                {
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

        #endregion

        #region Helper Methods

        private async Task<(DeliveryChallanVoucher, JournalEntry, Models.Location, DeliveryChallanEntry)>
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
            if (!postToCustomer)
            {
                return entry.AccountId;
            }

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

        #endregion
    }
}