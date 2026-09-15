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
        
        public Recipe Add(Recipe recipe)
        {
            _context.Recipes.Add(recipe);
            return recipe;
        }
        
        public async Task<Recipe?> GetByIdWithItemsAsync(int id)
        {
            // Add the missing Include statements for the related entities
            return await _context.Recipes
                .Include(r => r.Project)
                .Include(r => r.RecipeItems)
                    .ThenInclude(ri => ri.Category)
                .Include(r => r.RecipeItems)
                    .ThenInclude(ri => ri.Account)
                .FirstOrDefaultAsync(r => r.RecipeId == id);
        }
        
        public async Task<List<Recipe>> GetAllByProjectIdAsync(int projectId)
        {
            return await _context.Recipes
                .Include(r => r.Project)
                .Where(r => r.ProjectId == projectId)
                .ToListAsync();
        }
    }
}
