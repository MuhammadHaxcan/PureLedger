using PureLedger.ViewModels;
using System.Threading.Tasks;

namespace PureLedger.Services
{
    public interface IReportingService
    {
        Task<JournalVoucherReportViewModel?> GetSingleDocReportAsync(int journalVoucherId, string voucherType);
    }
}
