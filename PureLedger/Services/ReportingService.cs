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
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null) return null;

            var project = await _unitOfWork.Projects.GetByIdAsync(voucher.ProjectId);
            if (project == null) return null;

            if (voucherType == "DC")
            {
                var dcVoucher = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(journalVoucherId);

                if (dcVoucher == null)
                {
                    // DC not created yet for this JV
                    return null;
                }

                var isPartial = !voucher.IsProcessed;
                var filteredEntries = voucher.JournalEntries
                    .Where(e => e.IsProcessed) // Show only processed for partial
                    .Select(e => new JournalVoucherEntryReportModel
                    {
                        AccountName = e.Account?.AccountName ?? "",
                        Description = e.Description ?? "",
                        Quantity = e.Quantity,
                        Rate = e.Rate,
                        Debit = e.Debit,
                        Credit = e.Credit
                    }).ToList();

                // If normal DC, use all entries
                if (!isPartial)
                {
                    filteredEntries = voucher.JournalEntries
                        .Select(e => new JournalVoucherEntryReportModel
                        {
                            AccountName = e.Account?.AccountName ?? "",
                            Description = e.Description ?? "",
                            Quantity = e.Quantity,
                            Rate = e.Rate,
                            Debit = e.Debit,
                            Credit = e.Credit
                        }).ToList();
                }

                var report = new JournalVoucherReportViewModel
                {
                    JvId = voucher.JvId,
                    VoucherType = voucherType,
                    ProjectName = project.ProjectName,
                    BookerName = "", // Add if you store it
                    CreatedDate = voucher.EntryDate,
                    Entries = filteredEntries,
                    TotalQuantity = filteredEntries.Sum(e => e.Quantity),
                    TotalActualAmount = filteredEntries.Sum(e => e.Quantity * e.Rate),
                    TotalAmount = filteredEntries.Sum(e => e.Debit),
                    AccountName = "",
                    AuthorizedBy = "",
                    ReceivedBy = "",
                    // Add a new flag or message if you want to show a partial DC popup
                };

                if (isPartial)
                {
                    report.VoucherType += " - Partial"; // Optional label
                }

                return report;
            }
            else // Normal JV print
            {
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
                    CreatedDate = voucher.EntryDate,
                    Entries = entries,
                    TotalQuantity = entries.Sum(e => e.Quantity),
                    TotalActualAmount = entries.Sum(e => e.Quantity * e.Rate),
                    TotalAmount = entries.Sum(e => e.Debit),
                    AccountName = "",
                    AuthorizedBy = "",
                    ReceivedBy = ""
                };

                return report;
            }
        }


    }
}
