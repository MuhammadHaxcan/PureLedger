using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IJournalEntryRepository
    {
        Task<JournalEntry?> GetByIdWithDetailsAsync(int id);
        Task<JournalEntry> AddAsync(JournalEntry entry);
        Task<JournalEntry> UpdateAsync(JournalEntry entry);
        Task AddTagsAsync(int entryId, IEnumerable<int> tagIds);
        Task<List<JournalEntry>> GetEntriesByVoucherIdAsync(
            int voucherId,
            int? categoryId,
            int? accountId);
        Task<bool> DeleteAsync(int entryId);
        Task<JournalEntry?> GetByCartageId(int id);
        Task<List<JournalEntry>> GetEntriesByCriteriaAsync(List<int> accountIds, int categoryId, int? projectId, DateOnly referenceDate);
        Task<decimal> GetAccountBalanceUpToVoucherAsync(int accountId, int maxVoucherId);
        Task<List<JournalEntry>> GetJournalEntriesByFiltersAsync(List<int> accountIds, int? categoryId, int? projectId);
        Task<List<JournalEntry>> GetEntriesForMasterReportAsync(DateOnly startDate, DateOnly endDate, int? projectId, int? categoryId, int? accountId);
        Task<JournalEntry?> GetCartageEntryByJournalVoucherIdAsync(int journalVoucherId);
    }
}