using PureLedger.Models;
using PureLedger.ViewModels;

namespace PureLedger.Services
{
    public interface IDeliveryChallanService
    {
        Task<List<Project>> GetAllProjectsAsync();
        Task<List<Models.Location>> GetAllLocationsAsync();
        Task<List<object>> GetCategoriesByProjectAsync(int projectId);
        Task<List<object>> GetAccountsByCategoryAsync(int categoryId);
        Task<DeliveryChallanFilterViewModel> GetFilteredEntriesForDeliveryChallanAsync(
            DateOnly? sinceDate,
            DateOnly? toDate,
            int? projectId,
            int? categoryId,
            int? accountId);
            
        // Methods for delivery challan operations
        Task<DeliveryChallanVoucher> CreateDeliveryChallanVoucherAsync(int journalVoucherId, DateOnly? dcDate = null);
        Task<DeliveryChallanEntry> CreateDeliveryChallanEntryAsync(int deliveryChallanId, int journalEntryId, int locationId, string remarks = null, string otherReference = null);
        Task<DeliveryChallanVoucher> GetDeliveryChallanVoucherByJournalVoucherIdAsync(int journalVoucherId);
        Task<DeliveryChallanVoucher> GetDeliveryChallanVoucherByIdAsync(int deliveryChallanId);
        Task<List<DeliveryChallanVoucher>> GetDeliveryChallanVouchersByDateRangeAsync(DateOnly startDate, DateOnly endDate);
        Task<List<DeliveryChallanEntry>> GetDeliveryChallanEntriesByVoucherIdAsync(int deliveryChallanId);
        Task<bool> CompleteDeliveryChallanAsync(int deliveryChallanId, DateOnly finalDeliveryDate);
        
        // Additional methods
        Task<JournalEntry> GetJournalEntryByIdAsync(int journalEntryId);
        Task<List<JournalEntryDetailViewModel>> GetEntriesByVoucherIdAsync(int voucherId);
        Task<DeliveryChallanVoucher> UpdateDeliveryChallanVoucherAsync(DeliveryChallanVoucher voucher);
    }
}