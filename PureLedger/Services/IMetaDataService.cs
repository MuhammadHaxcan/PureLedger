using PureLedger.Models;

namespace PureLedger.Services
{
    public interface IMetaDataService
    {
        // Project operations
        Task<List<Project>> GetAllProjectsAsync();
        Task<Project?> GetProjectByIdAsync(int id);
        Task<Project> CreateProjectAsync(Project project);
        Task<Project> UpdateProjectAsync(Project project);
        
        // Category operations
        Task<List<Category>> GetAllCategoriesAsync();
        Task<List<Project>> GetAllProjectsForCategoriesAsync();
        Task<List<AccountType>> GetAllAccountTypesAsync();
        Task<Category?> GetCategoryByIdAsync(int id);
        Task<Category> CreateCategoryAsync(Category category);
        Task<Category> UpdateCategoryAsync(Category category);
        Task<List<object>> GetCategoriesByProjectAsync(int projectId);
        
        // Account operations
        Task<List<Account>> GetAllAccountsAsync();
        Task<List<Category>> GetCategoriesForAccountsAsync();
        Task<List<AccountGroup>> GetAccountGroupsAsync();
        Task<List<Project>> GetProjectsForAccountsAsync();
        Task<Account?> GetAccountByIdAsync(int id);
        Task<Account> CreateAccountAsync(Account account);
        Task<Account> UpdateAccountAsync(Account account);
        
        // Tag operations
        Task<List<Tag>> GetAllTagsAsync();
        Task<Tag> CreateTagAsync(Tag tag);
        Task<bool> DeleteTagAsync(int tagId);
        Task<bool> IsTagInUseAsync(int tagId);
        Task<List<object>> SearchTagsAsync(string term, int take = 20);
    }
}