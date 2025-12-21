using PureLedger.Models;
using PureLedger.Services;
using PureLedger.ViewModels.DeliveryChallan;

namespace PureLedger.Services.Interface
{
    public interface IDeliveryChallanService
    {
        #region Entity Retrieval

        Task<List<Project>> GetAllProjectsAsync();
        Task<List<Project>> GetUserProjectsAsync(int userId);
        Task<List<Location>> GetLocationsAsync();
        Task<JournalEntry?> GetJournalEntryByIdAsync(int journalEntryId);

        #endregion

        #region Journal Entry Retrieval

        Task<List<JournalEntryDetailViewModel>> GetVoucherEntriesAsync(int voucherId);
        Task<List<JournalEntry>> GetUnprocessedEntriesAsync(int voucherId);

        #endregion

        #region Delivery Challan Retrieval

        Task<DeliveryChallanVoucher?> GetDCByIdAsync(int deliveryChallanId);
        Task<DeliveryChallanVoucher?> GetDCByJournalVoucherIdAsync(int journalVoucherId);
        Task<List<DeliveryChallanVoucher>> GetDCsByDateRangeAsync(DateOnly startDate, DateOnly endDate);
        Task<List<DeliveryChallanEntry>> GetDCEntriesByVoucherIdAsync(int deliveryChallanId);

        #endregion

        #region Filtered Entries Retrieval

        Task<DeliveryChallanFilterViewModel> GetFilteredEntriesAsync(
            DateOnly? sinceDate,
            DateOnly? toDate,
            int? projectId = null,
            int? categoryId = null,
            int? accountId = null,
            int? userId = null);

        Task<DeliveryChallanFilterViewModel> GetPendingForChargesAsync(
            DateOnly? sinceDate,
            DateOnly? toDate,
            int? projectId = null,
            int? categoryId = null,
            int? accountId = null,
            int? userId = null);

        #endregion

        #region DC Voucher and Entry Creation

        Task<DeliveryChallanVoucher> CreateDCVoucherAsync(int journalVoucherId, DateOnly dcDate);
        Task<DeliveryChallanVoucher> CreateDCVoucherAsync(int journalVoucherId, DateOnly dcDate, string? reference);

        Task<DeliveryChallanEntry> CreateDCEntryAsync(int deliveryChallanId, int journalEntryId, int locationId, string? remarks, string? otherReference);
        Task<DeliveryChallanEntry> CreateDCEntryAsync(int deliveryChallanId, int journalEntryId, int locationId, string? remarks, string? otherReference, DateOnly entryDate);

        #endregion

        #region DC Processing - Transactional

        Task<DeliveryChallanEntry> CreateSingleDCAsync(
            int journalEntryId,
            DateOnly dcDate,
            string? otherReference,
            int locationId,
            string? remarks,
            bool includeForCharges,
            int forAmount,
            int? forAccountId = null);

        Task<(int deliveryChallanId, List<EntryProcessResult> results)> ProcessBatchDCAsync(
            int journalVoucherId,
            DateOnly dcDate,
            string? otherReference,
            int locationId,
            string? remarks,
            bool includeForCharges,
            int forAmount,
            int? forAccountId = null);

        Task<DeliveryChallanVoucher> ProcessCartageAsync(CartageViewModel model);

        Task<DeliveryChallanVoucher> ProcessCartageAsync(
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

        #region DC Update and Status Management

        Task<DeliveryChallanVoucher> UpdateDCVoucherAsync(DeliveryChallanVoucher voucher);
        Task<bool> CompleteDCAsync(int deliveryChallanId, DateOnly finalDeliveryDate);
        Task UpdateVoucherStatusAsync(int journalVoucherId);
        Task MarkVoucherCompletedAsync(int journalVoucherId);

        #endregion

        #region Lookup Operations

        Task<IEnumerable<Account>> GetAccountsAsync(int categoryId);
        Task<IEnumerable<Category>> GetCategoriesAsync(int projectId);
        Task<IEnumerable<Category>> GetCategoriesForUserAsync(int projectId, int userId);
        Task<ProjectCartageSettings?> GetCartageSettingsAsync(int projectId);

        #endregion

        #region FOR Charges Processing

        Task<JournalVoucher> ProcessForChargesAsync(
            int journalVoucherId,
            int? entryId,
            int amount,
            DateOnly dcDate,
            int? forAccountId = null);

        Task<int?> GetForChargesCategoryIdAsync(int projectId);

        #endregion
    }
}
