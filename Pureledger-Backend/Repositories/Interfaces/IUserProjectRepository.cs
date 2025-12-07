using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IUserProjectRepository
    {
        Task<List<UserProject>> GetByUserIdAsync(int userId);
        Task<List<UserProject>> GetByProjectIdAsync(int projectId);
        Task<UserProject?> GetByIdAsync(int id);
        Task<UserProject> AddAsync(UserProject userProject);
        Task<bool> DeleteAsync(int id);
        Task<bool> DeleteByUserIdAsync(int userId);
        Task<bool> DeleteByProjectIdAsync(int projectId);
        Task<bool> ExistsAsync(int userId, int projectId);
    }
}