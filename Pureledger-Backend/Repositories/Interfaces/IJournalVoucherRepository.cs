using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IJournalVoucherRepository
    {
        Task<List<JournalVoucher>> GetAllWithEntriesAsync();
        Task<JournalVoucher?> GetWithEntriesAsync(int id);
        Task<JournalVoucher> AddAsync(JournalVoucher voucher);
        Task<JournalVoucher> UpdateAsync(JournalVoucher voucher);
        Task<List<JournalVoucher>> GetFilteredVouchersAsync(
            DateOnly? sinceDate,
            DateOnly? toDate,
            int? projectId,
            bool includeEntries);
        Task<List<JournalVoucher>> GetVouchersWithModifiedEntriesAsync(int? projectId);
        Task<(List<JournalVoucher> Vouchers, int TotalCount)> GetVouchersWithModifiedEntriesPaginatedAsync(int? projectId, int page, int pageSize);
        Task<bool> DeleteAsync(int journalVoucherId);
        Task<List<JournalVoucher>> GetAllBaseVouchersAsync();
        Task<(List<JournalVoucher> Vouchers, int TotalCount)> GetAllBaseVouchersPaginatedAsync(int? projectId, int page, int pageSize);
        Task<JournalVoucher?> GetBaseVoucherWithUnprocessedEntriesAsync(int id);

        /// <summary>
        /// Batch fetch multiple vouchers with their entries - eliminates N+1 query pattern
        /// </summary>
        Task<List<JournalVoucher>> GetByIdsWithEntriesAsync(List<int> voucherIds);

        /// <summary>
        /// Get all base vouchers with entries for summary statistics calculation
        /// </summary>
        Task<List<JournalVoucher>> GetBaseVouchersSummaryDataAsync(int? projectId);
    }
}