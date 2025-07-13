using Microsoft.Identity.Client;
using PureLedger.Models;
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
            if (voucherType == "DC")
            {
                // Get Delivery Challan by JournalVoucherId
                var dcVoucher = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(journalVoucherId);
                if (dcVoucher == null) return null;

                var project = await _unitOfWork.Projects.GetByIdAsync(dcVoucher.JournalVoucher.ProjectId);
                if (project == null) return null;
                var jv = dcVoucher.JournalVoucher;
                if (jv == null) return null;

                bool isPartialDC = !jv.IsProcessed;

                var processedEntries = dcVoucher.DeliveryChallanEntries
                    .Where(e => e.JournalEntry != null && e.JournalEntry.IsProcessed)
                    .Select(e => new JournalVoucherEntryReportModel
                    {
                        AccountName = e.JournalEntry.Account?.AccountName ?? "",
                        Description = e.JournalEntry.Description ?? "",
                        Quantity = e.JournalEntry.Quantity,
                        Rate = e.JournalEntry.Rate,
                        Debit = e.JournalEntry.Debit,
                        Credit = e.JournalEntry.Credit
                    }).ToList();

                // 🟡 Find the customer account where Quantity == 0
                var customerEntry = dcVoucher.JournalVoucher.JournalEntries.FirstOrDefault(e => e.Quantity == 0);
                var customerAccountName = customerEntry?.Account?.AccountName ?? "";

                return new JournalVoucherReportViewModel
                {
                    JvId = dcVoucher.JournalVoucherId,
                    VoucherType = voucherType,
                    ProjectName = project.ProjectName,
                    BookerName = "", // Set as needed
                    CreatedDate = dcVoucher.DCDate,
                    Entries = processedEntries,
                    TotalQuantity = processedEntries.Sum(e => e.Quantity),
                    TotalActualAmount = processedEntries.Sum(e => e.LineTotal),
                    TotalAmount = processedEntries.Sum(e => e.Debit), // Optional
                    AccountName = customerAccountName, // ✅ Set this!
                    AuthorizedBy = "", // Set if needed
                    ReceivedBy = "",    // Set if needed
                    IsPartial = isPartialDC
                };
            }

            // Normal Journal Voucher logic...
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null) return null;

            var jvProject = await _unitOfWork.Projects.GetByIdAsync(voucher.ProjectId);
            if (jvProject == null) return null;

            var entries = voucher.JournalEntries.Select(e => new JournalVoucherEntryReportModel
            {
                AccountName = e.Account?.AccountName ?? "",
                Description = e.Description ?? "",
                Quantity = e.Quantity,
                Rate = e.Rate,
                Debit = e.Debit,
                Credit = e.Credit
            }).ToList();

            var customerAccount = voucher.JournalEntries.FirstOrDefault(e => e.Quantity == 0);
            var customerName = customerAccount?.Account?.AccountName ?? "";

            return new JournalVoucherReportViewModel
            {
                JvId = voucher.JvId,
                VoucherType = voucherType,
                ProjectName = jvProject.ProjectName,
                BookerName = "", // Set as needed
                CreatedDate = voucher.EntryDate,
                Entries = entries,
                TotalQuantity = entries.Sum(e => e.Quantity),
                TotalActualAmount = entries.Sum(e => e.LineTotal),
                TotalAmount = entries.Sum(e => e.Debit),
                AccountName = "", // Set if needed
                AuthorizedBy = "", // Set if needed
                ReceivedBy = "",    // Set if needed
                IsPartial = false
            };
        }



    }
}
