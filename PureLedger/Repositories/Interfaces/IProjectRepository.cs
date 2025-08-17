using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IProjectRepository
    {
        Task<List<Project>> GetAllAsync();
        Task<List<Project>> GetAllForUserAsync(int userId);
        Task<Project?> GetByIdAsync(int id);
        Task<Project?> GetByIdForUserAsync(int id, int userId);
        Task<Project> AddAsync(Project project);
        Task<Project> UpdateAsync(Project project);
        Task<bool> UserHasAccessToProjectAsync(int userId, int projectId);
    }
}