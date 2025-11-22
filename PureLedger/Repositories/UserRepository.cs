using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly LedgerDbContext _context;

        public UserRepository(LedgerDbContext context)
        {
            _context = context;
        }

        public async Task<List<User>> GetAllAsync()
        {
            return await _context.Users.ToListAsync();
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
        }

        public async Task<User?> GetByUsernameWithAccessAsync(string username)
        {
            return await _context.Users
                .Include(u => u.UserProjects)
                .Include(u => u.ControllerAccess)
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
        }

        public async Task<User?> GetByIdWithAccessAsync(int id)
        {
            return await _context.Users
                .Include(u => u.UserProjects)
                .Include(u => u.ControllerAccess)
                .FirstOrDefaultAsync(u => u.UserId == id);
        }

        public async Task<List<User>> GetBySubrouteAsync(string subroute)
        {
            return await _context.Users
                .Where(u => u.Subroute == subroute)
                .ToListAsync();
        }

        public async Task<User> AddAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<User> UpdateAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return false;

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UsernameExistsAsync(string username)
        {
            return await _context.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower());
        }

        public async Task<int> GetAdminCountAsync()
        {
            return await _context.Users.CountAsync(u => u.IsAdmin);
        }

        public async Task<bool> HasProjectAccessAsync(int userId, int projectId)
        {
            var user = await _context.Users
                .Include(u => u.UserProjects)
                .FirstOrDefaultAsync(u => u.UserId == userId);
                
            if (user == null)
                return false;

            // Admin has access to all projects
            if (user.IsAdmin)
                return true;

            // Check explicit project assignments
            return await _context.UserProjects
                .AnyAsync(up => up.UserId == userId && up.ProjectId == projectId);
        }

        public async Task<List<Project>> GetUserProjectsAsync(int userId)
        {
            var user = await _context.Users
                .Include(u => u.UserProjects)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return new List<Project>();

            // Admin: return all projects
            if (user.IsAdmin)
                return await _context.Projects.ToListAsync();

            // Return only projects the user has access to
            var projectIds = user.UserProjects.Select(up => up.ProjectId).ToList();
            return await _context.Projects
                .Where(p => projectIds.Contains(p.ProjectId))
                .ToListAsync();
        }
    }
}