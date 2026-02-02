using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class AccountTypeRepository : IAccountTypeRepository
    {
        private readonly TenantDbContext _context;

        public AccountTypeRepository(TenantDbContext context)
        {
            _context = context;
        }

        public async Task<List<AccountType>> GetAllAsync()
        {
            return await _context.AccountTypes.ToListAsync();
        }

        public async Task<AccountType?> GetByIdAsync(int id)
        {
            return await _context.AccountTypes.FindAsync(id);
        }
    }
}