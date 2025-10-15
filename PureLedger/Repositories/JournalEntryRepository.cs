using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

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

        // New optimized methods

        public async Task<List<JournalEntry>> GetEntriesByCriteriaAsync(
            List<int> accountIds, int categoryId, int? projectId, DateOnly referenceDate)
        {
            // Build a query that filters at the database level
            var query = _context.JournalEntries
                .Include(je => je.JournalVoucher)
                .Where(je =>
                    accountIds.Contains(je.AccountId) &&
                    je.CategoryId == categoryId &&
                    je.JournalVoucher.IsCompleted &&
                    je.JournalVoucher.CreditableDate <= referenceDate);

            // Apply project filter if specified
            if (projectId.HasValue)
            {
                query = query.Where(je => je.JournalVoucher.ProjectId == projectId.Value);
            }

            // Execute the optimized query
            return await query.ToListAsync();
        }

        public async Task<decimal> GetAccountBalanceUpToVoucherAsync(int accountId, int maxVoucherId)
        {
            // Calculate the base balance directly in the database
            var balance = await _context.JournalEntries
                .Where(je =>
                    je.AccountId == accountId &&
                    je.JournalVoucher.IsCompleted &&
                    je.JournalVoucherId < maxVoucherId  // Get balance up to but not including this voucher
                )
                .Select(je => new { je.Debit, je.Credit })
                .ToListAsync();

            // Sum up the results
            decimal totalDebit = balance.Sum(e => e.Debit);
            decimal totalCredit = balance.Sum(e => e.Credit);

            return totalDebit - totalCredit;
        }

        public async Task<decimal> GetAccountBalanceUpToVoucherAsyncMinus1(int accountId, int maxVoucherId)
        {
            // Calculate the base balance directly in the database
            var balance = await _context.JournalEntries
                .Where(je =>
                    je.AccountId == accountId &&
                    je.JournalVoucher.IsCompleted &&
                    je.JournalVoucherId <= maxVoucherId
                )
                .Select(je => new { je.Debit, je.Credit })
                .ToListAsync();

            // Sum up the results
            decimal totalDebit = balance.Sum(e => e.Debit);
            decimal totalCredit = balance.Sum(e => e.Credit);

            return totalDebit - totalCredit;
        }

        public async Task<List<JournalEntry>> GetJournalEntriesByFiltersAsync(
            List<int> accountIds, int? categoryId, int? projectId)
        {
            // Start with a base query that includes account filtering
            var query = _context.JournalEntries
                .Include(je => je.JournalVoucher)
                .Include(je => je.Account)
                .Include(je => je.Category)
                .Where(je =>
                    accountIds.Contains(je.AccountId) &&
                    je.JournalVoucher.IsCompleted);

            // Apply category filter if specified
            if (categoryId.HasValue)
            {
                query = query.Where(je => je.CategoryId == categoryId.Value);
            }

            // Apply project filter if specified
            if (projectId.HasValue)
            {
                query = query.Where(je => je.JournalVoucher.ProjectId == projectId.Value);
            }

            // Execute the optimized query
            return await query.ToListAsync();
        }


        public async Task<(List<JournalEntry> Entries, int TotalCount)> GetEntriesForMasterReportAsync(
            DateOnly startDate,
            DateOnly endDate,
            int? projectId,
            int? categoryId,
            int? accountId,
            int page,
            int pageSize)
        {
            var query = _context.JournalEntries
                .Include(je => je.JournalVoucher)
                    .ThenInclude(jv => jv.Project)
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

            // Get total count for pagination info
            int totalCount = await query.CountAsync();

            // Order by EntryDate for consistent reports and apply pagination
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
            // Get all cartage references for this voucher
            var cartageReferences = await _context.JournalVoucherReferences
                .Where(r => r.MainVoucherId == journalVoucherId && r.ReferenceType == "Cartage")
                .ToListAsync();

            if (cartageReferences == null || !cartageReferences.Any())
                return new List<JournalEntry>();

            List<JournalEntry> cartageEntries = new List<JournalEntry>();

            // Process each referenced cartage voucher
            foreach (var reference in cartageReferences)
            {
                var cartageEntry = await GetByCartageId(reference.ReferencedVoucherId);
                if (cartageEntry != null)
                {
                    cartageEntries.Add(cartageEntry);
                }
            }

            return cartageEntries;
        }

        public async Task<decimal> GetPreviousEntriesBalanceAsync(
            DateOnly startDate,
            DateOnly endDate,
            int? projectId,
            int? categoryId,
            int? accountId,
            int page,
            int pageSize)
        {
            // Build the same query as the main report
            var query = _context.JournalEntries
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

            // Order the same as the main report
            query = query.OrderBy(je => je.JournalVoucher.EntryDate)
                .ThenBy(je => je.JournalVoucherId)
                .ThenBy(je => je.JournalEntryId);

            // Take all entries before the current page
            var previousEntries = await query
                .Take(page * pageSize)
                .Select(je => new { je.Debit, je.Credit })
                .ToListAsync();

            decimal balance = previousEntries.Sum(e => e.Debit - e.Credit);
            return balance;
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
            // Start with a query for all JournalEntries that match our basic criteria
            var query = _context.JournalEntries
                .Include(je => je.JournalVoucher)
                .Include(je => je.Account)
                .Where(je =>
                    je.JournalVoucher.ProjectId == projectId &&
                    je.IsProcessed &&
                    je.Quantity > 0 && // Only get entries with quantity for product report
                    je.Account.AccountGroupId == 2 && // Only get product accounts (AccountGroupId = 2)
                    je.JournalVoucher.EntryDate >= startDate &&
                    je.JournalVoucher.EntryDate <= endDate);

            // Apply additional filters
            if (categoryId.HasValue)
            {
                query = query.Where(je => je.CategoryId == categoryId.Value);
            }

            if (accountId.HasValue)
            {
                query = query.Where(je => je.AccountId == accountId.Value);
            }

            // Get total count for pagination
            int totalCount = await query.CountAsync();

            // Apply ordering before pagination
            query = query.OrderBy(je => je.JournalVoucher.EntryDate)
                        .ThenBy(je => je.JournalVoucherId)
                        .ThenBy(je => je.JournalEntryId);

            // Apply pagination
            query = query.Skip(page * pageSize).Take(pageSize);

            // Get entries with a LEFT JOIN to DeliveryChallanEntry
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

            // Filter by location if specified
            if (locationId.HasValue)
            {
                entriesWithDC = entriesWithDC
                    .Where(x => x.DCEntry != null && x.DCEntry.LocationId == locationId.Value)
                    .ToList();
            }

            // Transform to the return type
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
                .Include(je => je.JournalVoucher)
                .Where(je =>
                    je.JournalVoucher.IsCompleted &&
                    je.JournalVoucher.EntryDate < startDate);

            if (projectId.HasValue)
                query = query.Where(je => je.JournalVoucher.ProjectId == projectId.Value);

            if (categoryId.HasValue)
                query = query.Where(je => je.CategoryId == categoryId.Value);

            if (accountId.HasValue)
                query = query.Where(je => je.AccountId == accountId.Value);

            var entries = await query.ToListAsync();
            decimal totalDebit = entries.Sum(e => e.Debit);
            decimal totalCredit = entries.Sum(e => e.Credit);

            return (totalDebit, totalCredit);
        }
    }
}