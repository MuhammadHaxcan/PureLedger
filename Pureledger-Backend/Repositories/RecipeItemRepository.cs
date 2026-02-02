using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class RecipeItemRepository : IRecipeItemRepository
    {
        private readonly TenantDbContext _context;
        
        public RecipeItemRepository(TenantDbContext context)
        {
            _context = context;
        }
        
        public async Task<RecipeItem> AddAsync(RecipeItem recipeItem)
        {
            _context.RecipeItems.Add(recipeItem);
            await _context.SaveChangesAsync();
            return recipeItem;
        }
        
        public async Task<List<RecipeItem>> GetByRecipeIdAsync(int recipeId)
        {
            return await _context.RecipeItems
                .Where(ri => ri.RecipeId == recipeId)
                .ToListAsync();
        }
    }
}