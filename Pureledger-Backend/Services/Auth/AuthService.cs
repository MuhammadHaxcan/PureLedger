using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;
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

        public AuthService(
            IHttpContextAccessor httpContextAccessor,
            IUnitOfWork unitOfWork,
            ILogger<AuthService> logger,
            IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _configuration = configuration;
        }

        #region User Authentication and Validation

        public async Task<User?> AuthenticateUserAsync(LoginViewModel model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrEmpty(model.Password))
                    return null;

                var user = await _unitOfWork.Users.GetByLoginWithAccessAsync(model.Username);
                if (user == null || !user.IsActive)
                    return null;

                // Check if user belongs to the current subroute
                var subroute = _httpContextAccessor.HttpContext?.Items["Subroute"]?.ToString() ?? "Default";
                if (user.Subroute != subroute && !user.IsAdmin)
                    return null;

                var result = CheckPassword(user, model.Password);
                if (result)
                    await UpdateLastLoginAsync(user);

                return result ? user : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating user login");
                return null;
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

        public bool CheckPassword(User user, string password) =>
            BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);

        public async Task UpdateLastLoginAsync(User user)
        {
            user.LastLoginDate = DateTime.UtcNow;
            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync();
        }

        #endregion

        #region JWT Token Generation and Validation

        public string GenerateToken(int userId, string username, string fullName, bool isAdmin, string subroute)
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
                new Claim(ClaimTypes.Role, isAdmin ? "Admin" : "User")
            };

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

            _unitOfWork.RefreshTokens.Add(refreshToken);
            await _unitOfWork.SaveChangesAsync();
            return refreshToken;
        }

        public async Task<RefreshToken?> GetRefreshTokenAsync(string token)
        {
            return await _unitOfWork.RefreshTokens.GetByTokenAsync(token);
        }

        public async Task<bool> RevokeRefreshTokenAsync(string token)
        {
            var revoked = await _unitOfWork.RefreshTokens.RevokeTokenAsync(token);
            if (revoked)
                await _unitOfWork.SaveChangesAsync();
            return revoked;
        }

        public async Task<bool> RevokeAllUserTokensAsync(int userId)
        {
            var revoked = await _unitOfWork.RefreshTokens.RevokeAllUserTokensAsync(userId);
            if (revoked)
                await _unitOfWork.SaveChangesAsync();
            return revoked;
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

        public Task<bool> CreateUserAsync(UserViewModel model) =>
            RetryOnSerializationConflictAsync(() => CreateUserCoreAsync(model));

        // Returns null when PostgreSQL aborted the transaction with a serialization conflict.
        private async Task<bool?> CreateUserCoreAsync(UserViewModel model)
        {
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

            // Serializable makes the uniqueness check and the insert one atomic decision: of two
            // concurrent creates with the same username/email, one fails with a serialization conflict
            // and is retried, so it then sees the committed row and reports the duplicate.
            await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                if (await _unitOfWork.Users.LoginIdentifierExistsAsync(model.Username) ||
                    await _unitOfWork.Users.LoginIdentifierExistsAsync(model.Email ?? string.Empty))
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                _unitOfWork.Users.Add(newUser);
                // The generated user ID is required by the access rows below.
                await _unitOfWork.SaveChangesAsync();

                if (model.ProjectIds != null)
                {
                    foreach (var projectId in model.ProjectIds)
                    {
                        _unitOfWork.UserProjects.Add(new UserProject
                        {
                            UserId = newUser.UserId,
                            ProjectId = projectId
                        });
                    }
                }

                if (model.AccessPermissions != null)
                {
                    foreach (var permission in model.AccessPermissions)
                    {
                        var parts = permission.Split('|');
                        _unitOfWork.UserControllerAccess.Add(new UserControllerAccess
                        {
                            UserId = newUser.UserId,
                            Controller = parts[0],
                            Action = parts.Length > 1 ? parts[1] : null,
                            HasAccess = true
                        });
                    }
                }

                if (model.HiddenCategoryIds != null)
                {
                    foreach (var categoryId in model.HiddenCategoryIds)
                    {
                        _unitOfWork.UserCategories.Add(new UserCategory
                        {
                            UserId = newUser.UserId,
                            CategoryId = categoryId
                        });
                    }
                }

                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex) when (IsSerializationConflict(ex))
            {
                // PostgreSQL can abort the transaction at COMMIT, leaving nothing to roll back.
                try { await transaction.RollbackAsync(); } catch (InvalidOperationException) { }
                return null;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<bool> RetryOnSerializationConflictAsync(Func<Task<bool?>> attempt)
        {
            const int maxAttempts = 3;
            for (var i = 1; i <= maxAttempts; i++)
            {
                var result = await attempt();
                if (result.HasValue) return result.Value;
                // Drop entities left over from the aborted attempt before re-reading.
                _unitOfWork.ClearTracking();
                _logger.LogWarning("User save hit a serialization conflict (attempt {Attempt} of {Max})", i, maxAttempts);
            }
            throw new InvalidOperationException("The user could not be saved because of concurrent changes. Please try again.");
        }

        private static bool IsSerializationConflict(Exception ex)
        {
            if (ex is PostgresException pg &&
                (pg.SqlState == PostgresErrorCodes.SerializationFailure || pg.SqlState == PostgresErrorCodes.DeadlockDetected))
                return true;
            return ex.InnerException != null && IsSerializationConflict(ex.InnerException);
        }

        public Task<bool> UpdateUserAsync(UserViewModel model) =>
            RetryOnSerializationConflictAsync(() => UpdateUserCoreAsync(model));

        // Returns null when PostgreSQL aborted the transaction with a serialization conflict.
        private async Task<bool?> UpdateUserCoreAsync(UserViewModel model)
        {
            var user = await _unitOfWork.Users.GetByIdWithAccessAsync(model.UserId);
            if (user == null)
                return false;

            await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                // Only a changed email can introduce a collision. Checking an unchanged one would block
                // every update (deactivation, role, password) of a user whose legacy email is duplicated.
                var requestedEmail = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
                var emailChanged = !string.Equals(requestedEmail, user.Email?.Trim(), StringComparison.OrdinalIgnoreCase);
                if (emailChanged && requestedEmail != null &&
                    await _unitOfWork.Users.LoginIdentifierExistsAsync(requestedEmail, user.UserId))
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                // Track if we need to invalidate tokens
                bool shouldInvalidateTokens = false;
                var invalidationReasons = new List<string>();

                // Update user properties
                user.FullName = model.FullName;
                user.Email = requestedEmail;

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
                            _unitOfWork.UserProjects.Add(new UserProject
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

                            _unitOfWork.UserControllerAccess.Add(new UserControllerAccess
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
                            _unitOfWork.UserCategories.Add(new UserCategory
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

                _unitOfWork.Users.Update(user);
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex) when (IsSerializationConflict(ex))
            {
                // PostgreSQL can abort the transaction at COMMIT, leaving nothing to roll back.
                try { await transaction.RollbackAsync(); } catch (InvalidOperationException) { }
                return null;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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

            var deleted = await _unitOfWork.Users.DeleteAsync(userId);
            if (deleted)
                await _unitOfWork.SaveChangesAsync();
            return deleted;
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
            await _unitOfWork.SaveChangesAsync();
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
            _unitOfWork.Signatures.Add(signature);
            await _unitOfWork.SaveChangesAsync();
            return signature;
        }

        public async Task<Signature> UpdateSignatureAsync(Signature signature)
        {
            _unitOfWork.Signatures.Update(signature);
            await _unitOfWork.SaveChangesAsync();
            return signature;
        }

        public async Task<bool> DeleteSignatureAsync(int id)
        {
            var deleted = await _unitOfWork.Signatures.DeleteAsync(id);
            if (deleted)
                await _unitOfWork.SaveChangesAsync();
            return deleted;
        }

        public async Task SetActiveSignatureAsync(int id)
        {
            await _unitOfWork.Signatures.SetActiveAsync(id);
            await _unitOfWork.SaveChangesAsync();
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
