using Microsoft.EntityFrameworkCore;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;
using PureLedger.ViewModels;
using PureLedger.ViewModels.PureLedger.ViewModels;

namespace PureLedger.Services
{
    public class JournalEntryService : IJournalEntryService
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
                CreditDays = voucher.CreditableDate.HasValue ?
                    (voucher.CreditableDate.Value.DayNumber - voucher.EntryDate.DayNumber) : 0,
                DueDate = voucher.CreditableDate ?? DateOnly.FromDateTime(DateTime.Now),
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
                IsProcessed = false,
                CreditableDate = DateOnly.FromDateTime(DateTime.Now.AddDays(model.CreditDays))
            };

            // Add voucher using repository
            journalVoucher = await _unitOfWork.JournalVouchers.AddAsync(journalVoucher);

            foreach (var entryModel in model.Entries)
            {
                var journalEntry = new JournalEntry
                {
                    JournalVoucherId = journalVoucher.JvId,
                    AccountId = entryModel.AccountId,
                    CategoryId = entryModel.CategoryId,
                    Description = entryModel.Des,
                    Quantity = entryModel.Qty,
                    Rate = entryModel.Rate,
                    DiscountPercent = entryModel.Percent,
                    Discount = entryModel.Disc,
                    Debit = entryModel.Debit,
                    Credit = entryModel.Credit,
                    Rbp = entryModel.Rbp
                };

                // Add entry using repository
                journalEntry = await _unitOfWork.JournalEntries.AddAsync(journalEntry);

                // Process tags if any
                if (entryModel.Tags != null && entryModel.Tags.Any())
                {
                    await _unitOfWork.JournalEntries.AddTagsAsync(journalEntry.EntryId, entryModel.Tags);
                }
            }

            return journalVoucher;
        }

        public async Task<JournalEntryUpdateViewModel?> GetJournalEntryForEditAsync(int entryId)
        {
            var entry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(entryId);

            if (entry == null)
                return null;

            return new JournalEntryUpdateViewModel
            {
                EntryId = entry.EntryId,
                AccountName = entry.Account?.AccountName,
                Description = entry.Description,
                Quantity = entry.Quantity,
                Rate = entry.Rate,
                Discount = entry.Discount,
                DiscountPercent = entry.DiscountPercent ?? 0,
                Debit = entry.Debit,
                Credit = entry.Credit,
                AccountId = entry.AccountId,      
                CategoryId = entry.CategoryId
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

            try
            {
                await _unitOfWork.JournalEntries.UpdateAsync(entry);
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
                entryId = e.EntryId,
                accountName = e.Account?.AccountName,
                description = e.Description,
                quantity = e.Quantity,
                rate = e.Rate,
                discount = e.Discount,
                discountPercent = e.DiscountPercent,
                debit = e.Debit,
                credit = e.Credit
            }).ToList();

            return new { entries, jvId = voucher.JvId };
        }

        public async Task<List<object>> GetCategoriesByProjectAsync(int projectId)
        {
            var categories = await _unitOfWork.Categories.GetByProjectIdAsync(projectId);

            return categories.Select(c => new { c.CategoryId, c.CategoryName })
                .Cast<object>()
                .ToList();
        }

        public async Task<List<object>> GetAccountsByCategoryAsync(int categoryId)
        {
            try
            {
                var category = await _unitOfWork.Categories.GetByIdWithAccountTypeAsync(categoryId);
                var accounts = await _unitOfWork.Accounts.GetByCategoryIdAsync(categoryId);

                return accounts.Select(a => new
                {
                    a.AccountId,
                    a.AccountName,
                    a.ProductData?.Type,
                    AccountTypeName = category?.AccountType?.AccountTypeName
                })
                .Cast<object>()
                .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accounts for category {CategoryId}", categoryId);
                return new List<object>();
            }
        }

        public async Task<object?> GetProductDetailsAsync(int accountId)
        {
            return await _unitOfWork.Accounts.GetProductDetailsAsync(accountId);
        }

        public async Task<List<Category>> GetAllCategoriesAsync()
        {
            return await _unitOfWork.Categories.GetAllAsync();
        }

        public async Task<List<Account>> GetAllAccountsAsync()
        {
            return await _unitOfWork.Accounts.GetAllAsync();
        }

    }
}