using PureLedger.Data;
using Microsoft.EntityFrameworkCore;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;
using PureLedger.ViewModels.Reporting;
using System.Linq;

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
                .AsNoTracking()
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
                .AsNoTracking()
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
                .AsNoTracking()
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
                .AsNoTracking()
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
                .AsNoTracking()
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

        // Optimized paginated query: project to MissedEntryViewModel to avoid loading navigation graphs
        public async Task<(List<MissedEntryViewModel> Entries, int TotalCount)> GetFilteredEntriesPaginatedAsync(
            DateOnly? startDate,
            DateOnly? endDate,
            int? projectId,
            int page,
            int pageSize)
        {
            // Defensive inputs
            page = Math.Max(0, page);
            pageSize = Math.Max(1, pageSize);

            var baseQuery = _dbContext.MissedEntries.AsNoTracking().AsQueryable();

            if (startDate.HasValue)
                baseQuery = baseQuery.Where(m => m.MissedDate >= startDate.Value);

            if (endDate.HasValue)
                baseQuery = baseQuery.Where(m => m.MissedDate <= endDate.Value);

            if (projectId.HasValue)
                baseQuery = baseQuery.Where(m => m.ProjectId == projectId.Value);

            var totalCount = await baseQuery.CountAsync();

            var dataQuery = baseQuery
                .OrderByDescending(m => m.MissedDate)
                .ThenByDescending(m => m.MissedEntryId)
                .Skip(page * pageSize)
                .Take(pageSize)
                .Select(m => new MissedEntryViewModel
                {
                    MissedEntryId = m.MissedEntryId,
                    CustomerName = m.CustomerAccount != null ? m.CustomerAccount.AccountName ?? "" : "Unknown Customer",
                    ProductName = m.ProductAccount != null ? m.ProductAccount.AccountName ?? "" : "Unknown Product",
                    ProjectName = m.Project != null ? m.Project.ProjectName ?? "" : "Unknown Project",
                    MissedDate = m.MissedDate,
                    Quantity = m.Quantity,
                    Rate = m.Rate,
                    Discount = m.Discount,
                    Description = m.Description,
                    TotalValue = (m.Quantity * m.Rate) - m.Discount,
                    CustomerAccountId = m.CustomerAccountId,
                    ProductAccountId = m.ProductAccountId,
                    ProjectId = m.ProjectId
                });

            var entries = await dataQuery.ToListAsync();

            return (entries, totalCount);
        }

        public async Task<MissedEntry?> GetByIdAsync(int id)
        {
            return await _dbContext.MissedEntries
                .AsNoTracking()
                .Include(m => m.CustomerAccount)
                .Include(m => m.ProductAccount)
                .Include(m => m.Project)
                .FirstOrDefaultAsync(m => m.MissedEntryId == id);
        }

        public async Task<List<MissedCustomerGroupDto>> GetMissedEntriesByCustomerGroupedAsync(DateOnly? startDate, DateOnly? endDate, int? projectId)
        {
            var query = _dbContext.MissedEntries.AsNoTracking().AsQueryable();

            if (startDate.HasValue) query = query.Where(m => m.MissedDate >= startDate.Value);
            if (endDate.HasValue) query = query.Where(m => m.MissedDate <= endDate.Value);
            if (projectId.HasValue) query = query.Where(m => m.ProjectId == projectId.Value);

            var grouped = await query
                .Where(m => m.CustomerAccountId != 0)
                .GroupBy(m => new { m.CustomerAccountId, m.CustomerAccount!.AccountName })
                .Select(g => new MissedCustomerGroupDto
                {
                    CustomerAccountId = g.Key.CustomerAccountId,
                    CustomerName = g.Key.AccountName ?? "Unknown",
                    EntryCount = g.Count(),
                    TotalQuantity = g.Sum(x => x.Quantity),
                    TotalValue = g.Sum(x => (x.Quantity * x.Rate) - x.Discount),
                    UniqueProductCount = g.Select(x => x.ProductAccountId).Distinct().Count(),
                    AverageQuantityPerEntry = g.Count() > 0 ? Math.Round((decimal)g.Sum(x => x.Quantity) / g.Count(), 2) : 0m
                })
                .OrderByDescending(x => x.TotalQuantity)
                .ToListAsync();

            return grouped;
        }

        public async Task<List<MissedProductGroupDto>> GetMissedEntriesByProductGroupedAsync(DateOnly? startDate, DateOnly? endDate, int? projectId)
        {
            var query = _dbContext.MissedEntries.AsNoTracking().AsQueryable();

            if (startDate.HasValue) query = query.Where(m => m.MissedDate >= startDate.Value);
            if (endDate.HasValue) query = query.Where(m => m.MissedDate <= endDate.Value);
            if (projectId.HasValue) query = query.Where(m => m.ProjectId == projectId.Value);

            var grouped = await query
                .Where(m => m.ProductAccountId != 0)
                .GroupBy(m => new { m.ProductAccountId, m.ProductAccount!.AccountName })
                .Select(g => new MissedProductGroupDto
                {
                    ProductAccountId = g.Key.ProductAccountId,
                    ProductName = g.Key.AccountName ?? "Unknown",
                    EntryCount = g.Count(),
                    TotalQuantity = g.Sum(x => x.Quantity),
                    TotalValue = g.Sum(x => (x.Quantity * x.Rate) - x.Discount),
                    UniqueCustomerCount = g.Select(x => x.CustomerAccountId).Distinct().Count(),
                    AverageRate = g.Any() ? Math.Round(g.Average(x => x.Rate), 2) : 0m,
                    AverageDiscount = g.Any() ? Math.Round(g.Average(x => x.Discount), 2) : 0m
                })
                .OrderByDescending(x => x.TotalQuantity)
                .ToListAsync();

            return grouped;
        }
    }
}