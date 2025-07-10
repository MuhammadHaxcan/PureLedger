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

        public async Task<List<object>> GetCategoriesByProjectAsync(int projectId)
        {
            var categories = await _unitOfWork.Categories.GetByProjectIdAsync(projectId);
            return categories.Select(c => new { c.CategoryId, Name = c.CategoryName })
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
    }
}