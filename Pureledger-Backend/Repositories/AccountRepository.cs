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
                .AsNoTracking()
                .Include(a => a.AccountData)
                .Include(a => a.LinkedBooker)
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
                .Include(a => a.LinkedBooker)
                .Include(a => a.ProductData)
                .FirstOrDefaultAsync(a => a.AccountId == id);
        }

        public Account Add(Account account)
        {
            _context.Accounts.Add(account);
            return account;
        }

        public Account Update(Account account)
        {
            _context.Accounts.Update(account);
            return account;
        }

        public bool Delete(Account account)
        {
            _context.Accounts.Remove(account);
            return true;
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

        // New method to check if account has associated journal entries
        public Task<List<int>> GetPendingApprovalIdsAsync(int accountId) => _context.ApprovalVoucherEntries
            .Where(e => e.AccountId == accountId && e.ApprovalVoucher.Status == ApprovalStatuses.Pending)
            .Select(e => e.ApprovalVoucherId).Distinct().OrderBy(id => id).ToListAsync();

        public async Task<List<int>> GetAssociatedJournalVoucherIds(int accountId)
        {
            return await _context.JournalEntries
                .Where(je => je.AccountId == accountId)
                .Select(je => je.JournalVoucherId)
                .Distinct()
                .ToListAsync();
        }

        public async Task<List<Account>> GetByProjectAsync(int projectId, int? categoryId = null)
        {
            var query = _context.Accounts
                .Include(a => a.LinkedBooker)
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

        public Task<bool> HasLinkedBookerAsync(int bookerId) => _context.Accounts
            .AnyAsync(a => a.LinkedBookerId == bookerId);
    }
}
