using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class UserCategoryRepository : IUserCategoryRepository
    {
        private readonly LedgerDbContext _context;

        public UserCategoryRepository(LedgerDbContext context)
        {
            _context = context;
        }

        public async Task<List<UserCategory>> GetByUserIdAsync(int userId)
        {
            return await _context.UserCategories
                .Where(uc => uc.UserId == userId)
                .ToListAsync();
        }

        public async Task<List<UserCategory>> GetByCategoryIdAsync(int categoryId)
        {
            return await _context.UserCategories
                .Where(uc => uc.CategoryId == categoryId)
                .ToListAsync();
        }

        public async Task<UserCategory?> GetByIdAsync(int id)
        {
            return await _context.UserCategories.FindAsync(id);
        }

        public UserCategory Add(UserCategory userCategory)
        {
            _context.UserCategories.Add(userCategory);
            return userCategory;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var userCategory = await _context.UserCategories.FindAsync(id);
            if (userCategory == null)
                return false;

            _context.UserCategories.Remove(userCategory);
            return true;
        }

        public async Task<bool> DeleteByUserIdAsync(int userId)
        {
            var userCategories = await _context.UserCategories
                .Where(uc => uc.UserId == userId)
                .ToListAsync();

            if (userCategories.Any())
                _context.UserCategories.RemoveRange(userCategories);

            return true;
        }

        public async Task<bool> ExistsAsync(int userId, int categoryId)
        {
            return await _context.UserCategories
                .AnyAsync(uc => uc.UserId == userId && uc.CategoryId == categoryId);
        }
    }
}

