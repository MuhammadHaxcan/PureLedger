using Microsoft.Identity.Client;
using PureLedger.Repositories.Interfaces;
using PureLedger.ViewModels;
using System.Linq;
using System.Threading.Tasks;

namespace PureLedger.Services
{
    public class ReportingService : IReportingService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ReportingService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<JournalVoucherReportViewModel?> GetSingleDocReportAsync(int journalVoucherId, string voucherType)
        {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null) return null;

            var project = await _unitOfWork.Projects.GetByIdAsync(voucher.ProjectId);
            if (project == null) return null;

            var entries = voucher.JournalEntries.Select(e => new JournalVoucherEntryReportModel
            {
                AccountName = e.Account?.AccountName ?? "",
                Description = e.Description ?? "",
                Quantity = e.Quantity,
                Rate = e.Rate,
                Debit = e.Debit,
                Credit = e.Credit
            }).ToList();

            var report = new JournalVoucherReportViewModel
            {
                JvId = voucher.JvId,
                VoucherType = voucherType,
                ProjectName = project.ProjectName,
                BookerName = "",
                CreatedDate = voucher.EntryDate, // assuming this field exists
                Entries = entries,
                TotalQuantity = entries.Sum(e => e.Quantity),
                TotalActualAmount = entries.Sum(e => e.Quantity * e.Rate),
                TotalAmount = entries.Sum(e => e.Debit + e.Credit),
                AccountName = "", // placeholder – update as per actual requirement
                AuthorizedBy = "",            // add logic if needed
                ReceivedBy = ""               // add logic if needed
            };

            return report;
        }
    }
}
