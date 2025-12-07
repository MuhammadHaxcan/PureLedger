using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IDeliveryChallanVoucherRepository
    {
        Task<DeliveryChallanVoucher?> GetByIdAsync(int id);
        Task<DeliveryChallanVoucher?> GetByIdWithEntriesAsync(int id);
        Task<DeliveryChallanVoucher?> GetByJournalVoucherIdAsync(int journalVoucherId);
        Task<List<DeliveryChallanVoucher>> GetByDateRangeAsync(DateOnly startDate, DateOnly endDate);
        Task<DeliveryChallanVoucher> AddAsync(DeliveryChallanVoucher deliveryChallanVoucher);
        Task<DeliveryChallanVoucher> UpdateAsync(DeliveryChallanVoucher deliveryChallanVoucher);
        Task<AccountData?> GetAccountDataByAccountNameAsync(string accountName);
        Task<bool> DeleteAsync(int deliveryChallanVoucherId);
    }
}