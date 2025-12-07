using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IRecipeRepository
    {
        Task<Recipe> AddAsync(Recipe recipe);
        Task<Recipe?> GetByIdWithItemsAsync(int id);
        Task<List<Recipe>> GetAllByProjectIdAsync(int projectId);
    }
}