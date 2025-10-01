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

        public async Task<List<Project>> GetAllProjectsForUserAsync(int userId)
        {
            return await _unitOfWork.Projects.GetAllForUserAsync(userId);
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
                Booker = "",
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
                var journalVoucher = new JournalVoucher
                {
                    ProjectId = model.ProjectId,
                    EntryDate = model.EntryDate, // Use the entry date from the model
                    IsCompleted = false,
                    IsBaseVoucher = model.IsBaseVoucher,
                    CreditableDate = model.EntryDate.AddDays(model.CreditDays) // Calculate due date from entry date
                };

                journalVoucher = await _unitOfWork.JournalVouchers.AddAsync(journalVoucher);
                await CreateJournalEntriesAsync(journalVoucher.JournalVoucherId, model.Entries);
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
                try
                {

                    _logger.LogInformation("Creating journal entry with AccountId={AccountId}, Tags={TagCount}",
                        entryModel.AccountId, entryModel.Tags?.Count ?? 0);

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
                        Rbp = entryModel.Rbp ?? "Y",
                        IsProcessed = false
                    };

                    journalEntry = await _unitOfWork.JournalEntries.AddAsync(journalEntry);
                    await _unitOfWork.SaveChangesAsync();

                    if (entryModel.Tags != null && entryModel.Tags.Any())
                    {
                        try
                        {
                            _logger.LogInformation("Adding {TagCount} tags to entry {EntryId}",
                                entryModel.Tags.Count, journalEntry.JournalEntryId);

                            var validTags = entryModel.Tags.Where(t => t > 0).ToList();
                            if (validTags.Any())
                            {
                                await _unitOfWork.JournalEntries.AddTagsAsync(journalEntry.JournalEntryId, validTags);
                                await _unitOfWork.SaveChangesAsync();
                            }
                        }
                        catch (Exception tagEx)
                        {
                            _logger.LogError(tagEx, "Error adding tags to entry {EntryId}", journalEntry.JournalEntryId);

                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating journal entry for account {AccountId}", entryModel.AccountId);
                    throw;
                }
            }
        }

        private async Task UpdateVoucherStatusBasedOnAccountsAsync(int journalVoucherId)
        {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null) return;

            var accountIds = voucher.JournalEntries.Select(e => e.AccountId).Distinct().ToList();

            var accounts = new List<Account>();
            foreach (var accountId in accountIds)
            {
                var account = await _unitOfWork.Accounts.GetByIdAsync(accountId);
                if (account != null)
                    accounts.Add(account);
            }

            bool allAccountGroup1 = accounts.All(a => a.AccountGroupId == 1);

            voucher.IsCompleted = allAccountGroup1;
            await _unitOfWork.JournalVouchers.UpdateAsync(voucher);

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

            var account = entry.AccountId > 0
                ? await _unitOfWork.Accounts.GetByIdWithDetailsAsync(entry.AccountId)
                : null;
            int accountGroupId = account?.AccountGroupId ?? 0;

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

                UpdateEntryFromViewModel(entry, model);

                await _unitOfWork.JournalEntries.UpdateAsync(entry);

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

            if (model.AccountId > 0 && model.AccountId != entry.AccountId)
            {
                entry.AccountId = model.AccountId;
            }

            if (model.CategoryId > 0 && model.CategoryId != entry.CategoryId)
            {
                entry.CategoryId = model.CategoryId;
            }

            entry.IsEdited = true;
            entry.ModifiedDateTime = DateOnly.FromDateTime(DateTime.Now);
            ;

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

                var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(voucherId, null, null);

                if (entries.Count == 1)
                {
                    await DeleteVoucherWithLastEntryAsync(entryId, voucherId);
                }
                else
                {

                    await DeleteFromJournalEntryTagsAsync(entryId);

                    await _unitOfWork.JournalEntries.DeleteAsync(entryId);

                    await DeleteRelatedDeliveryChallanEntriesAsync(voucherId, entryId);

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

        private async Task DeleteFromJournalEntryTagsAsync(int entryId)
        {
            try
            {
                var entryWithTags = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(entryId);
                var links = entryWithTags?.JournalEntryTags?.ToList();
                if (links == null || links.Count == 0)
                    return;

                entryWithTags!.JournalEntryTags.Clear();
                await _unitOfWork.JournalEntries.UpdateAsync(entryWithTags);
            }
            catch (Exception ex)
            {

                _logger.LogWarning(ex, "Failed to explicitly remove JournalEntryTags for entry {EntryId}. Relying on cascade delete.", entryId);
            }
        }

        private async Task DeleteVoucherWithLastEntryAsync(int entryId, int voucherId)
        {

            await DeleteFromJournalEntryTagsAsync(entryId);

            await _unitOfWork.JournalEntries.DeleteAsync(entryId);

            await DeleteRelatedDeliveryChallanEntriesAsync(voucherId, entryId);

            var deliveryChallanVoucher = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(voucherId);
            if (deliveryChallanVoucher != null)
            {
                await _unitOfWork.DeliveryChallanVouchers.DeleteAsync(deliveryChallanVoucher.DeliveryChallanVoucherId);
            }

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

        public async Task<List<JournalVoucher>> GetAllBaseVouchersAsync()
        {
            try
            {

                var baseVouchers = await _unitOfWork.JournalVouchers.GetAllBaseVouchersAsync();

                foreach (var voucher in baseVouchers)
                {
                    if (voucher.JournalEntries == null || !voucher.JournalEntries.Any())
                    {
                        _logger.LogWarning("Voucher {VoucherId} has no entries loaded, loading them explicitly",
                            voucher.JournalVoucherId);

                        var entriesForVoucher = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(
                            voucher.JournalVoucherId, null, null);

                        if (voucher.JournalEntries == null)
                            voucher.JournalEntries = new List<JournalEntry>();

                        if (entriesForVoucher.Any())
                        {
                            foreach (var entry in entriesForVoucher)
                            {
                                voucher.JournalEntries.Add(entry);
                            }
                        }
                    }
                }

                _logger.LogInformation("Retrieved {Count} base vouchers with their entries", baseVouchers.Count);
                return baseVouchers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving base vouchers");
                throw;
            }
        }

        public async Task<JournalVoucher?> GetBaseVoucherWithUnprocessedEntriesAsync(int id)
        {
            return await _unitOfWork.JournalVouchers.GetBaseVoucherWithUnprocessedEntriesAsync(id);
        }

        private async Task<bool> ShouldProcessAsFullVoucher(
            int baseVoucherId, 
            List<BaseVoucherEntryViewModel> entriesToProcess)
        {
            var baseVoucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(baseVoucherId);
            if (baseVoucher == null || !baseVoucher.IsBaseVoucher) return false;

            // Check if the voucher already has derived vouchers
            var existingReferences = await _unitOfWork.JournalVoucherReferences
                .GetByMainVoucherIdAsync(baseVoucherId);
            if (existingReferences.Any()) return false;

            // Get all non-account entries
            var regularEntries = baseVoucher.JournalEntries
                .Where(e => !e.IsProcessed)
                .ToList();

            if (!regularEntries.Any()) return false;

            // Check if all entries are being processed with full quantity
            foreach (var entry in regularEntries)
            {
                var processEntry = entriesToProcess
                    .FirstOrDefault(e => e.EntryId == entry.JournalEntryId);
                
                if (processEntry == null || processEntry.ProcessQuantity != entry.Quantity)
                {
                    return false;
                }
            }

            return true;
        }

        // Modify the CreateDerivedVoucherAsync method
        public async Task<int> CreateDerivedVoucherAsync(
            int baseVoucherId,
            List<BaseVoucherEntryViewModel> entriesToProcess,
            int accountEntryAmount = 0,
            bool accountEntryIsDebit = false,
            DateOnly? creditableDate = null)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Check if we should process this as a full voucher
                if (await ShouldProcessAsFullVoucher(baseVoucherId, entriesToProcess))
                {
                    var baseVoucherToOff = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(baseVoucherId);
                    if (baseVoucherToOff != null)
                    {
                        baseVoucherToOff.IsBaseVoucher = false;
                        // Explicitly update the voucher through the repository
                        await _unitOfWork.JournalVouchers.UpdateAsync(baseVoucherToOff);
                        await _unitOfWork.SaveChangesAsync();
                        await transaction.CommitAsync();

                        _logger.LogInformation("Base voucher {VoucherId} processed completely and turned off", baseVoucherId);
                        return baseVoucherToOff.JournalVoucherId;

                    }                    
                }

                var baseVoucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(baseVoucherId);
                if (baseVoucher == null || !baseVoucher.IsBaseVoucher)
                    throw new InvalidOperationException("Base voucher not found");

                var derivedVoucher = new JournalVoucher
                {
                    ProjectId = baseVoucher.ProjectId,
                    EntryDate = DateOnly.FromDateTime(DateTime.Now),
                    CreditableDate = creditableDate ?? DateOnly.FromDateTime(DateTime.Now),
                    IsCompleted = false
                };

                derivedVoucher = await _unitOfWork.JournalVouchers.AddAsync(derivedVoucher);
                await _unitOfWork.SaveChangesAsync();

                await _unitOfWork.JournalVoucherReferences.AddAsync(new JournalVoucherReference
                {
                    MainVoucherId = baseVoucherId,
                    ReferencedVoucherId = derivedVoucher.JournalVoucherId,
                    ReferenceType = "DerivedVoucher"
                });

                bool accountEntryAdded = false;

                foreach (var entryToProcess in entriesToProcess)
                {
                    var baseEntry = baseVoucher.JournalEntries
                        .FirstOrDefault(e => e.JournalEntryId == entryToProcess.EntryId);

                    if (baseEntry == null || baseEntry.IsProcessed)
                        continue;

                    var baseTagIds = baseEntry.JournalEntryTags?.Select(t => t.TagId).ToList() ?? new List<int>();

                    var account = await _unitOfWork.Accounts.GetByIdAsync(baseEntry.AccountId);
                    bool isAccountEntry = account != null && account.AccountGroupId == 1;

                    if (!isAccountEntry && (entryToProcess.ProcessQuantity <= 0 ||
                        entryToProcess.ProcessQuantity > baseEntry.Quantity))
                        continue;

                    if (isAccountEntry)
                    {
                        accountEntryAdded = true;

                        var newEntry = new JournalEntry
                        {
                            JournalVoucherId = derivedVoucher.JournalVoucherId,
                            AccountId = baseEntry.AccountId,
                            CategoryId = baseEntry.CategoryId,
                            Description = baseEntry.Description,
                            Quantity = 0,
                            Rate = baseEntry.Rate,
                            DiscountPercent = baseEntry.DiscountPercent,
                            Discount = 0,
                            Debit = accountEntryIsDebit ? accountEntryAmount : 0,
                            Credit = !accountEntryIsDebit ? accountEntryAmount : 0,
                            Rbp = baseEntry.Rbp,
                            IsProcessed = false
                        };

                        var savedEntry = await _unitOfWork.JournalEntries.AddAsync(newEntry);

                        if (baseTagIds.Any())
                        {
                            await _unitOfWork.JournalEntries.AddTagsAsync(savedEntry.JournalEntryId, baseTagIds);
                        }

                        continue;
                    }

                    var newRegularEntry = new JournalEntry
                    {
                        JournalVoucherId = derivedVoucher.JournalVoucherId,
                        AccountId = baseEntry.AccountId,
                        CategoryId = baseEntry.CategoryId,
                        Description = baseEntry.Description,
                        Quantity = entryToProcess.ProcessQuantity,
                        Rate = baseEntry.Rate,
                        DiscountPercent = baseEntry.DiscountPercent,
                        Discount = (int)(baseEntry.Discount * (entryToProcess.ProcessQuantity / (double)baseEntry.Quantity)),
                        Debit = baseEntry.Debit > 0 ? (int)(baseEntry.Debit * (entryToProcess.ProcessQuantity / (double)baseEntry.Quantity)) : 0,
                        Credit = baseEntry.Credit > 0 ? (int)(baseEntry.Credit * (entryToProcess.ProcessQuantity / (double)baseEntry.Quantity)) : 0,
                        Rbp = baseEntry.Rbp,
                        IsProcessed = false
                    };

                    var savedRegularEntry = await _unitOfWork.JournalEntries.AddAsync(newRegularEntry);

                    if (baseTagIds.Any())
                    {
                        await _unitOfWork.JournalEntries.AddTagsAsync(savedRegularEntry.JournalEntryId, baseTagIds);
                    }

                    baseEntry.Quantity -= entryToProcess.ProcessQuantity;
                    if (baseEntry.Quantity <= 0)
                    {
                        baseEntry.IsProcessed = true;
                    }
                    await _unitOfWork.JournalEntries.UpdateAsync(baseEntry);
                }

                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                return derivedVoucher.JournalVoucherId;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task MarkEntriesAsMissed(int voucherId)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(voucherId);
                if (voucher == null || !voucher.IsBaseVoucher)
                    throw new InvalidOperationException("Invalid voucher");

                var customerAccount = await GetCustomerAccountFromEntries(voucher.JournalEntries);
                if (customerAccount == null)
                    throw new InvalidOperationException("No customer account found in voucher");

                var unprocessedEntries = voucher.JournalEntries
                    .Where(e => !e.IsProcessed && 
                           e.Quantity > 0 && 
                           e.Account.AccountGroupId == 2)
                    .ToList();

                foreach (var entry in unprocessedEntries)
                {
                    var missedEntry = new MissedEntry
                    {
                        CustomerAccountId = customerAccount.AccountId,
                        ProductAccountId = entry.AccountId,
                        ProjectId = voucher.ProjectId, // Add ProjectId
                        MissedDate = DateOnly.FromDateTime(DateTime.Now),
                        Quantity = entry.Quantity,
                        Rate = entry.Rate,
                        Discount = entry.DiscountPercent ?? 0,
                        Description = entry.Description
                    };

                    await _unitOfWork.MissedEntries.AddAsync(missedEntry);
            
                    entry.Quantity = 0;
                    await _unitOfWork.JournalEntries.UpdateAsync(entry);
                }

                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Voucher {VoucherId} marked as missed with {Count} entries against customer {CustomerName}", 
                    voucherId, unprocessedEntries.Count, customerAccount.AccountName);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<MissedEntryViewModel>> GetFilteredMissedEntriesAsync(
    DateOnly? startDate, DateOnly? endDate, int? projectId)
        {
            var entries = await _unitOfWork.MissedEntries.GetFilteredEntriesAsync(startDate, endDate, projectId);

            return entries.Select(e => new MissedEntryViewModel
            {
                MissedEntryId = e.MissedEntryId,
                CustomerName = e.CustomerAccount?.AccountName ?? "Unknown Customer",
                ProductName = e.ProductAccount?.AccountName ?? "Unknown Product",
                ProjectName = e.Project?.ProjectName ?? "Unknown Project",
                MissedDate = e.MissedDate,
                Quantity = e.Quantity,
                Rate = e.Rate,
                Discount = e.Discount,
                Description = e.Description
            }).ToList();
        }

        private async Task<Account?> GetCustomerAccountFromEntries(IEnumerable<JournalEntry> entries)
        {
            foreach (var entry in entries)
            {
                var account = await _unitOfWork.Accounts.GetByIdWithDetailsAsync(entry.AccountId);
                if (account?.AccountGroupId == 1)
                {
                    return account;
                }
            }
            return null;
        }
    }
}