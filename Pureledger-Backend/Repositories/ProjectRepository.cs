using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class ProjectRepository : IProjectRepository
    {
        private readonly LedgerDbContext _context;

        public ProjectRepository(LedgerDbContext context)
        {
            _context = context;
        }

        public async Task<List<Project>> GetAllAsync()
        {
            return await _context.Projects
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<Project>> GetAllForUserAsync(int userId)
        {
            // Check if user is admin
            var user = await _context.Users.FindAsync(userId);
            if (user?.IsAdmin == true)
            {
                return await GetAllAsync();
            }

            // Return only projects the user has access to
            return await _context.Projects
                .AsNoTracking()
                .Where(p => _context.UserProjects.Any(up => up.UserId == userId && up.ProjectId == p.ProjectId))
                .ToListAsync();
        }

        public async Task<Project?> GetByIdAsync(int id)
        {
            return await _context.Projects.FindAsync(id);
        }

        public async Task<Project?> GetByIdForUserAsync(int id, int userId)
        {
            // Check if user is admin
            var user = await _context.Users.FindAsync(userId);
            if (user?.IsAdmin == true)
            {
                return await GetByIdAsync(id);
            }

            // Return project only if user has access to it
            return await _context.Projects
                .AsNoTracking()
                .Where(p => p.ProjectId == id &&
                           _context.UserProjects.Any(up => up.UserId == userId && up.ProjectId == p.ProjectId))
                .FirstOrDefaultAsync();
        }

        public Project Add(Project project)
        {
            _context.Projects.Add(project);
            return project;
        }

        public Project Update(Project project)
        {
            _context.Projects.Update(project);
            return project;
        }

        public async Task<bool> UserHasAccessToProjectAsync(int userId, int projectId)
        {
            // Check if user is admin
            var user = await _context.Users.FindAsync(userId);
            if (user?.IsAdmin == true)
            {
                return true;
            }

            // Check if user has explicit access to the project
            return await _context.UserProjects
                .AnyAsync(up => up.UserId == userId && up.ProjectId == projectId);
        }

        public async Task<bool> ExistsByNameAsync(string name, int? excludeId = null)
        {
            var normalizedName = name.Trim().ToLower();
            return await _context.Projects
                .AsNoTracking()
                .AnyAsync(p => p.ProjectName.ToLower().Trim() == normalizedName
                    && (!excludeId.HasValue || p.ProjectId != excludeId.Value));
        }
    }
}