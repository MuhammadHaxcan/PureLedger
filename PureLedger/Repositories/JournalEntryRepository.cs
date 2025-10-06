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

        public async Task AddTagsAsync(int entryId, IEnumerable<int> tagIds)
        {
            // Get existing tag IDs for this entry to avoid duplicates
            var existingTagIds = await _context.JournalEntryTags
                .Where(jet => jet.JournalEntryId == entryId)
                .Select(jet => jet.TagId)
                .ToListAsync();

            var newTagIds = tagIds
                .Where(t => t > 0 && !existingTagIds.Contains(t))
                .Distinct()
                .ToList();

            if (newTagIds.Count == 0)
                return;

            var journalEntryTags = newTagIds.Select(tagId => new JournalEntryTag
            {
                TagId = tagId,
                JournalEntryId = entryId
            });

            _context.JournalEntryTags.AddRange(journalEntryTags);
            await _context.SaveChangesAsync();
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


        public async Task<List<JournalEntry>> GetEntriesForMasterReportAsync(
    DateOnly startDate,
    DateOnly endDate,
    int? projectId,
    int? categoryId,
    int? accountId,
    int page,
    int pageSize,
    bool getLastEntryOnly)
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

            // Order consistently
            query = query.OrderBy(je => je.JournalVoucher.EntryDate)
                .ThenBy(je => je.JournalVoucherId)
                .ThenBy(je => je.JournalEntryId);

            if (getLastEntryOnly)
            {
                // Calculate the position of the last entry of the requested page
                int lastEntryPosition = (page * pageSize) - 1;

                // Get only that single entry
                return await query
                    .Skip(Math.Max(0, lastEntryPosition))
                    .Take(1)
                    .ToListAsync();
            }
            else
            {
                // Regular paged query
                return await query
                    .Skip(page * pageSize)
                    .Take(pageSize)
                    .ToListAsync();
            }
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
                    // Use only EntryDate for filtering, not CreditableDate
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
    }
}