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

        public async Task<List<JournalVoucher>> GetVouchersWithModifiedEntriesAsync()
        {
            return await _context.Set<JournalVoucher>()
                .Include(v => v.JournalEntries)
                    .ThenInclude(e => e.Category)
                .Include(v => v.JournalEntries)
                    .ThenInclude(e => e.Account)
                .Where(v => v.JournalEntries.Any(e => e.IsEdited))
                .ToListAsync();
        }
    }
}