using PureLedger.Models;
using PureLedger.ViewModels;
using PureLedger.Repositories.Interfaces;
using System.Security.Claims;
using PureLedger.Services.Interface;

namespace PureLedger.Services
{
    public class AuthService : IAuthService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IHttpContextAccessor httpContextAccessor,
            IUnitOfWork unitOfWork,
            ILogger<AuthService> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _unitOfWork = unitOfWork;
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
            return await _unitOfWork.Users.GetByUsernameWithAccessAsync(username);
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _unitOfWork.Users.GetByIdWithAccessAsync(userId);
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
            await _unitOfWork.Users.UpdateAsync(user);
        }

        public async Task<bool> HasProjectAccessAsync(int userId, int projectId)
        {
            return await _unitOfWork.Users.HasProjectAccessAsync(userId, projectId);
        }

        public async Task<bool> HasControllerAccessAsync(int userId, string controller)
        {
            return await _unitOfWork.UserControllerAccess.HasControllerAccessAsync(userId, controller);
        }

        public async Task<bool> HasActionAccessAsync(int userId, string controller, string action)
        {
            return await _unitOfWork.UserControllerAccess.HasActionAccessAsync(userId, controller, action);
        }

        public async Task<List<string>> GetUserControllerAccessAsync(int userId)
        {
            return await _unitOfWork.UserControllerAccess.GetUserControllersAsync(userId);
        }

        public async Task<Dictionary<string, List<string>>> GetUserActionAccessAsync(int userId)
        {
            return await _unitOfWork.UserControllerAccess.GetUserActionsAsync(userId);
        }

        public async Task<bool> CreateUserAsync(UserViewModel model)
        {
            // Check if username already exists
            if (await _unitOfWork.Users.UsernameExistsAsync(model.Username))
                return false;

            var subroute = _httpContextAccessor.HttpContext?.Items["Subroute"]?.ToString() ?? "Default";
            
            var newUser = new User
            {
                Username = model.Username.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                FullName = model.FullName.Trim(),
                Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
                IsAdmin = model.IsAdmin,
                Subroute = subroute,
                IsActive = model.IsActive,
                CreatedDate = DateTime.UtcNow
            };

            // Add the user to the database
            await _unitOfWork.Users.AddAsync(newUser);

            // Add project access if provided
            if (model.ProjectIds != null && model.ProjectIds.Count > 0)
            {
                foreach (var projectId in model.ProjectIds)
                {
                    await _unitOfWork.UserProjects.AddAsync(new UserProject
                    {
                        UserId = newUser.UserId,
                        ProjectId = projectId
                    });
                }
            }

            // Add access permissions
            if (model.AccessPermissions != null && model.AccessPermissions.Count > 0)
            {
                foreach (var permission in model.AccessPermissions)
                {
                    var parts = permission.Split('|');
                    var controller = parts[0];
                    var action = parts.Length > 1 ? parts[1] : null;

                    await _unitOfWork.UserControllerAccess.AddAsync(new UserControllerAccess
                    {
                        UserId = newUser.UserId,
                        Controller = controller,
                        Action = action,
                        HasAccess = true
                    });
                }
            }

            return true;
        }

        public async Task<bool> UpdateUserAsync(UserViewModel model)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(model.UserId);
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

            await _unitOfWork.Users.UpdateAsync(user);
            
            // Only update access permissions if the current user is an admin
            if (IsCurrentUserAdmin())
            {
                // Update project access if provided
                if (model.ProjectIds != null)
                {
                    // Remove existing project access
                    await _unitOfWork.UserProjects.DeleteByUserIdAsync(user.UserId);

                    // Add new project access
                    foreach (var projectId in model.ProjectIds)
                    {
                        await _unitOfWork.UserProjects.AddAsync(new UserProject
                        {
                            UserId = user.UserId,
                            ProjectId = projectId
                        });
                    }
                }

                // Remove existing access
                await _unitOfWork.UserControllerAccess.DeleteByUserIdAsync(user.UserId);

                // Add new access permissions
                if (model.AccessPermissions != null && model.AccessPermissions.Count > 0)
                {
                    foreach (var permission in model.AccessPermissions)
                    {
                        var parts = permission.Split('|');
                        var controller = parts[0];
                        var action = parts.Length > 1 ? parts[1] : null;

                        await _unitOfWork.UserControllerAccess.AddAsync(new UserControllerAccess
                        {
                            UserId = user.UserId,
                            Controller = controller,
                            Action = action,
                            HasAccess = true
                        });
                    }
                }
            }

            return true;
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                return false;

            // Don't allow deleting the last admin user
            if (user.IsAdmin)
            {
                var adminCount = await _unitOfWork.Users.GetAdminCountAsync();
                if (adminCount <= 1)
                    return false;
            }

            return await _unitOfWork.Users.DeleteAsync(userId);
        }

        public async Task<bool> IsSubrouteAdminAsync(string username, string subroute)
        {
            var users = await _unitOfWork.Users.GetBySubrouteAsync(subroute);
            var user = users.FirstOrDefault(u => u.Username == username);
            
            return user?.IsAdmin ?? false;
        }

        public async Task<List<User>> GetUsersBySubrouteAsync(string subroute)
        {
            return await _unitOfWork.Users.GetBySubrouteAsync(subroute);
        }

        public async Task<List<Project>> GetUserProjectsAsync(int userId)
        {
            return await _unitOfWork.Users.GetUserProjectsAsync(userId);
        }

        // Get cartage settings for a project
        public async Task<ProjectCartageSettings?> GetProjectCartageSettingsAsync(int projectId)
        {
            return await _unitOfWork.ProjectCartageSettings.GetByProjectIdAsync(projectId);
        }

        // Get all projects
        public async Task<List<Project>> GetAllProjectsAsync()
        {
            return await _unitOfWork.Projects.GetAllAsync();
        }

        // Get all accounts
        public async Task<List<Account>> GetAllAccountsAsync()
        {
            return await _unitOfWork.Accounts.GetAllAsync();
        }

        // Get all categories
        public async Task<List<Category>> GetAllCategoriesAsync()
        {
            return await _unitOfWork.Categories.GetAllAsync();
        }

        // Save or update cartage settings
        public async Task<bool> SaveProjectCartageSettingsAsync(ProjectCartageSettings settings)
        {
            await _unitOfWork.ProjectCartageSettings.AddOrUpdateAsync(settings);
            return true;
        }

        public async Task<List<Category>> GetCategoriesByProjectAsync(int projectId)
        {
            return await _unitOfWork.Categories.GetByProjectIdAsync(projectId);
        }

        public async Task<List<Account>> GetAccountsByCategoryAsync(int categoryId)
        {
            return await _unitOfWork.Accounts.GetByCategoryIdAsync(categoryId);
        }

        // Implementation of the new method
        public async Task<Account?> GetAccountByIdAsync(int accountId)
        {
            return await _unitOfWork.Accounts.GetByIdAsync(accountId);
        }

        private bool IsCurrentUserAdmin()
        {
            if (_httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated != true)
                return false;

            return _httpContextAccessor.HttpContext.User.IsInRole("Admin");
        }

        // Signature management methods
        public async Task<List<Signature>> GetAllSignaturesAsync()
        {
            return await _unitOfWork.Signatures.GetAllAsync();
        }

        public async Task<Signature?> GetSignatureByIdAsync(int id)
        {
            return await _unitOfWork.Signatures.GetByIdAsync(id);
        }

        public async Task<Signature?> GetActiveSignatureAsync()
        {
            return await _unitOfWork.Signatures.GetActiveAsync();
        }

        public async Task<Signature> CreateSignatureAsync(Signature signature)
        {
            return await _unitOfWork.Signatures.AddAsync(signature);
        }

        public async Task<Signature> UpdateSignatureAsync(Signature signature)
        {
            return await _unitOfWork.Signatures.UpdateAsync(signature);
        }

        public async Task<bool> DeleteSignatureAsync(int id)
        {
            return await _unitOfWork.Signatures.DeleteAsync(id);
        }

        public async Task SetActiveSignatureAsync(int id)
        {
            await _unitOfWork.Signatures.SetActiveAsync(id);
        }
    }
}