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

        public async Task<List<Project>> GetAllProjectsAsync()
        {
            return await _unitOfWork.Projects.GetAllAsync();
        }

        public async Task<List<Models.Location>> GetAllLocationsAsync()
        {
            return await _unitOfWork.Locations.GetAllAsync();
        }

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
                AccountId = accountId
            };

            try
            {
                // Get journal vouchers based on filters
                var vouchers = await _unitOfWork.JournalVouchers.GetFilteredVouchersAsync(
                    sinceDate, toDate, projectId, !accountId.HasValue && !categoryId.HasValue);

                foreach (var voucher in vouchers)
                {
                    // Skip vouchers that are fully processed
                    if (voucher.IsProcessed)
                        continue;

                    var voucherViewModel = new JournalVoucherViewModel
                    {
                        JvId = voucher.JvId,
                        ProjectName = voucher.Project?.ProjectName,
                        EntryDate = voucher.EntryDate,
                        IsProcessed = voucher.IsProcessed
                    };

                    // Get entries for this voucher
                    var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(
                        voucher.JvId, categoryId, accountId);

                    // Skip entries that are already processed and entries with 0 quantity
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

                    // Only add vouchers that have at least one entry
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

        // Implementation of the additional methods required by IDeliveryChallanService
        public async Task<DeliveryChallanVoucher> CreateDeliveryChallanVoucherAsync(int journalVoucherId, DateOnly? dcDate = null)
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
                    DCDate = dcDate ?? DateOnly.FromDateTime(DateTime.Now),
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
            int deliveryChallanId, int journalEntryId, int locationId, string remarks = null, string otherReference = null)
        {
            try
            {

                // Check if delivery challan voucher exists
                var deliveryChallan = await _unitOfWork.DeliveryChallanVouchers.GetByIdAsync(deliveryChallanId);
                if (deliveryChallan == null)
                {
                    _logger.LogError("Delivery challan voucher with ID {DeliveryChallanId} not found", deliveryChallanId);
                    throw new ArgumentException($"Delivery challan voucher with ID {deliveryChallanId} not found");
                }

                // Check if journal entry exists
                var journalEntry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(journalEntryId);
                if (journalEntry == null)
                {
                    _logger.LogError("Journal entry with ID {JournalEntryId} not found", journalEntryId);
                    throw new ArgumentException($"Journal entry with ID {journalEntryId} not found");
                }

                // Check if location exists
                var location = await _unitOfWork.Locations.GetByIdAsync(locationId);
                if (location == null)
                {
                    _logger.LogError("Location with ID {LocationId} not found", locationId);
                    throw new ArgumentException($"Location with ID {locationId} not found");
                }

                // Check if entry already exists for this journal entry in this delivery challan
                var existingEntry = await _unitOfWork.DeliveryChallanEntries.GetByDeliveryChallanIdAndJournalEntryIdAsync(
                    deliveryChallanId, journalEntryId);

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

        public async Task<DeliveryChallanVoucher> GetDeliveryChallanVoucherByJournalVoucherIdAsync(int journalVoucherId)
        {
            try
            {
                var deliveryChallanVoucher = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(journalVoucherId);
                return deliveryChallanVoucher;
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
                var deliveryChallanVouchers = await _unitOfWork.DeliveryChallanVouchers.GetByDateRangeAsync(startDate, endDate);
                return deliveryChallanVouchers;
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
                var entries = await _unitOfWork.DeliveryChallanEntries.GetByDeliveryChallanIdAsync(deliveryChallanId);
                return entries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting delivery challan entries for voucher ID {DeliveryChallanId}", deliveryChallanId);
                throw;
            }
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
                var journalVoucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(deliveryChallan.JournalVoucherId);
                if (journalVoucher != null)
                {
                    bool allEntriesProcessed = journalVoucher.JournalEntries
                        .All(je => je.IsProcessed || je.Quantity <= 0);

                    if (allEntriesProcessed)
                    {
                        journalVoucher.IsProcessed = true;
                        await _unitOfWork.JournalVouchers.UpdateAsync(journalVoucher);
                    }
                }

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

        public async Task<JournalEntry> GetJournalEntryByIdAsync(int journalEntryId)
        {
            return await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(journalEntryId);
        }

        public async Task<DeliveryChallanVoucher> GetDeliveryChallanVoucherByIdAsync(int deliveryChallanId)
        {
            return await _unitOfWork.DeliveryChallanVouchers.GetByIdWithEntriesAsync(deliveryChallanId);
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

        public async Task<DeliveryChallanVoucher> UpdateDeliveryChallanVoucherAsync(DeliveryChallanVoucher voucher)
        {
            var result = await _unitOfWork.DeliveryChallanVouchers.UpdateAsync(voucher);
            await _unitOfWork.SaveChangesAsync();
            return result;
        }

        public async Task<List<JournalEntry>> GetUnprocessedEntriesByVoucherIdAsync(int voucherId)
        {
            try
            {
                // Get entries for the voucher that haven't been processed and have quantity > 0
                var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(voucherId, null, null);
                return entries.Where(e => !e.IsProcessed && e.Quantity > 0).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unprocessed entries for voucher {VoucherId}", voucherId);
                throw;
            }
        }
        public async Task UpdateJournalVoucherProcessedStatusAsync(int journalVoucherId)
        {
            var unprocessed = await GetUnprocessedEntriesByVoucherIdAsync(journalVoucherId);
            if (unprocessed.Count == 0)
            {
                var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
                if (voucher != null && !voucher.IsProcessed)
                {
                    voucher.IsProcessed = true;
                    await _unitOfWork.JournalVouchers.UpdateAsync(voucher);
                    await _unitOfWork.SaveChangesAsync();
                }
            }
        }

        public async Task<DeliveryChallanVoucher> ProcessCartageAndDeliveryAsync(
    int journalVoucherId,
    int entryId,
    int transporterId,
    decimal amount,
    bool postToCustomer,
    string reference,
    string dcDate,
    string dcRemarks,
    string dcStation,
    string dcOtherRef,
    int dcLocation)
{
    var originalJV = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
    if (originalJV == null) throw new ArgumentException("Original JV not found");

    var entry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(entryId);
    if (entry == null) throw new ArgumentException("Journal entry not found");

    int debitAccountId = postToCustomer
        ? originalJV.ProjectId // Adjust if customer account is different
        : entry.AccountId;

    var cartageJV = new JournalVoucher
    {
        ProjectId = originalJV.ProjectId,
        EntryDate = DateOnly.FromDateTime(DateTime.Now),
        IsProcessed = false,
        CreditableDate = DateOnly.FromDateTime(DateTime.Now)
    };
    cartageJV = await _unitOfWork.JournalVouchers.AddAsync(cartageJV);

    var debitEntry = new JournalEntry
    {
        JournalVoucherId = cartageJV.JvId,
        AccountId = debitAccountId,
        CategoryId = entry.CategoryId,
        Description = $"Cartage Debit - Ref to JV-{originalJV.JvId}, EID-{entry.EntryId}",
        Quantity = 1,
        Rate = amount,
        Debit = amount,
        Credit = 0,
        Discount = 0,
        DiscountPercent = 0,
        Rbp = "N",
        LocationId = dcLocation,
        IsProcessed = true
    };
    await _unitOfWork.JournalEntries.AddAsync(debitEntry);

    var creditEntry = new JournalEntry
    {
        JournalVoucherId = cartageJV.JvId,
        AccountId = transporterId,
        CategoryId = 4, // Transporter category
        Description = $"Cartage Credit - Ref to JV-{originalJV.JvId}, EID-{entry.EntryId}",
        Quantity = 1,
        Rate = amount,
        Debit = 0,
        Credit = amount,
        Discount = 0,
        DiscountPercent = 0,
        Rbp = "N",
        LocationId = dcLocation,
        IsProcessed = true
    };
    await _unitOfWork.JournalEntries.AddAsync(creditEntry);

    cartageJV.IsProcessed = true;
    await _unitOfWork.JournalVouchers.UpdateAsync(cartageJV);

    var dcDateValue = !string.IsNullOrEmpty(dcDate)
        ? DateOnly.Parse(dcDate)
        : DateOnly.FromDateTime(DateTime.Now);

    var deliveryChallanVoucher = await CreateDeliveryChallanVoucherAsync(journalVoucherId, dcDateValue);
    await CreateDeliveryChallanEntryAsync(
        deliveryChallanVoucher.DeliveryChallanId,
        entry.EntryId,
        dcLocation,
        dcRemarks,
        dcOtherRef);

    await _unitOfWork.SaveChangesAsync();
    return deliveryChallanVoucher;
}

    }
}