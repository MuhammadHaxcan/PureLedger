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
                .FirstOrDefaultAsync(jv => jv.JvId == id);
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
    }
}