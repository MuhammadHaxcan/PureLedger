using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class RecipeRepository : IRecipeRepository
    {
        private readonly TenantDbContext _context;
        
        public RecipeRepository(TenantDbContext context)
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