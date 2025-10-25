using Microsoft.EntityFrameworkCore;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;
using PureLedger.Services.Interface;
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

                var accountReportEntries = entries.Select(entry => new AccountReportViewModel
                {
                    AccountId = entry.AccountId.ToString(),
                    AccountName = entry.Account?.AccountName ?? "Unknown Account",
                    CategoryName = entry.Account?.Category?.CategoryName ?? "Unknown Category",
                    TotalDebit = entry.Debit,
                    TotalCredit = entry.Credit,
                    NetBalance = entry.Debit - entry.Credit
                }).ToList();

                return new AccountReportResponseViewModel
                {
                    Entries = accountReportEntries,
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

        public async Task<MasterReportViewModel> GetMasterReportAsync(MasterReportRequest request)
        {
            var result = new MasterReportViewModel();

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

                    var (previousEntries, _) = await _unitOfWork.JournalEntries.GetEntriesForMasterReportAsync(
                        startDate, endDate, projectId, categoryId, accountId, bookerId, 0, page * pageSize);

                    previousBalance = openingBalance + previousEntries.Sum(e => e.Debit - e.Credit);
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

            return await ExecuteInTransactionAsync(async () =>
            {

                var allHistoricalEntries = await _unitOfWork.JournalEntries.GetAllEntriesForProductReportAsync(
                    DateOnly.MinValue, endDate, projectId, categoryId, accountId, locationId);

                var productGroups = allHistoricalEntries
                    .GroupBy(x => x.Entry.AccountId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var productSummaries = new List<ProductSummaryViewModel>();

                decimal totalDebit = 0;
                decimal totalCredit = 0;
                int totalDebitQty = 0;
                int totalCreditQty = 0;
                decimal totalCOGS = 0;
                decimal totalMargin = 0;

                foreach (var productId in productGroups.Keys)
                {
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

                    JournalEntry? lastEntry = null;

                    if (entriesInDateRange.Any())
                    {
                        lastEntry = entriesInDateRange
                            .OrderByDescending(x => x.Entry.JournalVoucher.EntryDate)
                            .ThenByDescending(x => x.Entry.JournalVoucherId)
                            .First().Entry;
                    }
                    else if (entriesBeforeStartDate.Any())
                    {
                        lastEntry = entriesBeforeStartDate
                            .OrderByDescending(x => x.Entry.JournalVoucher.EntryDate)
                            .ThenByDescending(x => x.Entry.JournalVoucherId)
                            .First().Entry;
                    }

                    if (lastEntry == null)
                        continue;

                    var summary = new ProductSummaryViewModel
                    {
                        ProductId = productId,
                        ProductName = productName,
                        LastVoucherId = lastEntry.JournalVoucherId,
                        EntryDate = lastEntry.JournalVoucher.EntryDate.ToString("dd-MMM yyyy"),
                        CreditableDate = lastEntry.JournalVoucher.CreditableDate.ToString("dd-MMM yyyy"),
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
                    }
                };

                return viewModel;
            });
        }

        public async Task<List<Location>> GetAllLocationsAsync()
        {
            return await _unitOfWork.Locations.GetAllAsync();
        }

        public async Task<MissedEntriesSummaryViewModel> GetMissedEntriesSummaryAsync(MissedEntriesSummaryRequest request)
        {
            DateOnly startDate = request.StartDate ?? DateOnly.FromDateTime(DateTime.Today.AddMonths(-1));
            DateOnly endDate = request.EndDate ?? DateOnly.FromDateTime(DateTime.Today);
            int? projectId = ParseNullableInt(request.ProjectId);

            return await ExecuteInTransactionAsync(async () =>
            {
                var entries = await _unitOfWork.MissedEntries.GetFilteredEntriesAsync(startDate, endDate, projectId);

                decimal totalValue = entries.Sum(e => (decimal)((e.Quantity * e.Rate) - e.Discount));

                var summary = new MissedEntriesSummaryViewModel
                {
                    TotalMissedEntries = entries.Count,
                    TotalMissedQuantity = entries.Sum(e => e.Quantity),
                    TotalMissedValue = totalValue,
                    UniqueCustomers = entries.Select(e => e.CustomerAccountId).Distinct().Count(),
                    UniqueProducts = entries.Select(e => e.ProductAccountId).Distinct().Count(),
                    AverageQuantityPerEntry = entries.Any() ? (decimal)entries.Average(e => e.Quantity) : 0,
                    AverageValuePerEntry = entries.Any() ? totalValue / entries.Count : 0,
                    StartDate = startDate,
                    EndDate = endDate,
                    ProjectId = projectId
                };

                var topProduct = entries
                    .GroupBy(e => e.ProductAccountId)
                    .OrderByDescending(g => g.Sum(e => e.Quantity))
                    .FirstOrDefault();

                if (topProduct != null)
                {
                    var productName = topProduct.First().ProductAccount?.AccountName ?? "Unknown";
                    summary.TopMissedProduct = productName;
                    summary.TopMissedProductQuantity = topProduct.Sum(e => e.Quantity);
                }

                var topCustomer = entries
                    .GroupBy(e => e.CustomerAccountId)
                    .OrderByDescending(g => g.Sum(e => e.Quantity))
                    .FirstOrDefault();

                if (topCustomer != null)
                {
                    var customerName = topCustomer.First().CustomerAccount?.AccountName ?? "Unknown";
                    summary.TopCustomer = customerName;
                    summary.TopCustomerMissedQuantity = topCustomer.Sum(e => e.Quantity);
                }

                return summary;
            });
        }

        public async Task<List<MissedEntriesByCustomerViewModel>> GetMissedEntriesByCustomerAsync(MissedEntriesReportRequest request)
        {
            DateOnly startDate = request.StartDate ?? DateOnly.FromDateTime(DateTime.Today.AddMonths(-1));
            DateOnly endDate = request.EndDate ?? DateOnly.FromDateTime(DateTime.Today);
            int? projectId = ParseNullableInt(request.ProjectId);

            return await ExecuteInTransactionAsync(async () =>
            {
                var entries = await _unitOfWork.MissedEntries.GetFilteredEntriesAsync(startDate, endDate, projectId);

                int totalQuantity = entries.Sum(e => e.Quantity);

                var groupedByCustomer = entries
                    .GroupBy(e => new { e.CustomerAccountId, CustomerName = e.CustomerAccount?.AccountName ?? "Unknown" })
                    .Select(g => {

                        var customerEntries = g.Select(e => new MissedEntryDetailViewModel
                        {
                            MissedEntryId = e.MissedEntryId,
                            CustomerName = g.Key.CustomerName,
                            ProductName = e.ProductAccount?.AccountName ?? "Unknown Product",
                            ProjectName = e.Project?.ProjectName ?? "Unknown Project",
                            MissedDate = e.MissedDate,
                            Quantity = e.Quantity,
                            Rate = e.Rate,
                            Discount = e.Discount,
                            Description = e.Description,
                            TotalValue = (decimal)((e.Quantity * e.Rate) - e.Discount),
                            CustomerAccountId = e.CustomerAccountId,
                            ProductAccountId = e.ProductAccountId,
                            ProjectId = e.ProjectId
                        }).ToList();

                        return new MissedEntriesByCustomerViewModel
                        {
                            CustomerAccountId = g.Key.CustomerAccountId,
                            CustomerName = g.Key.CustomerName,
                            EntryCount = g.Count(),
                            TotalQuantity = g.Sum(e => e.Quantity),
                            TotalValue = (decimal)g.Sum(e => (e.Quantity * e.Rate) - e.Discount),
                            UniqueProductCount = g.Select(e => e.ProductAccountId).Distinct().Count(),
                            AverageQuantityPerEntry = (decimal)g.Average(e => e.Quantity),
                            PercentOfTotal = totalQuantity > 0 ? ((decimal)g.Sum(e => e.Quantity) / totalQuantity) * 100 : 0,
                            CustomerEntries = customerEntries
                        };
                    })
                    .OrderByDescending(x => x.TotalQuantity)
                    .ToList();

                return groupedByCustomer;
            });
        }

        public async Task<List<MissedEntriesByProductViewModel>> GetMissedEntriesByProductAsync(MissedEntriesReportRequest request)
        {
            DateOnly startDate = request.StartDate ?? DateOnly.FromDateTime(DateTime.Today.AddMonths(-1));
            DateOnly endDate = request.EndDate ?? DateOnly.FromDateTime(DateTime.Today);
            int? projectId = ParseNullableInt(request.ProjectId);

            return await ExecuteInTransactionAsync(async () =>
            {
                var entries = await _unitOfWork.MissedEntries.GetFilteredEntriesAsync(startDate, endDate, projectId);

                var groupedByProduct = entries
                    .GroupBy(e => new { e.ProductAccountId, ProductName = e.ProductAccount?.AccountName ?? "Unknown" })
                    .Select(g => new MissedEntriesByProductViewModel
                    {
                        ProductAccountId = g.Key.ProductAccountId,
                        ProductName = g.Key.ProductName,
                        EntryCount = g.Count(),
                        TotalQuantity = g.Sum(e => e.Quantity),
                        TotalValue = (decimal)g.Sum(e => (e.Quantity * e.Rate) - e.Discount),
                        UniqueCustomerCount = g.Select(e => e.CustomerAccountId).Distinct().Count(),
                        AverageRate = (decimal)g.Average(e => e.Rate),
                        AverageDiscount = (decimal)g.Average(e => e.Discount)
                    })
                    .OrderByDescending(x => x.TotalQuantity)
                    .ToList();

                return groupedByProduct;
            });
        }

        public async Task<MissedEntriesDetailViewModel> GetMissedEntriesDetailAsync(MissedEntriesReportRequest request)
        {
            DateOnly startDate = request.StartDate ?? DateOnly.FromDateTime(DateTime.Today.AddMonths(-1));
            DateOnly endDate = request.EndDate ?? DateOnly.FromDateTime(DateTime.Today);
            int? projectId = ParseNullableInt(request.ProjectId);
            int page = Math.Max(0, request.Page);
            int pageSize = Math.Clamp(request.PageSize, 1, 500);

            return await ExecuteInTransactionAsync(async () =>
            {

                var entries = await _unitOfWork.MissedEntries.GetFilteredEntriesAsync(startDate, endDate, projectId);

                if (request.CustomerAccountId.HasValue)
                {
                    entries = entries.Where(e => e.CustomerAccountId == request.CustomerAccountId.Value).ToList();
                }

                var mappedEntries = entries.Select(e => new MissedEntryDetailViewModel
                {
                    MissedEntryId = e.MissedEntryId,
                    CustomerName = e.CustomerAccount?.AccountName ?? "Unknown Customer",
                    ProductName = e.ProductAccount?.AccountName ?? "Unknown Product",
                    ProjectName = e.Project?.ProjectName ?? "Unknown Project",
                    MissedDate = e.MissedDate,
                    Quantity = e.Quantity,
                    Rate = e.Rate,
                    Discount = e.Discount,
                    Description = e.Description,
                    TotalValue = (decimal)((e.Quantity * e.Rate) - e.Discount),
                    CustomerAccountId = e.CustomerAccountId,
                    ProductAccountId = e.ProductAccountId,
                    ProjectId = e.ProjectId
                }).ToList();

                var pagedEntries = mappedEntries
                    .Skip(page * pageSize)
                    .Take(pageSize)
                    .ToList();

                var viewModel = new MissedEntriesDetailViewModel
                {
                    Entries = pagedEntries,
                    TotalEntries = mappedEntries.Count,
                    TotalQuantity = mappedEntries.Sum(e => e.Quantity),
                    TotalValue = mappedEntries.Sum(e => e.TotalValue),
                    Page = page,
                    PageSize = pageSize,
                    HasMoreData = (page + 1) * pageSize < mappedEntries.Count
                };

                return viewModel;
            });
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

    }
}