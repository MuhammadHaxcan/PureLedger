using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface ICategoryRepository
    {
        Task<List<Category>> GetAllWithDetailsAsync();
        Task<Category?> GetByIdWithDetailsAsync(int id);
        Task<List<Category>> GetByProjectIdAsync(int projectId);
        Task<Category?> GetByIdWithAccountTypeAsync(int categoryId);
        Task<Category> AddAsync(Category category);
        Task<Category> UpdateAsync(Category category);
        Task<List<Category>> GetAllAsync();
        Task<bool> DeleteAsync(Category category);

        /// <summary>
        /// Checks if a category with the given name exists in the specified project (case-insensitive)
        /// </summary>
        Task<bool> ExistsByNameInProjectAsync(string name, int projectId, int? excludeId = null);

        /// <summary>
        /// Checks if the category has any associated accounts
        /// </summary>
        Task<bool> HasAssociatedAccountsAsync(int categoryId);

        /// <summary>
        /// Gets account IDs associated with a category without loading full entities
        /// </summary>
        Task<List<int>> GetAssociatedAccountIdsAsync(int categoryId);
    }
}