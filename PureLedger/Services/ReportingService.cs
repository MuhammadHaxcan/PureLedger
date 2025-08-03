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
        private readonly ICommonApiService _commonApiService;

        public ReportingService(
            IJournalVoucherRepository journalVoucherRepository,
            IDeliveryChallanVoucherRepository deliveryChallanVoucherRepository,
            IProjectRepository projectRepository,
            IAccountRepository accountRepository,
            ICategoryRepository categoryRepository,
            ICommonApiService commonApiService)
        {
            _journalVoucherRepository = journalVoucherRepository;
            _deliveryChallanVoucherRepository = deliveryChallanVoucherRepository;
            _projectRepository = projectRepository;
            _accountRepository = accountRepository;
            _categoryRepository = categoryRepository;
            _commonApiService = commonApiService;
        }

        public async Task<JournalVoucherReportViewModel?> GetSingleDocReportAsync(int journalVoucherId, string voucherType)
        {
            return voucherType switch
            {
                "DC" => await GetDeliveryChallanReportAsync(journalVoucherId, voucherType),
                "INV" => await GetDeliveryChallanReportAsync(journalVoucherId, voucherType), // you must implement this method
                _ => await GetJournalVoucherReportAsync(journalVoucherId, voucherType)
            };
        }

        private async Task<JournalVoucherReportViewModel?> GetDeliveryChallanReportAsync(int journalVoucherId, string voucherType)
        {
            var dcVoucher = await _deliveryChallanVoucherRepository.GetByJournalVoucherIdAsync(journalVoucherId);
            if (dcVoucher?.JournalVoucher == null) return null;

            string locationNames = string.Join(", ",
            dcVoucher.Entries
                .Where(e => e.Location != null)
                .Select(e => e.Location.LocationName)
                .Distinct());

            var project = await _projectRepository.GetByIdAsync(dcVoucher.JournalVoucher.ProjectId);
            if (project == null) return null;

            var entries = GetProcessedEntries(dcVoucher.Entries);
            var customerInfo = GetCustomerAccountName(dcVoucher.JournalVoucher);

            var cartageEntry = await _commonApiService.GetCartageEntryByJournalVoucherIdAsync(dcVoucher.JournalVoucherId);

            var viewModel = CreateVoucherViewModel(dcVoucher.JournalVoucherId, voucherType, project.ProjectName, dcVoucher.DCDate, entries, customerInfo, !dcVoucher.JournalVoucher.IsCompleted, cartageEntry);
            viewModel.Adda = locationNames;

            return viewModel;
        }

        private async Task<JournalVoucherReportViewModel?> GetJournalVoucherReportAsync(int journalVoucherId, string voucherType)
        {
            var voucher = await _journalVoucherRepository.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null) return null;

            var project = await _projectRepository.GetByIdAsync(voucher.ProjectId);
            if (project == null) return null;

            var isPartial = voucherType == "DC" && !voucher.IsCompleted;
            var entries = GetJournalVoucherEntries(voucher.JournalEntries, isPartial);
            var customerInfo = GetCustomerAccountName(voucher);
            var typeLabel = isPartial ? "DC - Partial" : voucherType;

            var cartageEntry = await _commonApiService.GetCartageEntryByJournalVoucherIdAsync(voucher.JournalVoucherId);

            return CreateVoucherViewModel(voucher.JournalVoucherId, typeLabel, project.ProjectName, voucher.EntryDate,
                entries, customerInfo, isPartial, cartageEntry);
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
                PKG = 1,  // reteive the package quantity from helper function
                Discount = entry.Discount,
                Debit = entry.Debit,
                Credit = entry.Credit
            };
        }

        private string GetCustomerAccountName(JournalVoucher voucher)
        {
            var entry = voucher.JournalEntries.FirstOrDefault(e => e.Account != null && e.Account.AccountGroupId == 1);
            var account = entry?.Account;

            if (account == null) return "";

            var accountData = account.AccountData;

            string name = account.AccountName ?? "";
            string phone = accountData?.PhoneNumber ?? "";
            string address = accountData?.Address ?? "";
            string city = accountData?.City ?? "";

            return $"{name} ({phone}, {address}, {city})";
        }
        

        private JournalVoucherReportViewModel CreateVoucherViewModel(
            int jvId, string type, string projectName, DateOnly date,
            List<JournalVoucherEntryReportModel> entries, string accountName, bool isPartial, JournalEntry? cartageEntry)
        {
                var (name, phone, address, city) = ParseCustomerInfo(accountName);
            

            return new JournalVoucherReportViewModel
            {
                JvId = jvId,
                VoucherType = type,
                ProjectName = projectName,
                BookerName = "",
                CreatedDate = date,
                Builty = cartageEntry?.Credit != null? cartageEntry?.Credit : 0,

                //Customer Info
                CustomerAccountName = name,
                CustomerAddress = address,
                CustomerCity= city,
                CustomerPhone = phone,

                Entries = entries,
                TotalQuantity = entries.Sum(e => e.Quantity),
                TotalActualAmount = entries.Sum(e => e.LineTotal),
                TotalAmount = entries.Sum(e => e.Credit),
                
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
                    v.IsCompleted && // Only include processed vouchers
                    (!projectId.HasValue || v.ProjectId == projectId.Value) &&
                    ((v.CreditableDate) <= request.ReferenceDate)
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

        public (string Name, string Phone, string Address, string City) ParseCustomerInfo(string customerInfo)
        {
            if (string.IsNullOrWhiteSpace(customerInfo)) return ("", "", "", "");

            // Split name and the rest inside parentheses
            int bracketIndex = customerInfo.IndexOf('(');
            if (bracketIndex == -1 || !customerInfo.EndsWith(")"))
                return (customerInfo, "", "", "");

            string name = customerInfo.Substring(0, bracketIndex).Trim();
            string inside = customerInfo.Substring(bracketIndex + 1, customerInfo.Length - bracketIndex - 2); // removes ( and )

            string[] parts = inside.Split(',');

            string phone = parts.Length > 0 ? parts[0].Trim() : "";
            string address = parts.Length > 1 ? parts[1].Trim() : "";
            string city = parts.Length > 2 ? parts[2].Trim() : "";

            return (name, phone, address, city);
        }


        public async Task<List<AccountReportViewModel>> GetAccountReportAsync(AccountReportRequest request)
        {
            // First, validate that we have at least one filter
            if (string.IsNullOrWhiteSpace(request.CategoryId) && 
                string.IsNullOrWhiteSpace(request.ProjectId) &&
                string.IsNullOrWhiteSpace(request.AccountId))
            {
                return new List<AccountReportViewModel>();
            }

            // Parse the filter values
            int? categoryId = null;
            if (!string.IsNullOrWhiteSpace(request.CategoryId) && 
                int.TryParse(request.CategoryId, out int cid))
            {
                categoryId = cid;
            }

            int? projectId = null;
            if (!string.IsNullOrWhiteSpace(request.ProjectId) && 
                int.TryParse(request.ProjectId, out int pid))
            {
                projectId = pid;
            }

            int? accountId = null;
            if (!string.IsNullOrWhiteSpace(request.AccountId) && 
                int.TryParse(request.AccountId, out int aid))
            {
                accountId = aid;
            }

            // Step 1: Get relevant accounts first - this is a key improvement
            List<Account> accounts;
            if (accountId.HasValue)
            {
                // If specific account is requested, get just that one
                var account = await _accountRepository.GetByIdAsync(accountId.Value);
                accounts = account != null ? new List<Account> { account } : new List<Account>();
            }
            else if (categoryId.HasValue)
            {
                // Get accounts by category
                accounts = await _accountRepository.GetByCategoryIdAsync(categoryId.Value);
            }
            else
            {
                // If no category or account specified, we must have a project
                // Let's get all accounts that have entries in this project
                var vouchers = await _journalVoucherRepository.GetAllWithEntriesAsync();
                var projectVouchers = vouchers
                    .Where(v => v.IsCompleted && v.ProjectId == projectId.Value) // Only processed vouchers
                    .ToList();
                
                // Extract unique account IDs from the entries
                var accountIds = projectVouchers
                    .SelectMany(v => v.JournalEntries.Select(e => e.AccountId))
                    .Distinct()
                    .ToList();
                
                // Get all those accounts
                accounts = new List<Account>();
                foreach (var id in accountIds)
                {
                    var account = await _accountRepository.GetByIdAsync(id);
                    if (account != null) accounts.Add(account);
                }
            }

            // Step 2: Now get all relevant vouchers
            var allVouchers = await _journalVoucherRepository.GetAllWithEntriesAsync();
            var filteredVouchers = allVouchers
                .Where(v => v.IsCompleted && (!projectId.HasValue || v.ProjectId == projectId.Value))
                .ToList();

            // Step 3: Process each account individually
            var result = new List<AccountReportViewModel>();
            foreach (var account in accounts)
            {
                // Skip if we're filtering by category and this account doesn't match
                if (categoryId.HasValue && account.CategoryId != categoryId.Value)
                    continue;

                var accountEntries = filteredVouchers
                    .SelectMany(v => v.JournalEntries
                        .Where(e => e.AccountId == account.AccountId))
                    .ToList();

                // Additional category filter on entries if needed
                if (categoryId.HasValue)
                    accountEntries = accountEntries.Where(e => e.CategoryId == categoryId.Value).ToList();

                // If no entries, skip this account
                if (accountEntries.Count == 0)
                    continue;

                decimal totalDebit = accountEntries.Sum(e => e.Debit);
                decimal totalCredit = accountEntries.Sum(e => e.Credit);

                result.Add(new AccountReportViewModel
                {
                    AccountId = account.AccountId.ToString(CultureInfo.InvariantCulture),
                    AccountName = account.AccountName ?? "",
                    CategoryName = account.Category?.CategoryName ?? "",
                    TotalDebit = totalDebit,
                    TotalCredit = totalCredit,
                    NetBalance = totalDebit - totalCredit
                });
            }

            return result;
        }

        public async Task<decimal> GetAccountBalanceUpToVoucherAsync(int journalVoucherId)
        {
            // Get the voucher with entries
            var voucher = await _journalVoucherRepository.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null)
                return 0m;

            var customerEntry = voucher.JournalEntries.FirstOrDefault(e => e.Account != null && e.Account.AccountGroupId == 1);
            if (customerEntry == null || customerEntry.Account == null)
                return 0m;

            int customerAccountId = customerEntry.AccountId;

            // Get all completed vouchers up to and including this one
            var allVouchers = await _journalVoucherRepository.GetAllWithEntriesAsync();
            var relevantVouchers = allVouchers
                .Where(v => v.IsCompleted && v.JournalVoucherId <= journalVoucherId)
                .ToList();

            // Get all entries for this account in those vouchers
            var entries = relevantVouchers
                .SelectMany(v => v.JournalEntries)
                .Where(e => e.AccountId == customerAccountId)
                .ToList();

            decimal totalDebit = entries.Sum(e => e.Debit);
            decimal totalCredit = entries.Sum(e => e.Credit);

            return totalDebit - totalCredit;
        }
    }
}
