// Services/ReportingService.cs
using Microsoft.EntityFrameworkCore;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;
using PureLedger.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
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
            return voucherType switch
            {
                "DC" => await GetDeliveryChallanReportAsync(journalVoucherId, voucherType),
                "INV" => await GetDeliveryChallanReportAsync(journalVoucherId, voucherType), // you must implement this method
                _ => await GetJournalVoucherReportAsync(journalVoucherId, voucherType)
            };
        }

        private async Task<JournalVoucherReportViewModel?> GetDeliveryChallanReportAsync(int journalVoucherId, string voucherType)
        {
            var dcVoucher = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(journalVoucherId);
            if (dcVoucher?.JournalVoucher == null) return null;

            string locationNames = string.Join(", ",
                dcVoucher.Entries
                    .Where(e => e.Location != null)
                    .Select(e => e.Location.LocationName)
                    .Distinct());

            var project = await _unitOfWork.Projects.GetByIdAsync(dcVoucher.JournalVoucher.ProjectId);
            if (project == null) return null;

            var entries = GetProcessedEntries(dcVoucher.Entries);
            var customerInfo = GetCustomerAccountName(dcVoucher.JournalVoucher);

            var cartageEntries = await _unitOfWork.JournalEntries.GetCartageEntriesByJournalVoucherIdAsync(dcVoucher.JournalVoucherId);
            var cartageEntry = cartageEntries.FirstOrDefault();

            var viewModel = CreateVoucherViewModel(dcVoucher.JournalVoucherId, voucherType, project.ProjectName, dcVoucher.DCDate, entries, customerInfo, !dcVoucher.JournalVoucher.IsCompleted, cartageEntry);
            viewModel.Adda = locationNames;

            return viewModel;
        }

        private async Task<JournalVoucherReportViewModel?> GetJournalVoucherReportAsync(int journalVoucherId, string voucherType)
        {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null) return null;

            var project = await _unitOfWork.Projects.GetByIdAsync(voucher.ProjectId);
            if (project == null) return null;

            var isPartial = voucherType == "DC" && !voucher.IsCompleted;
            var entries = GetJournalVoucherEntries(voucher.JournalEntries, isPartial);
            var customerInfo = GetCustomerAccountName(voucher);
            var typeLabel = isPartial ? "DC - Partial" : voucherType;

            var cartageEntries = await _unitOfWork.JournalEntries.GetCartageEntriesByJournalVoucherIdAsync(voucher.JournalVoucherId);
            var cartageEntry = cartageEntries.FirstOrDefault();

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
                PKG = 1,  // Default package quantity
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
                Builty = cartageEntry?.Credit ?? 0,
                CustomerAccountName = name,
                CustomerAddress = address,
                CustomerCity = city,
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

            // Get filtered accounts with entries in a more efficient way
            var accounts = await _unitOfWork.Accounts.GetByCategoryIdAsync(categoryId);
            
            if (accounts.Count == 0)
                return new List<AgingReportViewModel>();

            var accountIds = accounts.Select(a => a.AccountId).ToList();
            
            // Using a transaction for consistent read
            using var transaction = await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            
            try
            {
                // Get vouchers that match our criteria directly from the database
                var filteredEntries = await _unitOfWork.JournalEntries.GetEntriesByCriteriaAsync(
                    accountIds, 
                    categoryId,
                    projectId, 
                    request.ReferenceDate);

                var result = new List<AgingReportViewModel>();
                
                // Process account totals
                foreach (var account in accounts)
                {
                    var accountEntries = filteredEntries
                        .Where(e => e.AccountId == account.AccountId)
                        .ToList();

                    if (accountEntries.Count == 0)
                        continue;

                    decimal totalDebit = accountEntries.Sum(e => e.Debit);
                    decimal totalCredit = accountEntries.Sum(e => e.Credit);

                    result.Add(new AgingReportViewModel
                    {
                        AccountId = account.AccountId.ToString(CultureInfo.InvariantCulture),
                        AccountName = account.AccountName ?? "",
                        TotalDebit = totalDebit,
                        TotalCredit = totalCredit,
                        NetBalance = totalDebit - totalCredit
                    });
                }

                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<Project>> GetAllProjectsAsync()
        {
            return await _unitOfWork.Projects.GetAllAsync();
        }

        public async Task<List<Project>> GetAllProjectsForUserAsync(int userId)
        {
            return await _unitOfWork.Projects.GetAllForUserAsync(userId);
        }

        public async Task<List<Category>> GetAllCategoriesAsync()
        {
            return await _unitOfWork.Categories.GetAllAsync();
        }

        public (string Name, string Phone, string Address, string City) ParseCustomerInfo(string customerInfo)
        {
            if (string.IsNullOrWhiteSpace(customerInfo)) return ("", "", "", "");

            int bracketIndex = customerInfo.IndexOf('(');
            if (bracketIndex == -1 || !customerInfo.EndsWith(")"))
                return (customerInfo, "", "", "");

            string name = customerInfo.Substring(0, bracketIndex).Trim();
            string inside = customerInfo.Substring(bracketIndex + 1, customerInfo.Length - bracketIndex - 2);

            string[] parts = inside.Split(',');

            string phone = parts.Length > 0 ? parts[0].Trim() : "";
            string address = parts.Length > 1 ? parts[1].Trim() : "";
            string city = parts.Length > 2 ? parts[2].Trim() : "";

            return (name, phone, address, city);
        }

        public async Task<List<AccountReportViewModel>> GetAccountReportAsync(AccountReportRequest request)
        {
            // Validate that we have at least one filter
            if (string.IsNullOrWhiteSpace(request.CategoryId) && 
                string.IsNullOrWhiteSpace(request.ProjectId) &&
                string.IsNullOrWhiteSpace(request.AccountId))
            {
                return new List<AccountReportViewModel>();
            }

            // Parse filter values
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

            // Use a transaction for consistent reads
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            
            try
            {
                // Get accounts based on criteria (optimized database query)
                List<Account> accounts;
                if (accountId.HasValue)
                {
                    var account = await _unitOfWork.Accounts.GetByIdWithCategoryAsync(accountId.Value);
                    accounts = account != null ? new List<Account> { account } : new List<Account>();
                }
                else if (categoryId.HasValue)
                {
                    accounts = await _unitOfWork.Accounts.GetByCategoryIdWithCategoryAsync(categoryId.Value);
                }
                else if (projectId.HasValue)
                {
                    // Optimized to fetch only account IDs with entries in the project
                    accounts = await _unitOfWork.Accounts.GetAccountsWithEntriesInProjectAsync(projectId.Value);
                }
                else
                {
                    return new List<AccountReportViewModel>();
                }

                if (!accounts.Any())
                {
                    await transaction.CommitAsync();
                    return new List<AccountReportViewModel>();
                }

                // Get all account IDs for filtering
                var accountIds = accounts.Select(a => a.AccountId).ToList();

                // Get relevant journal entries based on criteria (filtered at database level)
                var journalEntries = await _unitOfWork.JournalEntries.GetJournalEntriesByFiltersAsync(
                    accountIds, categoryId, projectId);

                // Group entries by account and calculate totals
                var accountTotals = journalEntries
                    .GroupBy(e => e.AccountId)
                    .ToDictionary(
                        g => g.Key,
                        g => new {
                            TotalDebit = g.Sum(e => e.Debit),
                            TotalCredit = g.Sum(e => e.Credit)
                        });

                // Build report view models
                var result = new List<AccountReportViewModel>();
                foreach (var account in accounts)
                {
                    // Skip if no entries
                    if (!accountTotals.ContainsKey(account.AccountId))
                        continue;

                    var totals = accountTotals[account.AccountId];
                    
                    result.Add(new AccountReportViewModel
                    {
                        AccountId = account.AccountId.ToString(CultureInfo.InvariantCulture),
                        AccountName = account.AccountName ?? "",
                        CategoryName = account.Category?.CategoryName ?? "",
                        TotalDebit = totals.TotalDebit,
                        TotalCredit = totals.TotalCredit,
                        NetBalance = totals.TotalDebit - totals.TotalCredit
                    });
                }

                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<decimal> GetAccountBalanceUpToVoucherAsync(int journalVoucherId)
        {
            // Get the voucher with entries to find the customer account
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null)
                return 0m;

            var customerEntry = voucher.JournalEntries.FirstOrDefault(e => e.Account != null && e.Account.AccountGroupId == 1);
            if (customerEntry == null || customerEntry.Account == null)
                return 0m;

            int customerAccountId = customerEntry.AccountId;

            // Efficient direct query to calculate balance up to specific voucher ID
            return await _unitOfWork.JournalEntries.GetAccountBalanceUpToVoucherAsync(
                customerAccountId, journalVoucherId);
        }

        public async Task<MasterReportViewModel> GetMasterReportAsync(MasterReportRequest request)
        {
            // Initialize result
            var result = new MasterReportViewModel();

            // Normalize dates
            DateOnly startDate = request.StartDate ?? DateOnly.FromDateTime(DateTime.Today);
            DateOnly endDate = request.EndDate ?? DateOnly.FromDateTime(DateTime.Today);

            // Parse filter parameters
            int? projectId = null;
            if (!string.IsNullOrWhiteSpace(request.ProjectId) && int.TryParse(request.ProjectId, out int pid))
                projectId = pid;

            int? categoryId = null;
            if (!string.IsNullOrWhiteSpace(request.CategoryId) && int.TryParse(request.CategoryId, out int cid))
                categoryId = cid;

            int? accountId = null;
            if (!string.IsNullOrWhiteSpace(request.AccountId) && int.TryParse(request.AccountId, out int aid))
                accountId = aid;

            // Use a transaction for consistent read
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            
            try
            {
                // Get filtered entries directly from database with all required data
                var entries = await _unitOfWork.JournalEntries.GetEntriesForMasterReportAsync(
                    startDate, endDate, projectId, categoryId, accountId);

                // Process entries into view models with running balance
                result.Entries = new List<MasterReportEntryViewModel>();
                decimal runningBalance = 0;

                foreach (var entry in entries)
                {
                    // Update running balance
                    runningBalance += entry.Debit - entry.Credit;

                    // Format ID
                    string formattedId = $"{entry.JournalVoucher.ProjectId}-{entry.CategoryId}-{entry.AccountId}";

                    // Add entry to results
                    result.Entries.Add(new MasterReportEntryViewModel
                    {
                        Id = entry.JournalEntryId,
                        FormattedId = formattedId,
                        JournalVoucherId = entry.JournalVoucherId,
                        TransactionDate = GetEffectiveDate(entry.JournalVoucher),
                        Description = entry.Description,
                        AccountName = entry.Account?.AccountName ?? string.Empty,
                        AccountId = entry.AccountId,
                        Debit = entry.Debit,
                        Credit = entry.Credit,
                        Balance = runningBalance,
                        ProjectName = entry.JournalVoucher.Project?.ProjectName ?? string.Empty,
                        ProjectId = entry.JournalVoucher.ProjectId,
                        CategoryName = entry.Category?.CategoryName ?? string.Empty,
                        CategoryId = entry.CategoryId
                    });
                }

                // Calculate totals
                result.TotalDebit = result.Entries.Sum(e => e.Debit);
                result.TotalCredit = result.Entries.Sum(e => e.Credit);
                result.NetBalance = result.TotalDebit - result.TotalCredit;

                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private DateOnly GetEffectiveDate(JournalVoucher voucher)
        {
            // If voucher has a creditable date, use it
            if (voucher?.CreditableDate != null)
                return voucher.CreditableDate;
            
            // Otherwise fall back to entry date
            return voucher.EntryDate;
        }

        public async Task<IEnumerable<Account>> GetAccountsByCategoryAsync(int categoryId)
        {
            try
            {
                return await _unitOfWork.Accounts.GetByCategoryIdAsync(categoryId);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving accounts for category ID {categoryId}: {ex.Message}", ex);
            }
        }

        // Implement this method in ReportingService class
        public async Task<IEnumerable<Category>> GetCategoriesByProjectAsync(int projectId)
        {
            // Use _unitOfWork to get categories by project ID directly
            var categories = await _unitOfWork.Categories.GetByProjectIdAsync(projectId);
            return categories;
        }

        // Add this new method to check if a voucher is a base voucher
        public async Task<(bool Exists, bool IsBaseVoucher)> ValidateVoucherAsync(int journalVoucherId, string type)
        {
            // First check if the voucher exists
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null)
            {
                return (false, false);
            }

            // For types other than "JV", check if we can get a valid report
            if (type != "JV")
            {
                // For DC and INV types, check if we can get a report
                var report = await GetSingleDocReportAsync(journalVoucherId, type);
                return (report != null, voucher.IsBaseVoucher);
            }

            // For JV type, just check if it's a base voucher
            return (true, voucher.IsBaseVoucher);
        }
    }
}
