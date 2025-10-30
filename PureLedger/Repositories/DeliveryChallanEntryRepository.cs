using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class DeliveryChallanEntryRepository : IDeliveryChallanEntryRepository
    {
        private readonly LedgerDbContext _context;

        public DeliveryChallanEntryRepository(LedgerDbContext context)
        {
            _context = context;
        }

        public async Task<DeliveryChallanEntry?> GetByIdAsync(int id)
        {
            return await _context.DeliveryChallanEntries
                .FirstOrDefaultAsync(dce => dce.DeliveryChallanEntryId == id);
        }

        public async Task<List<DeliveryChallanEntry>> GetByDeliveryChallanIdAsync(int deliveryChallanId)
        {
            return await _context.DeliveryChallanEntries
                .Include(dce => dce.JournalEntry)
                    .ThenInclude(je => je.Account)
                .Include(dce => dce.Location)
                .Where(dce => dce.DeliveryChallanVoucherId == deliveryChallanId)
                .OrderBy(dce => dce.EntryDate)
                .ToListAsync();
        }

        public async Task<DeliveryChallanEntry?> GetByDeliveryChallanIdAndJournalEntryIdAsync(int deliveryChallanId, int journalEntryId)
        {
            var deliveryChallanVoucherId = await _context.DeliveryChallanVouchers
                .Where(dcv => dcv.JournalVoucherId == deliveryChallanId)
                .Select(dcv => dcv.DeliveryChallanVoucherId)
                .FirstOrDefaultAsync();

            if (deliveryChallanVoucherId == 0)
                return null;

            // Now get the DeliveryChallanEntry using DeliveryChallanVoucherId and JournalEntryId
            return await _context.DeliveryChallanEntries
                .FirstOrDefaultAsync(dce => dce.DeliveryChallanVoucherId == deliveryChallanVoucherId
                                          && dce.JournalEntryId == journalEntryId);
        }

        public async Task<DeliveryChallanEntry> AddAsync(DeliveryChallanEntry deliveryChallanEntry)
        {
            await _context.DeliveryChallanEntries.AddAsync(deliveryChallanEntry);
            await _context.SaveChangesAsync(); // Pattern A: Save within repository
            return deliveryChallanEntry;
        }

        public async Task<DeliveryChallanEntry> UpdateAsync(DeliveryChallanEntry deliveryChallanEntry)
        {
            _context.DeliveryChallanEntries.Update(deliveryChallanEntry);
            await _context.SaveChangesAsync(); // Pattern A: Save within repository
            return deliveryChallanEntry;
        }

        public async Task<bool> DeleteAsync(int deliveryChallanEntryId)
        {
            var entry = await _context.DeliveryChallanEntries
                .FirstOrDefaultAsync(e => e.DeliveryChallanEntryId == deliveryChallanEntryId);

            if (entry == null)
                return false;

            _context.DeliveryChallanEntries.Remove(entry);
            await _context.SaveChangesAsync(); // Pattern A: Already present
            return true;
        }
    }
}