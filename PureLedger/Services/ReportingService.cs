using PureLedger.Models;
using PureLedger.Repositories.Interfaces;
using PureLedger.ViewModels;
using System.Linq;
using System.Threading.Tasks;

namespace PureLedger.Services
{
    public class ReportingService : IReportingService
    {
        private readonly IJournalVoucherRepository _journalVoucherRepository;
        private readonly IDeliveryChallanVoucherRepository _deliveryChallanVoucherRepository;
        private readonly IProjectRepository _projectRepository;

        public ReportingService(
            IJournalVoucherRepository journalVoucherRepository,
            IDeliveryChallanVoucherRepository deliveryChallanVoucherRepository,
            IProjectRepository projectRepository)
        {
            _journalVoucherRepository = journalVoucherRepository;
            _deliveryChallanVoucherRepository = deliveryChallanVoucherRepository;
            _projectRepository = projectRepository;
        }

        public async Task<JournalVoucherReportViewModel?> GetSingleDocReportAsync(int journalVoucherId, string voucherType)
        {
            if (voucherType == "DC")
            {
                // Get Delivery Challan by JournalVoucherId
                var dcVoucher = await _deliveryChallanVoucherRepository.GetByJournalVoucherIdAsync(journalVoucherId);
                if (dcVoucher == null) return null;

                var project = await _projectRepository.GetByIdAsync(dcVoucher.JournalVoucher.ProjectId);
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

                // Find the customer account where Quantity == 0
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
                    TotalAmount = processedEntries.Sum(e => e.Debit),
                    AccountName = customerAccountName,
                    AuthorizedBy = "",
                    ReceivedBy = "",
                    IsPartial = isPartialDC
                };
            }

            // Normal Journal Voucher logic...
            var voucher = await _journalVoucherRepository.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null) return null;

            var jvProject = await _projectRepository.GetByIdAsync(voucher.ProjectId);
            if (jvProject == null) return null;

            if (voucherType == "DC")
            {
                var dcVoucher = await _deliveryChallanVoucherRepository.GetByJournalVoucherIdAsync(journalVoucherId);

                if (dcVoucher == null)
                {
                    // DC not created yet for this JV
                    return null;
                }

                var isPartial = !voucher.IsProcessed;
                var filteredEntries = voucher.JournalEntries
                    .Where(e => e.IsProcessed)
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
                    ProjectName = jvProject.ProjectName,
                    BookerName = "",
                    CreatedDate = voucher.EntryDate,
                    Entries = filteredEntries,
                    TotalQuantity = filteredEntries.Sum(e => e.Quantity),
                    TotalActualAmount = filteredEntries.Sum(e => e.Quantity * e.Rate),
                    TotalAmount = filteredEntries.Sum(e => e.Debit),
                    AccountName = "",
                    AuthorizedBy = "",
                    ReceivedBy = "",
                };

                if (isPartial)
                {
                    report.VoucherType += " - Partial";
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

                var customerAccount = voucher.JournalEntries.FirstOrDefault(e => e.Quantity == 0);
                var customerName = customerAccount?.Account?.AccountName ?? "";

                return new JournalVoucherReportViewModel
                {
                    JvId = voucher.JvId,
                    VoucherType = voucherType,
                    ProjectName = jvProject.ProjectName,
                    BookerName = "",
                    CreatedDate = voucher.EntryDate,
                    Entries = entries,
                    TotalQuantity = entries.Sum(e => e.Quantity),
                    TotalActualAmount = entries.Sum(e => e.LineTotal),
                    TotalAmount = entries.Sum(e => e.Debit),
                    AccountName = "",
                    AuthorizedBy = "",
                    ReceivedBy = "",
                    IsPartial = false
                };
            }
        }
    }
}