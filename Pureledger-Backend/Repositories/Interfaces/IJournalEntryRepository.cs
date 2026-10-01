using PureLedger.Models;
using PureLedger.ViewModels.Reporting;

namespace PureLedger.Repositories.Interfaces
{
    public interface IJournalEntryRepository
    {
        #region Basic CRUD Operations

        Task<JournalEntry?> GetByIdWithDetailsAsync(int id);
        JournalEntry Add(JournalEntry entry);
        JournalEntry Update(JournalEntry entry);
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
            int? locationId,
            int page,
            int pageSize);

        Task<decimal> GetPreviousEntriesBalanceAsync(
            DateOnly startDate,
            DateOnly endDate,
            int? projectId,
            int? categoryId,
            int? accountId,
            int? bookerId,
            int? locationId,
            int page,
            int pageSize);

        Task<(decimal Debit, decimal Credit)> GetOpeningBalanceAsync(
            DateOnly startDate,
            int? projectId,
            int? categoryId,
            int? accountId);

        Task<Dictionary<int, int>> GetPreviousQtyBalancePerAccountAsync(
            DateOnly startDate,
            DateOnly endDate,
            int? projectId,
            int? categoryId,
            int? accountId,
            int? bookerId,
            int? locationId,
            int page,
            int pageSize);

        #endregion

        #region User Report Queries

        Task<(List<(JournalEntry Entry, DeliveryChallanEntry? DCEntry)> Entries, int TotalCount)> GetEntriesForUserReportAsync(
            DateOnly startDate,
            DateOnly endDate,
            int? projectId,
            int categoryId,
            int? accountId,
            int? bookerId,
            int? locationId,
            int page,
            int pageSize);

        Task<decimal> GetPreviousEntriesBalanceForUserReportAsync(
            DateOnly startDate,
            DateOnly endDate,
            int? projectId,
            int categoryId,
            int? accountId,
            int? bookerId,
            int? locationId,
            int page,
            int pageSize);

        Task<(decimal Debit, decimal Credit)> GetOpeningBalanceForUserReportAsync(
            DateOnly startDate,
            int? projectId,
            int categoryId,
            int? accountId);

        #endregion

        #region Product Report Queries

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

        Task<List<ProductStockLocationMovementDto>> GetProductStockLocationMovementsAsync(
            int projectId,
            int productId,
            DateOnly endDate);

        Task<List<ProductTransitMovementDto>> GetProductTransitMovementsAsync(
            int projectId,
            DateOnly endDate,
            int? categoryId = null,
            int? productId = null);

        #endregion

        #region Account and Aging Report Queries

        Task<(List<AccountReportViewModels> Entries, int TotalCount)> GetEntriesForAccountReportAsync(
            int projectId,
            int? categoryId,
            int? accountId,
            int? bookerId,
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
        Task<List<(int ProductId, string ProductName, decimal TotalQuantity, int EntryCount)>> GetDerivedVoucherProductQuantitiesAsync(int? projectId, DateOnly? startDate = null, DateOnly? endDate = null);

        #endregion
    }
}
