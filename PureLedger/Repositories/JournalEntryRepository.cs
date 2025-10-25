using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;
using PureLedger.ViewModels;

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

            var balance = await _context.JournalEntries
                .Where(je =>
                    je.AccountId == accountId &&
                    je.JournalVoucher.IsCompleted &&
                    je.JournalVoucherId < maxVoucherId
                )
                .Select(je => new { je.Debit, je.Credit })
                .ToListAsync();

            decimal totalDebit = balance.Sum(e => e.Debit);
            decimal totalCredit = balance.Sum(e => e.Credit);

            return totalDebit - totalCredit;
        }

        public async Task<decimal> GetAccountBalanceUpToVoucherAsyncMinus1(int accountId, int maxVoucherId)
        {

            var balance = await _context.JournalEntries
                .Where(je =>
                    je.AccountId == accountId &&
                    je.JournalVoucher.IsCompleted &&
                    je.JournalVoucherId <= maxVoucherId
                )
                .Select(je => new { je.Debit, je.Credit })
                .ToListAsync();

            decimal totalDebit = balance.Sum(e => e.Debit);
            decimal totalCredit = balance.Sum(e => e.Credit);

            return totalDebit - totalCredit;
        }

        public async Task<List<JournalEntry>> GetJournalEntriesByFiltersAsync(
            List<int> accountIds, int? categoryId, int? projectId)
        {

            var query = _context.JournalEntries
                .Include(je => je.JournalVoucher)
                .Include(je => je.Account)
                .Include(je => je.Category)
                .Where(je =>
                    accountIds.Contains(je.AccountId) &&
                    je.JournalVoucher.IsCompleted);

            if (categoryId.HasValue)
            {
                query = query.Where(je => je.CategoryId == categoryId.Value);
            }

            if (projectId.HasValue)
            {
                query = query.Where(je => je.JournalVoucher.ProjectId == projectId.Value);
            }

            return await query.ToListAsync();
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

            var cartageReferences = await _context.JournalVoucherReferences
                .Where(r => r.MainVoucherId == journalVoucherId && r.ReferenceType == "Cartage")
                .ToListAsync();

            if (cartageReferences == null || !cartageReferences.Any())
                return new List<JournalEntry>();

            List<JournalEntry> cartageEntries = new List<JournalEntry>();

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

            query = query.OrderBy(je => je.JournalVoucher.EntryDate)
                .ThenBy(je => je.JournalVoucherId)
                .ThenBy(je => je.JournalEntryId);

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

            var query = _context.JournalEntries
                .Include(je => je.JournalVoucher)
                .Include(je => je.Account)
                .Where(je =>
                    je.JournalVoucher.ProjectId == projectId &&
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

        public async Task<List<(JournalEntry Entry, DeliveryChallanEntry? DCEntry)>> GetAllEntriesForProductReportAsync(
    DateOnly startDate,
    DateOnly endDate,
    int projectId,
    int? categoryId,
    int? accountId,
    int? locationId)
        {

            var query = _context.JournalEntries
                .Include(je => je.JournalVoucher)
                .Include(je => je.Account)
                .Include(je => je.Category)
                .Where(je =>
                    je.JournalVoucher.ProjectId == projectId &&
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

        public async Task<(List<AccountReportViewModel> Entries, int TotalCount)> GetEntriesForAccountReportAsync(
            int projectId,
            int? categoryId,
            int? accountId,
            int page,
            int pageSize)
        {

            var query = _context.JournalEntries
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

            var groupedQuery = query
                .GroupBy(je => new {
                    je.AccountId,
                    AccountName = je.Account.AccountName,
                    CategoryName = je.Account.Category.CategoryName
                })
                .Select(g => new AccountReportViewModel
                {
                    AccountId = g.Key.AccountId.ToString(),
                    AccountName = g.Key.AccountName ?? "Unknown Account",
                    CategoryName = g.Key.CategoryName ?? "Unknown Category",
                    TotalDebit = g.Sum(je => je.Debit),
                    TotalCredit = g.Sum(je => je.Credit),
                    NetBalance = g.Sum(je => je.Debit) - g.Sum(je => je.Credit)
                });

            int totalCount = await groupedQuery.CountAsync();

            var pagedResults = await groupedQuery
                .OrderBy(g => g.AccountName)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (pagedResults, totalCount);
        }

        public async Task<(List<AgingReportViewModel> Entries, int TotalCount)> GetEntriesForAgingReportAsync(
            List<int> accountIds,
            int categoryId,
            int? projectId,
            DateOnly referenceDate,
            int page,
            int pageSize)
        {

            var query = _context.JournalEntries
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

            var agingReportEntries = pagedResults.Select(result => new AgingReportViewModel
            {
                AccountId = result.AccountId.ToString(),
                AccountName = result.AccountName ?? "Unknown Account",
                TotalDebit = result.TotalDebit,
                TotalCredit = result.TotalCredit,
                NetBalance = result.TotalDebit - result.TotalCredit
            }).ToList();

            return (agingReportEntries, totalCount);
        }
    }
}