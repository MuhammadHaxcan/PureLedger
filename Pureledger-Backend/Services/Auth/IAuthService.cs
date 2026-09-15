using PureLedger.Models;
using PureLedger.ViewModels;
using System.Security.Claims;
using LoginViewModel = PureLedger.ViewModels.Auth.LoginViewModel;

namespace PureLedger.Services.Auth
{
    /// <summary>
    /// Interface for consolidated authentication service that handles:
    /// - User authentication and validation
    /// - JWT token generation and validation
    /// - Refresh token management
    /// - User access management
    /// - User CRUD operations
    /// - Project and settings management
    /// - Signature management
    /// </summary>
    public interface IAuthService
    {
        // User Authentication and Validation
        Task<bool> ValidateUserAsync(LoginViewModel model);
        Task<User?> GetUserByUsernameAsync(string username);
        Task<User?> GetUserByIdAsync(int userId);
        bool CheckPassword(User user, string password);
        Task UpdateLastLoginAsync(User user);

        // JWT Token Generation and Validation
        string GenerateToken(int userId, string username, string fullName, bool isAdmin, string subroute);
        string GenerateRefreshToken();
        ClaimsPrincipal? ValidateToken(string token);
        ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);

        // Refresh Token Management
        Task<RefreshToken> CreateRefreshTokenAsync(int userId, string token);
        Task<RefreshToken?> GetRefreshTokenAsync(string token);
        Task<bool> ValidateRefreshTokenAsync(string token);
        Task<RefreshToken?> RefreshAccessTokenAsync(string accessToken, string refreshToken);
        Task<bool> RevokeRefreshTokenAsync(string token);
        Task<bool> RevokeAllUserTokensAsync(int userId);

        // User Access Management
        Task<bool> HasProjectAccessAsync(int userId, int projectId);
        Task<bool> HasControllerAccessAsync(int userId, string controller);
        Task<bool> HasActionAccessAsync(int userId, string controller, string action);
        Task<List<string>> GetUserControllerAccessAsync(int userId);
        Task<Dictionary<string, List<string>>> GetUserActionAccessAsync(int userId);
        Task<List<int>> GetUserHiddenCategoriesAsync(int userId);
        Task<List<Project>> GetUserProjectsAsync(int userId);

        // User Management
        Task<bool> CreateUserAsync(UserViewModel model);
        Task<bool> UpdateUserAsync(UserViewModel model);
        Task<bool> DeleteUserAsync(int userId);
        Task<bool> IsSubrouteAdminAsync(string username, string subroute);
        Task<List<User>> GetUsersBySubrouteAsync(string subroute);

        // Project and Settings Management
        Task<ProjectCartageSettings?> GetProjectCartageSettingsAsync(int projectId);
        Task<List<Project>> GetAllProjectsAsync();
        Task<List<Account>> GetAllAccountsAsync();
        Task<List<Category>> GetAllCategoriesAsync();
        Task<bool> SaveProjectCartageSettingsAsync(ProjectCartageSettings settings);
        Task<List<Category>> GetCategoriesByProjectAsync(int projectId);
        Task<List<Account>> GetAccountsByCategoryAsync(int categoryId);
        Task<Account?> GetAccountByIdAsync(int accountId);

        // Signature Management
        Task<List<Signature>> GetAllSignaturesAsync();
        Task<Signature?> GetSignatureByIdAsync(int id);
        Task<Signature?> GetActiveSignatureAsync();
        Task<Signature> CreateSignatureAsync(Signature signature);
        Task<Signature> UpdateSignatureAsync(Signature signature);
        Task<bool> DeleteSignatureAsync(int id);
        Task SetActiveSignatureAsync(int id);
    }
}
