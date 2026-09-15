using PureLedger.ViewModels.Reporting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PureLedger.Repositories.Interfaces
{
    public interface IMissedEntryRepository
    {
        MissedEntry Add(MissedEntry entry);
        Task<List<MissedEntry>> GetFilteredEntriesAsync(DateOnly? startDate, DateOnly? endDate, int? projectId);

        Task<MissedEntry?> GetByIdAsync(int id);

        // Return lightweight DTOs for paginated queries to avoid loading full entity graphs
        Task<(List<MissedEntryViewModel> Entries, int TotalCount)> GetFilteredEntriesPaginatedAsync(
            DateOnly? startDate,
            DateOnly? endDate,
            int? projectId,
            int page,
            int pageSize);

        Task<List<MissedEntry>> GetByCustomerAccountIdAsync(int customerAccountId);

        // New DB-side grouped queries (lightweight DTOs) to avoid loading entire MissedEntry sets
        Task<List<MissedCustomerGroupDto>> GetMissedEntriesByCustomerGroupedAsync(DateOnly? startDate, DateOnly? endDate, int? projectId);

        Task<List<MissedProductGroupDto>> GetMissedEntriesByProductGroupedAsync(DateOnly? startDate, DateOnly? endDate, int? projectId);
    }
}
