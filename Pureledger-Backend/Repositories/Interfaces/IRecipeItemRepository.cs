using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IRecipeItemRepository
    {
        RecipeItem Add(RecipeItem recipeItem);
        Task<List<RecipeItem>> GetByRecipeIdAsync(int recipeId);
    }
}
