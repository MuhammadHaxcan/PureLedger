using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class RecipeItemRepository : IRecipeItemRepository
    {
        private readonly LedgerDbContext _context;
        
        public RecipeItemRepository(LedgerDbContext context)
        {
            _context = context;
        }
        
        public RecipeItem Add(RecipeItem recipeItem)
        {
            _context.RecipeItems.Add(recipeItem);
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
