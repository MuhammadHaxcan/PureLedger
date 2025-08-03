using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class AccountRepository : IAccountRepository
    {
        private readonly LedgerDbContext _context;

        public AccountRepository(LedgerDbContext context)
        {
            _context = context;
        }

        public async Task<List<Account>> GetAllWithDetailsAsync()
        {
            return await _context.Accounts
                .Include(a => a.AccountData)
                .Include(a => a.ProductData)
                .Include(a => a.Category)
                    .ThenInclude(c => c.Project)
                .Include(a => a.AccountGroup)
                .ToListAsync();
        }

        public async Task<List<Account>> GetByCategoryIdAsync(int categoryId)
        {
            return await _context.Accounts
                .Where(a => a.CategoryId == categoryId)
                .ToListAsync();
        }

        public async Task<Account?> GetByIdAsync(int id)
        {
            return await _context.Accounts
                .Include(a => a.ProductData)
                .FirstOrDefaultAsync(a => a.AccountId == id);
        }

        public async Task<Account?> GetByIdWithDetailsAsync(int id)
        {
            return await _context.Accounts
                .Include(a => a.AccountData)
                .Include(a => a.ProductData)
                .FirstOrDefaultAsync(a => a.AccountId == id);
        }

        public async Task<Account> AddAsync(Account account)
        {
            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();
            return account;
        }

        public async Task<Account> UpdateAsync(Account account)
        {
            _context.Accounts.Update(account);
            await _context.SaveChangesAsync();
            return account;
        }

        public async Task<object?> GetProductDetailsAsync(int accountId)
        {
            return await _context.Accounts
                .Where(a => a.AccountId == accountId)
                .Select(a => new
                {
                    a.ProductData.SaleRate,
                    a.ProductData.Pkg,
                    a.ProductData.PkgForm,
                    a.ProductData.Type
                })
                .FirstOrDefaultAsync();
        }

        public async Task<List<Account>> GetAllAsync()
        {
            return await _context.Accounts.ToListAsync();
        }

        public async Task<Account?> GetByIdWithCategoryAsync(int id)
        {
            return await _context.Accounts
                .Include(a => a.Category)
                .FirstOrDefaultAsync(a => a.AccountId == id);
        }

        public async Task<List<Account>> GetByCategoryIdWithCategoryAsync(int categoryId)
        {
            return await _context.Accounts
                .Include(a => a.Category)
                .Where(a => a.CategoryId == categoryId)
                .ToListAsync();
        }

    }
}