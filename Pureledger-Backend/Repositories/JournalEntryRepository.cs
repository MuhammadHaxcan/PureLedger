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
                    !je.JournalVoucher.IsBaseVoucher &&
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
                    !je.JournalVoucher.IsBaseVoucher &&
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

        public async Task<decimal> GetPreviousEntriesBalanceForUserReportAsync(
            DateOnly startDate,
            DateOnly endDate,
            int? projectId,
            int categoryId,
            int? accountId,
            int? bookerId,
            int? locationId,
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
                    !je.JournalVoucher.IsBaseVoucher &&
                    je.JournalVoucher.EntryDate >= startDate &&
                    je.JournalVoucher.EntryDate <= endDate &&
                    je.CategoryId == categoryId);

            if (projectId.HasValue)
            {
                baseQuery = baseQuery.Where(je => je.JournalVoucher.ProjectId == projectId.Value);
            }

            if (accountId.HasValue)
            {
                baseQuery = baseQuery.Where(je => je.AccountId == accountId.Value);
            }

            if (bookerId.HasValue)
            {
                baseQuery = baseQuery.Where(je => je.JournalVoucher.BookerId == bookerId.Value);
            }

            // Filter by location - only include entries that have a DCEntry with matching LocationId
            if (locationId.HasValue)
            {
                baseQuery = baseQuery.Where(je => _context.DeliveryChallanEntries
                    .Any(dce => dce.JournalEntryId == je.JournalEntryId && dce.LocationId == locationId.Value));
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

        public async Task<decimal> GetAccountBalanceUpToVoucherAsyncMinus1(int accountId, int maxVoucherId)
        {
            // Optimized: Single aggregate query instead of two separate queries
            var totals = await _context.JournalEntries
                .AsNoTracking()
                .Where(je =>
                    je.AccountId == accountId &&
                    je.JournalVoucher.IsCompleted &&
                    !je.JournalVoucher.IsBaseVoucher &&
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

        public async Task<(List<(JournalEntry Entry, DeliveryChallanEntry? DCEntry)> Entries, int TotalCount)> GetEntriesForMasterReportAsync(
            DateOnly startDate,
            DateOnly endDate,
            int? projectId,
            int? categoryId,
            int? accountId,
            int? bookerId,
            int? locationId,
            int page,
            int pageSize)
        {
            var query = _context.JournalEntries
                .AsNoTracking()
                .AsSplitQuery()
                .Include(je => je.JournalVoucher)
                    .ThenInclude(jv => jv.Project)
                .Include(je => je.JournalVoucher)
                    .ThenInclude(jv => jv.Booker)
                .Include(je => je.Account)
                .Include(je => je.Category)
                .Where(je =>
                    je.JournalVoucher.IsCompleted &&
                    !je.JournalVoucher.IsBaseVoucher &&
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

            // Filter by location - only include entries that have a DCEntry with matching LocationId
            if (locationId.HasValue)
            {
                query = query.Where(je => _context.DeliveryChallanEntries
                    .Any(dce => dce.JournalEntryId == je.JournalEntryId && dce.LocationId == locationId.Value));
            }

            int totalCount = await query.CountAsync();

            var pagedResults = await query
                .OrderBy(je => je.JournalVoucher.EntryDate)
                .ThenBy(je => je.JournalVoucherId)
                .ThenBy(je => je.JournalEntryId)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // LEFT JOIN with DeliveryChallanEntries to get Location
            // Note: A JournalEntry can have multiple DeliveryChallanEntries, so we take the first one
            var journalEntryIds = pagedResults.Select(je => je.JournalEntryId).ToList();
            var dcEntries = await _context.DeliveryChallanEntries
                .AsNoTracking()
                .Include(dce => dce.Location)
                .Where(dce => journalEntryIds.Contains(dce.JournalEntryId))
                .ToListAsync();

            var dcEntryLookup = dcEntries
                .GroupBy(dce => dce.JournalEntryId)
                .ToDictionary(g => g.Key, g => g.First());

            var results = pagedResults.Select(je => (
                Entry: je,
                DCEntry: dcEntryLookup.TryGetValue(je.JournalEntryId, out var dce) ? dce : null
            )).ToList();

            return (results, totalCount);
        }

        public async Task<(List<(JournalEntry Entry, DeliveryChallanEntry? DCEntry)> Entries, int TotalCount)> GetEntriesForUserReportAsync(
            DateOnly startDate,
            DateOnly endDate,
            int? projectId,
            int categoryId,
            int? accountId,
            int? bookerId,
            int? locationId,
            int page,
            int pageSize)
        {
            var query = _context.JournalEntries
                .AsNoTracking()
                .AsSplitQuery()
                .Include(je => je.JournalVoucher)
                    .ThenInclude(jv => jv.Project)
                .Include(je => je.JournalVoucher)
                    .ThenInclude(jv => jv.Booker)
                .Include(je => je.Account)
                .Include(je => je.Category)
                .Where(je =>
                    je.JournalVoucher.IsCompleted &&
                    !je.JournalVoucher.IsBaseVoucher &&
                    je.JournalVoucher.EntryDate >= startDate &&
                    je.JournalVoucher.EntryDate <= endDate &&
                    je.CategoryId == categoryId);

            if (projectId.HasValue)
            {
                query = query.Where(je => je.JournalVoucher.ProjectId == projectId.Value);
            }

            if (accountId.HasValue)
            {
                query = query.Where(je => je.AccountId == accountId.Value);
            }

            if (bookerId.HasValue)
            {
                query = query.Where(je => je.JournalVoucher.BookerId == bookerId.Value);
            }

            // Filter by location - only include entries that have a DCEntry with matching LocationId
            if (locationId.HasValue)
            {
                query = query.Where(je => _context.DeliveryChallanEntries
                    .Any(dce => dce.JournalEntryId == je.JournalEntryId && dce.LocationId == locationId.Value));
            }

            int totalCount = await query.CountAsync();

            var pagedResults = await query
                .OrderBy(je => je.JournalVoucher.EntryDate)
                .ThenBy(je => je.JournalVoucherId)
                .ThenBy(je => je.JournalEntryId)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // LEFT JOIN with DeliveryChallanEntries to get Location
            var journalEntryIds = pagedResults.Select(je => je.JournalEntryId).ToList();
            var dcEntries = await _context.DeliveryChallanEntries
                .AsNoTracking()
                .Include(dce => dce.Location)
                .Where(dce => journalEntryIds.Contains(dce.JournalEntryId))
                .ToListAsync();

            var dcEntryLookup = dcEntries
                .GroupBy(dce => dce.JournalEntryId)
                .ToDictionary(g => g.Key, g => g.First());

            var results = pagedResults.Select(je => (
                Entry: je,
                DCEntry: dcEntryLookup.TryGetValue(je.JournalEntryId, out var dce) ? dce : null
            )).ToList();

            return (results, totalCount);
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
                .Include(e => e.JournalVoucher)
                .Where(e => 
                    referencedVoucherIds.Contains(e.JournalVoucherId) && 
                    e.Debit == 0 && 
                    e.Credit > 0 &&
                    e.JournalVoucher.IsCompleted &&
                    !e.JournalVoucher.IsBaseVoucher)
                .ToListAsync();
        }

        public async Task<decimal> GetPreviousEntriesBalanceAsync(
            DateOnly startDate,
            DateOnly endDate,
            int? projectId,
            int? categoryId,
            int? accountId,
            int? bookerId,
            int? locationId,
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
                    !je.JournalVoucher.IsBaseVoucher &&
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

            // Filter by location - only include entries that have a DCEntry with matching LocationId
            if (locationId.HasValue)
            {
                baseQuery = baseQuery.Where(je => _context.DeliveryChallanEntries
                    .Any(dce => dce.JournalEntryId == je.JournalEntryId && dce.LocationId == locationId.Value));
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

        public async Task<Dictionary<int, int>> GetPreviousQtyBalancePerAccountAsync(
            DateOnly startDate,
            DateOnly endDate,
            int? projectId,
            int? categoryId,
            int? accountId,
            int? bookerId,
            int? locationId,
            int page,
            int pageSize)
        {
            if (page <= 0 || pageSize <= 0)
            {
                return new Dictionary<int, int>();
            }

            var baseQuery = _context.JournalEntries
                .AsNoTracking()
                .Where(je =>
                    je.JournalVoucher.IsCompleted &&
                    !je.JournalVoucher.IsBaseVoucher &&
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

            // Filter by location - only include entries that have a DCEntry with matching LocationId
            if (locationId.HasValue)
            {
                baseQuery = baseQuery.Where(je => _context.DeliveryChallanEntries
                    .Any(dce => dce.JournalEntryId == je.JournalEntryId && dce.LocationId == locationId.Value));
            }

            int takeCount = page * pageSize;

            // Get entries before current page, grouped by AccountId
            // QtyBalance: Debit > 0 means +Quantity, Credit > 0 means -Quantity
            var accountQtyBalances = await baseQuery
                .OrderBy(je => je.JournalVoucher.EntryDate)
                .ThenBy(je => je.JournalVoucherId)
                .ThenBy(je => je.JournalEntryId)
                .Take(takeCount)
                .GroupBy(je => je.AccountId)
                .Select(g => new
                {
                    AccountId = g.Key,
                    QtyBalance = g.Sum(je => je.Debit > 0 ? je.Quantity : -je.Quantity)
                })
                .ToListAsync();

            return accountQtyBalances.ToDictionary(x => x.AccountId, x => x.QtyBalance);
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
                    !je.JournalVoucher.IsBaseVoucher &&
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
                    !je.JournalVoucher.IsBaseVoucher &&
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

        public async Task<(decimal Debit, decimal Credit)> GetOpeningBalanceForUserReportAsync(
            DateOnly startDate,
            int? projectId,
            int categoryId,
            int? accountId)
        {
            var query = _context.JournalEntries
                .AsNoTracking()
                .Where(je =>
                    je.JournalVoucher.IsCompleted &&
                    !je.JournalVoucher.IsBaseVoucher &&
                    je.JournalVoucher.EntryDate < startDate &&
                    je.CategoryId == categoryId);

            if (projectId.HasValue)
                query = query.Where(je => je.JournalVoucher.ProjectId == projectId.Value);

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
                    !je.JournalVoucher.IsBaseVoucher &&
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
                    je.JournalVoucher.IsCompleted &&
                    !je.JournalVoucher.IsBaseVoucher);

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
            // Base query conditions (common to both queries)
            IQueryable<JournalEntry> BuildBaseQuery(bool includeCreditableDateFilter)
            {
                var baseQuery = _context.JournalEntries
                    .AsNoTracking()
                    .Include(je => je.JournalVoucher)
                    .Include(je => je.Account)
                    .Where(je =>
                        accountIds.Contains(je.AccountId) &&
                        je.CategoryId == categoryId &&
                        je.JournalVoucher.IsCompleted &&
                        !je.JournalVoucher.IsBaseVoucher);

                if (includeCreditableDateFilter)
                {
                    baseQuery = baseQuery.Where(je => je.JournalVoucher.CreditableDate <= referenceDate);
                }

                if (projectId.HasValue)
                {
                    baseQuery = baseQuery.Where(je => je.JournalVoucher.ProjectId == projectId.Value);
                }

                return baseQuery;
            }

            // Query 1: Due Till Date - filtered by CreditableDate <= referenceDate
            var dueTillDateQuery = BuildBaseQuery(includeCreditableDateFilter: true)
                .GroupBy(je => new { je.AccountId, je.Account.AccountName })
                .Select(g => new
                {
                    AccountId = g.Key.AccountId,
                    AccountName = g.Key.AccountName,
                    NetBalance = g.Sum(je => je.Debit) - g.Sum(je => je.Credit)
                });

            // Query 2: Total Due - NO CreditableDate filter
            var totalDueQuery = BuildBaseQuery(includeCreditableDateFilter: false)
                .GroupBy(je => new { je.AccountId, je.Account.AccountName })
                .Select(g => new
                {
                    AccountId = g.Key.AccountId,
                    AccountName = g.Key.AccountName,
                    NetBalance = g.Sum(je => je.Debit) - g.Sum(je => je.Credit)
                });

            // Get all accounts for pagination (use the filtered query for count)
            var allAccountsQuery = dueTillDateQuery.Select(g => new { g.AccountId, g.AccountName });
            int totalCount = await allAccountsQuery.CountAsync();

            // Get paginated account list
            var pagedAccountList = await allAccountsQuery
                .OrderBy(g => g.AccountName)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();

            if (!pagedAccountList.Any())
            {
                return (new List<AgingReportViewModels>(), totalCount);
            }

            var accountIdsForPage = pagedAccountList.Select(a => a.AccountId).ToList();

            // Get Due Till Date results for paginated accounts
            var dueTillDateResults = await dueTillDateQuery
                .Where(g => accountIdsForPage.Contains(g.AccountId))
                .ToDictionaryAsync(g => g.AccountId, g => g.NetBalance);

            // Get Total Due results for paginated accounts
            var totalDueResults = await totalDueQuery
                .Where(g => accountIdsForPage.Contains(g.AccountId))
                .ToDictionaryAsync(g => g.AccountId, g => g.NetBalance);

            // Join results and create ViewModels
            var agingReportEntries = pagedAccountList.Select(account => new AgingReportViewModels
            {
                AccountId = account.AccountId.ToString(),
                AccountName = account.AccountName ?? "Unknown Account",
                DueTillDate = dueTillDateResults.GetValueOrDefault(account.AccountId, 0),
                TotalDue = totalDueResults.GetValueOrDefault(account.AccountId, 0)
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
            !je.JournalVoucher.IsBaseVoucher &&
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

    // Apply location filter using SQL subquery instead of loading IDs into memory
    if (locationId.HasValue)
    {
        query = query.Where(je => _context.DeliveryChallanEntries
            .Any(dce => dce.JournalEntryId == je.JournalEntryId && dce.LocationId == locationId.Value));
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

        /// <summary>
        /// Batch fetch entries for multiple voucher IDs and a specific account - fixes N+1 query issue
        /// </summary>
        public async Task<List<JournalEntry>> GetEntriesByVoucherIdsAndAccountAsync(
            List<int> voucherIds,
            int accountId)
        {
            if (voucherIds == null || !voucherIds.Any())
                return new List<JournalEntry>();

            return await _context.JournalEntries
                .AsNoTracking()
                .Include(je => je.JournalVoucher)
                .Where(je => 
                    voucherIds.Contains(je.JournalVoucherId) && 
                    je.AccountId == accountId &&
                    je.JournalVoucher.IsCompleted &&
                    !je.JournalVoucher.IsBaseVoucher)
                .ToListAsync();
        }

        /// <summary>
        /// Get aggregated product data for the product report - avoids loading all entries into memory
        /// Uses two queries: one for previous period, one for current period, then combines in memory
        /// </summary>
        public async Task<List<ProductAggregateDto>> GetProductAggregatesAsync(
            List<int> productIds,
            DateOnly startDate,
            DateOnly endDate,
            int projectId,
            int? categoryId,
            int? locationId)
        {
            if (productIds == null || !productIds.Any())
                return new List<ProductAggregateDto>();

            // Base query builder for product entries
            IQueryable<JournalEntry> BuildBaseQuery(DateOnly? fromDate, DateOnly toDate)
            {
                var query = _context.JournalEntries
                    .AsNoTracking()
                    .Include(je => je.Account)
                    .Where(je =>
                        productIds.Contains(je.AccountId) &&
                        je.JournalVoucher.ProjectId == projectId &&
                        je.JournalVoucher.IsCompleted &&
                        !je.JournalVoucher.IsBaseVoucher &&
                        je.IsProcessed &&
                        je.Quantity > 0 &&
                        je.Account.AccountGroupId == 2 &&
                        je.JournalVoucher.EntryDate <= toDate);

                if (fromDate.HasValue)
                {
                    query = query.Where(je => je.JournalVoucher.EntryDate >= fromDate.Value);
                }

                if (categoryId.HasValue)
                {
                    query = query.Where(je => je.CategoryId == categoryId.Value);
                }

                // Location filter using subquery instead of loading IDs into memory
                if (locationId.HasValue)
                {
                    query = query.Where(je => _context.DeliveryChallanEntries
                        .Any(dce => dce.JournalEntryId == je.JournalEntryId && dce.LocationId == locationId.Value));
                }

                return query;
            }

            // Query 1: Get aggregates for entries BEFORE start date (previous balance)
            // Handle edge case where startDate is MinValue (no previous period exists)
            List<dynamic> prevAggregates;
            if (startDate == DateOnly.MinValue)
            {
                prevAggregates = new List<dynamic>();
            }
            else
            {
                var prevQuery = BuildBaseQuery(null, startDate.AddDays(-1));
                prevAggregates = (await prevQuery
                    .GroupBy(je => new { je.AccountId, je.Account.AccountName })
                    .Select(g => new
                    {
                        ProductId = g.Key.AccountId,
                        ProductName = g.Key.AccountName ?? "",
                        DebitQty = g.Where(je => je.Debit > 0).Sum(je => je.Quantity),
                        CreditQty = g.Where(je => je.Credit > 0).Sum(je => je.Quantity),
                        Debit = g.Sum(je => je.Debit),
                        Credit = g.Sum(je => je.Credit)
                    })
                    .ToListAsync()).Cast<dynamic>().ToList();
            }

            // Query 2: Get aggregates for entries IN date range (current period)
            var currentQuery = BuildBaseQuery(startDate, endDate);
            var currentAggregates = await currentQuery
                .GroupBy(je => new { je.AccountId, je.Account.AccountName })
                .Select(g => new
                {
                    ProductId = g.Key.AccountId,
                    ProductName = g.Key.AccountName ?? "",
                    DebitQty = g.Where(je => je.Debit > 0).Sum(je => je.Quantity),
                    CreditQty = g.Where(je => je.Credit > 0).Sum(je => je.Quantity),
                    Debit = g.Sum(je => je.Debit),
                    Credit = g.Sum(je => je.Credit)
                })
                .ToListAsync();

            // Get product names for products that might not have any entries in either period
            var productNames = await _context.Accounts
                .AsNoTracking()
                .Where(a => productIds.Contains(a.AccountId))
                .Select(a => new { a.AccountId, a.AccountName })
                .ToDictionaryAsync(a => a.AccountId, a => a.AccountName ?? "Unknown");

            // Combine results
            var prevDict = prevAggregates.ToDictionary(p => p.ProductId);
            var currentDict = currentAggregates.ToDictionary(c => c.ProductId);

            var results = new List<ProductAggregateDto>();

            foreach (var productId in productIds)
            {
                var prev = prevDict.GetValueOrDefault(productId);
                var current = currentDict.GetValueOrDefault(productId);

                // Skip products with no data at all
                if (prev == null && current == null)
                    continue;

                results.Add(new ProductAggregateDto
                {
                    ProductId = productId,
                    ProductName = prev?.ProductName ?? current?.ProductName ?? productNames.GetValueOrDefault(productId, "Unknown"),
                    PrevDebitQty = prev?.DebitQty ?? 0,
                    PrevCreditQty = prev?.CreditQty ?? 0,
                    PrevDebit = prev?.Debit ?? 0,
                    PrevCredit = prev?.Credit ?? 0,
                    CurrentDebitQty = current?.DebitQty ?? 0,
                    CurrentCreditQty = current?.CreditQty ?? 0,
                    CurrentDebit = current?.Debit ?? 0,
                    CurrentCredit = current?.Credit ?? 0
                });
            }

            return results;
        }

        /// <summary>
        /// Get aggregated product quantities from derived vouchers for base vouchers summary
        /// Efficiently calculates most ordered products by aggregating at database level
        /// </summary>
        public async Task<List<(int ProductId, string ProductName, decimal TotalQuantity, int EntryCount)>> GetDerivedVoucherProductQuantitiesAsync(int? projectId)
        {
            // Step 1: Get base voucher IDs filtered by project
            var baseVoucherQuery = _context.JournalVouchers
                .AsNoTracking()
                .Where(v => v.IsBaseVoucher);

            if (projectId.HasValue)
            {
                baseVoucherQuery = baseVoucherQuery.Where(v => v.ProjectId == projectId.Value);
            }

            var baseVoucherIds = await baseVoucherQuery.Select(v => v.JournalVoucherId).ToListAsync();

            if (!baseVoucherIds.Any())
            {
                return new List<(int ProductId, string ProductName, decimal TotalQuantity, int EntryCount)>();
            }

            // Step 2: Get derived voucher IDs from references
            var derivedVoucherIds = await _context.JournalVoucherReferences
                .AsNoTracking()
                .Where(r => r.ReferenceType == "DerivedVoucher" && baseVoucherIds.Contains(r.MainVoucherId))
                .Select(r => r.ReferencedVoucherId)
                .Distinct()
                .ToListAsync();

            if (!derivedVoucherIds.Any())
            {
                return new List<(int ProductId, string ProductName, decimal TotalQuantity, int EntryCount)>();
            }

            // Step 3: Get product entries from derived vouchers and aggregate
            var aggregated = await _context.JournalEntries
                .AsNoTracking()
                .Where(e => derivedVoucherIds.Contains(e.JournalVoucherId))
                .Join(_context.Accounts,
                    entry => entry.AccountId,
                    account => account.AccountId,
                    (entry, account) => new { Entry = entry, Account = account })
                .Where(x => x.Account.AccountGroupId == 2) // Product accounts only
                .GroupBy(x => new { x.Entry.AccountId, x.Account.AccountName })
                .Select(g => new
                {
                    ProductId = g.Key.AccountId,
                    ProductName = g.Key.AccountName ?? "Unknown",
                    TotalQuantity = (decimal)g.Sum(x => x.Entry.Quantity),
                    EntryCount = g.Count()
                })
                .ToListAsync();

            return aggregated.Select(x => (x.ProductId, x.ProductName, x.TotalQuantity, x.EntryCount)).ToList();
        }
    }
}