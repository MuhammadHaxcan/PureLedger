using PureLedger.ViewModels.Reporting;

namespace PureLedger.Services.Interface
{
    public interface IDashboardService
    {
        Task<DashboardResponse> GetDashboardDataAsync(int? projectId, int days = 30);

        // Dashboard Settings
        Task<DashboardSettingsViewModel?> GetSettingsAsync(int projectId);
        Task<DashboardSettingsViewModel> SaveSettingsAsync(DashboardSettingsUpdateRequest request);
        Task<DashboardSettingsCategoriesResponse> GetCategoriesForSettingsAsync(int projectId);
    }
}
