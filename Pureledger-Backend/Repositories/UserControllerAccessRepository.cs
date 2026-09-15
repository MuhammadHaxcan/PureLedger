using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class UserControllerAccessRepository : IUserControllerAccessRepository
    {
        private readonly LedgerDbContext _context;

        public UserControllerAccessRepository(LedgerDbContext context)
        {
            _context = context;
        }

        public async Task<List<UserControllerAccess>> GetByUserIdAsync(int userId)
        {
            return await _context.UserControllerAccess
                .Where(uca => uca.UserId == userId)
                .ToListAsync();
        }

        public async Task<List<UserControllerAccess>> GetAllAsync()
        {
            return await _context.UserControllerAccess.ToListAsync();
        }

        public async Task<UserControllerAccess?> GetByIdAsync(int id)
        {
            return await _context.UserControllerAccess.FindAsync(id);
        }

        public UserControllerAccess Add(UserControllerAccess access)
        {
            _context.UserControllerAccess.Add(access);
            return access;
        }

        public UserControllerAccess Update(UserControllerAccess access)
        {
            _context.UserControllerAccess.Update(access);
            return access;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var access = await _context.UserControllerAccess.FindAsync(id);
            if (access == null)
                return false;

            _context.UserControllerAccess.Remove(access);
            return true;
        }

        public async Task<bool> DeleteByUserIdAsync(int userId)
        {
            var accessList = await _context.UserControllerAccess
                .Where(uca => uca.UserId == userId)
                .ToListAsync();

            if (accessList.Any())
                _context.UserControllerAccess.RemoveRange(accessList);

            return true;
        }

        public async Task<bool> HasControllerAccessAsync(int userId, string controller)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
                return false;

            // Admin has access to all controllers
            if (user.IsAdmin)
                return true;

            // Check if user has explicit access to the controller
            return await _context.UserControllerAccess
                .AnyAsync(uca => uca.UserId == userId && 
                           uca.Controller == controller && 
                           uca.Action == null &&
                           uca.HasAccess);
        }

        public async Task<bool> HasActionAccessAsync(int userId, string controller, string action)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
                return false;

            // Admin has access to all actions
            if (user.IsAdmin)
                return true;

            // Check for specific action access
            var actionAccess = await _context.UserControllerAccess
                .FirstOrDefaultAsync(uca => uca.UserId == userId && 
                               uca.Controller == controller && 
                               uca.Action == action && 
                               uca.HasAccess);

            if (actionAccess != null)
                return true;

            // If no action-specific access is defined, fall back to controller-level access
            return await HasControllerAccessAsync(userId, controller);
        }

        public async Task<List<string>> GetUserControllersAsync(int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
                return new List<string>();
                
            // If admin, return all controllers
            if (user.IsAdmin)
            {
                return await _context.UserControllerAccess
                    .Select(uca => uca.Controller)
                    .Distinct()
                    .ToListAsync();
            }

            // Return controller names that the user has access to
            return await _context.UserControllerAccess
                .Where(uca => uca.UserId == userId && uca.HasAccess && uca.Action == null)
                .Select(uca => uca.Controller)
                .Distinct()
                .ToListAsync();
        }

        public async Task<Dictionary<string, List<string>>> GetUserActionsAsync(int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            var result = new Dictionary<string, List<string>>();
            
            if (user == null)
                return result;

            // If admin, return all controller actions
            if (user.IsAdmin)
            {
                var allControllerActions = await _context.UserControllerAccess
                    .Where(uca => uca.Action != null)
                    .GroupBy(uca => uca.Controller)
                    .Select(g => new { 
                        Controller = g.Key, 
                        Actions = g.Select(uca => uca.Action).Where(a => a != null).ToList() 
                    })
                    .ToListAsync();
                
                foreach (var item in allControllerActions)
                {
                    result[item.Controller] = item.Actions!;
                }
                
                return result;
            }
            
            // Get actions grouped by controller that the user has access to
            var accessibleActions = await _context.UserControllerAccess
                .Where(uca => uca.UserId == userId && uca.HasAccess && uca.Action != null)
                .GroupBy(uca => uca.Controller)
                .ToDictionaryAsync(
                    g => g.Key,
                    g => g.Select(uca => uca.Action!)
                          .Where(a => !string.IsNullOrEmpty(a))
                          .ToList()
                );
            
            return accessibleActions;
        }

        public async Task<List<string>> GetAllControllersAsync()
        {
            return await _context.UserControllerAccess
                .Select(uca => uca.Controller)
                .Distinct()
                .ToListAsync();
        }

        public async Task<Dictionary<string, List<string>>> GetAllControllerActionsAsync()
        {
            return await _context.UserControllerAccess
                .Where(uca => uca.Action != null)
                .GroupBy(uca => uca.Controller)
                .ToDictionaryAsync(
                    g => g.Key,
                    g => g.Select(uca => uca.Action!)
                          .Where(a => !string.IsNullOrEmpty(a))
                          .Distinct()
                          .ToList()
                );
        }
    }
}
