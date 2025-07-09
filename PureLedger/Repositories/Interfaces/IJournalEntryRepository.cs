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
    }
}