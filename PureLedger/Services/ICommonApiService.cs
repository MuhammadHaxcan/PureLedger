using PureLedger.Models;

namespace PureLedger.Services
{
    public interface ICommonApiService
    {
        Task<IEnumerable<Project>> GetProjectsAsync();
        Task<IEnumerable<Category>> GetCategoriesByProjectAsync(int projectId);
        Task<IEnumerable<Account>> GetAccountsByCategoryAsync(int categoryId);
        Task<IEnumerable<Account>> GetAccountsByCategoryAndProjectAsync(int categoryId, int projectId);
    }
}
