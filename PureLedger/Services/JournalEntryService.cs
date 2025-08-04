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
            try
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

                // Create journal entries
                await CreateJournalEntriesAsync(journalVoucher.JournalVoucherId, model.Entries);

                // Update voucher status based on account groups
                await UpdateVoucherStatusBasedOnAccountsAsync(journalVoucher.JournalVoucherId);

                await _unitOfWork.SaveChangesAsync();
                return journalVoucher;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating journal voucher");
                throw;
            }
        }

        private async Task CreateJournalEntriesAsync(int journalVoucherId, List<JournalEntryRowModel> entryModels)
        {
            foreach (var entryModel in entryModels)
            {
                var journalEntry = new JournalEntry
                {
                    JournalVoucherId = journalVoucherId,
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

                journalEntry = await _unitOfWork.JournalEntries.AddAsync(journalEntry);

                // Process tags if any
                if (entryModel.Tags != null && entryModel.Tags.Any())
                {
                    await _unitOfWork.JournalEntries.AddTagsAsync(journalEntry.JournalEntryId, entryModel.Tags);
                }
            }
        }

        private async Task UpdateVoucherStatusBasedOnAccountsAsync(int journalVoucherId)
        {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null) return;

            // Get all account IDs from the entries
            var accountIds = voucher.JournalEntries.Select(e => e.AccountId).Distinct().ToList();

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

            // Update voucher and entries status
            voucher.IsCompleted = allAccountGroup1;
            await _unitOfWork.JournalVouchers.UpdateAsync(voucher);

            // Update entry processing status
            var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(journalVoucherId, null, null);
            foreach (var entry in entries)
            {
                entry.IsProcessed = allAccountGroup1;
                await _unitOfWork.JournalEntries.UpdateAsync(entry);
            }
        }

        public async Task<JournalEntryUpdateViewModel?> GetJournalEntryForEditAsync(int entryId)
        {
            var entry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(entryId);
            if (entry == null)
                return null;

            var projectId = entry.JournalVoucher?.ProjectId ?? 0;
            int? deliveryChallanEntryId = null;
            int? locationId = null;
            List<Location>? allLocations = null;

            // Always fetch the account with details to get AccountGroupId
            var account = entry.AccountId > 0
                ? await _unitOfWork.Accounts.GetByIdWithDetailsAsync(entry.AccountId)
                : null;
            int accountGroupId = account?.AccountGroupId ?? 0;

            // Load additional data for AccountGroup 2
            if (accountGroupId == 2)
            {
                var dcEntry = await _unitOfWork.DeliveryChallanEntries
                    .GetByDeliveryChallanIdAndJournalEntryIdAsync(entry.JournalVoucherId, entry.JournalEntryId);
                if (dcEntry != null)
                {
                    deliveryChallanEntryId = dcEntry.DeliveryChallanEntryId;
                    locationId = dcEntry.LocationId;
                }
                allLocations = await _unitOfWork.Locations.GetAllAsync();
            }

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
                ProjectId = projectId,
                AccountGroupId = accountGroupId,
                DeliveryChallanEntryId = deliveryChallanEntryId,
                LocationId = locationId,
                Locations = allLocations
            };
        }

        public async Task<bool> UpdateJournalEntryAsync(JournalEntryUpdateViewModel model)
        {
            try
            {
                var entry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(model.EntryId);
                if (entry == null)
                    return false;

                // Map view model to entity
                UpdateEntryFromViewModel(entry, model);

                await _unitOfWork.JournalEntries.UpdateAsync(entry);

                // Update DeliveryChallanEntry Location if needed
                if (model.AccountGroupId == 2 && model.DeliveryChallanEntryId.HasValue && model.LocationId.HasValue)
                {
                    await UpdateDeliveryChallanEntryLocationAsync(model.DeliveryChallanEntryId.Value, model.LocationId.Value);
                }

                await _unitOfWork.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating journal entry {EntryId}", model.EntryId);
                return false;
            }
        }

        private void UpdateEntryFromViewModel(JournalEntry entry, JournalEntryUpdateViewModel model)
        {
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
            entry.ModifiedDateTime = DateTime.Now; // <-- Set modification time

        }

        private async Task UpdateDeliveryChallanEntryLocationAsync(int deliveryChallanEntryId, int locationId)
        {
            var dcEntry = await _unitOfWork.DeliveryChallanEntries.GetByIdAsync(deliveryChallanEntryId);
            if (dcEntry != null)
            {
                dcEntry.LocationId = locationId;
                await _unitOfWork.DeliveryChallanEntries.UpdateAsync(dcEntry);
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

        public async Task<bool> DeleteJournalEntryAsync(int entryId)
        {
            try
            {
                var entry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(entryId);
                if (entry == null)
                    return false;

                var voucherId = entry.JournalVoucherId;

                // Get all entries for this voucher
                var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(voucherId, null, null);

                if (entries.Count == 1)
                {
                    // This is the last entry - delete entry, voucher, and related data
                    await DeleteVoucherWithLastEntryAsync(entryId, voucherId);
                }
                else
                {
                    // Just delete the entry
                    await _unitOfWork.JournalEntries.DeleteAsync(entryId);

                    // Delete associated DeliveryChallanEntries
                    await DeleteRelatedDeliveryChallanEntriesAsync(voucherId, entryId);

                    // Update voucher status
                    await UpdateVoucherStatusBasedOnAccountsAsync(voucherId);
                }

                await _unitOfWork.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting journal entry {EntryId}", entryId);
                return false;
            }
        }

        private async Task DeleteVoucherWithLastEntryAsync(int entryId, int voucherId)
        {
            // Delete the entry
            await _unitOfWork.JournalEntries.DeleteAsync(entryId);

            // Delete related DeliveryChallanEntries
            await DeleteRelatedDeliveryChallanEntriesAsync(voucherId, entryId);

            // Delete the delivery challan voucher if it exists
            var deliveryChallanVoucher = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(voucherId);
            if (deliveryChallanVoucher != null)
            {
                await _unitOfWork.DeliveryChallanVouchers.DeleteAsync(deliveryChallanVoucher.DeliveryChallanVoucherId);
            }

            // Delete the journal voucher
            await _unitOfWork.JournalVouchers.DeleteAsync(voucherId);
        }

        private async Task DeleteRelatedDeliveryChallanEntriesAsync(int voucherId, int entryId)
        {
            var deliveryChallanEntries = await _unitOfWork.DeliveryChallanEntries.GetByDeliveryChallanIdAsync(voucherId);
            if (deliveryChallanEntries != null && deliveryChallanEntries.Any())
            {
                foreach (var dcEntry in deliveryChallanEntries.Where(e => e.JournalEntryId == entryId))
                {
                    await _unitOfWork.DeliveryChallanEntries.DeleteAsync(dcEntry.DeliveryChallanEntryId);
                }
            }
        }

        public async Task<List<JournalVoucher>> GetVouchersWithModifiedEntriesAsync()
        {
            return await _unitOfWork.JournalVouchers.GetVouchersWithModifiedEntriesAsync();
        }

        public async Task<IEnumerable<Account>> GetAccountsByCategoryAsync(int categoryId)
        {
            try
            {
                return await _unitOfWork.Accounts.GetByCategoryIdAsync(categoryId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accounts for category {CategoryId}", categoryId);
                throw;
            }
        }

        // Add this method to JournalEntryService
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
    }
}