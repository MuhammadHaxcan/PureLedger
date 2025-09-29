using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class MissedEntryRepository : IMissedEntryRepository
    {
        private readonly LedgerDbContext _dbContext;

        public MissedEntryRepository(LedgerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<MissedEntry> AddAsync(MissedEntry entry)
        {
            var result = await _dbContext.MissedEntries.AddAsync(entry);
            return result.Entity;
        }

        public async Task<List<MissedEntry>> GetByCustomerAccountIdAsync(int customerAccountId)
        {
            return await _dbContext.MissedEntries
                .Include(m => m.CustomerAccount)
                .Include(m => m.ProductAccount)
                .Include(m => m.Project)
                .Where(m => m.CustomerAccountId == customerAccountId)
                .OrderByDescending(m => m.MissedDate)
                .ToListAsync();
        }

        public async Task<List<MissedEntry>> GetByProductAccountIdAsync(int productAccountId)
        {
            return await _dbContext.MissedEntries
                .Include(m => m.CustomerAccount)
                .Include(m => m.ProductAccount)
                .Include(m => m.Project)
                .Where(m => m.ProductAccountId == productAccountId)
                .OrderByDescending(m => m.MissedDate)
                .ToListAsync();
        }

        public async Task<List<MissedEntry>> GetByDateRangeAsync(DateOnly startDate, DateOnly endDate)
        {
            return await _dbContext.MissedEntries
                .Include(m => m.CustomerAccount)
                .Include(m => m.ProductAccount)
                .Include(m => m.Project)
                .Where(m => m.MissedDate >= startDate && m.MissedDate <= endDate)
                .OrderByDescending(m => m.MissedDate)
                .ToListAsync();
        }

        public async Task<List<MissedEntry>> GetByProjectAsync(int projectId)
        {
            return await _dbContext.MissedEntries
                .Include(m => m.CustomerAccount)
                .Include(m => m.ProductAccount)
                .Include(m => m.Project)
                .Where(m => m.ProjectId == projectId)
                .OrderByDescending(m => m.MissedDate)
                .ToListAsync();
        }

        public async Task<List<MissedEntry>> GetFilteredEntriesAsync(DateOnly? startDate, DateOnly? endDate, int? projectId)
        {
            var query = _dbContext.MissedEntries
                .Include(m => m.CustomerAccount)
                .Include(m => m.ProductAccount)
                .Include(m => m.Project)
                .AsQueryable();

            if (startDate.HasValue)
                query = query.Where(m => m.MissedDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(m => m.MissedDate <= endDate.Value);

            if (projectId.HasValue)
                query = query.Where(m => m.ProjectId == projectId.Value);

            return await query
                .OrderByDescending(m => m.MissedDate)
                .ToListAsync();
        }

        public async Task<MissedEntry?> GetByIdAsync(int id)
        {
            return await _dbContext.MissedEntries
                .Include(m => m.CustomerAccount)
                .Include(m => m.ProductAccount)
                .Include(m => m.Project)
                .FirstOrDefaultAsync(m => m.MissedEntryId == id);
        }
    }
}