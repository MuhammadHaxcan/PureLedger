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

        Task<bool> DeleteJournalEntryAsync(int entryId);

        // Voucher summaries and mapping
        Task<JournalEntryViewModel> MapVoucherToViewModelAsync(JournalVoucher voucher);
        Task<object> GetVoucherSummaryAsync(int voucherId);

        // Lookup operations
        Task<object?> GetProductDetailsAsync(int accountId);

        Task<List<JournalVoucher>> GetVouchersWithModifiedEntriesAsync();
    }
}