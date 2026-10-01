using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class BookerRepository : IBookerRepository
    {
        private readonly LedgerDbContext _context;

        public BookerRepository(LedgerDbContext context)
        {
            _context = context;
        }

        public async Task<List<Booker>> GetAllAsync()
        {
            return await _context.Bookers
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<Booker>> GetAllEnabledAsync(){
            return await _context.Bookers
                .AsNoTracking()
                .Where(b => b.IsEnabled)
                .ToListAsync();
        }


        public async Task<Booker?> GetByIdAsync(int id)
        {
            return await _context.Bookers.FindAsync(id);
        }

        public Booker Add(Booker booker)
        {
            _context.Bookers.Add(booker);
            return booker;
        }

        public Booker Update(Booker booker)
        {
            _context.Bookers.Update(booker);
            return booker;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var booker = await _context.Bookers.FindAsync(id);
            if (booker == null)
                return false;

            _context.Bookers.Remove(booker);
            return true;
        }

        public async Task<bool> ExistsByNameAsync(string name, int? excludeId = null)
        {
            var normalizedName = name.Trim().ToLower();
            return await _context.Bookers
                .AsNoTracking()
                .AnyAsync(b => b.BookerName.ToLower().Trim() == normalizedName
                    && (!excludeId.HasValue || b.BookerID != excludeId.Value));
        }
    }
}
