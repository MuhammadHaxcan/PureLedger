using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IRecipeItemRepository
    {
        RecipeItem Add(RecipeItem recipeItem);
    }
}
