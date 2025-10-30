using PureLedger.Models;
using PureLedger.ViewModels;

namespace PureLedger.Services.Interface
{
    public interface IDeliveryChallanService
    {
        #region Entity Retrieval
        Task<List<Project>> GetAllProjectsAsync();
        Task<List<Project>> GetAllProjectsForUserAsync(int userId);
        Task<List<Location>> GetAllLocationsAsync();
        Task<JournalEntry?> GetJournalEntryByIdAsync(int journalEntryId);

        // Entry retrieval methods
        Task<List<JournalEntryDetailViewModel>> GetEntriesByVoucherIdAsync(int voucherId);
        Task<List<JournalEntry>> GetUnprocessedEntriesByVoucherIdAsync(int voucherId);

        // DeliveryChallan retrieval methods with overloads
        Task<DeliveryChallanVoucher?> GetDeliveryChallanVoucherByIdAsync(int deliveryChallanId);
        Task<DeliveryChallanVoucher?> GetDeliveryChallanVoucherByJournalVoucherIdAsync(int journalVoucherId);
        Task<List<DeliveryChallanVoucher>> GetDeliveryChallanVouchersByDateRangeAsync(DateOnly startDate, DateOnly endDate);
        Task<List<DeliveryChallanEntry>> GetDeliveryChallanEntriesByVoucherIdAsync(int deliveryChallanId);

        // Filtered entries retrieval
        Task<DeliveryChallanFilterViewModel> GetFilteredEntriesForDeliveryChallanAsync(
            DateOnly? sinceDate,
            DateOnly? toDate,
            int? projectId = null,
            int? categoryId = null,
            int? accountId = null);
        #endregion

        #region DeliveryChallan Operations
        Task<DeliveryChallanVoucher> CreateDeliveryChallanVoucherAsync(int journalVoucherId, DateOnly dcDate);
        Task<DeliveryChallanVoucher> CreateDeliveryChallanVoucherAsync(int journalVoucherId, DateOnly dcDate, string? reference);

        Task<DeliveryChallanEntry> CreateDeliveryChallanEntryAsync(int deliveryChallanId, int journalEntryId, int locationId, string? remarks, string? otherReference);
        Task<DeliveryChallanEntry> CreateDeliveryChallanEntryAsync(int deliveryChallanId, int journalEntryId, int locationId, string? remarks, string? otherReference, DateOnly entryDate);
        #endregion

        #region DeliveryChallan Update and Processing
        Task<DeliveryChallanVoucher> UpdateDeliveryChallanVoucherAsync(DeliveryChallanVoucher voucher);
        Task<bool> CompleteDeliveryChallanAsync(int deliveryChallanId, DateOnly finalDeliveryDate);
        Task UpdateJournalVoucherProcessedStatusAsync(int journalVoucherId);

        Task<DeliveryChallanVoucher> ProcessCartageAndDeliveryAsync(CartageViewModel model);
        Task<DeliveryChallanVoucher> ProcessCartageAndDeliveryAsync(
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
            int dcLocation);
        #endregion

        Task<IEnumerable<Account>> GetAccountsByCategoryAsync(int categoryId);
        Task<IEnumerable<Category>> GetCategoriesByProjectAsync(int projectId);

        Task<JournalVoucher> ProcessForChargesAsync(
            int journalVoucherId,
            int? entryId,
            int amount,
            DateOnly dcDate);

        Task<ProjectCartageSettings?> GetProjectCartageSettingsAsync(int projectId);
    }
}