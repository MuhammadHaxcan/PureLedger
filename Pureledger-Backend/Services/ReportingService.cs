using Microsoft.EntityFrameworkCore;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.Reporting;
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

            var cartageEntryList = await _unitOfWork.JournalEntries
                .GetCartageEntriesByJournalVoucherIdAsync(journalVoucherId);
            var cartageEntry = cartageEntryList.FirstOrDefault();

            // Get project image from cartage settings (already stored as base64 string)
            string? projectImage = null;
            var projectCartageSettings = await _unitOfWork.ProjectCartageSettings.GetByProjectIdAsync(project.ProjectId);
            if (!string.IsNullOrEmpty(projectCartageSettings?.ProjectImage))
            {
                projectImage = projectCartageSettings.ProjectImage;
            }

            // Optimized: Parse customer info once instead of calling 4 times
            var parsedCustomerInfo = ParseCustomerInfo(customerInfo);

            var viewModel = new JournalVoucherReportViewModel
            {
                JvId = journalVoucherId,
                VoucherType = voucherType,
                ProjectName = project.ProjectName,
                CreatedDate = dcVoucher.DCDate,
                Entries = processedEntries,

                CustomerAccountName = parsedCustomerInfo.Name,
                CustomerPhone = parsedCustomerInfo.Phone,
                CustomerAddress = parsedCustomerInfo.Address,
                CustomerCity = parsedCustomerInfo.City,

                Adda = locationNames,
                ForChargesAmount = forChargesAmount,
                ForChargesEntries = forChargesEntries,
                CartageAmount = cartageAmount,
                CartageEntries = cartageEntries,

                Builty = cartageEntry?.Credit != null ? (int)cartageEntry.Credit : 0,
                ProjectImage = projectImage
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
            
            // Get project image from cartage settings (already stored as base64 string)
            string? projectImage = null;
            var projectCartageSettings = await _unitOfWork.ProjectCartageSettings.GetByProjectIdAsync(project.ProjectId);
            if (!string.IsNullOrEmpty(projectCartageSettings?.ProjectImage))
            {
                projectImage = projectCartageSettings.ProjectImage;
            }

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
                Builty = cartageEntry?.Credit ?? 0,
                ProjectImage = projectImage
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
                AccountGroupId = entry.Account?.AccountGroupId ?? 0,
                Description = entry.Description ?? "",
                Quantity = entry.Quantity,
                Rate = entry.Rate,
                PKG = entry.Account?.ProductData?.Pkg ?? 1,
                Discount = entry.DiscountPercent ?? 0,
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

        public async Task<AgingReportResponseViewModel> GetAgingReportAsync(AgingReportRequest request)
        {
            if (!int.TryParse(request.CategoryId, out int categoryId))
                return new AgingReportResponseViewModel();

            int? projectId = ParseNullableInt(request.ProjectId);

            return await ExecuteInTransactionAsync(async () =>
            {
                var accounts = await _unitOfWork.Accounts.GetByCategoryIdAsync(categoryId);
                if (accounts.Count == 0)
                    return new AgingReportResponseViewModel();

                var accountIds = accounts.Select(a => a.AccountId).ToList();

                var (entries, totalCount) = await _unitOfWork.JournalEntries.GetEntriesForAgingReportAsync(
                    accountIds,
                    categoryId,
                    projectId,
                    request.ReferenceDate,
                    request.Page,
                    request.PageSize);

                return new AgingReportResponseViewModel
                {
                    Entries = entries,
                    TotalRecords = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    HasMoreData = (request.Page + 1) * request.PageSize < totalCount
                };
            });
        }

        public async Task<AccountReportResponseViewModel> GetAccountReportAsync(AccountReportRequestViewModel request)
        {
            if (string.IsNullOrWhiteSpace(request.ProjectId))
            {
                return new AccountReportResponseViewModel();
            }

            int? categoryId = ParseNullableInt(request.CategoryId);
            int? accountId = ParseNullableInt(request.AccountId);

            if (!int.TryParse(request.ProjectId, out int projectId))
            {
                return new AccountReportResponseViewModel();
            }

            return await ExecuteInTransactionAsync(async () =>
            {
                var (entries, totalCount) = await _unitOfWork.JournalEntries.GetEntriesForAccountReportAsync(
                    projectId,
                    categoryId,
                    accountId,
                    request.Page,
                    request.PageSize);

                return new AccountReportResponseViewModel
                {
                    Entries = entries,
                    TotalRecords = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    HasMoreData = (request.Page + 1) * request.PageSize < totalCount
                };
            });
        }

        private async Task<List<Account>> GetFilteredAccountsAsync(int? accountId, int? categoryId, int projectId)
        {
            if (accountId.HasValue)
            {
                var account = await _unitOfWork.Accounts.GetByIdWithCategoryAsync(accountId.Value);

                if (account?.Category?.ProjectId == projectId)
                {
                    return new List<Account> { account };
                }
                return new List<Account>();
            }
            else if (categoryId.HasValue)
            {
                var accounts = await _unitOfWork.Accounts.GetByCategoryIdWithCategoryAsync(categoryId.Value);

                return accounts.Where(a => a.Category?.ProjectId == projectId).ToList();
            }
            else
            {

                return await _unitOfWork.Accounts.GetAccountsWithEntriesInProjectAsync(projectId);
            }
        }

        public async Task<decimal> GetAccountBalanceUpToVoucherAsync(int journalVoucherId)
        {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null)
                return 0m;

            var customerEntry = voucher.JournalEntries
                .FirstOrDefault(e => e.Account != null && e.Account.AccountGroupId == 1);

            if (customerEntry?.Account == null)
                return 0m;

            int customerAccountId = customerEntry.AccountId;

            var baseBalance = await _unitOfWork.JournalEntries
                .GetAccountBalanceUpToVoucherAsync(customerAccountId, journalVoucherId);

            decimal currentTransactionAmount = customerEntry.Debit - customerEntry.Credit;

            decimal forChargesImpact = 0;
            var forReferences = await _unitOfWork.JournalVoucherReferences
                .GetByMainVoucherIdAndTypeAsync(journalVoucherId, "ForCharges");

            foreach (var reference in forReferences)
            {

                var customerForEntries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(
                    reference.ReferencedVoucherId, null, customerAccountId);

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

            decimal finalBalance = baseBalance + currentTransactionAmount + forChargesImpact + cartageImpact;
            return finalBalance;
        }

        public async Task<MasterReportViewModels> GetMasterReportAsync(MasterReportRequest request)
        {
            var result = new MasterReportViewModels();

            DateOnly startDate = request.StartDate ?? DateOnly.MinValue;
            DateOnly endDate = request.EndDate ?? DateOnly.MaxValue;

            int? projectId = ParseNullableInt(request.ProjectId);
            int? categoryId = ParseNullableInt(request.CategoryId);
            int? accountId = ParseNullableInt(request.AccountId);
            int? bookerId = ParseNullableInt(request.BookerId);

            int page = Math.Max(0, request.Page);
            int pageSize = Math.Clamp(request.PageSize, 1, 500);

            return await ExecuteInTransactionAsync(async () =>
            {

                var (openingDebit, openingCredit) = await _unitOfWork.JournalEntries.GetOpeningBalanceAsync(
                    startDate, projectId, categoryId, accountId);

                decimal openingBalance = openingDebit - openingCredit;

                decimal previousBalance = openingBalance;
                if (page > 0)
                {
                    var previousEntriesBalance = await _unitOfWork.JournalEntries.GetPreviousEntriesBalanceAsync(
                        startDate, endDate, projectId, categoryId, accountId, bookerId, page, pageSize);

                    previousBalance = openingBalance + previousEntriesBalance;
                }

                var (entries, totalCount) = await _unitOfWork.JournalEntries.GetEntriesForMasterReportAsync(
                    startDate, endDate, projectId, categoryId, accountId, bookerId, page, pageSize);

                result.Entries = new List<MasterReportEntryViewModel>();
                result.PreviousBalance = previousBalance;

                decimal runningBalance = previousBalance;

                bool includeOpening = (page == 0);
                if (includeOpening)
                {
                    result.Entries.Add(new MasterReportEntryViewModel
                    {
                        Id = 0,
                        JournalVoucherId = 0,
                        FormattedId = "OPENING",
                        TransactionDate = startDate,
                        CreditableDate = startDate,
                        Description = "Opening Balance",
                        AccountName = accountId.HasValue ? entries.FirstOrDefault()?.Account?.AccountName ?? "" : "",
                        AccountId = accountId ?? 0,
                        Debit = openingDebit,
                        Credit = openingCredit,
                        Balance = openingBalance,
                        ProjectName = "",
                        ProjectId = projectId ?? 0,
                        CategoryName = "",
                        CategoryId = categoryId ?? 0
                    });
                }

                foreach (var entry in entries)
                {
                    decimal entryImpact = entry.Debit - entry.Credit;
                    runningBalance += entryImpact;

                    string formattedId = $"{entry.JournalVoucher.ProjectId}-{entry.CategoryId}-{entry.AccountId}";

                    result.Entries.Add(new MasterReportEntryViewModel
                    {
                        Id = entry.JournalEntryId,
                        JournalVoucherId = entry.JournalVoucherId,
                        FormattedId = formattedId,
                        TransactionDate = entry.JournalVoucher.EntryDate,
                        CreditableDate = entry.JournalVoucher.CreditableDate,
                        Description = entry.Description,
                        AccountName = entry.Account?.AccountName ?? string.Empty,
                        AccountId = entry.AccountId,
                        Debit = entry.Debit,
                        Credit = entry.Credit,
                        Balance = runningBalance,
                        ProjectName = entry.JournalVoucher.Project?.ProjectName ?? string.Empty,
                        ProjectId = entry.JournalVoucher.ProjectId,
                        CategoryName = entry.Category?.CategoryName ?? string.Empty,
                        CategoryId = entry.CategoryId,
                        Quantity = entry.Quantity,
                        BookerId = entry.JournalVoucher.BookerId,
                        BookerName = entry.JournalVoucher.Booker?.BookerName ?? string.Empty
                    });
                }

                result.TotalDebit = 0;
                result.TotalCredit = 0;
                result.NetBalance = runningBalance;

                result.TotalRecords = totalCount + (includeOpening ? 1 : 0);
                result.Page = page;
                result.PageSize = pageSize;
                result.HasMoreData = (page + 1) * pageSize < totalCount;

                return result;
            });
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

            var customerEntry = voucher.JournalEntries
                .FirstOrDefault(e => e.Account != null && e.Account.AccountGroupId == 1);

            if (customerEntry?.Account == null)
                return 0m;

            int customerAccountId = customerEntry.AccountId;

            var previousBalance = await _unitOfWork.JournalEntries
                .GetAccountBalanceUpToVoucherAsyncMinus1(customerAccountId, journalVoucherId - 1);

            return previousBalance;
        }

        public async Task<ProductReportViewModel> GetProductReportAsync(ProductReportRequest request)
        {
            if (string.IsNullOrEmpty(request.ProjectId))
            {
                return new ProductReportViewModel();
            }

            int projectId = int.Parse(request.ProjectId);
            int? categoryId = !string.IsNullOrEmpty(request.CategoryId) ? int.Parse(request.CategoryId) : null;
            int? accountId = !string.IsNullOrEmpty(request.AccountId) ? int.Parse(request.AccountId) : null;
            int? locationId = !string.IsNullOrEmpty(request.LocationId) ? int.Parse(request.LocationId) : null;

            DateOnly startDate = request.StartDate ?? DateOnly.MinValue;
            DateOnly endDate = request.EndDate ?? DateOnly.MaxValue;

            int page = Math.Max(0, request.Page);
            int pageSize = Math.Clamp(request.PageSize, 1, 500);

            return await ExecuteInTransactionAsync(async () =>
            {
                // Get paginated product IDs
                var (productIds, totalCount) = await _unitOfWork.JournalEntries.GetProductIdsForReportAsync(
                    startDate, endDate, projectId, categoryId, accountId, locationId, page, pageSize);

                if (!productIds.Any())
                {
                    return new ProductReportViewModel
                    {
                        TotalRecords = 0,
                        Page = page,
                        PageSize = pageSize,
                        HasMoreData = false
                    };
                }

                // Get all historical entries for the paginated products
                var allHistoricalEntries = await _unitOfWork.JournalEntries.GetAllEntriesForProductReportAsync(
                    DateOnly.MinValue, endDate, projectId, categoryId, null, locationId);

                // Filter to only include the paginated product IDs
                var productGroups = allHistoricalEntries
                    .Where(x => productIds.Contains(x.Entry.AccountId))
                    .GroupBy(x => x.Entry.AccountId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var productSummaries = new List<ProductSummaryViewModel>();

                decimal totalDebit = 0;
                decimal totalCredit = 0;
                int totalDebitQty = 0;
                int totalCreditQty = 0;
                decimal totalCOGS = 0;
                decimal totalMargin = 0;

                foreach (var productId in productIds)
                {
                    if (!productGroups.ContainsKey(productId))
                        continue;

                    var productEntries = productGroups[productId];

                    if (!productEntries.Any())
                        continue;

                    var productName = productEntries.First().Entry.Account?.AccountName ?? "Unknown Product";

                    var entriesBeforeStartDate = productEntries
                        .Where(x => x.Entry.JournalVoucher.EntryDate < startDate)
                        .ToList();

                    var entriesInDateRange = productEntries
                        .Where(x => x.Entry.JournalVoucher.EntryDate >= startDate &&
                                   x.Entry.JournalVoucher.EntryDate <= endDate)
                        .ToList();

                    var prevDebitEntries = entriesBeforeStartDate.Where(x => x.Entry.Debit > 0).ToList();
                    var prevCreditEntries = entriesBeforeStartDate.Where(x => x.Entry.Credit > 0).ToList();

                    int prevDebitQty = prevDebitEntries.Sum(x => x.Entry.Quantity);
                    int prevCreditQty = prevCreditEntries.Sum(x => x.Entry.Quantity);
                    int prevQtyBalance = prevDebitQty - prevCreditQty;

                    decimal prevTotalDebit = prevDebitEntries.Sum(x => x.Entry.Debit);
                    decimal prevTotalCredit = prevCreditEntries.Sum(x => x.Entry.Credit);
                    decimal prevAvgRate = prevDebitQty > 0 ? prevTotalDebit / prevDebitQty : 0;

                    var currentDebitEntries = entriesInDateRange.Where(x => x.Entry.Debit > 0).ToList();
                    var currentCreditEntries = entriesInDateRange.Where(x => x.Entry.Credit > 0).ToList();

                    int currentDebitQty = currentDebitEntries.Sum(x => x.Entry.Quantity);
                    int currentCreditQty = currentCreditEntries.Sum(x => x.Entry.Quantity);

                    decimal currentDebit = currentDebitEntries.Sum(x => x.Entry.Debit);
                    decimal currentCredit = currentCreditEntries.Sum(x => x.Entry.Credit);

                    int totalProductDebitQty = prevDebitQty + currentDebitQty;
                    int totalProductCreditQty = prevCreditQty + currentCreditQty;
                    int qtyBalance = totalProductDebitQty - totalProductCreditQty;

                    decimal averageRate = totalProductDebitQty > 0 ?
                        (prevTotalDebit + currentDebit) / totalProductDebitQty :
                        prevAvgRate;

                    decimal cogs = averageRate * currentCreditQty;
                    decimal margin = currentCredit - cogs;
                    decimal marginPercentage = currentCredit > 0 ? (margin / currentCredit) * 100 : 0;

                    var summary = new ProductSummaryViewModel
                    {
                        ProductId = productId,
                        ProductName = productName,
                        PreviousDebitQty = prevDebitQty,
                        PreviousCreditQty = prevCreditQty,
                        PreviousBalance = prevQtyBalance,
                        PreviousDebit = prevTotalDebit,
                        PreviousCredit = prevTotalCredit,
                        PreviousAvgRate = prevAvgRate,
                        TotalDebitQty = currentDebitQty,
                        TotalCreditQty = currentCreditQty,
                        QtyBalance = qtyBalance,
                        AverageRate = averageRate,
                        TotalDebit = currentDebit,
                        TotalCredit = currentCredit,
                        COGS = cogs,
                        Margin = margin,
                        MarginPercentage = marginPercentage,
                        HasPreviousBalance = prevDebitQty > 0 || prevCreditQty > 0
                    };

                    productSummaries.Add(summary);

                    totalDebit += currentDebit;
                    totalCredit += currentCredit;
                    totalDebitQty += currentDebitQty;
                    totalCreditQty += currentCreditQty;
                    totalCOGS += cogs;
                    totalMargin += margin;
                }

                if (request.ExcludeNilProducts)
                {
                    productSummaries = productSummaries.Where(p => p.QtyBalance != 0).ToList();
                    
                    // Recalculate totals from filtered products
                    totalDebit = productSummaries.Sum(p => p.TotalDebit);
                    totalCredit = productSummaries.Sum(p => p.TotalCredit);
                    totalDebitQty = productSummaries.Sum(p => p.TotalDebitQty);
                    totalCreditQty = productSummaries.Sum(p => p.TotalCreditQty);
                    totalCOGS = productSummaries.Sum(p => p.COGS);
                    totalMargin = productSummaries.Sum(p => p.Margin);
                }

                decimal totalMarginPercentage = totalCredit > 0 ? (totalMargin / totalCredit) * 100 : 0;

                var viewModel = new ProductReportViewModel
                {
                    ProductSummaries = productSummaries.OrderBy(p => p.ProductName).ToList(),
                    Totals = new ProductReportTotalsViewModel
                    {
                        TotalDebit = totalDebit,
                        TotalCredit = totalCredit,
                        TotalDebitQty = totalDebitQty,
                        TotalCreditQty = totalCreditQty,
                        NetQuantity = totalDebitQty - totalCreditQty,
                        TotalCOGS = totalCOGS,
                        TotalMargin = totalMargin,
                        MarginPercentage = totalMarginPercentage
                    },
                    TotalRecords = request.ExcludeNilProducts ? productSummaries.Count : totalCount,
                    Page = page,
                    PageSize = pageSize,
                    HasMoreData = request.ExcludeNilProducts ? false : (page + 1) * pageSize < totalCount
                };

                return viewModel;
            });
        }

        public async Task<List<Location>> GetAllLocationsAsync()
        {
            return await _unitOfWork.Locations.GetAllAsync();
        }

        public async Task<ProjectCartageSettings?> GetProjectCartageSettingsAsync(int projectId)
        {
            return await _unitOfWork.ProjectCartageSettings.GetByProjectIdAsync(projectId);
        }

        public async Task<JournalVoucher?> GetJournalVoucherAsync(int journalVoucherId)
        {
            return await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
        }

        public async Task<List<Booker>> GetAllBookersAsync()
        {
            return await _unitOfWork.Bookers.GetAllAsync();
        }

        #region Missed Entries

        public async Task<List<MissedEntryViewModel>> GetFilteredMissedEntriesAsync(
            DateOnly? startDate,
            DateOnly? endDate,
            int? projectId)
        {
            var entries = await _unitOfWork.MissedEntries.GetFilteredEntriesAsync(startDate, endDate, projectId);

            return entries.Select(e => new MissedEntryViewModel
            {
                MissedEntryId = e.MissedEntryId,
                CustomerName = e.CustomerAccount?.AccountName ?? "Unknown Customer",
                ProductName = e.ProductAccount?.AccountName ?? "Unknown Product",
                ProjectName = e.Project?.ProjectName ?? "Unknown Project",
                MissedDate = e.MissedDate,
                Quantity = e.Quantity,
                Rate = e.Rate,
                Discount = e.Discount,
                Description = e.Description
            }).ToList();
        }

        public async Task<object> GetMissedEntriesSummaryAsync(
    DateOnly? startDate,
    DateOnly? endDate,
    int? projectId)
        {
            var entries = await _unitOfWork.MissedEntries.GetFilteredEntriesAsync(startDate, endDate, projectId);

            int totalMissedEntries = entries.Count;
            int totalMissedQuantity = entries.Sum(e => e.Quantity);
            int uniqueCustomers = entries.Select(e => e.CustomerAccountId).Distinct().Count();
            int uniqueProducts = entries.Select(e => e.ProductAccountId).Distinct().Count();

            var topCustomerGroup = entries
                .Where(e => e.CustomerAccountId > 0)
                .GroupBy(e => e.CustomerAccountId)
                .Select(g => new
                {
                    CustomerAccountId = g.Key,
                    Name = g.FirstOrDefault()?.CustomerAccount?.AccountName ?? "Unknown",
                    Quantity = g.Sum(x => x.Quantity)
                })
                .OrderByDescending(x => x.Quantity)
                .FirstOrDefault();

            var topProductGroup = entries
                .Where(e => e.ProductAccountId > 0)
                .GroupBy(e => e.ProductAccountId)
                .Select(g => new
                {
                    ProductAccountId = g.Key,
                    Name = g.FirstOrDefault()?.ProductAccount?.AccountName ?? "Unknown",
                    Quantity = g.Sum(x => x.Quantity)
                })
                .OrderByDescending(x => x.Quantity)
                .FirstOrDefault();

            return new
            {
                totalMissedEntries,
                totalMissedQuantity,
                uniqueCustomers,
                uniqueProducts,
                topCustomer = topCustomerGroup?.Name ?? null,
                topCustomerMissedQuantity = topCustomerGroup?.Quantity ?? 0,
                topMissedProduct = topProductGroup?.Name ?? null,
                topMissedProductQuantity = topProductGroup?.Quantity ?? 0
            };
        }

        public async Task<List<object>> GetMissedEntriesByCustomerAsync(
            DateOnly? startDate,
            DateOnly? endDate,
            int? projectId)
        {
            var grouped = await _unitOfWork.MissedEntries.GetMissedEntriesByCustomerGroupedAsync(startDate, endDate, projectId);

            var result = grouped.Select(g => (object)new
            {
                customerAccountId = g.CustomerAccountId,
                customerName = g.CustomerName,
                entryCount = g.EntryCount,
                totalQuantity = g.TotalQuantity,
                totalValue = g.TotalValue,
                uniqueProductCount = g.UniqueProductCount,
                averageQuantityPerEntry = Math.Round(g.AverageQuantityPerEntry, 2)
            })
            .OrderByDescending(o =>
            {
                dynamic d = o;
                return (long)d.totalQuantity;
            })
            .ToList();

            return result;
        }

        public async Task<List<object>> GetMissedEntriesByProductAsync(
            DateOnly? startDate,
            DateOnly? endDate,
            int? projectId)
        {
            var grouped = await _unitOfWork.MissedEntries.GetMissedEntriesByProductGroupedAsync(startDate, endDate, projectId);

            var result = grouped.Select(g => (object)new
            {
                productAccountId = g.ProductAccountId,
                productName = g.ProductName,
                entryCount = g.EntryCount,
                totalQuantity = g.TotalQuantity,
                totalValue = g.TotalValue,
                uniqueCustomerCount = g.UniqueCustomerCount,
                averageRate = Math.Round(g.AverageRate, 2),
                averageDiscount = Math.Round(g.AverageDiscount, 2)
            })
            .OrderByDescending(o =>
            {
                dynamic d = o;
                return (long)d.totalQuantity;
            })
            .ToList();

            return result;
        }

        public async Task<object> GetMissedEntriesDataAsync(
            int customerAccountId,
            DateOnly? startDate,
            DateOnly? endDate,
            int? projectId)
        {
            var entries = await _unitOfWork.MissedEntries.GetByCustomerAccountIdAsync(customerAccountId);

            var filtered = entries
                .Where(e => (!startDate.HasValue || e.MissedDate >= startDate.Value)
                         && (!endDate.HasValue || e.MissedDate <= endDate.Value)
                         && (!projectId.HasValue || e.ProjectId == projectId.Value))
                .Select(e => new
                {
                    missedEntryId = e.MissedEntryId,
                    missedDate = e.MissedDate,
                    projectName = e.Project?.ProjectName ?? "",
                    customerAccountId = e.CustomerAccountId,
                    customerName = e.CustomerAccount?.AccountName ?? "",
                    productAccountId = e.ProductAccountId,
                    productName = e.ProductAccount?.AccountName ?? "",
                    quantity = e.Quantity,
                    rate = e.Rate,
                    discount = e.Discount,
                    description = e.Description,
                    totalValue = (e.Quantity * e.Rate) - e.Discount
                })
                .OrderByDescending(x => x.missedDate)
                .ToList();

            return new { entries = filtered };
        }

        public async Task<MissedEntriesPagedResultViewModel> GetFilteredMissedEntriesPaginatedAsync(
            DateOnly? startDate,
            DateOnly? endDate,
            int? projectId,
            int page,
            int pageSize)
        {
            var (entries, totalCount) = await _unitOfWork.MissedEntries.GetFilteredEntriesPaginatedAsync(
                startDate, endDate, projectId, page, pageSize);

            // Repository already returns lightweight MissedEntryViewModel, so no further projection needed
            var viewModels = entries ?? new List<MissedEntryViewModel>();

            return new MissedEntriesPagedResultViewModel
            {
                Entries = viewModels,
                TotalRecords = totalCount,
                Page = page,
                PageSize = pageSize,
                HasMoreData = (page + 1) * pageSize < totalCount
            };
        }
        #endregion
    }
}