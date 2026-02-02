using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class AccountRepository : IAccountRepository
    {
        private readonly TenantDbContext _context;

        public AccountRepository(TenantDbContext context)
        {
            _context = context;
        }

        public async Task<List<Account>> GetAllWithDetailsAsync()
        {
            return await _context.Accounts
                .AsNoTracking()
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
                .AsNoTracking()
                .Where(a => a.CategoryId == categoryId)
                .ToListAsync();
        }

        public async Task<List<Account>> GetEnabledByCategoryIdAsync(int categoryId) {
            return await _context.Accounts
                .AsNoTracking()
                .Where(a => a.CategoryId == categoryId && a.IsEnabled)
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
                    a.AccountGroupId
                })
                .FirstOrDefaultAsync();
        }

        public async Task<List<Account>> GetAllAsync()
        {
            return await _context.Accounts
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Account?> GetByIdWithCategoryAsync(int id)
        {
            return await _context.Accounts
                .AsNoTracking()
                .Include(a => a.Category)
                .FirstOrDefaultAsync(a => a.AccountId == id);
        }

        public async Task<List<Account>> GetByCategoryIdWithCategoryAsync(int categoryId)
        {
            return await _context.Accounts
                .AsNoTracking()
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

        public async Task<List<Account>> GetByProjectAsync(int projectId, int? categoryId = null)
        {
            var query = _context.Accounts
                .Include(a => a.Category)
                    .ThenInclude(c => c.Project)
                .Include(a => a.AccountGroup)
                .AsQueryable();

            query = query.Where(a => a.Category != null && a.Category.ProjectId == projectId);

            if (categoryId.HasValue)
            {
                query = query.Where(a => a.CategoryId == categoryId.Value);
            }

            return await query.ToListAsync();
        }

        public async Task<bool> ExistsByNameInCategoryAsync(string name, int categoryId, int? excludeId = null)
        {
            var normalizedName = name.Trim().ToLower();
            return await _context.Accounts
                .AsNoTracking()
                .AnyAsync(a => a.AccountName.ToLower().Trim() == normalizedName
                    && a.CategoryId == categoryId
                    && (!excludeId.HasValue || a.AccountId != excludeId.Value));
        }

        public async Task<bool> HasAssociatedJournalEntriesAsync(int accountId)
        {
            return await _context.JournalEntries
                .AsNoTracking()
                .AnyAsync(je => je.AccountId == accountId);
        }
    }
}