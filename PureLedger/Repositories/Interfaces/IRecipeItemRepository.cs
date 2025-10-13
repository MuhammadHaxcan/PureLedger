using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IRecipeItemRepository
    {
        Task<RecipeItem> AddAsync(RecipeItem recipeItem);
        Task<List<RecipeItem>> GetByRecipeIdAsync(int recipeId);
    }
}