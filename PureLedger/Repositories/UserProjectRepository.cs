using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class UserProjectRepository : IUserProjectRepository
    {
        private readonly LedgerDbContext _context;

        public UserProjectRepository(LedgerDbContext context)
        {
            _context = context;
        }

        public async Task<List<UserProject>> GetByUserIdAsync(int userId)
        {
            return await _context.UserProjects
                .Where(up => up.UserId == userId)
                .ToListAsync();
        }

        public async Task<List<UserProject>> GetByProjectIdAsync(int projectId)
        {
            return await _context.UserProjects
                .Where(up => up.ProjectId == projectId)
                .ToListAsync();
        }

        public async Task<UserProject?> GetByIdAsync(int id)
        {
            return await _context.UserProjects.FindAsync(id);
        }

        public async Task<UserProject> AddAsync(UserProject userProject)
        {
            _context.UserProjects.Add(userProject);
            await _context.SaveChangesAsync();
            return userProject;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var userProject = await _context.UserProjects.FindAsync(id);
            if (userProject == null)
                return false;

            _context.UserProjects.Remove(userProject);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteByUserIdAsync(int userId)
        {
            var userProjects = await _context.UserProjects
                .Where(up => up.UserId == userId)
                .ToListAsync();

            if (userProjects.Any())
            {
                _context.UserProjects.RemoveRange(userProjects);
                await _context.SaveChangesAsync();
            }

            return true;
        }

        public async Task<bool> DeleteByProjectIdAsync(int projectId)
        {
            var userProjects = await _context.UserProjects
                .Where(up => up.ProjectId == projectId)
                .ToListAsync();

            if (userProjects.Any())
            {
                _context.UserProjects.RemoveRange(userProjects);
                await _context.SaveChangesAsync();
            }

            return true;
        }

        public async Task<bool> ExistsAsync(int userId, int projectId)
        {
            return await _context.UserProjects
                .AnyAsync(up => up.UserId == userId && up.ProjectId == projectId);
        }
    }
}