using PureLedger.Models;
using PureLedger.ViewModels;

namespace PureLedger.Services
{
    public interface IDeliveryChallanService
    {
        Task<List<Project>> GetAllProjectsAsync();
        Task<List<Models.Location>> GetAllLocationsAsync();
        Task<List<object>> GetCategoriesByProjectAsync(int projectId);
        Task<List<object>> GetAccountsByCategoryAsync(int categoryId);
        Task<DeliveryChallanFilterViewModel> GetFilteredEntriesForDeliveryChallanAsync(
            DateOnly? sinceDate,
            DateOnly? toDate,
            int? projectId,
            int? categoryId,
            int? accountId);
    }
}