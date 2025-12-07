using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IUserRepository
    {
        Task<List<User>> GetAllAsync();
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByUsernameAsync(string username);
        Task<User?> GetByUsernameWithAccessAsync(string username);
        Task<User?> GetByIdWithAccessAsync(int id);
        Task<List<User>> GetBySubrouteAsync(string subroute);
        Task<User> AddAsync(User user);
        Task<User> UpdateAsync(User user);
        Task<bool> DeleteAsync(int id);
        Task<bool> UsernameExistsAsync(string username);
        Task<int> GetAdminCountAsync();
        Task<bool> HasProjectAccessAsync(int userId, int projectId);
        Task<List<Project>> GetUserProjectsAsync(int userId);
    }
}