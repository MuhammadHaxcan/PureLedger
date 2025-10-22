using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IJournalEntryRepository
    {
        Task<JournalEntry?> GetByIdWithDetailsAsync(int id);
        Task<JournalEntry> AddAsync(JournalEntry entry);
        Task<JournalEntry> UpdateAsync(JournalEntry entry);
        Task<List<JournalEntry>> GetEntriesByVoucherIdAsync(
            int voucherId,
            int? categoryId,
            int? accountId);
        Task<bool> DeleteAsync(int entryId);
        Task<JournalEntry?> GetByCartageId(int id);
        Task<List<JournalEntry>> GetEntriesByCriteriaAsync(List<int> accountIds, int categoryId, int? projectId, DateOnly referenceDate);
        Task<decimal> GetAccountBalanceUpToVoucherAsync(int accountId, int maxVoucherId);
        Task<List<JournalEntry>> GetJournalEntriesByFiltersAsync(List<int> accountIds, int? categoryId, int? projectId);
        Task<List<JournalEntry>> GetCartageEntriesByJournalVoucherIdAsync(int journalVoucherId);

        Task<(List<JournalEntry> Entries, int TotalCount)> GetEntriesForMasterReportAsync(
            DateOnly startDate,
            DateOnly endDate,
            int? projectId,
            int? categoryId,
            int? accountId,
            int page,
            int pageSize);

        Task<decimal> GetPreviousEntriesBalanceAsync(
            DateOnly startDate,
            DateOnly endDate,
            int? projectId,
            int? categoryId,
            int? accountId,
            int page,
            int pageSize);
        Task<decimal> GetAccountBalanceUpToVoucherAsyncMinus1(int accountId, int maxVoucherId);
        Task<(List<(JournalEntry Entry, DeliveryChallanEntry? DCEntry)>, int TotalCount)> GetProcessedEntriesForProductReportAsync(
            DateOnly startDate,
            DateOnly endDate,
            int projectId,
            int? categoryId,
            int? accountId,
            int? locationId,
            int page,
            int pageSize);
        Task<(decimal Debit, decimal Credit)> GetOpeningBalanceAsync(
            DateOnly startDate,
            int? projectId,
            int? categoryId,
            int? accountId);

        Task<List<(JournalEntry Entry, DeliveryChallanEntry? DCEntry)>> GetAllEntriesForProductReportAsync(
    DateOnly startDate,
    DateOnly endDate,
    int projectId,
    int? categoryId,
    int? accountId,
    int? locationId);
    }
}