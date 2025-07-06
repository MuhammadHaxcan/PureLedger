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
                .Where(c => c.ProjectId == projectId)
                .ToListAsync();
        }

        public async Task<Category?> GetByIdWithAccountTypeAsync(int categoryId)
        {
            return await _context.Categories
                .Include(c => c.AccountType)
                .FirstOrDefaultAsync(c => c.CategoryId == categoryId);
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
    }
}