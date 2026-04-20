using PureLedger.ViewModels.Reporting;

namespace PureLedger.Services.Interface
{
    public interface IOldReportingService
    {
        Task<OldMasterReportResponse> GetOldMasterReportAsync(OldMasterReportRequest request, string? legacyDatabase = null);
        Task<OldMasterReportInitResponse> GetOldMasterReportInitAsync(string? legacyDatabase = null);
        Task<List<OldReportLookupOption>> GetCategoriesByProjectAsync(int projectId, string? legacyDatabase = null);
        Task<List<OldReportLookupOption>> GetAccountsByCategoryAsync(string categoryName, string? legacyDatabase = null);
    }
}
