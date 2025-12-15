using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface ICategoryRepository
    {
        Task<List<Category>> GetAllWithDetailsAsync();
        Task<Category?> GetByIdWithDetailsAsync(int id);
        Task<List<Category>> GetByProjectIdAsync(int projectId);
        Task<Category> AddAsync(Category category);
        Task<Category> UpdateAsync(Category category);
        Task<List<Category>> GetAllAsync();
        Task<bool> DeleteAsync(Category category);
        Task<bool> ExistsByNameInProjectAsync(string name, int projectId, int? excludeId = null);
        Task<List<int>> GetAssociatedAccountIdsAsync(int categoryId);
        Task<List<Category>> GetByProjectIdForUserAsync(int projectId, int userId);

    }
}