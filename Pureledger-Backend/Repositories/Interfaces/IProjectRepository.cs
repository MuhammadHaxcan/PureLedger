using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IProjectRepository
    {
        Task<List<Project>> GetAllAsync();
        Task<List<Project>> GetAllForUserAsync(int userId);
        Task<Project?> GetByIdAsync(int id);
        Project Add(Project project);
        Project Update(Project project);

        /// <summary>
        /// Checks if a project with the given name exists (case-insensitive)
        /// </summary>
        /// <param name="name">Project name to check</param>
        /// <param name="excludeId">Optional project ID to exclude from check (for updates)</param>
        Task<bool> ExistsByNameAsync(string name, int? excludeId = null);
    }
}