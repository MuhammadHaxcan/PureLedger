using PureLedger.Models;
using PureLedger.ViewModels;

namespace PureLedger.Services.Interface
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
        Task<bool> HasActionAccessAsync(int userId, string controller, string action);
        Task<List<Project>> GetUserProjectsAsync(int userId);
        Task<List<string>> GetUserControllerAccessAsync(int userId);
        Task<Dictionary<string, List<string>>> GetUserActionAccessAsync(int userId);
        Task<bool> CreateUserAsync(UserViewModel model);
        Task<bool> UpdateUserAsync(UserViewModel model);
        Task<bool> DeleteUserAsync(int userId);
        Task<bool> IsSubrouteAdminAsync(string username, string subroute);
        Task<List<User>> GetUsersBySubrouteAsync(string subroute);
        Task<ProjectCartageSettings?> GetProjectCartageSettingsAsync(int projectId);
        Task<List<Project>> GetAllProjectsAsync();
        Task<List<Account>> GetAllAccountsAsync();
        Task<List<Category>> GetAllCategoriesAsync();
        Task<bool> SaveProjectCartageSettingsAsync(ProjectCartageSettings settings);
        Task<List<Category>> GetCategoriesByProjectAsync(int projectId);
        Task<List<Account>> GetAccountsByCategoryAsync(int categoryId);

        // New method to get account by ID
        Task<Account?> GetAccountByIdAsync(int accountId);

        // Signature management methods
        Task<List<Signature>> GetAllSignaturesAsync();
        Task<Signature?> GetSignatureByIdAsync(int id);
        Task<Signature?> GetActiveSignatureAsync();
        Task<Signature> CreateSignatureAsync(Signature signature);
        Task<Signature> UpdateSignatureAsync(Signature signature);
        Task<bool> DeleteSignatureAsync(int id);
        Task SetActiveSignatureAsync(int id);
    }

}
