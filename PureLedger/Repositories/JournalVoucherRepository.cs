using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class JournalVoucherRepository : IJournalVoucherRepository
    {
        private readonly LedgerDbContext _context;

        public JournalVoucherRepository(LedgerDbContext context)
        {
            _context = context;
        }

        public async Task<List<JournalVoucher>> GetAllWithEntriesAsync()
        {
            return await _context.JournalVouchers
                .Include(jv => jv.JournalEntries)
                    .ThenInclude(e => e.Account)
                .OrderByDescending(jv => jv.EntryDate)
                .ToListAsync();
        }

        public async Task<JournalVoucher?> GetWithEntriesAsync(int id)
        {
            return await _context.JournalVouchers
                .Include(jv => jv.JournalEntries)
                    .ThenInclude(e => e.Account)
                .Include(jv => jv.JournalEntries)
                    .ThenInclude(e => e.Category)
                .FirstOrDefaultAsync(jv => jv.JournalVoucherId == id);
        }

        public async Task<JournalVoucher> AddAsync(JournalVoucher voucher)
        {
            _context.JournalVouchers.Add(voucher);
            await _context.SaveChangesAsync();
            return voucher;
        }

        public async Task<JournalVoucher> UpdateAsync(JournalVoucher voucher)
        {
            _context.JournalVouchers.Update(voucher);
            await _context.SaveChangesAsync();
            return voucher;
        }

        public async Task<List<JournalVoucher>> GetFilteredVouchersAsync(
            DateOnly? sinceDate,
            DateOnly? toDate,
            int? projectId,
            bool includeEntries)
        {
            IQueryable<JournalVoucher> query = _context.JournalVouchers
                .Include(jv => jv.Project);
                
            if (includeEntries)
            {
                query = query.Include(jv => jv.JournalEntries)
                    .ThenInclude(je => je.Account);
            }
            
            if (sinceDate.HasValue)
            {
                query = query.Where(jv => jv.EntryDate >= sinceDate.Value);
            }
            
            if (toDate.HasValue)
            {
                query = query.Where(jv => jv.EntryDate <= toDate.Value);
            }
            
            if (projectId.HasValue)
            {
                query = query.Where(jv => jv.ProjectId == projectId.Value);
            }
            
            return await query.OrderByDescending(jv => jv.EntryDate).ToListAsync();
        }

        public async Task<List<JournalVoucher>> GetVouchersWithModifiedEntriesAsync(int? projectId)
        {
            var (vouchers, _) = await GetVouchersWithModifiedEntriesPaginatedAsync(projectId, 0, int.MaxValue);
            return vouchers;
        }

        public async Task<(List<JournalVoucher> Vouchers, int TotalCount)> GetVouchersWithModifiedEntriesPaginatedAsync(int? projectId, int page, int pageSize)
        {
            page = Math.Max(0, page);
            pageSize = Math.Clamp(pageSize, 1, 500);

            var query = _context.Set<JournalVoucher>()
                .AsNoTracking()
                .Include(v => v.JournalEntries)
                    .ThenInclude(e => e.Category)
                .Include(v => v.JournalEntries)
                    .ThenInclude(e => e.Account)
                .Where(v => v.JournalEntries.Any(e => e.IsEdited));

            if (projectId.HasValue)
            {
                query = query.Where(v => v.ProjectId == projectId.Value);
            }

            var totalCount = await query.CountAsync();

            var vouchers = await query
                .OrderByDescending(v => v.EntryDate)
                .ThenByDescending(v => v.JournalVoucherId)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (vouchers, totalCount);
        }

        public async Task<bool> DeleteAsync(int journalVoucherId)
        {
            var voucher = await _context.JournalVouchers
                .Include(jv => jv.JournalEntries)
                .FirstOrDefaultAsync(jv => jv.JournalVoucherId == journalVoucherId);

            if (voucher == null)
                return false;

            // Remove related entries first if needed (cascade delete may handle this)
            if (voucher.JournalEntries != null)
                _context.JournalEntries.RemoveRange(voucher.JournalEntries);

            _context.JournalVouchers.Remove(voucher);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<JournalVoucher>> GetAllBaseVouchersAsync()
        {
            var (vouchers, _) = await GetAllBaseVouchersPaginatedAsync(null, 0, int.MaxValue);
            return vouchers;
        }

        public async Task<(List<JournalVoucher> Vouchers, int TotalCount)> GetAllBaseVouchersPaginatedAsync(int? projectId, int page, int pageSize)
        {
            page = Math.Max(0, page);
            pageSize = Math.Clamp(pageSize, 1, 500);

            var baseQuery = _context.JournalVouchers
                .AsNoTracking()
                .Where(v => v.IsBaseVoucher);

            if (projectId.HasValue)
            {
                baseQuery = baseQuery.Where(v => v.ProjectId == projectId.Value);
            }

            var query = baseQuery
                .Include(v => v.Project)
                .Include(v => v.JournalEntries)
                    .ThenInclude(e => e.Account);

            var totalCount = await baseQuery.CountAsync();

            var vouchers = await query
                .OrderByDescending(v => v.EntryDate)
                .ThenByDescending(v => v.JournalVoucherId)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (vouchers, totalCount);
        }

        public async Task<JournalVoucher?> GetBaseVoucherWithUnprocessedEntriesAsync(int id)
        {
            return await _context.JournalVouchers
                .Where(v => v.IsBaseVoucher && v.JournalVoucherId == id)
                .Include(v => v.Project)
                .Include(v => v.JournalEntries)
                    .ThenInclude(e => e.Account)
                .Include(v => v.JournalEntries)
                    .ThenInclude(e => e.Category)
                .FirstOrDefaultAsync();
        }
    }
}