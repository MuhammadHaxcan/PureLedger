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

        public async Task<JournalVoucherReportViewModel?> GetSingleDocReportAsync(int journalVoucherId, VoucherType voucherType)
        {
            return voucherType switch
            {
                VoucherType.DC => await GetDeliveryChallanReportAsync(journalVoucherId, voucherType),
                VoucherType.INV => await GetDeliveryChallanReportAsync(journalVoucherId, voucherType),
                _ => await GetJournalVoucherReportAsync(journalVoucherId, voucherType)
            };
        }

        private async Task<JournalVoucherReportViewModel?> GetDeliveryChallanReportAsync(int journalVoucherId, VoucherType voucherType)
        {
            var dcVoucher = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(journalVoucherId);
            if (dcVoucher?.JournalVoucher == null) return null;

            var project = await _unitOfWork.Projects.GetByIdAsync(dcVoucher.JournalVoucher.ProjectId);
            if (project == null) return null;

            string locationNames = GetLocationNames(dcVoucher.Entries);
            var processedEntries = GetProcessedEntries(dcVoucher.Entries);
            var customerInfo = GetCustomerAccountName(dcVoucher.JournalVoucher);

            var (forChargesAmount, forChargesEntries) = await GetForChargesDataAsync(journalVoucherId);
            var (cartageAmount, cartageEntries) = await GetCartageDataAsync(journalVoucherId);

            // Get cartage entry for builty information
            var cartageEntryList = await _unitOfWork.JournalEntries
                .GetCartageEntriesByJournalVoucherIdAsync(journalVoucherId);
            var cartageEntry = cartageEntryList.FirstOrDefault();
            
            var viewModel = new JournalVoucherReportViewModel
            {
                JvId = journalVoucherId,
                VoucherType = voucherType,
                ProjectName = project.ProjectName,
                CreatedDate = dcVoucher.DCDate,
                Entries = processedEntries,

                CustomerAccountName = ParseCustomerInfo(customerInfo).Name,
                CustomerPhone = ParseCustomerInfo(customerInfo).Phone,
                CustomerAddress = ParseCustomerInfo(customerInfo).Address,
                CustomerCity = ParseCustomerInfo(customerInfo).City,

                Adda = locationNames,
                ForChargesAmount = forChargesAmount,
                ForChargesEntries = forChargesEntries,
                CartageAmount = cartageAmount,
                CartageEntries = cartageEntries,
                
                // Use the cartage entry for Builty
                Builty = cartageEntry?.Credit != null ? (int)cartageEntry.Credit : 0
            };
            return viewModel;
        }

        private async Task<JournalVoucherReportViewModel?> GetJournalVoucherReportAsync(int journalVoucherId, VoucherType voucherType)
        {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null) return null;

            var project = await _unitOfWork.Projects.GetByIdAsync(voucher.ProjectId);
            if (project == null) return null;

            var entries = GetJournalVoucherEntries(voucher.JournalEntries);
            var customerInfo = GetCustomerAccountName(voucher);

            var cartageEntries = await _unitOfWork.JournalEntries
                .GetCartageEntriesByJournalVoucherIdAsync(journalVoucherId);
            var cartageEntry = cartageEntries.FirstOrDefault();

            var customerDetails = ParseCustomerInfo(customerInfo);
            var viewModel = new JournalVoucherReportViewModel
            {
                JvId = journalVoucherId,
                VoucherType = voucherType,
                ProjectName = project.ProjectName,
                CreatedDate = voucher.EntryDate,
                Entries = entries,

                CustomerAccountName = customerDetails.Name,
                CustomerPhone = customerDetails.Phone,
                CustomerAddress = customerDetails.Address,
                CustomerCity = customerDetails.City,

                CartageEntry = cartageEntry,
                Builty = cartageEntry?.Credit ?? 0
            };

            return viewModel;
        }

        private string GetLocationNames(IEnumerable<DeliveryChallanEntry> entries)
        {
            return string.Join(", ",
                entries
                    .Where(e => e.Location != null)
                    .Select(e => e.Location.LocationName)
                    .Distinct());
        }

        private List<JournalVoucherEntryReportModel> GetProcessedEntries(IEnumerable<DeliveryChallanEntry> entries)
        {
            return entries
                .Where(e => e.JournalEntry?.IsProcessed == true)
                .Select(e => MapToEntryReportModel(e.JournalEntry!))
                .ToList();
        }

        private List<JournalVoucherEntryReportModel> GetJournalVoucherEntries(IEnumerable<JournalEntry> entries)
        {
            return entries
                .Select(MapToEntryReportModel)
                .ToList();
        }

        private JournalVoucherEntryReportModel MapToEntryReportModel(JournalEntry entry)
        {
            return new JournalVoucherEntryReportModel
            {
                AccountId = entry.AccountId,
                AccountName = entry.Account?.AccountName ?? "",
                Description = entry.Description ?? "",
                Quantity = entry.Quantity,
                Rate = entry.Rate,
                PKG = entry.Account?.ProductData?.Pkg ?? 1,
                Discount = (int)entry.DiscountPercent,
                Debit = entry.Debit,
                Credit = entry.Credit,
                Rbp = entry.Rbp ?? "Y"
            };
        }

        private string GetCustomerAccountName(JournalVoucher voucher)
        {
            var entry = voucher.JournalEntries
                .FirstOrDefault(e => e.Account != null && e.Account.AccountGroupId == 1);

            var account = entry?.Account;
            if (account == null) return "";

            var accountData = account.AccountData;

            string name = account.AccountName ?? "";
            string phone = accountData?.PhoneNumber ?? "";
            string address = accountData?.Address ?? "";
            string city = accountData?.City ?? "";

            return $"{name} ({phone}, {address}, {city})";
        }

        private async Task<(decimal TotalAmount, List<JournalVoucherEntryReportModel> Entries)> GetForChargesDataAsync(int journalVoucherId)
        {

            var references = await _unitOfWork.JournalVoucherReferences
                .GetByMainVoucherIdAndTypeAsync(journalVoucherId, "ForCharges");

            decimal totalAmount = 0;
            var forChargesEntries = new List<JournalVoucherEntryReportModel>();

            foreach (var reference in references)
            {
                var forVoucher = await _unitOfWork.JournalVouchers
                    .GetWithEntriesAsync(reference.ReferencedVoucherId);

                if (forVoucher == null) continue;

                var debitEntry = forVoucher.JournalEntries.FirstOrDefault(e => e.Debit > 0);
                if (debitEntry != null)
                {
                    totalAmount += debitEntry.Debit;
                    forChargesEntries.Add(MapToEntryReportModel(debitEntry));
                }
            }

            return (totalAmount, forChargesEntries);
        }

        private async Task<(decimal TotalAmount, List<JournalVoucherEntryReportModel> Entries)> GetCartageDataAsync(int journalVoucherId)
        {

            var references = await _unitOfWork.JournalVoucherReferences
                .GetByMainVoucherIdAndTypeAsync(journalVoucherId, "Cartage");

            decimal totalAmount = 0;
            var cartageEntries = new List<JournalVoucherEntryReportModel>();

            foreach (var reference in references)
            {
                var cartageVoucher = await _unitOfWork.JournalVouchers
                    .GetWithEntriesAsync(reference.ReferencedVoucherId);

                if (cartageVoucher == null) continue;

                var creditEntry = cartageVoucher.JournalEntries.FirstOrDefault(e => e.Credit > 0);
                if (creditEntry != null)
                {
                    totalAmount += creditEntry.Credit;
                    cartageEntries.Add(MapToEntryReportModel(creditEntry));
                }
            }

            return (totalAmount, cartageEntries);
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

        private DateOnly GetEffectiveDate(JournalVoucher voucher)
        {
            return voucher?.CreditableDate ?? voucher.EntryDate;
        }

        private async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                var result = await operation();
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #region Public API Methods

        public async Task<List<AgingReportViewModel>> GetAgingReportAsync(AgingReportRequest request)
        {
            if (!int.TryParse(request.CategoryId, out int categoryId))
                return new List<AgingReportViewModel>();

            int? projectId = ParseNullableInt(request.ProjectId);

            var accounts = await _unitOfWork.Accounts.GetByCategoryIdAsync(categoryId);
            if (accounts.Count == 0)
                return new List<AgingReportViewModel>();

            var accountIds = accounts.Select(a => a.AccountId).ToList();

            return await ExecuteInTransactionAsync(async () =>
            {

                var filteredEntries = await _unitOfWork.JournalEntries.GetEntriesByCriteriaAsync(
                    accountIds, categoryId, projectId, request.ReferenceDate);

                var result = new List<AgingReportViewModel>();

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

                return result;
            });
        }

        public async Task<List<AccountReportViewModel>> GetAccountReportAsync(AccountReportRequest request)
        {

            if (string.IsNullOrWhiteSpace(request.CategoryId) &&
                string.IsNullOrWhiteSpace(request.ProjectId) &&
                string.IsNullOrWhiteSpace(request.AccountId))
            {
                return new List<AccountReportViewModel>();
            }

            int? categoryId = ParseNullableInt(request.CategoryId);
            int? projectId = ParseNullableInt(request.ProjectId);
            int? accountId = ParseNullableInt(request.AccountId);

            return await ExecuteInTransactionAsync(async () =>
            {

                List<Account> accounts = await GetFilteredAccountsAsync(accountId, categoryId, projectId);

                if (!accounts.Any())
                    return new List<AccountReportViewModel>();

                var accountIds = accounts.Select(a => a.AccountId).ToList();

                var journalEntries = await _unitOfWork.JournalEntries.GetJournalEntriesByFiltersAsync(
                    accountIds, categoryId, projectId);

                var accountTotals = journalEntries
                    .GroupBy(e => e.AccountId)
                    .ToDictionary(
                        g => g.Key,
                        g => new {
                            TotalDebit = g.Sum(e => e.Debit),
                            TotalCredit = g.Sum(e => e.Credit)
                        });

                var result = new List<AccountReportViewModel>();
                foreach (var account in accounts)
                {

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

                return result;
            });
        }

        private async Task<List<Account>> GetFilteredAccountsAsync(int? accountId, int? categoryId, int? projectId)
        {
            if (accountId.HasValue)
            {
                var account = await _unitOfWork.Accounts.GetByIdWithCategoryAsync(accountId.Value);
                return account != null ? new List<Account> { account } : new List<Account>();
            }
            else if (categoryId.HasValue)
            {
                return await _unitOfWork.Accounts.GetByCategoryIdWithCategoryAsync(categoryId.Value);
            }
            else if (projectId.HasValue)
            {
                return await _unitOfWork.Accounts.GetAccountsWithEntriesInProjectAsync(projectId.Value);
            }

            return new List<Account>();
        }

        public async Task<decimal> GetAccountBalanceUpToVoucherAsync(int journalVoucherId)
        {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null)
                return 0m;

            // Find the customer entry in the current voucher
            var customerEntry = voucher.JournalEntries
                .FirstOrDefault(e => e.Account != null && e.Account.AccountGroupId == 1);

            if (customerEntry?.Account == null)
                return 0m;

            int customerAccountId = customerEntry.AccountId;

            // Get base balance up to this voucher
            var baseBalance = await _unitOfWork.JournalEntries
                .GetAccountBalanceUpToVoucherAsync(customerAccountId, journalVoucherId);

            // Get the direct impact of the customer entry in the current voucher
            decimal currentTransactionAmount = customerEntry.Debit - customerEntry.Credit;

            // Process FOR charges impact on customer account
            decimal forChargesImpact = 0;
            var forReferences = await _unitOfWork.JournalVoucherReferences
                .GetByMainVoucherIdAndTypeAsync(journalVoucherId, "ForCharges");

            foreach (var reference in forReferences)
            {
                // Get entries from this FOR voucher that affect the customer account
                var customerForEntries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(
                    reference.ReferencedVoucherId, null, customerAccountId);

                // Calculate the impact on customer account
                foreach (var entry in customerForEntries)
                {
                    forChargesImpact += entry.Debit - entry.Credit;
                }
            }

            decimal cartageImpact = 0;
            var cartageReferences = await _unitOfWork.JournalVoucherReferences
                .GetByMainVoucherIdAndTypeAsync(journalVoucherId, "Cartage");

            foreach (var reference in cartageReferences)
            {
                var customerCartageEntries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(
                    reference.ReferencedVoucherId, null, customerAccountId);

                foreach (var entry in customerCartageEntries)
                {
                    cartageImpact += entry.Debit - entry.Credit;
                }
            }

            // Calculate final balance including all impacts
            decimal finalBalance = baseBalance + currentTransactionAmount + forChargesImpact + cartageImpact;
            return finalBalance;
        }

        public async Task<MasterReportViewModel> GetMasterReportAsync(MasterReportRequest request)
        {
            var result = new MasterReportViewModel();

            DateOnly startDate = request.StartDate ?? DateOnly.FromDateTime(DateTime.Today);
            DateOnly endDate = request.EndDate ?? DateOnly.FromDateTime(DateTime.Today);

            int? projectId = ParseNullableInt(request.ProjectId);
            int? categoryId = ParseNullableInt(request.CategoryId);
            int? accountId = ParseNullableInt(request.AccountId);
            
            int page = Math.Max(0, request.Page); // Ensure page is not negative
            int pageSize = Math.Clamp(request.PageSize, 1, 1); // Allow any size from 1 to 500

            return await ExecuteInTransactionAsync(async () =>
            {
                var (entries, totalCount) = await _unitOfWork.JournalEntries.GetEntriesForMasterReportAsync(
                    startDate, endDate, projectId, categoryId, accountId, page, pageSize);

                result.Entries = new List<MasterReportEntryViewModel>();
                decimal runningBalance = 0;

                // If this is not the first page, we need to calculate the running balance up to this point
                if (page > 0)
                {
                    runningBalance = await _unitOfWork.JournalEntries.GetPreviousEntriesBalanceAsync(
                        startDate, endDate, projectId, categoryId, accountId, page, pageSize);
                    Console.WriteLine($"Previous entries balance: {runningBalance}");
                }

                foreach (var entry in entries)
                {
                    runningBalance += entry.Debit - entry.Credit;

                    string formattedId = $"{entry.JournalVoucher.ProjectId}-{entry.CategoryId}-{entry.AccountId}";

                    result.Entries.Add(new MasterReportEntryViewModel
                    {    
                        Id = entry.JournalEntryId,
                        JournalVoucherId = entry.JournalVoucherId,
                        FormattedId = formattedId,
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

                // Calculate totals for the current chunk only
                result.TotalDebit = result.Entries.Sum(e => e.Debit);
                result.TotalCredit = result.Entries.Sum(e => e.Credit);
                result.NetBalance = result.TotalDebit - result.TotalCredit;
                
                // Include pagination metadata
                result.TotalRecords = totalCount;
                result.Page = page;
                result.PageSize = pageSize;
                result.HasMoreData = (page + 1) * pageSize < totalCount;

                return result;
            });
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

        public async Task<IEnumerable<Category>> GetCategoriesByProjectAsync(int projectId)
        {
            return await _unitOfWork.Categories.GetByProjectIdAsync(projectId);
        }

        public async Task<(bool Exists, bool IsBaseVoucher)> ValidateVoucherAsync(int journalVoucherId, VoucherType type)
        {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null)
            {
                return (false, false);
            }

            if (type != VoucherType.JV)
            {

                var report = await GetSingleDocReportAsync(journalVoucherId, type);
                return (report != null, voucher.IsBaseVoucher);
            }

            return (true, voucher.IsBaseVoucher);
        }

        private int? ParseNullableInt(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (int.TryParse(value, out int result))
                return result;

            return null;
        }

        #endregion

        public async Task<decimal> GetAccountBalanceBeforeVoucherAsync(int journalVoucherId)
        {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null)
                return 0m;

            // Find the customer entry in the current voucher
            var customerEntry = voucher.JournalEntries
                .FirstOrDefault(e => e.Account != null && e.Account.AccountGroupId == 1);

            if (customerEntry?.Account == null)
                return 0m;

            int customerAccountId = customerEntry.AccountId;

            // Get base balance up to but not including this voucher
            var previousBalance = await _unitOfWork.JournalEntries
                .GetAccountBalanceUpToVoucherAsyncMinus1(customerAccountId, journalVoucherId - 1);
            
            return previousBalance;
        }

        public async Task<ProductReportViewModel> GetProductReportAsync(ProductReportRequest request)
        {
            // Validate inputs
            if (string.IsNullOrEmpty(request.ProjectId))
            {
                return new ProductReportViewModel();
            }

            int projectId = int.Parse(request.ProjectId);
            int? categoryId = !string.IsNullOrEmpty(request.CategoryId) ? int.Parse(request.CategoryId) : null;
            int? accountId = !string.IsNullOrEmpty(request.AccountId) ? int.Parse(request.AccountId) : null;
            int? locationId = !string.IsNullOrEmpty(request.LocationId) ? int.Parse(request.LocationId) : null;

            var startDate = request.StartDate ?? DateOnly.FromDateTime(DateTime.Today.AddMonths(-1));
            var endDate = request.EndDate ?? DateOnly.FromDateTime(DateTime.Today);

            int page = Math.Max(0, request.Page); // Ensure page is not negative
            int pageSize = Math.Clamp(request.PageSize, 1, 500); // Allow any size from 1 to 500

            return await ExecuteInTransactionAsync(async () =>
            {
                // Get paginated entries with total count
                var (entriesWithDC, totalCount) = await _unitOfWork.JournalEntries.GetProcessedEntriesForProductReportAsync(
                    startDate, endDate, projectId, categoryId, accountId, locationId, page, pageSize);

                var reportEntries = new List<ProductReportEntryViewModel>();

                // Initialize running totals
                decimal totalDebit = 0;
                decimal totalCredit = 0;
                int totalDebitQty = 0;
                int totalCreditQty = 0;
                decimal totalCOGS = 0;
                decimal totalMargin = 0;

                // Group entries by product (AccountId) to calculate weighted average costs
                var accountGroups = entriesWithDC.GroupBy(x => x.Entry.AccountId);

                foreach (var accountGroup in accountGroups)
                {
                    // Sort entries by date for chronological processing
                    var accountEntries = accountGroup.OrderBy(x => x.Entry.JournalVoucher.EntryDate)
                                                   .ThenBy(x => x.Entry.JournalVoucherId)
                                                   .ToList();

                    // Track running totals for weighted average calculation
                    decimal totalCost = 0;
                    int totalPurchaseQty = 0;
                    decimal weightedAvgCost = 0;

                    foreach (var (entry, dcEntry) in accountEntries)
                    {
                        // Calculate debit and credit quantities
                        int debitQty = entry.Debit > 0 ? entry.Quantity : 0;
                        int creditQty = entry.Credit > 0 ? entry.Quantity : 0;

                        // Update running totals for purchases (debits represent inventory additions)
                        if (debitQty > 0)
                        {
                            totalCost += entry.Debit;
                            totalPurchaseQty += debitQty;
                            weightedAvgCost = totalPurchaseQty > 0 ? totalCost / totalPurchaseQty : 0;
                        }

                        // Calculate COGS and margin
                        decimal cogs = creditQty * weightedAvgCost;
                        decimal margin = entry.Credit - cogs;

                        // Add to report totals
                        totalDebit += entry.Debit;
                        totalCredit += entry.Credit;
                        totalDebitQty += debitQty;
                        totalCreditQty += creditQty;
                        totalCOGS += cogs;
                        totalMargin += margin;

                        // Create entry view model
                        var reportEntry = new ProductReportEntryViewModel
                        {
                            Id = entry.JournalVoucherId,
                            EntryId = entry.JournalEntryId,  // Add the Entry ID
                            EntryDate = entry.JournalVoucher.EntryDate.ToString("dd-MMM yyyy"),
                            DueDate = entry.JournalVoucher.CreditableDate.ToString("dd-MMM yyyy"),
                            Description = entry.Description ?? "",
                            AccountName = entry.Account?.AccountName ?? "",
                            Quantity = entry.Quantity,
                            QtyBalance = totalPurchaseQty - (accountEntries.Take(accountEntries.IndexOf((entry, dcEntry)) + 1)
                                                     .Sum(x => x.Entry.Credit > 0 ? x.Entry.Quantity : 0)),
                            Rate = entry.Rate,
                            DebitQty = debitQty,
                            CreditQty = creditQty,
                            QuantityBalance = debitQty - creditQty,
                            AvgRate = weightedAvgCost,
                            Debit = entry.Debit,
                            Credit = entry.Credit,
                            Balance = entry.Debit - entry.Credit,
                            COGS = cogs,
                            Margin = margin,
                            LocationName = dcEntry?.Location?.LocationName ?? "N/A"
                        };

                        reportEntries.Add(reportEntry);
                    }
                }

                // Create final view model with pagination
                var viewModel = new ProductReportViewModel
                {
                    Entries = reportEntries,
                    TotalDebitQty = totalDebitQty,
                    TotalCreditQty = totalCreditQty,
                    NetQuantity = totalDebitQty - totalCreditQty,
                    TotalDebit = totalDebit,
                    TotalCredit = totalCredit,
                    NetBalance = totalDebit - totalCredit,
                    TotalQuantity = reportEntries.Sum(e => e.Quantity),
                    TotalRecords = totalCount,
                    TotalCOGS = totalCOGS,
                    TotalMargin = totalMargin,

                    // Pagination metadata
                    Page = page,
                    PageSize = pageSize,
                    HasMoreData = (page + 1) * pageSize < totalCount
                };

                return viewModel;
            });
        }

        public async Task<List<Location>> GetAllLocationsAsync()
        {
            return await _unitOfWork.Locations.GetAllAsync();
        }
    }
}