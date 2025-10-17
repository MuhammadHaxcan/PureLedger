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
            return await _context.Bookers.ToListAsync();
        }

        public async Task<Booker?> GetByIdAsync(int id)
        {
            return await _context.Bookers.FindAsync(id);
        }

        public async Task<Booker> AddAsync(Booker booker)
        {
            _context.Bookers.Add(booker);
            await _context.SaveChangesAsync();
            return booker;
        }

        public async Task<Booker> UpdateAsync(Booker booker)
        {
            _context.Bookers.Update(booker);
            await _context.SaveChangesAsync();
            return booker;
        }
    }
}
