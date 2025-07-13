// Services/ReportingService.cs
using Microsoft.EntityFrameworkCore;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;
using PureLedger.ViewModels;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace PureLedger.Services
{
    public class ReportingService : IReportingService
    {
        private readonly IJournalVoucherRepository _journalVoucherRepository;
        private readonly IDeliveryChallanVoucherRepository _deliveryChallanVoucherRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly ICategoryRepository _categoryRepository;

        public ReportingService(
            IJournalVoucherRepository journalVoucherRepository,
            IDeliveryChallanVoucherRepository deliveryChallanVoucherRepository,
            IProjectRepository projectRepository,
            IAccountRepository accountRepository,
            ICategoryRepository categoryRepository)
        {
            _journalVoucherRepository = journalVoucherRepository;
            _deliveryChallanVoucherRepository = deliveryChallanVoucherRepository;
            _projectRepository = projectRepository;
            _accountRepository = accountRepository;
            _categoryRepository = categoryRepository;
        }

        public async Task<JournalVoucherReportViewModel?> GetSingleDocReportAsync(int journalVoucherId, string voucherType)
        {
            return voucherType == "DC"
                ? await GetDeliveryChallanReportAsync(journalVoucherId)
                : await GetJournalVoucherReportAsync(journalVoucherId, voucherType);
        }

        private async Task<JournalVoucherReportViewModel?> GetDeliveryChallanReportAsync(int journalVoucherId)
        {
            var dcVoucher = await _deliveryChallanVoucherRepository.GetByJournalVoucherIdAsync(journalVoucherId);
            if (dcVoucher?.JournalVoucher == null) return null;

            var project = await _projectRepository.GetByIdAsync(dcVoucher.JournalVoucher.ProjectId);
            if (project == null) return null;

            var entries = GetProcessedEntries(dcVoucher.DeliveryChallanEntries);
            var customerAccountName = GetCustomerAccountName(dcVoucher.JournalVoucher);

            return CreateVoucherViewModel(dcVoucher.JournalVoucherId, "DC", project.ProjectName, dcVoucher.DCDate,
                entries, customerAccountName, !dcVoucher.JournalVoucher.IsProcessed);
        }

        private async Task<JournalVoucherReportViewModel?> GetJournalVoucherReportAsync(int journalVoucherId, string voucherType)
        {
            var voucher = await _journalVoucherRepository.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null) return null;

            var project = await _projectRepository.GetByIdAsync(voucher.ProjectId);
            if (project == null) return null;

            var isPartial = voucherType == "DC" && !voucher.IsProcessed;
            var entries = GetJournalVoucherEntries(voucher.JournalEntries, isPartial);
            var customerAccountName = GetCustomerAccountName(voucher);
            var typeLabel = isPartial ? "DC - Partial" : voucherType;

            return CreateVoucherViewModel(voucher.JvId, typeLabel, project.ProjectName, voucher.EntryDate,
                entries, customerAccountName, isPartial);
        }

        private List<JournalVoucherEntryReportModel> GetProcessedEntries(IEnumerable<DeliveryChallanEntry> entries)
        {
            return entries
                .Where(e => e.JournalEntry?.IsProcessed == true)
                .Select(e => MapToEntryReportModel(e.JournalEntry!))
                .ToList();
        }

        private List<JournalVoucherEntryReportModel> GetJournalVoucherEntries(IEnumerable<JournalEntry> entries, bool isPartial)
        {
            return entries
                .Where(e => !isPartial || e.IsProcessed)
                .Select(MapToEntryReportModel)
                .ToList();
        }

        private JournalVoucherEntryReportModel MapToEntryReportModel(JournalEntry entry)
        {
            return new JournalVoucherEntryReportModel
            {
                AccountName = entry.Account?.AccountName ?? "",
                Description = entry.Description ?? "",
                Quantity = entry.Quantity,
                Rate = entry.Rate,
                Debit = entry.Debit,
                Credit = entry.Credit
            };
        }

        private string GetCustomerAccountName(JournalVoucher voucher)
        {
            return voucher.JournalEntries.FirstOrDefault(e => e.Quantity == 0)?.Account?.AccountName ?? "";
        }

        private JournalVoucherReportViewModel CreateVoucherViewModel(
            int jvId, string type, string projectName, DateOnly date,
            List<JournalVoucherEntryReportModel> entries, string accountName, bool isPartial)
        {
            return new JournalVoucherReportViewModel
            {
                JvId = jvId,
                VoucherType = type,
                ProjectName = projectName,
                BookerName = "",
                CreatedDate = date,
                Entries = entries,
                TotalQuantity = entries.Sum(e => e.Quantity),
                TotalActualAmount = entries.Sum(e => e.LineTotal),
                TotalAmount = entries.Sum(e => e.Debit),
                AccountName = accountName,
                AuthorizedBy = "",
                ReceivedBy = "",
                IsPartial = isPartial
            };
        }

        public async Task<List<AgingReportViewModel>> GetAgingReportAsync(AgingReportRequest request)
        {
            if (!int.TryParse(request.CategoryId, out int categoryId))
                return new List<AgingReportViewModel>();

            int? projectId = null;
            if (!string.IsNullOrWhiteSpace(request.ProjectId) && int.TryParse(request.ProjectId, out int pid))
                projectId = pid;

            // 1. Fetch all accounts for the selected category
            var accounts = await _accountRepository.GetByCategoryIdAsync(categoryId);

            var result = new List<AgingReportViewModel>();

            // 2. Fetch all vouchers (with entries) for the selected project (if any)
            var vouchers = await _journalVoucherRepository.GetAllWithEntriesAsync();
            var filteredVouchers = vouchers
                .Where(v =>
                    (!projectId.HasValue || v.ProjectId == projectId.Value) &&
                    // Use CreditableDate if present, otherwise EntryDate
                    ((v.CreditableDate ?? v.EntryDate) <= request.ReferenceDate)
                )
                .ToList();

            // 3. For each account, filter entries and calculate totals
            foreach (var account in accounts)
            {
                var entries = filteredVouchers
                    .SelectMany(v => v.JournalEntries
                        .Where(e => e.AccountId == account.AccountId && e.CategoryId == categoryId))
                    .ToList();

                if (entries.Count == 0)
                    continue;

                decimal totalDebit = entries.Sum(e => e.Debit);
                decimal totalCredit = entries.Sum(e => e.Credit);

                result.Add(new AgingReportViewModel
                {
                    AccountId = account.AccountId.ToString(CultureInfo.InvariantCulture),
                    AccountName = account.AccountName ?? "",
                    TotalDebit = totalDebit,
                    TotalCredit = totalCredit,
                    NetBalance = totalDebit - totalCredit
                });
            }

            return result;
        }

        public async Task<List<Project>> GetAllProjectsAsync()
        {
            // Assuming your IProjectRepository has GetAllAsync()
            return await _projectRepository.GetAllAsync();
        }

        public async Task<List<Category>> GetAllCategoriesAsync()
        {
            // Use the provided method from ICategoryRepository
            return await _categoryRepository.GetAllAsync();
        }
    }
}
