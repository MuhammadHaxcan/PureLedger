using PureLedger.Models;
using PureLedger.ViewModels;
using PureLedger.ViewModels.PureLedger.ViewModels;

namespace PureLedger.Services
{
    public interface IJournalEntryService
    {
        //Project Operations 
        Task<List<Project>> GetAllProjectsAsync();
        // Journal voucher operations
        Task<List<JournalVoucher>> GetAllVouchersAsync();
        Task<JournalVoucher?> GetVoucherWithEntriesAsync(int jvId);
        Task<JournalVoucher> CreateJournalVoucherAsync(JournalEntryViewModel model);

        // Journal entry operations
        Task<JournalEntryUpdateViewModel?> GetJournalEntryForEditAsync(int entryId);
        Task<bool> UpdateJournalEntryAsync(JournalEntryUpdateViewModel model);

        Task<List<Category>> GetAllCategoriesAsync();
        Task<List<Account>> GetAllAccountsAsync();
        Task<bool> DeleteJournalEntryAsync(int entryId);

        // Voucher summaries and mapping
        Task<JournalEntryViewModel> MapVoucherToViewModelAsync(JournalVoucher voucher);
        Task<object> GetVoucherSummaryAsync(int voucherId);

        // Lookup operations
        Task<List<object>> GetCategoriesByProjectAsync(int projectId);
        Task<List<object>> GetAccountsByCategoryAsync(int categoryId);
        Task<object?> GetProductDetailsAsync(int accountId);
    }
}