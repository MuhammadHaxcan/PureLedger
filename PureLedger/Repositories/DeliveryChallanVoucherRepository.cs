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
                .FirstOrDefaultAsync(dc => dc.DeliveryChallanId == id);
        }

        public async Task<DeliveryChallanVoucher?> GetByIdWithEntriesAsync(int id)
        {
            return await _context.DeliveryChallanVouchers
                .Include(dc => dc.DeliveryChallanEntries)
                .FirstOrDefaultAsync(dc => dc.DeliveryChallanId == id);
        }

        public async Task<DeliveryChallanVoucher?> GetByJournalVoucherIdAsync(int journalVoucherId)
        {
            return await _context.DeliveryChallanVouchers
                .Include(dc => dc.JournalVoucher)
                    .ThenInclude(jv => jv.JournalEntries)
                        .ThenInclude(je => je.Account)
                .Include(dc => dc.DeliveryChallanEntries)
                    .ThenInclude(dce => dce.JournalEntry)
                .Include(dc => dc.DeliveryChallanEntries)
                    .ThenInclude(dce => dce.Location)
                .FirstOrDefaultAsync(dc => dc.JournalVoucherId == journalVoucherId);
        }

        public async Task<List<DeliveryChallanVoucher>> GetByDateRangeAsync(DateOnly startDate, DateOnly endDate)
        {
            return await _context.DeliveryChallanVouchers
                .Include(dc => dc.JournalVoucher)
                    .ThenInclude(jv => jv.Project)
                .Include(dc => dc.DeliveryChallanEntries)
                    .ThenInclude(dce => dce.JournalEntry)
                        .ThenInclude(je => je.Account)
                .Include(dc => dc.DeliveryChallanEntries)
                    .ThenInclude(dce => dce.Location)
                .Where(dc => dc.DCDate >= startDate && dc.DCDate <= endDate)
                .OrderByDescending(dc => dc.DCDate)
                .ToListAsync();
        }

        public async Task<DeliveryChallanVoucher> AddAsync(DeliveryChallanVoucher deliveryChallanVoucher)
        {
            await _context.DeliveryChallanVouchers.AddAsync(deliveryChallanVoucher);
            return deliveryChallanVoucher;
        }

        public async Task<DeliveryChallanVoucher> UpdateAsync(DeliveryChallanVoucher deliveryChallanVoucher)
        {
            _context.DeliveryChallanVouchers.Update(deliveryChallanVoucher);
            return deliveryChallanVoucher;
        }

        public async Task<AccountData?> GetAccountDataByAccountNameAsync(string accountName)
        {
            return await _context.AccountDatas
                .Include(ad => ad.Account)
                .Where(ad => ad.Account.AccountName == accountName)
                .FirstOrDefaultAsync();
        }

    }
}