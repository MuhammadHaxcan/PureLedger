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

        public async Task<bool> DeleteAsync(Account account)
        {
            try
            {
                _context.Accounts.Remove(account);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
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
                    a.ProductData.Type,
                    a.AccountGroupId
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

        // New method to check if account has associated journal entries
        public async Task<List<int>> GetAssociatedJournalVoucherIds(int accountId)
        {
            return await _context.JournalEntries
                .Where(je => je.AccountId == accountId)
                .Select(je => je.JournalVoucherId)
                .Distinct()
                .ToListAsync();
        }

        // New optimized method to get accounts with entries in a specific project
        public async Task<List<Account>> GetAccountsWithEntriesInProjectAsync(int projectId)
        {
            // This query finds all accounts that have at least one journal entry 
            // in a voucher belonging to the specified project
            var accountIds = await _context.JournalEntries
                .Where(je => je.JournalVoucher.ProjectId == projectId && 
                            je.JournalVoucher.IsCompleted)
                .Select(je => je.AccountId)
                .Distinct()
                .ToListAsync();

            // Then fetch those accounts with their categories in a single query
            return await _context.Accounts
                .Include(a => a.Category)
                .Where(a => accountIds.Contains(a.AccountId))
                .ToListAsync();
        }

        public async Task<IEnumerable<Account>> GetAccountsByCategoryAndProjectAsync(int categoryId, int projectId)
        {
            var accounts = await GetByCategoryIdAsync(categoryId);
            return accounts.Where(a => a.Category?.ProjectId == projectId);
        }
    }
}