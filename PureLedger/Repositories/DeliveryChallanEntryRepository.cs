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
            return await _context.DeliveryChallanEntries
                .FirstOrDefaultAsync(dce => dce.DeliveryChallanVoucherId == deliveryChallanId && dce.JournalEntryId == journalEntryId);
        }

        public async Task<DeliveryChallanEntry> AddAsync(DeliveryChallanEntry deliveryChallanEntry)
        {
            await _context.DeliveryChallanEntries.AddAsync(deliveryChallanEntry);
            return deliveryChallanEntry;
        }

        public async Task<DeliveryChallanEntry> UpdateAsync(DeliveryChallanEntry deliveryChallanEntry)
        {
            _context.DeliveryChallanEntries.Update(deliveryChallanEntry);
            return deliveryChallanEntry;
        }
    }
}