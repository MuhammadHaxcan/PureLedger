using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;
using PureLedger.Services.Tenant;
using PureLedger.ViewModels;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LoginViewModel = PureLedger.ViewModels.Auth.LoginViewModel;

namespace PureLedger.Services.Auth
{
    /// <summary>
    /// Consolidated authentication service that handles user authentication, JWT token generation,
    /// refresh token management, and security stamp validation.
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AuthService> _logger;
        private readonly IConfiguration _configuration;
        private readonly ITenantCacheService _tenantCacheService;
        private readonly MasterDbContext _masterDb;
        private readonly ITenantSchemaProvider _schemaProvider;

        public AuthService(
            IHttpContextAccessor httpContextAccessor,
            IUnitOfWork unitOfWork,
            ILogger<AuthService> logger,
            IConfiguration configuration,
            ITenantCacheService tenantCacheService,
            MasterDbContext masterDb,
            ITenantSchemaProvider schemaProvider)
        {
            _httpContextAccessor = httpContextAccessor;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _configuration = configuration;
            _tenantCacheService = tenantCacheService;
            _masterDb = masterDb;
            _schemaProvider = schemaProvider;
        }

        #region User Authentication and Validation

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

        /// <summary>
        /// Validates a tenant user login with format "slug|username".
        /// </summary>
        /// <param name="fullUsername">The full username in format "slug|username"</param>
        /// <param name="password">The password</param>
        /// <returns>Tuple of (success, user, schemaName, tenantName)</returns>
        public async Task<TenantLoginResult> ValidateTenantUserAsync(string fullUsername, string password)
        {
            try
            {
                // Parse the slug|username format
                var parts = fullUsername.Split('|');
                if (parts.Length != 2)
                {
                    return TenantLoginResult.Failed("Invalid username format. Use: tenant|username");
                }

                var slug = parts[0].Trim().ToLowerInvariant();
                var username = parts[1].Trim().ToLowerInvariant();

                // Look up tenant in cache
                var tenant = await _tenantCacheService.GetTenantBySlugAsync(slug);
                if (tenant == null)
                {
                    _logger.LogWarning("Tenant not found for slug: {Slug}", slug);
                    return TenantLoginResult.Failed("Invalid tenant or credentials");
                }

                if (!tenant.IsActive)
                {
                    _logger.LogWarning("Attempted login to deactivated tenant: {TenantName}", tenant.Name);
                    return TenantLoginResult.Failed("This tenant is currently deactivated");
                }

                // Set tenant schema context before looking up user
                _schemaProvider.SetTenant(tenant.SchemaName, tenant.Id, tenant.Name);

                // Look up user in tenant schema
                var user = await _unitOfWork.Users.GetByUsernameWithAccessAsync(username);
                if (user == null || !user.IsActive)
                {
                    _logger.LogWarning("User not found or inactive: {Username} in tenant {TenantName}", username, tenant.Name);
                    return TenantLoginResult.Failed("Invalid tenant or credentials");
                }

                // Verify password
                if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    _logger.LogWarning("Invalid password for user {Username} in tenant {TenantName}", username, tenant.Name);
                    return TenantLoginResult.Failed("Invalid tenant or credentials");
                }

                // Update last login
                await UpdateLastLoginAsync(user);

                _logger.LogInformation("Successful tenant login: {Username} in {TenantName}", username, tenant.Name);

                return TenantLoginResult.Succeeded(user, tenant.SchemaName, tenant.Name, tenant.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating tenant user login for {Username}", fullUsername);
                return TenantLoginResult.Failed("An error occurred during login");
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
            return await Task.FromResult(BCrypt.Net.BCrypt.Verify(password, user.PasswordHash));
        }

        public async Task UpdateLastLoginAsync(User user)
        {
            user.LastLoginDate = DateTime.UtcNow;
            await _unitOfWork.Users.UpdateAsync(user);
        }

        #endregion

        #region JWT Token Generation and Validation

        public string GenerateToken(int userId, string username, string fullName, bool isAdmin, string subroute)
        {
            // Default call without tenant context (for backward compatibility)
            return GenerateTenantToken(userId, username, fullName, isAdmin, subroute, null, null);
        }

        /// <summary>
        /// Generates a JWT token for a tenant user with tenant claims.
        /// </summary>
        public string GenerateTenantToken(int userId, string username, string fullName, bool isAdmin, string subroute,
            string? tenantSchema, string? tenantName)
        {
            var secretKey = _configuration["Jwt:SecretKey"] ?? "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!";
            var issuer = _configuration["Jwt:Issuer"] ?? "PureLedger";
            var audience = _configuration["Jwt:Audience"] ?? "PureLedger";
            var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "1440"); // 24 hours default

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.GivenName, fullName),
                new Claim("Subroute", subroute),
                new Claim(ClaimTypes.Role, isAdmin ? "Admin" : "User"),
                new Claim("user_type", "TenantUser")
            };

            // Add tenant-specific claims
            if (!string.IsNullOrEmpty(tenantSchema))
            {
                claims.Add(new Claim("tenant_schema", tenantSchema));
            }
            if (!string.IsNullOrEmpty(tenantName))
            {
                claims.Add(new Claim("tenant_name", tenantName));
            }

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        public ClaimsPrincipal? ValidateToken(string token)
        {
            try
            {
                var secretKey = _configuration["Jwt:SecretKey"] ?? "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!";
                var issuer = _configuration["Jwt:Issuer"] ?? "PureLedger";
                var audience = _configuration["Jwt:Audience"] ?? "PureLedger";

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
                var tokenHandler = new JwtSecurityTokenHandler();

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
                return principal;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed");
                return null;
            }
        }

        public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            try
            {
                var secretKey = _configuration["Jwt:SecretKey"] ?? "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!";
                var issuer = _configuration["Jwt:Issuer"] ?? "PureLedger";
                var audience = _configuration["Jwt:Audience"] ?? "PureLedger";

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
                var tokenHandler = new JwtSecurityTokenHandler();

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = false, // Don't validate lifetime for expired tokens
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

                if (validatedToken is not JwtSecurityToken jwtSecurityToken ||
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new SecurityTokenException("Invalid token");
                }

                return principal;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed for expired token");
                return null;
            }
        }

        #endregion

        #region Refresh Token Management

        public async Task<RefreshToken> CreateRefreshTokenAsync(int userId, string token)
        {
            var refreshTokenExpiryDays = int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"] ?? "7");

            var refreshToken = new RefreshToken
            {
                UserId = userId,
                Token = token,
                ExpiryDate = DateTime.UtcNow.AddDays(refreshTokenExpiryDays),
                CreatedDate = DateTime.UtcNow,
                IsRevoked = false
            };

            return await _unitOfWork.RefreshTokens.AddAsync(refreshToken);
        }

        public async Task<RefreshToken?> GetRefreshTokenAsync(string token)
        {
            return await _unitOfWork.RefreshTokens.GetByTokenAsync(token);
        }

        public async Task<bool> ValidateRefreshTokenAsync(string token)
        {
            var refreshToken = await _unitOfWork.RefreshTokens.GetByTokenAsync(token);

            if (refreshToken == null)
                return false;

            if (refreshToken.IsRevoked)
            {
                _logger.LogWarning("Attempted to use revoked refresh token for user {UserId}", refreshToken.UserId);
                return false;
            }

            if (refreshToken.IsExpired)
            {
                _logger.LogWarning("Attempted to use expired refresh token for user {UserId}", refreshToken.UserId);
                return false;
            }

            return true;
        }

        public async Task<RefreshToken?> RefreshAccessTokenAsync(string accessToken, string refreshToken)
        {
            var storedRefreshToken = await _unitOfWork.RefreshTokens.GetByTokenAsync(refreshToken);

            if (storedRefreshToken == null || !storedRefreshToken.IsActive)
            {
                return null;
            }

            return storedRefreshToken;
        }

        public async Task<bool> RevokeRefreshTokenAsync(string token)
        {
            return await _unitOfWork.RefreshTokens.RevokeTokenAsync(token);
        }

        public async Task<bool> RevokeAllUserTokensAsync(int userId)
        {
            return await _unitOfWork.RefreshTokens.RevokeAllUserTokensAsync(userId);
        }

        #endregion

        #region User Access Management

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

        public async Task<List<int>> GetUserHiddenCategoriesAsync(int userId)
        {
            try
            {
                var userCategories = await _unitOfWork.UserCategories.GetByUserIdAsync(userId);
                return userCategories.Select(uc => uc.CategoryId).ToList();
            }
            catch (Exception ex)
            {
                // If table doesn't exist or any other error, return empty list
                _logger.LogWarning(ex, "Error getting hidden categories for user {UserId}, returning empty list", userId);
                return new List<int>();
            }
        }

        public async Task<List<Project>> GetUserProjectsAsync(int userId)
        {
            return await _unitOfWork.Users.GetUserProjectsAsync(userId);
        }

        #endregion

        #region User Management

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

            // Add hidden categories if provided
            if (model.HiddenCategoryIds != null && model.HiddenCategoryIds.Count > 0)
            {
                foreach (var categoryId in model.HiddenCategoryIds)
                {
                    await _unitOfWork.UserCategories.AddAsync(new UserCategory
                    {
                        UserId = newUser.UserId,
                        CategoryId = categoryId
                    });
                }
            }

            return true;
        }

        public async Task<bool> UpdateUserAsync(UserViewModel model)
        {
            var user = await _unitOfWork.Users.GetByIdWithAccessAsync(model.UserId);
            if (user == null)
                return false;

            // Track if we need to invalidate tokens
            bool shouldInvalidateTokens = false;
            var invalidationReasons = new List<string>();

            // Update user properties
            user.FullName = model.FullName;
            user.Email = model.Email;

            // Only update these if the current user is an admin
            if (IsCurrentUserAdmin())
            {
                // CASE 4: Check if role (IsAdmin) changed
                if (user.IsAdmin != model.IsAdmin)
                {
                    shouldInvalidateTokens = true;
                    invalidationReasons.Add($"Role changed from {(user.IsAdmin ? "Admin" : "User")} to {(model.IsAdmin ? "Admin" : "User")}");
                }
                user.IsAdmin = model.IsAdmin;

                // CASE 3: Check if user is being deactivated
                if (user.IsActive && !model.IsActive)
                {
                    shouldInvalidateTokens = true;
                    invalidationReasons.Add("User deactivated");
                }
                user.IsActive = model.IsActive;
            }

            // Update password if provided
            if (!string.IsNullOrEmpty(model.Password))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);
                shouldInvalidateTokens = true;
                invalidationReasons.Add("Password changed");
            }

            // Only update access permissions if the current user is an admin
            if (IsCurrentUserAdmin())
            {
                // CASE 2: Check if project access changed
                if (model.ProjectIds != null)
                {
                    var currentProjectIds = user.UserProjects?.Select(up => up.ProjectId).OrderBy(x => x).ToList() ?? new List<int>();
                    var newProjectIds = model.ProjectIds.OrderBy(x => x).ToList();

                    if (!currentProjectIds.SequenceEqual(newProjectIds))
                    {
                        shouldInvalidateTokens = true;
                        invalidationReasons.Add("Project access changed");
                    }

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

                // CASE 1: Check if permissions changed
                if (model.AccessPermissions != null)
                {
                    var currentPermissions = user.ControllerAccess?
                        .Select(ca => ca.Action != null ? $"{ca.Controller}|{ca.Action}" : ca.Controller)
                        .OrderBy(x => x)
                        .ToList() ?? new List<string>();
                    var newPermissions = model.AccessPermissions.OrderBy(x => x).ToList();

                    if (!currentPermissions.SequenceEqual(newPermissions))
                    {
                        shouldInvalidateTokens = true;
                        invalidationReasons.Add("Permissions changed");
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

                // Update hidden categories if provided
                if (model.HiddenCategoryIds != null)
                {
                    // Get current hidden category IDs
                    var currentHiddenCategoryIds = user.UserCategories?
                        .Select(uc => uc.CategoryId)
                        .OrderBy(x => x)
                        .ToList() ?? new List<int>();
                    
                    var newHiddenCategoryIds = model.HiddenCategoryIds.OrderBy(x => x).ToList();
                    
                    // Check if hidden categories changed
                    if (!currentHiddenCategoryIds.SequenceEqual(newHiddenCategoryIds))
                    {
                        shouldInvalidateTokens = true;
                        invalidationReasons.Add("Hidden categories changed");
                    }
                    
                    // Remove existing hidden categories
                    await _unitOfWork.UserCategories.DeleteByUserIdAsync(user.UserId);

                    // Add new hidden categories
                    foreach (var categoryId in model.HiddenCategoryIds)
                    {
                        await _unitOfWork.UserCategories.AddAsync(new UserCategory
                        {
                            UserId = user.UserId,
                            CategoryId = categoryId
                        });
                    }
                }
            }

            // SECURITY: If any security-relevant changes occurred, invalidate all refresh tokens
            if (shouldInvalidateTokens)
            {
                // Revoke all existing refresh tokens for this user
                await _unitOfWork.RefreshTokens.RevokeAllUserTokensAsync(user.UserId);

                _logger.LogInformation(
                    "User {UserId} ({Username}) refresh tokens revoked. Reasons: {Reasons}",
                    user.UserId,
                    user.Username,
                    string.Join(", ", invalidationReasons));
            }

            await _unitOfWork.Users.UpdateAsync(user);

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

        #endregion

        #region Project and Settings Management

        public async Task<ProjectCartageSettings?> GetProjectCartageSettingsAsync(int projectId)
        {
            return await _unitOfWork.ProjectCartageSettings.GetByProjectIdAsync(projectId);
        }

        public async Task<List<Project>> GetAllProjectsAsync()
        {
            return await _unitOfWork.Projects.GetAllAsync();
        }

        public async Task<List<Account>> GetAllAccountsAsync()
        {
            return await _unitOfWork.Accounts.GetAllAsync();
        }

        public async Task<List<Category>> GetAllCategoriesAsync()
        {
            return await _unitOfWork.Categories.GetAllAsync();
        }

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

        public async Task<Account?> GetAccountByIdAsync(int accountId)
        {
            return await _unitOfWork.Accounts.GetByIdAsync(accountId);
        }

        #endregion

        #region Signature Management

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

        #endregion

        #region Private Helper Methods

        private bool IsCurrentUserAdmin()
        {
            if (_httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated != true)
                return false;

            return _httpContextAccessor.HttpContext.User.IsInRole("Admin");
        }

        #endregion
    }
}
