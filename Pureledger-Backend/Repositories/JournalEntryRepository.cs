using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;
using PureLedger.ViewModels.Reporting;

namespace PureLedger.Repositories
{
    public class JournalEntryRepository : IJournalEntryRepository
    {
        private readonly LedgerDbContext _context;

        public JournalEntryRepository(LedgerDbContext context)
        {
            _context = context;
        }

        public async Task<JournalEntry?> GetByIdWithDetailsAsync(int id)
        {
            return await _context.JournalEntries
                .Include(e => e.Account)
                .Include(e => e.Category)
                .Include(e => e.JournalVoucher)
                .FirstOrDefaultAsync(e => e.JournalEntryId == id);
        }

        public async Task<JournalEntry> AddAsync(JournalEntry entry)
        {
            _context.JournalEntries.Add(entry);
            await _context.SaveChangesAsync();
            return entry;
        }

        public async Task<JournalEntry> UpdateAsync(JournalEntry entry)
        {
            _context.JournalEntries.Update(entry);
            await _context.SaveChangesAsync();
            return entry;
        }

        public async Task<List<JournalEntry>> GetEntriesByVoucherIdAsync(
            int voucherId,
            int? categoryId,
            int? accountId)
        {
            IQueryable<JournalEntry> query = _context.JournalEntries
                .Include(je => je.Account)
                .Include(je => je.Category)
                .Where(je => je.JournalVoucherId == voucherId);

            if (categoryId.HasValue)
            {
                query = query.Where(je => je.CategoryId == categoryId.Value);
            }

            if (accountId.HasValue)
            {
                query = query.Where(je => je.AccountId == accountId.Value);
            }

            return await query.ToListAsync();
        }

