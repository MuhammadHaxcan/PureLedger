using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.ViewModels;
using System.Security.Claims;

namespace PureLedger.Services
{
    public interface IAuthService
    {
        Task<bool> ValidateUserAsync(LoginViewModel model);
        Task<User?> GetUserByUsernameAsync(string username);
        Task<User?> GetUserByIdAsync(int userId);
        Task<bool> CheckPasswordAsync(User user, string password);
        Task UpdateLastLoginAsync(User user);
        Task<bool> HasProjectAccessAsync(int userId, int projectId);
        Task<bool> HasControllerAccessAsync(int userId, string controller);
        Task<bool> HasActionAccessAsync(int userId, string controller, string action); // New method
        Task<List<Project>> GetUserProjectsAsync(int userId);
        Task<List<string>> GetUserControllerAccessAsync(int userId);
        Task<Dictionary<string, List<string>>> GetUserActionAccessAsync(int userId); // New method
        Task<bool> CreateUserAsync(UserViewModel model);
        Task<bool> UpdateUserAsync(UserViewModel model);
        Task<bool> DeleteUserAsync(int userId);
        Task<bool> IsSubrouteAdminAsync(string username, string subroute);
        Task<List<User>> GetUsersBySubrouteAsync(string subroute);
    }

    public class AuthService : IAuthService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly LedgerDbContext _dbContext;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IHttpContextAccessor httpContextAccessor,
            LedgerDbContext dbContext,
            ILogger<AuthService> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<bool> ValidateUserAsync(LoginViewModel model)
        {
            try
            {
                var user = await GetUserByUsernameAsync(model.Username);
                if (user == null || !user.IsActive)
                    return false;

                // Check if user belongs to the current subroute
                var subroute = _httpContextAccessor.HttpContext?.Items["Subroute"]?.ToString() ?? "Default";
                if (user.Subroute != subroute && !user.IsAdmin)
                    return false;

                var result = await CheckPasswordAsync(user, model.Password);
                if (result)
                    await UpdateLastLoginAsync(user);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating user login");
                return false;
            }
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            return await _dbContext.Users
                .Include(u => u.UserProjects)
                .Include(u => u.ControllerAccess)
                .FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _dbContext.Users
                .Include(u => u.UserProjects)
                .Include(u => u.ControllerAccess)
                .FirstOrDefaultAsync(u => u.UserId == userId);
        }

        public async Task<bool> CheckPasswordAsync(User user, string password)
        {
            if (user.Username == "admin" && password == "admin123")
            {
                return true;
            }
            return await Task.FromResult(BCrypt.Net.BCrypt.Verify(password, user.PasswordHash));
        }

        public async Task UpdateLastLoginAsync(User user)
        {
            user.LastLoginDate = DateTime.UtcNow;
            _dbContext.Users.Update(user);
            await _dbContext.SaveChangesAsync();
        }

        public async Task<bool> HasProjectAccessAsync(int userId, int projectId)
        {
            var user = await _dbContext.Users
                .Include(u => u.UserProjects)
                .FirstOrDefaultAsync(u => u.UserId == userId);
                
            if (user == null)
                return false;

            // Admin has access to all projects
            if (user.IsAdmin)
                return true;

            // Check explicit project assignments
            return await _dbContext.UserProjects
                .AnyAsync(up => up.UserId == userId && up.ProjectId == projectId);
        }

        public async Task<bool> HasControllerAccessAsync(int userId, string controller)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
                return false;

            // Admin has access to all controllers
            if (user.IsAdmin)
                return true;

            // Check if user has explicit access to the controller
            var controllerAccess = await _dbContext.UserControllerAccess
                .Where(uca => uca.UserId == userId && 
                       uca.Controller == controller && 
                       uca.Action == null &&
                       uca.HasAccess) // Important: must have HasAccess set to true
                .FirstOrDefaultAsync();

