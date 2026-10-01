using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IDeliveryChallanVoucherRepository
    {
        Task<DeliveryChallanVoucher?> GetByIdAsync(int id);
        Task<DeliveryChallanVoucher?> GetByIdWithEntriesAsync(int id);
        Task<DeliveryChallanVoucher?> GetByJournalVoucherIdAsync(int journalVoucherId);
        Task<List<DeliveryChallanVoucher>> GetByDateRangeAsync(DateOnly startDate, DateOnly endDate);
        DeliveryChallanVoucher Add(DeliveryChallanVoucher deliveryChallanVoucher);
        DeliveryChallanVoucher Update(DeliveryChallanVoucher deliveryChallanVoucher);
        Task<AccountData?> GetAccountDataByAccountNameAsync(string accountName);
        Task<bool> DeleteAsync(int deliveryChallanVoucherId);
    }
}
