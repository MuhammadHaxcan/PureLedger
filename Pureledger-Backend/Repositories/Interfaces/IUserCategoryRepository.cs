using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IUserCategoryRepository
    {
        Task<List<UserCategory>> GetByUserIdAsync(int userId);
        Task<List<UserCategory>> GetByCategoryIdAsync(int categoryId);
        Task<UserCategory?> GetByIdAsync(int id);
        UserCategory Add(UserCategory userCategory);
        Task<bool> DeleteAsync(int id);
        Task<bool> DeleteByUserIdAsync(int userId);
        Task<bool> DeleteByCategoryIdAsync(int categoryId);
        Task<bool> ExistsAsync(int userId, int categoryId);
    }
}

