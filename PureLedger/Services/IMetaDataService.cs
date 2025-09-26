using PureLedger.Models;

namespace PureLedger.Services
{
    public interface IMetaDataService
    {
        // Project operations
        Task<List<Project>> GetAllProjectsAsync();
        Task<List<Project>> GetAllProjectsForUserAsync(int userId);
        Task<Project?> GetProjectByIdAsync(int id);
        Task<List<string>> CreateProjectAsync(Project project);
        Task<List<string>> UpdateProjectAsync(Project project);

        // Category operations
        Task<List<Category>> GetAllCategoriesAsync();
        Task<List<Project>> GetAllProjectsForCategoriesAsync();
        Task<List<Project>> GetAllProjectsForCategoriesForUserAsync(int userId);
        Task<List<AccountType>> GetAllAccountTypesAsync();
        Task<Category?> GetCategoryByIdAsync(int id);
        Task<List<string>> CreateCategoryAsync(Category category);
        Task<List<string>> UpdateCategoryAsync(Category category);
        Task<List<object>> GetCategoriesByProjectAsync(int projectId);
        
        // Account operations
        Task<List<Account>> GetAllAccountsAsync();
        Task<List<Category>> GetCategoriesForAccountsAsync();
        Task<List<AccountGroup>> GetAccountGroupsAsync();
        Task<List<Project>> GetProjectsForAccountsAsync();
        Task<List<Project>> GetProjectsForAccountsForUserAsync(int userId);
        Task<Account?> GetAccountByIdAsync(int id);
        Task<List<string>> CreateAccountAsync(Account account);
        Task<List<string>> UpdateAccountAsync(Account account);
        Task<(bool Success, List<int> AssociatedVoucherIds)> DeleteAccountAsync(int id);
        Task<List<int>> GetAssociatedVoucherIdsAsync(int accountId);

        // Tag operations
        Task<List<Tag>> GetAllTagsAsync();
        Task<List<string>> CreateTagAsync(Tag tag);
        Task<bool> DeleteTagAsync(int tagId);
        Task<bool> IsTagInUseAsync(int tagId);
        Task<List<object>> SearchTagsAsync(string term, int take = 20);

        // Location operations
        Task<List<Location>> GetAllLocationsAsync();
        Task<Location?> GetLocationByIdAsync(int id);
        Task<List<string>> CreateLocationAsync(Location location);
        Task<List<string>> UpdateLocationAsync(Location location);

    }
}