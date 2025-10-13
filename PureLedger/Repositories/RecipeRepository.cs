using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class RecipeRepository : IRecipeRepository
    {
        private readonly LedgerDbContext _context;
        
        public RecipeRepository(LedgerDbContext context)
        {
            _context = context;
        }
        
        public async Task<Recipe> AddAsync(Recipe recipe)
        {
            _context.Recipes.Add(recipe);
            await _context.SaveChangesAsync();
            return recipe;
        }
        
        public async Task<Recipe?> GetByIdWithItemsAsync(int id)
        {
            return await _context.Recipes
                .Include(r => r.RecipeItems)
                .FirstOrDefaultAsync(r => r.RecipeId == id);
        }
        
        public async Task<List<Recipe>> GetAllByProjectIdAsync(int projectId)
        {
            return await _context.Recipes
                .Where(r => r.ProjectId == projectId)
                .ToListAsync();
        }
    }
}