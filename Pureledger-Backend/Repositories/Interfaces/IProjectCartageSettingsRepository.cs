using PureLedger.Models;

public interface IProjectCartageSettingsRepository
{
    Task<ProjectCartageSettings?> GetByProjectIdAsync(int projectId);
    Task<ProjectCartageSettings> AddOrUpdateAsync(ProjectCartageSettings settings);
    Task<List<ProjectCartageSettings>> GetAllAsync();
}