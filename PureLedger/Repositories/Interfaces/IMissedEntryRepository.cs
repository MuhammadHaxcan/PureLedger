public interface IMissedEntryRepository
{
    Task<MissedEntry> AddAsync(MissedEntry entry);
    Task<List<MissedEntry>> GetByCustomerAccountIdAsync(int customerAccountId);
    Task<List<MissedEntry>> GetByProductAccountIdAsync(int productAccountId);
    Task<List<MissedEntry>> GetByDateRangeAsync(DateOnly startDate, DateOnly endDate);
    Task<List<MissedEntry>> GetByProjectAsync(int projectId);
    Task<List<MissedEntry>> GetFilteredEntriesAsync(DateOnly? startDate, DateOnly? endDate, int? projectId);
    Task<MissedEntry?> GetByIdAsync(int id);
}