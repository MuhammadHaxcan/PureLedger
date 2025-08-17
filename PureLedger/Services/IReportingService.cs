using PureLedger.Models;
using PureLedger.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PureLedger.Services
{
    public interface IReportingService
    {
        Task<JournalVoucherReportViewModel?> GetSingleDocReportAsync(int journalVoucherId, string voucherType);
        Task<List<AgingReportViewModel>> GetAgingReportAsync(AgingReportRequest request);
        Task<List<AccountReportViewModel>> GetAccountReportAsync(AccountReportRequest request);
        Task<List<Project>> GetAllProjectsAsync();
        Task<List<Project>> GetAllProjectsForUserAsync(int userId);
        Task<List<Category>> GetAllCategoriesAsync();
        Task<decimal> GetAccountBalanceUpToVoucherAsync(int journalVoucherId);
        Task<MasterReportViewModel> GetMasterReportAsync(MasterReportRequest request);
        Task<IEnumerable<Account>> GetAccountsByCategoryAsync(int categoryId);
        Task<IEnumerable<Category>> GetCategoriesByProjectAsync(int projectId);
    }
}
