using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly LedgerDbContext _context;

        public CategoryRepository(LedgerDbContext context)
        {
            _context = context;
        }

        public async Task<List<Category>> GetAllWithDetailsAsync()
        {
            return await _context.Categories
                .AsNoTracking()
                .Include(c => c.Project)
                .Include(c => c.AccountType)
                .ToListAsync();
        }

        public async Task<Category?> GetByIdWithDetailsAsync(int id)
        {
            return await _context.Categories
                .Include(c => c.Project)
                .Include(c => c.AccountType)
                .FirstOrDefaultAsync(c => c.CategoryId == id);
        }

        public async Task<List<Category>> GetByProjectIdAsync(int projectId)
        {
            return await _context.Categories
                .AsNoTracking()
                .Where(c => c.ProjectId == projectId)
                .ToListAsync();
        }

        public async Task<Category> AddAsync(Category category)
        {
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return category;
        }

        public async Task<Category> UpdateAsync(Category category)
        {
            _context.Categories.Update(category);
            await _context.SaveChangesAsync();
            return category;
        }

        public async Task<List<Category>> GetAllAsync()
        {
            return await _context.Categories
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<bool> DeleteAsync(Category category)
        {
            try
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> ExistsByNameInProjectAsync(string name, int projectId, int? excludeId = null)
        {
            var normalizedName = name.Trim().ToLower();
            return await _context.Categories
                .AsNoTracking()
                .AnyAsync(c => c.CategoryName.ToLower().Trim() == normalizedName
                    && c.ProjectId == projectId
                    && (!excludeId.HasValue || c.CategoryId != excludeId.Value));
        }

        public async Task<List<int>> GetAssociatedAccountIdsAsync(int categoryId)
        {
            return await _context.Accounts
                .AsNoTracking()
                .Where(a => a.CategoryId == categoryId)
                .Select(a => a.AccountId)
                .ToListAsync();
        }

        public async Task<List<Category>> GetByProjectIdForUserAsync(int projectId, int userId)
        
        {
            // Check if user is admin
            var user = await _context.Users.FindAsync(userId);
            if (user?.IsAdmin == true)
            {
                return await GetByProjectIdAsync(projectId);
            }

            var hasProjectAccess = await _context.UserProjects
                .AnyAsync(up => up.UserId == userId && up.ProjectId == projectId);

            if (!hasProjectAccess)
            {
                return new List<Category>();
            }

            // Get hidden category IDs for this user
            var hiddenCategoryIds = await _context.UserCategories
                .Where(uc => uc.UserId == userId)
                .Select(uc => uc.CategoryId)
                .ToListAsync();

            // Return categories from this project, excluding hidden ones
            return await _context.Categories
                .AsNoTracking()
                .Where(c => c.ProjectId == projectId && !hiddenCategoryIds.Contains(c.CategoryId))
                .ToListAsync();
        }
    }
}