using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

public class ProjectCartageSettingsRepository : IProjectCartageSettingsRepository
{
    private readonly TenantDbContext _dbContext;

    public ProjectCartageSettingsRepository(TenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProjectCartageSettings?> GetByProjectIdAsync(int projectId)
    {
        return await _dbContext.ProjectCartageSettings
            .Include(x => x.Project)
            .Include(x => x.ForExpenseCategory)
            .Include(x => x.CartageTransporterCategory)
            .FirstOrDefaultAsync(x => x.ProjectId == projectId);
    }

    public async Task<ProjectCartageSettings> AddOrUpdateAsync(ProjectCartageSettings settings)
    {
        var existing = await _dbContext.ProjectCartageSettings
            .FirstOrDefaultAsync(x => x.ProjectId == settings.ProjectId);

        if (existing != null)
        {
            // Update existing record with all properties including ProjectImage
            existing.ForExpenseCategoryId = settings.ForExpenseCategoryId;
            existing.CartageTransporterCategoryId = settings.CartageTransporterCategoryId;

            // Update the image if a new one is provided (base64 string)
            if (!string.IsNullOrEmpty(settings.ProjectImage))
            {
                existing.ProjectImage = settings.ProjectImage;
            }

            _dbContext.ProjectCartageSettings.Update(existing);
            await _dbContext.SaveChangesAsync();
            return existing;
        }
        else
        {
            // Add new record with all properties
            _dbContext.ProjectCartageSettings.Add(settings);
            await _dbContext.SaveChangesAsync();
            return settings;
        }
    }

    public async Task<List<ProjectCartageSettings>> GetAllAsync()
    {
        return await _dbContext.ProjectCartageSettings
            .Include(x => x.Project)
            .Include(x => x.ForExpenseCategory)
            .Include(x => x.CartageTransporterCategory)
            .ToListAsync();
    }
}