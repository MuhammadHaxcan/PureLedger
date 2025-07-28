using Microsoft.EntityFrameworkCore;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;
using PureLedger.ViewModels;
using PureLedger.ViewModels.PureLedger.ViewModels;

namespace PureLedger.Services
{
    public class JournalEntryService 
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<JournalEntryService> _logger;

        public JournalEntryService(
            IUnitOfWork unitOfWork,
            ILogger<JournalEntryService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<List<Project>> GetAllProjectsAsync()
        {
            return await _unitOfWork.Projects.GetAllAsync();
        }

        public async Task<List<JournalVoucher>> GetAllVouchersAsync()
        {
            return await _unitOfWork.JournalVouchers.GetAllWithEntriesAsync();
        }

        public async Task<JournalVoucher?> GetVoucherWithEntriesAsync(int jvId)
        {
            return await _unitOfWork.JournalVouchers.GetWithEntriesAsync(jvId);
        }

        public async Task<JournalEntryViewModel> MapVoucherToViewModelAsync(JournalVoucher voucher)
        {
            return new JournalEntryViewModel
            {
                ProjectId = voucher.ProjectId,
                CreditDays = voucher.EntryDate.Day - voucher.CreditableDate.Day,
                DueDate = voucher.CreditableDate,
                Booker = "", // Set if you have this info
                Entries = voucher.JournalEntries.Select(e => new JournalEntryRowModel
                {
                    CategoryId = e.CategoryId,
                    AccountId = e.AccountId,
                    Des = e.Description,
                    Qty = e.Quantity,
                    Rate = e.Rate,
                    Percent = e.DiscountPercent ?? 0,
                    Disc = e.Discount,
                    Debit = e.Debit,
                    Credit = e.Credit,
                    Rbp = e.Rbp
                }).ToList()
            };
        }

        public async Task<JournalVoucher> CreateJournalVoucherAsync(JournalEntryViewModel model)
        {
            // Create journal voucher
            var journalVoucher = new JournalVoucher
            {
                ProjectId = model.ProjectId,
                EntryDate = DateOnly.FromDateTime(DateTime.Now),
                IsCompleted = false,
                CreditableDate = DateOnly.FromDateTime(DateTime.Now.AddDays(model.CreditDays))
            };

            // Add voucher using repository
            journalVoucher = await _unitOfWork.JournalVouchers.AddAsync(journalVoucher);

            foreach (var entryModel in model.Entries)
            {
                var journalEntry = new JournalEntry
                {
                    JournalVoucherId = journalVoucher.JournalVoucherId,
                    AccountId = entryModel.AccountId,
                    CategoryId = entryModel.CategoryId,
                    Description = entryModel.Des,
                    Quantity = entryModel.Qty,
                    Rate = entryModel.Rate,
                    DiscountPercent = entryModel.Percent,
                    Discount = entryModel.Disc,
                    Debit = entryModel.Debit,
                    Credit = entryModel.Credit,
                    Rbp = entryModel.Rbp,
                    IsProcessed = false
                };

                // Add entry using repository
                journalEntry = await _unitOfWork.JournalEntries.AddAsync(journalEntry);

                // Process tags if any
                if (entryModel.Tags != null && entryModel.Tags.Any())
                {
                    await _unitOfWork.JournalEntries.AddTagsAsync(journalEntry.JournalEntryId, entryModel.Tags);
                }
            }

            // Update voucher processed status
//            await UpdateVoucherProcessedStatusAsync(journalVoucher.JournalVoucherId);

            // Get all account IDs from the entries
            var accountIds = model.Entries.Select(e => e.AccountId).Distinct().ToList();

            // Fetch accounts with their AccountGroupId
            var accounts = new List<Account>();
            foreach (var accountId in accountIds)
            {
                var account = await _unitOfWork.Accounts.GetByIdAsync(accountId);
                if (account != null)
                    accounts.Add(account);
            }

            // Check if all accounts have AccountGroupId == 1
            bool allAccountGroup1 = accounts.All(a => a.AccountGroupId == 1);

            // Set flags based on the check
            if (allAccountGroup1)
            {
                journalVoucher.IsCompleted = true;
                // You need to fetch entries from the DB to update IsProcessed
                var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(journalVoucher.JournalVoucherId, null, null);
                foreach (var entry in entries)
                {
                    entry.IsProcessed = true;
                    await _unitOfWork.JournalEntries.UpdateAsync(entry);
                }
            }
            else
            {
                journalVoucher.IsCompleted = false;
                var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(journalVoucher.JournalVoucherId, null, null);
                foreach (var entry in entries)
                {
                    entry.IsProcessed = false;
                    await _unitOfWork.JournalEntries.UpdateAsync(entry);
                }
            }

            // Save changes as usual
            await _unitOfWork.JournalVouchers.UpdateAsync(journalVoucher);
            await _unitOfWork.SaveChangesAsync();

            return journalVoucher;
        }

        public async Task<JournalEntryUpdateViewModel?> GetJournalEntryForEditAsync(int entryId)
        {
            var entry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(entryId);

            if (entry == null)
                return null;

            var projectId = entry.JournalVoucher?.ProjectId ?? 0;

            return new JournalEntryUpdateViewModel
            {
                EntryId = entry.JournalEntryId,
                AccountName = entry.Account?.AccountName,
                Description = entry.Description,
                Quantity = entry.Quantity,
                Rate = entry.Rate,
                Discount = entry.Discount,
                DiscountPercent = entry.DiscountPercent ?? 0,
                Debit = entry.Debit,
                Credit = entry.Credit,
                AccountId = entry.AccountId,      
                CategoryId = entry.CategoryId,
                ProjectId = projectId
            };
        }

        public async Task<bool> UpdateJournalEntryAsync(JournalEntryUpdateViewModel model)
        {
            var entry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(model.EntryId);
            if (entry == null)
                return false;

            // Map view model to entity
            entry.Description = model.Description;
            entry.Quantity = model.Quantity;
            entry.Rate = model.Rate;
            entry.Discount = model.Discount;
            entry.DiscountPercent = model.DiscountPercent;
            entry.Debit = model.Debit;
            entry.Credit = model.Credit;
            
            // Update account and category if they were changed
            if (model.AccountId > 0 && model.AccountId != entry.AccountId)
            {
                entry.AccountId = model.AccountId;
            }
            
            if (model.CategoryId > 0 && model.CategoryId != entry.CategoryId)
            {
                entry.CategoryId = model.CategoryId;
            }

            entry.IsEdited = true;

            try
            {
                await _unitOfWork.JournalEntries.UpdateAsync(entry);
                
                // Update voucher processed status
                //await UpdateVoucherProcessedStatusAsync(entry.JournalVoucherId);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating journal entry {EntryId}", model.EntryId);
                return false;
            }
        }

        public async Task<object> GetVoucherSummaryAsync(int voucherId)
        {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(voucherId);

            if (voucher == null)
                return new { entries = new List<object>(), jvId = voucherId };

            var entries = voucher.JournalEntries.Select(e => new {
                entryId = e.JournalEntryId,
                accountName = e.Account?.AccountName,
                description = e.Description,
                quantity = e.Quantity,
                rate = e.Rate,
                discount = e.Discount,
                discountPercent = e.DiscountPercent,
                debit = e.Debit,
                credit = e.Credit
            }).ToList();

            return new { entries, jvId = voucher.JournalVoucherId };
        }

        public async Task<object?> GetProductDetailsAsync(int accountId)
        {
            return await _unitOfWork.Accounts.GetProductDetailsAsync(accountId);
        }
        
        // Helper method to update the voucher processed status
        private async Task UpdateVoucherProcessedStatusAsync(int voucherId)
        {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(voucherId);
            if (voucher == null) return;

            // Check if all entries are processed
            var allProcessed = voucher.JournalEntries != null && voucher.JournalEntries.Count > 0
                && voucher.JournalEntries.All(e => e.IsProcessed);

            if (voucher.IsCompleted != allProcessed)
            {
                voucher.IsCompleted = allProcessed;
                await _unitOfWork.JournalVouchers.UpdateAsync(voucher);
            }
        }

        public async Task<bool> DeleteJournalEntryAsync(int entryId)
        {
            var entry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(entryId);
            if (entry == null)
                return false;

            var voucherId = entry.JournalVoucherId;

            // Delete associated DeliveryChallanEntry if it exists
            // Assuming DeliveryChallanEntry has a JournalEntryId foreign key
            var deliveryChallanEntries = await _unitOfWork.DeliveryChallanEntries.GetByDeliveryChallanIdAsync(voucherId);
            if (deliveryChallanEntries != null && deliveryChallanEntries.Any())
            {
                foreach (var dcEntry in deliveryChallanEntries.Where(e => e.JournalEntryId == entryId))
                {
                    // If you have a DeleteAsync method for DeliveryChallanEntry, use it here
                    await _unitOfWork.DeliveryChallanEntries.DeleteAsync(dcEntry.DeliveryChallanEntryId);
                }
            }

            // Get all entries for this voucher
            var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(voucherId, null, null);

            if (entries.Count == 1)
            {
                // This is the last entry, so delete the entry, the voucher, and any related delivery challan voucher

                // Delete the entry
                var entryDeleted = await _unitOfWork.JournalEntries.DeleteAsync(entryId);

                // Delete the delivery challan voucher if it exists
                var deliveryChallanVoucher = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(voucherId);
                if (deliveryChallanVoucher != null)
                {
                    // Assuming you have a DeleteAsync method for DeliveryChallanVoucher
                    await _unitOfWork.DeliveryChallanVouchers.DeleteAsync(deliveryChallanVoucher.DeliveryChallanVoucherId);
                }

                // Delete the journal voucher
                await _unitOfWork.JournalVouchers.DeleteAsync(voucherId);

                await _unitOfWork.SaveChangesAsync();
                return entryDeleted;
            }
            else
            {
                // Just delete the entry and update voucher status
                var success = await _unitOfWork.JournalEntries.DeleteAsync(entryId);

                if (success)
                {
                    await UpdateVoucherProcessedStatusAsync(voucherId);
                    await _unitOfWork.SaveChangesAsync();
                }

                return success;
            }
        }

        public async Task<List<JournalVoucher>> GetVouchersWithModifiedEntriesAsync()
        {
            return await _unitOfWork.JournalVouchers.GetVouchersWithModifiedEntriesAsync();
        }
    }
}