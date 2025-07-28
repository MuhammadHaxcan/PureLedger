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
        Task<List<JournalVoucher>> GetVouchersWithModifiedEntriesAsync();
        Task<bool> DeleteAsync(int journalVoucherId);
    }
}