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
            foreach (var tagId in tagIds.Where(t => t > 0))
            {
                var journalEntryTag = new JournalEntryTag
                {
                    TagId = tagId,
                    JournalEntryId = entryId
                };
                _context.JournalEntryTags.Add(journalEntryTag);
            }
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
            // Calculate the balance directly in the database with a single query
            var balance = await _context.JournalEntries
                .Where(je => 
                    je.AccountId == accountId && 
                    je.JournalVoucher.IsCompleted &&
                    je.JournalVoucherId <= maxVoucherId)
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
            DateOnly startDate, DateOnly endDate, int? projectId, int? categoryId, int? accountId)
        {
            // Build a query with filters applied at the database level
            var query = _context.JournalEntries
                .Include(je => je.JournalVoucher)
                    .ThenInclude(jv => jv.Project)
                .Include(je => je.Account)
                .Include(je => je.Category)
                .Where(je => 
                    je.JournalVoucher.IsCompleted &&
                    // Use effective date for filtering
                    ((je.JournalVoucher.CreditableDate != null
                        ? je.JournalVoucher.CreditableDate 
                        : je.JournalVoucher.EntryDate) >= startDate) &&
                    ((je.JournalVoucher.CreditableDate != null
                        ? je.JournalVoucher.CreditableDate 
                        : je.JournalVoucher.EntryDate) <= endDate));

            // Apply project filter if specified
            if (projectId.HasValue)
            {
                query = query.Where(je => je.JournalVoucher.ProjectId == projectId.Value);
            }

            // Apply category filter if specified
            if (categoryId.HasValue)
            {
                query = query.Where(je => je.CategoryId == categoryId.Value);
            }

            // Apply account filter if specified
            if (accountId.HasValue)
            {
                query = query.Where(je => je.AccountId == accountId.Value);
            }

            // Order by date for consistent reports
            query = query.OrderBy(je => 
                je.JournalVoucher.CreditableDate != null 
                    ? je.JournalVoucher.CreditableDate 
                    : je.JournalVoucher.EntryDate)
                .ThenBy(je => je.JournalVoucherId)
                .ThenBy(je => je.JournalEntryId);

            // Execute the optimized query
            return await query.ToListAsync();
        }
    }
}