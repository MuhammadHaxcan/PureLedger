using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IRecipeRepository
    {
        Recipe Add(Recipe recipe);
        Task<Recipe?> GetByIdWithItemsAsync(int id);
        Task<List<Recipe>> GetAllByProjectIdAsync(int projectId);
    }
}
