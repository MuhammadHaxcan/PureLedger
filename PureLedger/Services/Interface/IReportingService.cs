using PureLedger.Models;
using PureLedger.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PureLedger.Services.Interface
{
    public interface IReportingService
    {
        Task<JournalVoucherReportViewModel?> GetSingleDocReportAsync(int journalVoucherId, VoucherType voucherType);
        Task<List<AgingReportViewModel>> GetAgingReportAsync(AgingReportRequest request);
        Task<AccountReportResponseViewModel> GetAccountReportAsync(AccountReportRequestViewModel request);
        Task<List<Project>> GetAllProjectsForUserAsync(int userId);
        Task<List<Category>> GetAllCategoriesAsync();
        Task<decimal> GetAccountBalanceUpToVoucherAsync(int journalVoucherId);
        Task<decimal> GetAccountBalanceBeforeVoucherAsync(int journalVoucherId);
        Task<MasterReportViewModel> GetMasterReportAsync(MasterReportRequest request);
        Task<IEnumerable<Account>> GetAccountsByCategoryAsync(int categoryId);
        Task<IEnumerable<Category>> GetCategoriesByProjectAsync(int projectId);
        Task<(bool Exists, bool IsBaseVoucher)> ValidateVoucherAsync(int journalVoucherId, VoucherType type);
        Task<ProductReportViewModel> GetProductReportAsync(ProductReportRequest request);
        Task<List<Location>> GetAllLocationsAsync();
        Task<MissedEntriesSummaryViewModel> GetMissedEntriesSummaryAsync(MissedEntriesSummaryRequest request);
        Task<List<MissedEntriesByCustomerViewModel>> GetMissedEntriesByCustomerAsync(MissedEntriesReportRequest request);
        Task<List<MissedEntriesByProductViewModel>> GetMissedEntriesByProductAsync(MissedEntriesReportRequest request);
        Task<MissedEntriesDetailViewModel> GetMissedEntriesDetailAsync(MissedEntriesReportRequest request);
        Task<ProjectCartageSettings?> GetProjectCartageSettingsAsync(int projectId);
        Task<JournalVoucher?> GetJournalVoucherAsync(int journalVoucherId);
        Task<List<Booker>> GetAllBookersAsync();
    }
}
