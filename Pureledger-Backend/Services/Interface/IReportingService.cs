using PureLedger.Models;
using PureLedger.ViewModels.Reporting;

namespace PureLedger.Services.Interface
{
    public interface IReportingService
    {
        Task<JournalVoucherReportViewModel?> GetSingleDocReportAsync(int journalVoucherId, VoucherType voucherType);
        Task<AgingReportResponseViewModel> GetAgingReportAsync(AgingReportRequest request);
        Task<AgingFifoResponse> GetAgingFifoAsync(AgingFifoRequest request, CancellationToken cancellationToken = default);
        Task<AccountReportResponseViewModel> GetAccountReportAsync(AccountReportRequestViewModel request);
        Task<List<Project>> GetAllProjectsForUserAsync(int userId);
        Task<List<Category>> GetAllCategoriesAsync();
        Task<decimal> GetAccountBalanceUpToVoucherAsync(int journalVoucherId);
        Task<decimal> GetAccountBalanceBeforeVoucherAsync(int journalVoucherId);
        Task<MasterReportViewModels> GetMasterReportAsync(MasterReportRequest request);
        Task<UserReportViewModels> GetUserReportAsync(UserReportRequest request, int userId);
        Task<object> GetUserReportInitAsync(int userId);
        Task<IEnumerable<Account>> GetAccountsByCategoryAsync(int categoryId);
        Task<IEnumerable<Category>> GetCategoriesByProjectAsync(int projectId);
        Task<IEnumerable<Category>> GetCategoriesByProjectForUserAsync(int projectId, int userId);
        Task<(bool Exists, bool IsBaseVoucher)> ValidateVoucherAsync(int journalVoucherId, VoucherType type);
        Task<ProductReportViewModel> GetProductReportAsync(ProductReportRequest request);
        Task<ProductStockBreakdownViewModel> GetProductStockBreakdownAsync(int projectId, int productId, DateOnly? endDate);
        Task<List<Location>> GetAllLocationsAsync();
        Task<ProjectCartageSettings?> GetProjectCartageSettingsAsync(int projectId);
        Task<List<Booker>> GetAllBookersAsync();

        Task<MissedEntriesPagedResultViewModel> GetFilteredMissedEntriesPaginatedAsync(DateOnly? startDate, DateOnly? endDate, int? projectId, int page, int pageSize);
        Task<object> GetMissedEntriesSummaryAsync(DateOnly? startDate, DateOnly? endDate, int? projectId);
        Task<List<object>> GetMissedEntriesByCustomerAsync(DateOnly? startDate, DateOnly? endDate, int? projectId);
        Task<List<object>> GetMissedEntriesByProductAsync(DateOnly? startDate, DateOnly? endDate, int? projectId);
        Task<object> GetMissedEntriesDataAsync(int customerAccountId, DateOnly? startDate, DateOnly? endDate, int? projectId);
    }
}
