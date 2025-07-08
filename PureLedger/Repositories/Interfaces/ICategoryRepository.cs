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
    }
}