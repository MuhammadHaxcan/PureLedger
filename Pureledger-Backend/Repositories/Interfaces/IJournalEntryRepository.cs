using PureLedger.Models;
using PureLedger.ViewModels.Reporting;

namespace PureLedger.Repositories.Interfaces
{
    public interface IJournalEntryRepository
    {
        #region Basic CRUD Operations

        Task<JournalEntry?> GetByIdWithDetailsAsync(int id);
        Task<JournalEntry> AddAsync(JournalEntry entry);
        Task<JournalEntry> UpdateAsync(JournalEntry entry);
        Task<bool> DeleteAsync(int entryId);

        #endregion

        #region Entry Retrieval By Voucher

        Task<List<JournalEntry>> GetEntriesByVoucherIdAsync(
            int voucherId,
            int? categoryId,
            int? accountId);
        Task<List<JournalEntry>> GetCartageEntriesByJournalVoucherIdAsync(int journalVoucherId);

        #endregion

        #region Entry Retrieval By Criteria

        Task<JournalEntry?> GetByCartageId(int id);
        Task<List<JournalEntry>> GetEntriesByCriteriaAsync(List<int> accountIds, int categoryId, int? projectId, DateOnly referenceDate);
        Task<List<JournalEntry>> GetEntriesByVoucherIdsAndAccountAsync(List<int> voucherIds, int accountId);

        #endregion

        #region Account Balance Calculations

        Task<decimal> GetAccountBalanceUpToVoucherAsync(int accountId, int maxVoucherId);
        Task<decimal> GetAccountBalanceUpToVoucherAsyncMinus1(int accountId, int maxVoucherId);

        #endregion

        #region Master Report Queries

        Task<(List<(JournalEntry Entry, DeliveryChallanEntry? DCEntry)> Entries, int TotalCount)> GetEntriesForMasterReportAsync(
            DateOnly startDate,
            DateOnly endDate,
            int? projectId,
            int? categoryId,
            int? accountId,
            int? bookerId,
            int page,
            int pageSize);

        Task<decimal> GetPreviousEntriesBalanceAsync(
            DateOnly startDate,
            DateOnly endDate,
            int? projectId,
            int? categoryId,
            int? accountId,
            int? bookerId,
            int page,
            int pageSize);

        Task<(decimal Debit, decimal Credit)> GetOpeningBalanceAsync(
            DateOnly startDate,
            int? projectId,
            int? categoryId,
            int? accountId);

        #endregion

        #region User Report Queries

        Task<(List<JournalEntry> Entries, int TotalCount)> GetEntriesForUserReportAsync(
            DateOnly startDate,
            DateOnly endDate,
            int? projectId,
            int categoryId,
            int? accountId,
            int? bookerId,
            int page,
            int pageSize);

        Task<decimal> GetPreviousEntriesBalanceForUserReportAsync(
            DateOnly startDate,
            DateOnly endDate,
            int? projectId,
            int categoryId,
            int? accountId,
            int? bookerId,
            int page,
            int pageSize);

        Task<(decimal Debit, decimal Credit)> GetOpeningBalanceForUserReportAsync(
            DateOnly startDate,
            int? projectId,
            int categoryId,
            int? accountId);

        #endregion

        #region Product Report Queries

        Task<(List<(JournalEntry Entry, DeliveryChallanEntry? DCEntry)>, int TotalCount)> GetProcessedEntriesForProductReportAsync(
            DateOnly startDate,
            DateOnly endDate,
            int projectId,
            int? categoryId,
            int? accountId,
            int? locationId,
            int page,
            int pageSize);

        Task<List<(JournalEntry Entry, DeliveryChallanEntry? DCEntry)>> GetAllEntriesForProductReportAsync(
            DateOnly startDate,
            DateOnly endDate,
            int projectId,
            int? categoryId,
            int? accountId,
            int? locationId);

        Task<(List<int> ProductIds, int TotalCount)> GetProductIdsForReportAsync(
            DateOnly startDate,
            DateOnly endDate,
            int projectId,
            int? categoryId,
            int? accountId,
            int? locationId,
            int page,
            int pageSize);

        Task<List<ProductAggregateDto>> GetProductAggregatesAsync(
            List<int> productIds,
            DateOnly startDate,
            DateOnly endDate,
            int projectId,
            int? categoryId,
            int? locationId);

        #endregion

        #region Account and Aging Report Queries

        Task<(List<AccountReportViewModels> Entries, int TotalCount)> GetEntriesForAccountReportAsync(
            int projectId,
            int? categoryId,
            int? accountId,
            int page,
            int pageSize);

        Task<(List<AgingReportViewModels> Entries, int TotalCount)> GetEntriesForAgingReportAsync(
            List<int> accountIds,
            int categoryId,
            int? projectId,
            DateOnly referenceDate,
            int page,
            int pageSize);

        #endregion

        #region Base Vouchers Summary Queries

        /// <summary>
        /// Get aggregated product quantities from derived vouchers for base vouchers summary
        /// Efficiently calculates most ordered products by aggregating at database level
        /// </summary>
        Task<List<(int ProductId, string ProductName, decimal TotalQuantity, int EntryCount)>> GetDerivedVoucherProductQuantitiesAsync(int? projectId);

        #endregion
    }
}
