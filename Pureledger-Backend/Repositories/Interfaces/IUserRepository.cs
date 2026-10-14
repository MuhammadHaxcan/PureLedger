using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IUserRepository
    {
        Task<List<User>> GetAllAsync();
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByUsernameWithAccessAsync(string username);
        Task<User?> GetByLoginWithAccessAsync(string identifier);
        Task<bool> LoginIdentifierExistsAsync(string identifier, int? excludedUserId = null);
        Task<User?> GetByIdWithAccessAsync(int id);
        Task<List<User>> GetBySubrouteAsync(string subroute);
        User Add(User user);
        User Update(User user);
        Task<bool> DeleteAsync(int id);
        Task<int> GetAdminCountAsync();
        Task<bool> HasProjectAccessAsync(int userId, int projectId);
        Task<List<Project>> GetUserProjectsAsync(int userId);
    }
}
