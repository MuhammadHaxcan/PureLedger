using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IJournalVoucherRepository
    {
        Task<List<JournalVoucher>> GetAllWithEntriesAsync();
        Task<JournalVoucher?> GetWithEntriesAsync(int id);
        Task<JournalVoucher> AddAsync(JournalVoucher voucher);
        Task<JournalVoucher> UpdateAsync(JournalVoucher voucher);
    }
}