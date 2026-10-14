using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class DeliveryChallanVoucherRepository : IDeliveryChallanVoucherRepository
    {
        private readonly LedgerDbContext _context;

        public DeliveryChallanVoucherRepository(LedgerDbContext context)
        {
            _context = context;
        }

        public async Task<DeliveryChallanVoucher?> GetByIdAsync(int id)
        {
            return await _context.DeliveryChallanVouchers
                .FirstOrDefaultAsync(dc => dc.DeliveryChallanVoucherId == id);
        }

        public async Task<DeliveryChallanVoucher?> GetByIdWithEntriesAsync(int id)
        {
            return await _context.DeliveryChallanVouchers
                .Include(dc => dc.Entries)
                .FirstOrDefaultAsync(dc => dc.DeliveryChallanVoucherId == id);
        }

        public async Task<DeliveryChallanVoucher?> GetByJournalVoucherIdAsync(int journalVoucherId)
        {
            return await _context.DeliveryChallanVouchers
                .Include(dc => dc.JournalVoucher)
                    .ThenInclude(jv => jv.JournalEntries)
                        .ThenInclude(je => je.Account)
                            .ThenInclude(ad => ad.AccountData)
                .Include(dc => dc.Entries)
                    .ThenInclude(dce => dce.JournalEntry)
                .Include(dc => dc.Entries)
                    .ThenInclude(dce => dce.Location)
                .FirstOrDefaultAsync(dc => dc.JournalVoucherId == journalVoucherId);
        }

        public async Task<List<DeliveryChallanVoucher>> GetByDateRangeAsync(DateOnly startDate, DateOnly endDate)
        {
            return await _context.DeliveryChallanVouchers
                .Include(dc => dc.JournalVoucher)
                    .ThenInclude(jv => jv.Project)
                .Include(dc => dc.Entries)
                    .ThenInclude(dce => dce.JournalEntry)
                        .ThenInclude(je => je.Account)
                .Include(dc => dc.Entries)
                    .ThenInclude(dce => dce.Location)
                .Where(dc => dc.DCDate >= startDate && dc.DCDate <= endDate)
                .OrderByDescending(dc => dc.DCDate)
                .ToListAsync();
        }

        public DeliveryChallanVoucher Add(DeliveryChallanVoucher deliveryChallanVoucher)
        {
            _context.DeliveryChallanVouchers.Add(deliveryChallanVoucher);
            return deliveryChallanVoucher;
        }

        public DeliveryChallanVoucher Update(DeliveryChallanVoucher deliveryChallanVoucher)
        {
            _context.DeliveryChallanVouchers.Update(deliveryChallanVoucher);
            return deliveryChallanVoucher;
        }

        public async Task<bool> DeleteAsync(int deliveryChallanVoucherId)
        {
            var voucher = await _context.DeliveryChallanVouchers
                .Include(dcv => dcv.Entries)
                .FirstOrDefaultAsync(dcv => dcv.DeliveryChallanVoucherId == deliveryChallanVoucherId);

            if (voucher == null)
                return false;

            // Remove related entries first if needed (cascade delete may handle this)
            if (voucher.Entries != null)
                _context.DeliveryChallanEntries.RemoveRange(voucher.Entries);

            _context.DeliveryChallanVouchers.Remove(voucher);
            return true;
        }
    }
}