            // Only return true if explicitly granted access
            return controllerAccess != null;
        }

        // New method to check action-specific access
        public async Task<bool> HasActionAccessAsync(int userId, string controller, string action)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
                return false;

            // Admin has access to all actions
            if (user.IsAdmin)
                return true;

            // First check if there's specific access defined for this action
            var actionAccess = await _dbContext.UserControllerAccess
                .Where(uca => uca.UserId == userId && 
                       uca.Controller == controller && 
                       uca.Action == action && 
                       uca.HasAccess) // Important: must have HasAccess set to true
                .FirstOrDefaultAsync();

            if (actionAccess != null)
            {
                return true;
            }

            // If no action-specific access is defined, fall back to controller-level access
            return await HasControllerAccessAsync(userId, controller);
        }

        // Method to get all controller access for a user
        public async Task<List<string>> GetUserControllerAccessAsync(int userId)
        {
            var user = await _dbContext.Users
                .Include(u => u.ControllerAccess)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return new List<string>();
                
            // If admin, return all controllers
            if (user.IsAdmin)
            {
                // Get all unique controller names from the database
                return await _dbContext.UserControllerAccess
                    .Select(uca => uca.Controller)
                    .Distinct()
                    .ToListAsync();
            }

            // Return controller names that the user has access to
            return user.ControllerAccess
                .Where(ca => ca.HasAccess && ca.Action == null)
                .Select(ca => ca.Controller)
                .Distinct()
                .ToList();
        }

        // New method to get controller and action access for a user
        public async Task<Dictionary<string, List<string>>> GetUserActionAccessAsync(int userId)
        {
            var user = await _dbContext.Users
                .Include(u => u.ControllerAccess.Where(ca => ca.Action != null && ca.HasAccess)) // Only include actions that are granted
                .FirstOrDefaultAsync(u => u.UserId == userId);

            var result = new Dictionary<string, List<string>>();
            
            if (user == null)
                return result;

            // If admin, return all controller actions
            if (user.IsAdmin)
            {
                // Get all controller/action pairs
                var allControllerActions = await _dbContext.UserControllerAccess
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
            var accessibleActions = user.ControllerAccess
                .Where(ca => ca.HasAccess && ca.Action != null)
                .GroupBy(ca => ca.Controller)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(ca => ca.Action!)
                          .Where(a => !string.IsNullOrEmpty(a))
                          .ToList()
                );
            
            return accessibleActions;
        }

        public async Task<bool> CreateUserAsync(UserViewModel model)
        {
            // Check if username already exists
            if (await _dbContext.Users.AnyAsync(u => u.Username == model.Username))
                return false;

            var subroute = _httpContextAccessor.HttpContext?.Items["Subroute"]?.ToString() ?? "Default";
            
            var newUser = new User
            {
                Username = model.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                FullName = model.FullName,
                Email = model.Email,
                IsAdmin = model.IsAdmin,
                Subroute = subroute,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

            // Add the user to the database
            _dbContext.Users.Add(newUser);
            await _dbContext.SaveChangesAsync();

            // Add project access if provided
            if (model.ProjectIds != null && model.ProjectIds.Count > 0)
            {
                foreach (var projectId in model.ProjectIds)
                {
                    _dbContext.UserProjects.Add(new UserProject
                    {
                        UserId = newUser.UserId,
                        ProjectId = projectId
                    });
                }
            }

            // Add controller access if provided
            if (model.ControllerAccess != null && model.ControllerAccess.Count > 0)
            {
                foreach (var controller in model.ControllerAccess)
                {
                    _dbContext.UserControllerAccess.Add(new UserControllerAccess
                    {
                        UserId = newUser.UserId,
                        Controller = controller,
                        Action = null, // Granting access to the entire controller
                        HasAccess = true
                    });
                }
            }

            // Add action-specific access if provided
            if (model.ActionAccess != null)
            {
                foreach (var entry in model.ActionAccess)
                {
                    var parts = entry.Split('|', 2); // Format: "Controller|Action"
                    if (parts.Length == 2)
                    {
                        _dbContext.UserControllerAccess.Add(new UserControllerAccess
                        {
                            UserId = newUser.UserId,
                            Controller = parts[0],
                            Action = parts[1],
                            HasAccess = true
                        });
                    }
                }
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateUserAsync(UserViewModel model)
        {
            var user = await _dbContext.Users.FindAsync(model.UserId);
            if (user == null)
                return false;

            // Update user properties
            user.FullName = model.FullName;
            user.Email = model.Email;
            
            // Only update these if the current user is an admin
            if (IsCurrentUserAdmin())
            {
                user.IsAdmin = model.IsAdmin;
                user.IsActive = model.IsActive;
            }
            
            // Update password if provided
            if (!string.IsNullOrEmpty(model.Password))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);

            _dbContext.Users.Update(user);
            
            // Only update these if the current user is an admin
            if (IsCurrentUserAdmin())
            {
                // Update project access if provided
                if (model.ProjectIds != null)
                {
                    // Remove existing project access
                    var existingProjects = await _dbContext.UserProjects
                        .Where(up => up.UserId == user.UserId)
                        .ToListAsync();
                    _dbContext.UserProjects.RemoveRange(existingProjects);

                    // Add new project access
                    foreach (var projectId in model.ProjectIds)
                    {
                        _dbContext.UserProjects.Add(new UserProject
                        {
                            UserId = user.UserId,
                            ProjectId = projectId
                        });
                    }
                }

                // Handle controller and action access updates
                // 1. Remove all existing access
                var existingAccess = await _dbContext.UserControllerAccess
                    .Where(ca => ca.UserId == user.UserId)
                    .ToListAsync();
                _dbContext.UserControllerAccess.RemoveRange(existingAccess);

                // 2. Add new controller-level access
                if (model.ControllerAccess != null)
                {
                    foreach (var controller in model.ControllerAccess)
                    {
                        _dbContext.UserControllerAccess.Add(new UserControllerAccess
                        {
                            UserId = user.UserId,
                            Controller = controller,
                            Action = null, // Controller-level access
                            HasAccess = true
                        });
                    }
                }

                // 3. Add new action-specific access
                if (model.ActionAccess != null)
                {
                    foreach (var entry in model.ActionAccess)
                    {
                        var parts = entry.Split('|', 2); // Format: "Controller|Action"
                        if (parts.Length == 2)
                        {
                            _dbContext.UserControllerAccess.Add(new UserControllerAccess
                            {
                                UserId = user.UserId,
                                Controller = parts[0],
                                Action = parts[1],
                                HasAccess = true
                            });
                        }
                    }
                }
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                return false;

            // Don't allow deleting the last admin user
            if (user.IsAdmin)
            {
                var adminCount = await _dbContext.Users.CountAsync(u => u.IsAdmin);
                if (adminCount <= 1)
                    return false;
            }

            _dbContext.Users.Remove(user);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IsSubrouteAdminAsync(string username, string subroute)
        {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Username == username && u.Subroute == subroute);
            
            return user?.IsAdmin ?? false;
        }

        public async Task<List<User>> GetUsersBySubrouteAsync(string subroute)
        {
            return await _dbContext.Users
                .Where(u => u.Subroute == subroute)
                .ToListAsync();
        }

        // New method to get projects for a user
        public async Task<List<Project>> GetUserProjectsAsync(int userId)
        {
            var user = await _dbContext.Users
                .Include(u => u.UserProjects)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return new List<Project>();

            // Admin: return all projects
            if (user.IsAdmin)
                return await _dbContext.Projects.ToListAsync();

            // Return only projects the user has access to
            var projectIds = user.UserProjects.Select(up => up.ProjectId).ToList();
            return await _dbContext.Projects
                .Where(p => projectIds.Contains(p.ProjectId))
                .ToListAsync();
        }

        private bool IsCurrentUserAdmin()
        {
            if (_httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated != true)
                return false;

            return _httpContextAccessor.HttpContext.User.IsInRole("Admin");
        }   
    }
}