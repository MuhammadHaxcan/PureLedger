using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class AccountGroupRepository : IAccountGroupRepository
    {
        private readonly TenantDbContext _context;

        public AccountGroupRepository(TenantDbContext context)
        {
            _context = context;
        }

        public async Task<List<AccountGroup>> GetAllAsync()
        {
            return await _context.AccountGroups.ToListAsync();
        }

        public async Task<AccountGroup?> GetByIdAsync(int id)
        {
            return await _context.AccountGroups.FindAsync(id);
        }
    }
}