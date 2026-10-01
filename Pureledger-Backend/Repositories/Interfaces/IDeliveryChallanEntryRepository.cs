using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IDeliveryChallanEntryRepository
    {
        Task<DeliveryChallanEntry?> GetByIdAsync(int id);
        Task<List<DeliveryChallanEntry>> GetByDeliveryChallanIdAsync(int deliveryChallanId);
        Task<DeliveryChallanEntry?> GetByDeliveryChallanIdAndJournalEntryIdAsync(int deliveryChallanId, int journalEntryId);
        Task<DeliveryChallanEntry?> GetByJournalEntryIdAsync(int journalEntryId);
        DeliveryChallanEntry Add(DeliveryChallanEntry deliveryChallanEntry);
        DeliveryChallanEntry Update(DeliveryChallanEntry deliveryChallanEntry);
        Task<bool> DeleteAsync(int deliveryChallanEntryId);
    }
}