        public async Task<bool> DeleteAsync(int entryId)
        {
            var entry = await _context.JournalEntries.FindAsync(entryId);
            if (entry == null)
                return false;

            _context.JournalEntries.Remove(entry);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<JournalEntry?> GetByCartageId(int id)
        {
            return await _context.JournalEntries.FirstOrDefaultAsync(e => e.JournalVoucherId == id && e.Debit == 0 && e.Credit > 0);
        }

        public async Task<List<JournalEntry>> GetEntriesByCriteriaAsync(
            List<int> accountIds, int categoryId, int? projectId, DateOnly referenceDate)
        {

            var query = _context.JournalEntries
                
                .Include(je => je.JournalVoucher)
                .Where(je =>
                    accountIds.Contains(je.AccountId) &&
                    je.CategoryId == categoryId &&
                    je.JournalVoucher.IsCompleted &&
                    je.JournalVoucher.CreditableDate <= referenceDate);

            if (projectId.HasValue)
            {
                query = query.Where(je => je.JournalVoucher.ProjectId == projectId.Value);
            }

            return await query.ToListAsync();
        }

        public async Task<decimal> GetAccountBalanceUpToVoucherAsync(int accountId, int maxVoucherId)
        {
            // Optimized: Single aggregate query instead of two separate queries
            var totals = await _context.JournalEntries
                .AsNoTracking()
                .Where(je =>
                    je.AccountId == accountId &&
                    je.JournalVoucher.IsCompleted &&
                    je.JournalVoucherId < maxVoucherId
                )
                .GroupBy(je => 1)
                .Select(g => new
                {
                    Debit = g.Sum(je => (decimal?)je.Debit) ?? 0m,
                    Credit = g.Sum(je => (decimal?)je.Credit) ?? 0m
                })
                .FirstOrDefaultAsync();

            return (totals?.Debit ?? 0m) - (totals?.Credit ?? 0m);
        }

        public async Task<decimal> GetAccountBalanceUpToVoucherAsyncMinus1(int accountId, int maxVoucherId)
        {
            // Optimized: Single aggregate query instead of two separate queries
            var totals = await _context.JournalEntries
                .AsNoTracking()
                .Where(je =>
                    je.AccountId == accountId &&
                    je.JournalVoucher.IsCompleted &&
                    je.JournalVoucherId <= maxVoucherId)
                .GroupBy(je => 1)
                .Select(g => new
                {
                    Debit = g.Sum(je => (decimal?)je.Debit) ?? 0m,
                    Credit = g.Sum(je => (decimal?)je.Credit) ?? 0m
                })
                .FirstOrDefaultAsync();

            return (totals?.Debit ?? 0m) - (totals?.Credit ?? 0m);
        }

        public async Task<(List<JournalEntry> Entries, int TotalCount)> GetEntriesForMasterReportAsync(
            DateOnly startDate,
            DateOnly endDate,
            int? projectId,
            int? categoryId,
            int? accountId,
            int? bookerId,
            int page,
            int pageSize)
        {
            var query = _context.JournalEntries
                .AsNoTracking()
                .Include(je => je.JournalVoucher)
                    .ThenInclude(jv => jv.Project)
                .Include(je => je.JournalVoucher)
                    .ThenInclude(jv => jv.Booker)
                .Include(je => je.Account)
                .Include(je => je.Category)
                .Where(je =>
                    je.JournalVoucher.IsCompleted &&
                    je.JournalVoucher.EntryDate >= startDate &&
                    je.JournalVoucher.EntryDate <= endDate);

            if (projectId.HasValue)
            {
                query = query.Where(je => je.JournalVoucher.ProjectId == projectId.Value);
            }

            if (categoryId.HasValue)
            {
                query = query.Where(je => je.CategoryId == categoryId.Value);
            }

            if (accountId.HasValue)
            {
                query = query.Where(je => je.AccountId == accountId.Value);
            }

            if (bookerId.HasValue)
            {
                query = query.Where(je => je.JournalVoucher.BookerId == bookerId.Value);
            }

            int totalCount = await query.CountAsync();

            var pagedResults = await query
                .OrderBy(je => je.JournalVoucher.EntryDate)
                .ThenBy(je => je.JournalVoucherId)
                .ThenBy(je => je.JournalEntryId)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (pagedResults, totalCount);
        }

        public async Task<List<JournalEntry>> GetCartageEntriesByJournalVoucherIdAsync(int journalVoucherId)
        {
            // Optimized: Batch fetch all entries in a single query instead of N+1 queries
            var referencedVoucherIds = await _context.JournalVoucherReferences
                .AsNoTracking()
                .Where(r => r.MainVoucherId == journalVoucherId && r.ReferenceType == "Cartage")
                .Select(r => r.ReferencedVoucherId)
                .ToListAsync();

            if (!referencedVoucherIds.Any())
                return new List<JournalEntry>();

            // Single query to fetch all cartage entries instead of looping
            return await _context.JournalEntries
                .AsNoTracking()
                .Where(e => referencedVoucherIds.Contains(e.JournalVoucherId) && e.Debit == 0 && e.Credit > 0)
                .ToListAsync();
        }

        public async Task<decimal> GetPreviousEntriesBalanceAsync(
            DateOnly startDate,
            DateOnly endDate,
            int? projectId,
            int? categoryId,
            int? accountId,
            int? bookerId,
            int page,
            int pageSize)
        {
            if (page <= 0 || pageSize <= 0)
            {
                return 0m;
            }

            var baseQuery = _context.JournalEntries
                .AsNoTracking()
                .Where(je =>
                    je.JournalVoucher.IsCompleted &&
                    je.JournalVoucher.EntryDate >= startDate &&
                    je.JournalVoucher.EntryDate <= endDate);

            if (projectId.HasValue)
            {
                baseQuery = baseQuery.Where(je => je.JournalVoucher.ProjectId == projectId.Value);
            }

            if (categoryId.HasValue)
            {
                baseQuery = baseQuery.Where(je => je.CategoryId == categoryId.Value);
            }

            if (accountId.HasValue)
            {
                baseQuery = baseQuery.Where(je => je.AccountId == accountId.Value);
            }

            if (bookerId.HasValue)
            {
                baseQuery = baseQuery.Where(je => je.JournalVoucher.BookerId == bookerId.Value);
            }

            int takeCount = page * pageSize;

            // Optimized: Single aggregate query instead of two separate queries
            var totals = await baseQuery
                .OrderBy(je => je.JournalVoucher.EntryDate)
                .ThenBy(je => je.JournalVoucherId)
                .ThenBy(je => je.JournalEntryId)
                .Take(takeCount)
                .GroupBy(je => 1)
                .Select(g => new
                {
                    Debit = g.Sum(je => (decimal?)je.Debit) ?? 0m,
                    Credit = g.Sum(je => (decimal?)je.Credit) ?? 0m
                })
                .FirstOrDefaultAsync();

            return (totals?.Debit ?? 0m) - (totals?.Credit ?? 0m);
        }

        public async Task<(List<(JournalEntry Entry, DeliveryChallanEntry? DCEntry)>, int TotalCount)> GetProcessedEntriesForProductReportAsync(
    DateOnly startDate,
    DateOnly endDate,
    int projectId,
    int? categoryId,
    int? accountId,
    int? locationId,
    int page,
    int pageSize)
        {

            var query = _context.JournalEntries
                .AsNoTracking()
                .Include(je => je.JournalVoucher)
                .Include(je => je.Account)
                .Where(je =>
                    je.JournalVoucher.ProjectId == projectId &&
                    je.JournalVoucher.IsCompleted &&  
                    je.IsProcessed &&                 
                    je.Quantity > 0 &&
                    je.Account.AccountGroupId == 2 &&
                    je.JournalVoucher.EntryDate >= startDate &&
                    je.JournalVoucher.EntryDate <= endDate);

            if (categoryId.HasValue)
            {
                query = query.Where(je => je.CategoryId == categoryId.Value);
            }

            if (accountId.HasValue)
            {
                query = query.Where(je => je.AccountId == accountId.Value);
            }

            int totalCount = await query.CountAsync();

            query = query.OrderBy(je => je.JournalVoucher.EntryDate)
                        .ThenBy(je => je.JournalVoucherId)
                        .ThenBy(je => je.JournalEntryId);

            query = query.Skip(page * pageSize).Take(pageSize);

            var entriesWithDC = await query
                .GroupJoin(
                    _context.DeliveryChallanEntries.Include(dce => dce.Location),
                    je => je.JournalEntryId,
                    dce => dce.JournalEntryId,
                    (je, dces) => new { JournalEntry = je, DCEntries = dces })
                .SelectMany(
                    x => x.DCEntries.DefaultIfEmpty(),
                    (x, dce) => new { JournalEntry = x.JournalEntry, DCEntry = dce })
                .ToListAsync();

            if (locationId.HasValue)
            {
                entriesWithDC = entriesWithDC
                    .Where(x => x.DCEntry != null && x.DCEntry.LocationId == locationId.Value)
                    .ToList();
            }

            var results = entriesWithDC.Select(x => (x.JournalEntry, x.DCEntry)).ToList();

            return (results, totalCount);
        }

        public async Task<(decimal Debit, decimal Credit)> GetOpeningBalanceAsync(
            DateOnly startDate,
            int? projectId,
            int? categoryId,
            int? accountId)
        {
            var query = _context.JournalEntries
                .AsNoTracking()
                .Where(je =>
                    je.JournalVoucher.IsCompleted &&
                    je.JournalVoucher.EntryDate < startDate);

            if (projectId.HasValue)
                query = query.Where(je => je.JournalVoucher.ProjectId == projectId.Value);

            if (categoryId.HasValue)
                query = query.Where(je => je.CategoryId == categoryId.Value);

            if (accountId.HasValue)
                query = query.Where(je => je.AccountId == accountId.Value);

            // Optimized: Single aggregate query instead of two separate queries
            var totals = await query
                .GroupBy(je => 1)
                .Select(g => new
                {
                    Debit = g.Sum(je => (decimal?)je.Debit) ?? 0m,
                    Credit = g.Sum(je => (decimal?)je.Credit) ?? 0m
                })
                .FirstOrDefaultAsync();

            return (totals?.Debit ?? 0m, totals?.Credit ?? 0m);
        }

        public async Task<List<(JournalEntry Entry, DeliveryChallanEntry? DCEntry)>> GetAllEntriesForProductReportAsync(
    DateOnly startDate,
    DateOnly endDate,
    int projectId,
    int? categoryId,
    int? accountId,
    int? locationId)
        {

            var query = _context.JournalEntries
                .AsNoTracking()
                .Include(je => je.JournalVoucher)
                .Include(je => je.Account)
                .Include(je => je.Category)
                .Where(je =>
                    je.JournalVoucher.ProjectId == projectId &&
                    je.JournalVoucher.IsCompleted &&  
                    je.IsProcessed &&                 
                    je.Quantity > 0 &&
                    je.Account.AccountGroupId == 2 &&
                    je.JournalVoucher.EntryDate <= endDate);

            if (startDate != DateOnly.MinValue)
            {
                query = query.Where(je => je.JournalVoucher.EntryDate >= startDate);
            }

            if (categoryId.HasValue)
            {
                query = query.Where(je => je.CategoryId == categoryId.Value);
            }

            if (accountId.HasValue)
            {
                query = query.Where(je => je.AccountId == accountId.Value);
            }

            query = query.OrderBy(je => je.JournalVoucher.EntryDate)
                        .ThenBy(je => je.JournalVoucherId)
                        .ThenBy(je => je.JournalEntryId);

            var entriesWithDC = await query
                .GroupJoin(
                    _context.DeliveryChallanEntries.Include(dce => dce.Location),
                    je => je.JournalEntryId,
                    dce => dce.JournalEntryId,
                    (je, dces) => new { JournalEntry = je, DCEntries = dces })
                .SelectMany(
                    x => x.DCEntries.DefaultIfEmpty(),
                    (x, dce) => new { JournalEntry = x.JournalEntry, DCEntry = dce })
                .ToListAsync();

            if (locationId.HasValue)
            {
                entriesWithDC = entriesWithDC
                    .Where(x => x.DCEntry != null && x.DCEntry.LocationId == locationId.Value)
                    .ToList();
            }

            var results = entriesWithDC.Select(x => (x.JournalEntry, x.DCEntry)).ToList();
            return results;
        }

        public async Task<(List<AccountReportViewModels> Entries, int TotalCount)> GetEntriesForAccountReportAsync(
            int projectId,
            int? categoryId,
            int? accountId,
            int page,
            int pageSize)
        {

            var query = _context.JournalEntries
                .AsNoTracking()
                .Include(je => je.Account)
                    .ThenInclude(a => a.Category)
                .Include(je => je.JournalVoucher)
                .Where(je =>
                    je.JournalVoucher.ProjectId == projectId &&
                    je.JournalVoucher.IsCompleted);

            if (categoryId.HasValue)
            {
                query = query.Where(je => je.CategoryId == categoryId.Value);
            }

            if (accountId.HasValue)
            {
                query = query.Where(je => je.AccountId == accountId.Value);
            }

            // Group by account + project + category id so repository returns IDs directly
            var groupedQuery = query
                .GroupBy(je => new {
                    je.AccountId,
                    AccountName = je.Account.AccountName,
                    CategoryName = je.Account.Category.CategoryName,
                    ProjectId = je.JournalVoucher.ProjectId,
                    CategoryId = je.CategoryId
                })
                .Select(g => new AccountReportViewModels
                {
                    AccountId = g.Key.AccountId.ToString(),
                    AccountName = g.Key.AccountName ?? "Unknown Account",
                    CategoryName = g.Key.CategoryName ?? "Unknown Category",
                    ProjectId = g.Key.ProjectId.ToString(),
                    CategoryId = g.Key.CategoryId.ToString(),
                    TotalDebit = g.Sum(je => je.Debit),
                    TotalCredit = g.Sum(je => je.Credit),
                    NetBalance = g.Sum(je => je.Debit) - g.Sum(je => je.Credit)
                });

            int totalCount = await groupedQuery.CountAsync();

            var pagedResults = await groupedQuery
                .OrderBy(g => g.CategoryName)
                .ThenBy(g => g.AccountName)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (pagedResults, totalCount);
        }

        public async Task<(List<AgingReportViewModels> Entries, int TotalCount)> GetEntriesForAgingReportAsync(
            List<int> accountIds,
            int categoryId,
            int? projectId,
            DateOnly referenceDate,
            int page,
            int pageSize)
        {

            var query = _context.JournalEntries
                .AsNoTracking()
                .Include(je => je.JournalVoucher)
                .Include(je => je.Account)
                .Where(je =>
                    accountIds.Contains(je.AccountId) &&
                    je.CategoryId == categoryId &&
                    je.JournalVoucher.IsCompleted &&
                    je.JournalVoucher.CreditableDate <= referenceDate);

            if (projectId.HasValue)
            {
                query = query.Where(je => je.JournalVoucher.ProjectId == projectId.Value);
            }

            var groupedQuery = query
                .GroupBy(je => new { je.AccountId, je.Account.AccountName })
                .Select(g => new
                {
                    AccountId = g.Key.AccountId,
                    AccountName = g.Key.AccountName,
                    TotalDebit = g.Sum(je => je.Debit),
                    TotalCredit = g.Sum(je => je.Credit)
                });

            int totalCount = await groupedQuery.CountAsync();

            var pagedResults = await groupedQuery
                .OrderBy(g => g.AccountName)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var agingReportEntries = pagedResults.Select(result => new AgingReportViewModels
            {
                AccountId = result.AccountId.ToString(),
                AccountName = result.AccountName ?? "Unknown Account",
                TotalDebit = result.TotalDebit,
                TotalCredit = result.TotalCredit,
                NetBalance = result.TotalDebit - result.TotalCredit
            }).ToList();

            return (agingReportEntries, totalCount);
        }

        public async Task<(List<int> ProductIds, int TotalCount)> GetProductIdsForReportAsync(
    DateOnly startDate,
    DateOnly endDate,
    int projectId,
    int? categoryId,
    int? accountId,
    int? locationId,
    int page,
    int pageSize)
{
    var query = _context.JournalEntries
        .AsNoTracking()
        .Include(je => je.JournalVoucher)
        .Include(je => je.Account)
        .Where(je =>
            je.JournalVoucher.ProjectId == projectId &&
            je.JournalVoucher.IsCompleted &&  
            je.IsProcessed &&                 
            je.Quantity > 0 &&
            je.Account.AccountGroupId == 2 &&
            je.JournalVoucher.EntryDate <= endDate);

    if (startDate != DateOnly.MinValue)
    {
        query = query.Where(je => je.JournalVoucher.EntryDate >= startDate);
    }

    if (categoryId.HasValue)
    {
        query = query.Where(je => je.CategoryId == categoryId.Value);
    }

    if (accountId.HasValue)
    {
        query = query.Where(je => je.AccountId == accountId.Value);
    }

    // Apply location filter if needed
    if (locationId.HasValue)
    {
        var validEntryIds = await _context.DeliveryChallanEntries
            .AsNoTracking()
            .Where(dce => dce.LocationId == locationId.Value)
            .Select(dce => dce.JournalEntryId)
            .ToListAsync();

        query = query.Where(je => validEntryIds.Contains(je.JournalEntryId));
    }

    // Get distinct product IDs
    var distinctProductIds = await query
        .Select(je => je.AccountId)
        .Distinct()
        .OrderBy(id => id)
        .ToListAsync();

    int totalCount = distinctProductIds.Count;

    // Apply pagination to product IDs
    var pagedProductIds = distinctProductIds
        .Skip(page * pageSize)
        .Take(pageSize)
        .ToList();

    return (pagedProductIds, totalCount);
}
    }
}