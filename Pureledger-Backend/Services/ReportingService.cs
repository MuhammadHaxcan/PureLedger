using Microsoft.EntityFrameworkCore;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.Reporting;
using PureLedger.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace PureLedger.Services {
    public class ReportingService : IReportingService {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IApprovalGuardService _approvalGuard;
        private readonly IPaymentReceiptClassifier _paymentReceiptClassifier;
        private readonly LedgerDbContext _context;

        public ReportingService(IUnitOfWork unitOfWork, IApprovalGuardService approvalGuard,
            IPaymentReceiptClassifier paymentReceiptClassifier, LedgerDbContext context) {
            _unitOfWork = unitOfWork;
            _approvalGuard = approvalGuard;
            _paymentReceiptClassifier = paymentReceiptClassifier;
            _context = context;
        }

        public async Task<JournalVoucherReportViewModel?> GetSingleDocReportAsync(int journalVoucherId, VoucherType voucherType) {
            await _approvalGuard.EnsureVoucherNotPendingAsync(journalVoucherId);
            voucherType = await _paymentReceiptClassifier.ResolvePrintableTypeAsync(journalVoucherId, voucherType);
            return voucherType switch {
                VoucherType.DC => await GetDeliveryChallanReportAsync(journalVoucherId, voucherType),
                VoucherType.INV => await GetDeliveryChallanReportAsync(journalVoucherId, voucherType),
                _ => await GetJournalVoucherReportAsync(journalVoucherId, voucherType)
            };
        }

        private async Task<JournalVoucherReportViewModel?> GetDeliveryChallanReportAsync(int journalVoucherId, VoucherType voucherType) {
            var dcVoucher = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(journalVoucherId);
            if (dcVoucher?.JournalVoucher == null) return null;

            var project = await _unitOfWork.Projects.GetByIdAsync(dcVoucher.JournalVoucher.ProjectId);
            if (project == null) return null;

            string locationNames = GetLocationNames(dcVoucher.Entries, dcVoucher.JournalVoucher.IsStockTransfer);
            var processedEntries = GetProcessedEntries(dcVoucher.Entries, dcVoucher.JournalVoucher.IsStockTransfer);
            var customerInfo = GetCustomerAccountName(dcVoucher.JournalVoucher);

            var (forChargesAmount, forChargesEntries) = await GetForChargesDataAsync(journalVoucherId);
            var (cartageAmount, cartageEntries) = await GetCartageDataAsync(journalVoucherId);

            var cartageEntryList = await _unitOfWork.JournalEntries
                .GetCartageEntriesByJournalVoucherIdAsync(journalVoucherId);
            var cartageEntry = cartageEntryList.FirstOrDefault();

            var projectImage = await _context.ProjectImgs.AsNoTracking()
                .Where(img => img.ProjectId == project.ProjectId)
                .Select(img => img.ImageDataUrl)
                .SingleOrDefaultAsync();

            // Optimized: Parse customer info once instead of calling 4 times
            var parsedCustomerInfo = ParseCustomerInfo(customerInfo);

            // Concatenate all non-empty remarks from DCE records
            var allRemarks = dcVoucher.Entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Remarks))
                .Select(e => e.Remarks!.Trim())
                .Distinct()
                .ToList();
            string concatenatedRemarks = allRemarks.Count > 0
                ? string.Join(" | ", allRemarks)
                : string.Empty;

            // Concatenate all non-empty other references from DCE records
            var allOtherRefs = dcVoucher.Entries
                .Where(e => !string.IsNullOrWhiteSpace(e.OtherReference))
                .Select(e => e.OtherReference!.Trim())
                .Distinct()
                .ToList();
            string concatenatedOtherRefs = allOtherRefs.Count > 0
                ? string.Join(" | ", allOtherRefs)
                : string.Empty;

            var viewModel = new JournalVoucherReportViewModel {
                JvId = journalVoucherId,
                VoucherType = voucherType,
                ProjectName = project.ProjectName,
                CreatedDate = dcVoucher.DCDate,
                DueDate = dcVoucher.JournalVoucher.CreditableDate,
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
                ProjectImage = projectImage,
                Remarks = concatenatedRemarks,
                OtherReference = concatenatedOtherRefs
            };
            return viewModel;
        }

        private async Task<JournalVoucherReportViewModel?> GetJournalVoucherReportAsync(int journalVoucherId, VoucherType voucherType) {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null) return null;

            var project = await _unitOfWork.Projects.GetByIdAsync(voucher.ProjectId);
            if (project == null) return null;

            IEnumerable<JournalEntry> reportEntries = voucherType == VoucherType.PaymentReceipt
                ? voucher.JournalEntries.OrderBy(x => x.JournalEntryId)
                : voucher.JournalEntries;
            var entries = GetJournalVoucherEntries(reportEntries);
            var customerInfo = GetCustomerAccountName(voucher);

            var cartageEntries = await _unitOfWork.JournalEntries
                .GetCartageEntriesByJournalVoucherIdAsync(journalVoucherId);
            var cartageEntry = cartageEntries.FirstOrDefault();

            var customerDetails = ParseCustomerInfo(customerInfo);

            var projectImage = await _context.ProjectImgs.AsNoTracking()
                .Where(img => img.ProjectId == project.ProjectId)
                .Select(img => img.ImageDataUrl)
                .SingleOrDefaultAsync();

            var viewModel = new JournalVoucherReportViewModel {
                JvId = journalVoucherId,
                VoucherType = voucherType,
                ProjectName = project.ProjectName,
                CreatedDate = voucher.EntryDate,
                DueDate = voucher.CreditableDate,
                Entries = entries,

                CustomerAccountName = customerDetails.Name,
                CustomerPhone = customerDetails.Phone,
                CustomerAddress = customerDetails.Address,
                CustomerCity = customerDetails.City,

                CartageEntry = cartageEntry,
                Builty = cartageEntry?.Credit ?? 0,
                ProjectImage = projectImage
            };

            if (voucherType == VoucherType.PaymentReceipt) {
                var audit = await _context.ApprovalVouchers.AsNoTracking()
                    .Where(x => x.JournalVoucherId == journalVoucherId &&
                                x.VoucherType == ApprovalVoucherType.PaymentReceipt &&
                                x.Status == ApprovalStatuses.Approved)
                    .OrderByDescending(x => x.ApprovalVoucherId)
                    .Select(x => new {
                        x.SubmittedDate,
                        x.DecisionDate,
                        x.HadDuplicateWarningsAtDecision,
                        SubmittedBy = _context.Users.Where(u => u.UserId == x.PostedByUserId)
                            .Select(u => u.Username).FirstOrDefault(),
                        ApprovedBy = _context.Users.Where(u => u.UserId == x.DecisionByUserId)
                            .Select(u => u.Username).FirstOrDefault()
                    }).FirstOrDefaultAsync();
                if (audit != null) {
                    viewModel.ApprovalSubmittedDate = audit.SubmittedDate;
                    viewModel.ApprovalDecisionDate = audit.DecisionDate;
                    viewModel.SubmittedBy = audit.SubmittedBy;
                    viewModel.ApprovedBy = audit.ApprovedBy;
                    viewModel.HadDuplicateWarningsAtDecision = audit.HadDuplicateWarningsAtDecision;
                }
            }

            return viewModel;
        }

        private string GetLocationNames(IEnumerable<DeliveryChallanEntry> entries, bool isStockTransfer) {
            if (isStockTransfer) {
                var physical = entries.Where(e => e.JournalEntry?.Account?.AccountGroupId == 2)
                    .Select(e => e.Location?.LocationName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
                var route = entries.Where(e => e.JournalEntry?.Account?.AccountGroupId == 1 &&
                                               e.JournalEntry.Quantity == 0)
                    .Select(e => e.Location?.LocationName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
                if (route.Count > 0)
                    return $"{string.Join(", ", physical)} → {string.Join(", ", route)}";
            }
            return string.Join(", ",
                entries
                    .Where(e => e.Location != null)
                    .Select(e => e.Location.LocationName)
                    .Distinct());
        }

        private List<JournalVoucherEntryReportModel> GetProcessedEntries(IEnumerable<DeliveryChallanEntry> entries, bool isStockTransfer) {
            return entries
                .Where(e => e.JournalEntry?.IsProcessed == true &&
                            (!isStockTransfer || e.JournalEntry.Account?.AccountGroupId == 2))
                .Select(e => MapToEntryReportModel(e.JournalEntry!))
                .ToList();
        }

        private List<JournalVoucherEntryReportModel> GetJournalVoucherEntries(IEnumerable<JournalEntry> entries) {
            return entries
                .Select(MapToEntryReportModel)
                .ToList();
        }

        private JournalVoucherEntryReportModel MapToEntryReportModel(JournalEntry entry) {
            return new JournalVoucherEntryReportModel {
                AccountId = entry.AccountId,
                AccountName = entry.Account?.AccountName ?? "",
                CategoryId = entry.CategoryId,
                CategoryName = entry.Category?.CategoryName ?? "",
                AccountGroupId = entry.Account?.AccountGroupId ?? 0,
                Description = entry.Description ?? "",
                Quantity = entry.Quantity,
                Rate = entry.Rate,
                PKG = entry.Account?.ProductData?.Pkg ?? 1,
                Discount = entry.DiscountPercent ?? 0,
                Debit = entry.Debit,
                Credit = entry.Credit,
                Rbp = entry.Rbp ?? "Y",
                InstrumentType = entry.InstrumentDetail?.InstrumentType,
                ReferenceNumber = entry.InstrumentDetail?.ReferenceNumber,
                InstrumentDate = entry.InstrumentDetail?.InstrumentDate
            };
        }

        private static string FormatReportDescription(JournalEntry entry) {
            var description = entry.Description ?? string.Empty;
            var detail = entry.InstrumentDetail;
            if (detail == null) return description;
            var metadata = detail.InstrumentType switch {
                PaymentInstrumentType.Cash => "[P&R: Cash]",
                PaymentInstrumentType.Cheque =>
                    $"[P&R: Cheque; Cheque No: {detail.ReferenceNumber}; Date: {detail.InstrumentDate:yyyy-MM-dd}]",
                _ =>
                    $"[P&R: {detail.InstrumentType}; Ref No: {detail.ReferenceNumber}; Date: {detail.InstrumentDate:yyyy-MM-dd}]"
            };
            return string.IsNullOrWhiteSpace(description) ? metadata : $"{description} {metadata}";
        }

        private string GetCustomerAccountName(JournalVoucher voucher) {
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

        private async Task<(decimal TotalAmount, List<JournalVoucherEntryReportModel> Entries)> GetForChargesDataAsync(int journalVoucherId) {
            var references = await _unitOfWork.JournalVoucherReferences
                .GetByMainVoucherIdAndTypeAsync(journalVoucherId, "ForCharges");

            if (!references.Any())
                return (0, new List<JournalVoucherEntryReportModel>());

            // Batch fetch all vouchers in a single query instead of N+1
            var voucherIds = references.Select(r => r.ReferencedVoucherId).ToList();
            var vouchers = await _unitOfWork.JournalVouchers.GetByIdsWithEntriesAsync(voucherIds);

            decimal totalAmount = 0;
            var forChargesEntries = new List<JournalVoucherEntryReportModel>();

            foreach (var voucher in vouchers) {
                var debitEntry = voucher.JournalEntries.FirstOrDefault(e => e.Debit > 0);
                if (debitEntry != null) {
                    totalAmount += debitEntry.Debit;
                    forChargesEntries.Add(MapToEntryReportModel(debitEntry));
                }
            }

            return (totalAmount, forChargesEntries);
        }

        private async Task<(decimal TotalAmount, List<JournalVoucherEntryReportModel> Entries)> GetCartageDataAsync(int journalVoucherId) {
            var references = await _unitOfWork.JournalVoucherReferences
                .GetByMainVoucherIdAndTypeAsync(journalVoucherId, "Cartage");

            if (!references.Any())
                return (0, new List<JournalVoucherEntryReportModel>());

            // Batch fetch all vouchers in a single query instead of N+1
            var voucherIds = references.Select(r => r.ReferencedVoucherId).ToList();
            var vouchers = await _unitOfWork.JournalVouchers.GetByIdsWithEntriesAsync(voucherIds);

            decimal totalAmount = 0;
            var cartageEntries = new List<JournalVoucherEntryReportModel>();

            foreach (var voucher in vouchers) {
                var creditEntry = voucher.JournalEntries.FirstOrDefault(e => e.Credit > 0);
                if (creditEntry != null) {
                    totalAmount += creditEntry.Credit;
                    cartageEntries.Add(MapToEntryReportModel(creditEntry));
                }
            }

            return (totalAmount, cartageEntries);
        }

        public (string Name, string Phone, string Address, string City) ParseCustomerInfo(string customerInfo) {
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

        private async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation) {
            await using var transaction = await _unitOfWork.BeginTransactionAsync();

            try {
                var result = await operation();
                await transaction.CommitAsync();
                return result;
            } catch {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #region Public API Methods

        public async Task<AgingReportResponseViewModel> GetAgingReportAsync(AgingReportRequest request) {
            if (!int.TryParse(request.CategoryId, out int categoryId))
                return new AgingReportResponseViewModel();

            int page = Math.Max(0, request.Page);
            int pageSize = Math.Clamp(request.PageSize, 1, 2500);
            int? projectId = ParseNullableInt(request.ProjectId);

            var accounts = await _unitOfWork.Accounts.GetByCategoryIdAsync(categoryId);
            if (accounts.Count == 0)
                return new AgingReportResponseViewModel();

            var accountIds = accounts.Select(a => a.AccountId).ToList();

            var (entries, totalCount) = await _unitOfWork.JournalEntries.GetEntriesForAgingReportAsync(
                accountIds,
                categoryId,
                projectId,
                request.ReferenceDate,
                page,
                pageSize);

            return new AgingReportResponseViewModel {
                Entries = entries,
                TotalRecords = totalCount,
                Page = page,
                PageSize = pageSize,
                HasMoreData = (page + 1) * pageSize < totalCount
            };
        }

        public async Task<AccountReportResponseViewModel> GetAccountReportAsync(AccountReportRequestViewModel request) {
            if (string.IsNullOrWhiteSpace(request.ProjectId)) {
                return new AccountReportResponseViewModel();
            }

            int page = Math.Max(0, request.Page);
            int pageSize = Math.Clamp(request.PageSize, 1, 2500);
            int? categoryId = ParseNullableInt(request.CategoryId);
            int? accountId = ParseNullableInt(request.AccountId);
            int? bookerId = ParseNullableInt(request.BookerId);

            if (!int.TryParse(request.ProjectId, out int projectId)) {
                return new AccountReportResponseViewModel();
            }

            var (entries, totalCount) = await _unitOfWork.JournalEntries.GetEntriesForAccountReportAsync(
                projectId,
                categoryId,
                accountId,
                bookerId,
                page,
                pageSize);

            return new AccountReportResponseViewModel {
                Entries = entries,
                TotalRecords = totalCount,
                Page = page,
                PageSize = pageSize,
                HasMoreData = (page + 1) * pageSize < totalCount
            };
        }

        public async Task<decimal> GetAccountBalanceUpToVoucherAsync(int journalVoucherId) {
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

            // Get all references at once instead of two separate calls
            var allReferences = await _unitOfWork.JournalVoucherReferences
                .GetByMainVoucherIdAsync(journalVoucherId);

            // Collect all referenced voucher IDs for both ForCharges and Cartage
            var referencedVoucherIds = allReferences
                .Where(r => r.ReferenceType == "ForCharges" || r.ReferenceType == "Cartage")
                .Select(r => r.ReferencedVoucherId)
                .ToList();

            if (!referencedVoucherIds.Any()) {
                return baseBalance + currentTransactionAmount;
            }

            // Batch fetch all entries for the customer account across all referenced vouchers
            var allReferencedEntries = await _unitOfWork.JournalEntries
                .GetEntriesByVoucherIdsAndAccountAsync(referencedVoucherIds, customerAccountId);

            decimal totalImpact = allReferencedEntries.Sum(e => e.Debit - e.Credit);

            return baseBalance + currentTransactionAmount + totalImpact;
        }

        public async Task<MasterReportViewModels> GetMasterReportAsync(MasterReportRequest request) {
            var result = new MasterReportViewModels();

            DateOnly startDate = request.StartDate ?? DateOnly.MinValue;
            DateOnly endDate = request.EndDate ?? DateOnly.MaxValue;

            int? projectId = ParseNullableInt(request.ProjectId);
            int? categoryId = ParseNullableInt(request.CategoryId);
            int? accountId = ParseNullableInt(request.AccountId);
            int? bookerId = ParseNullableInt(request.BookerId);
            int? locationId = ParseNullableInt(request.LocationId);

            int page = Math.Max(0, request.Page);
            int pageSize = Math.Clamp(request.PageSize, 1, 2500);

            var (openingDebit, openingCredit) = await _unitOfWork.JournalEntries.GetOpeningBalanceAsync(
                startDate, projectId, categoryId, accountId);

            decimal openingBalance = openingDebit - openingCredit;

            decimal previousBalance = openingBalance;
            if (page > 0) {
                var previousEntriesBalance = await _unitOfWork.JournalEntries.GetPreviousEntriesBalanceAsync(
                    startDate, endDate, projectId, categoryId, accountId, bookerId, locationId, page, pageSize);

                previousBalance = openingBalance + previousEntriesBalance;
            }

            var (entriesWithDC, totalCount) = await _unitOfWork.JournalEntries.GetEntriesForMasterReportAsync(
                startDate, endDate, projectId, categoryId, accountId, bookerId, locationId, page, pageSize);

            result.Entries = new List<MasterReportEntryViewModel>();
            result.PreviousBalance = previousBalance;

            decimal runningBalance = previousBalance;

            // Track running qty balance per account when ShowQtyBalance is enabled
            // Initialize with previous page balances if on page > 0
            Dictionary<int, int> accountQtyBalance = new();
            if (page > 0 && request.ShowQtyBalance) {
                accountQtyBalance = await _unitOfWork.JournalEntries.GetPreviousQtyBalancePerAccountAsync(
                    startDate, endDate, projectId, categoryId, accountId, bookerId, locationId, page, pageSize);
            }

            bool includeOpening = (page == 0);
            if (includeOpening) {
                result.Entries.Add(new MasterReportEntryViewModel {
                    Id = 0,
                    JournalVoucherId = 0,
                    FormattedId = "OPENING",
                    TransactionDate = startDate,
                    CreditableDate = startDate,
                    Description = "Opening Balance",
                    AccountName = accountId.HasValue ? entriesWithDC.FirstOrDefault().Entry?.Account?.AccountName ?? "" : "",
                    AccountId = accountId ?? 0,
                    Debit = 0,
                    Credit = 0,
                    Balance = openingBalance,
                    ProjectName = "",
                    ProjectId = projectId ?? 0,
                    CategoryName = "",
                    CategoryId = categoryId ?? 0,
                    QtyBalance = 0
                });
            }

            foreach (var (entry, dcEntry) in entriesWithDC) {
                decimal entryImpact = entry.Debit - entry.Credit;
                runningBalance += entryImpact;

                // Calculate running qty balance per account
                // Debit > 0 means purchase/stock-in (+qty), Credit > 0 means sale/stock-out (-qty)
                int qtyImpact = entry.Debit > 0 ? entry.Quantity : -entry.Quantity;
                if (!accountQtyBalance.ContainsKey(entry.AccountId))
                    accountQtyBalance[entry.AccountId] = 0;
                accountQtyBalance[entry.AccountId] += qtyImpact;

                string formattedId = $"{entry.JournalVoucher.ProjectId}-{entry.CategoryId}-{entry.AccountId}";

                result.Entries.Add(new MasterReportEntryViewModel {
                    Id = entry.JournalEntryId,
                    JournalVoucherId = entry.JournalVoucherId,
                    FormattedId = formattedId,
                    TransactionDate = entry.JournalVoucher.EntryDate,
                    CreditableDate = entry.JournalVoucher.CreditableDate,
                    Description = FormatReportDescription(entry),
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
                    BookerName = entry.JournalVoucher.Booker?.BookerName ?? string.Empty,
                    LocationId = dcEntry?.LocationId,
                    LocationName = dcEntry?.Location?.LocationName ?? string.Empty,
                    QtyBalance = request.ShowQtyBalance ? accountQtyBalance[entry.AccountId] : 0,
                    Rate = entry.Rate
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
        }

        public async Task<object> GetUserReportInitAsync(int userId) {
            var projects = await GetAllProjectsForUserAsync(userId);
            var locations = await GetAllLocationsAsync();
            return new {
                success = true,
                projects = projects.Select(p => new { id = p.ProjectId, name = p.ProjectName }),
                categories = Array.Empty<object>(),
                locations = locations.Select(l => new { id = l.LocationId, name = l.LocationName })
            };
        }

        public async Task<UserReportViewModels> GetUserReportAsync(UserReportRequest request, int userId) {
            var result = new UserReportViewModels();

            // Validate CategoryId is provided (required field)
            if (string.IsNullOrWhiteSpace(request.CategoryId)) {
                throw new ArgumentException("CategoryId is required for User Report");
            }

            if (!int.TryParse(request.CategoryId, out int categoryId)) {
                throw new ArgumentException("Invalid CategoryId format");
            }

            // Get user's hidden category IDs
            var userCategories = await _unitOfWork.UserCategories.GetByUserIdAsync(userId);
            var hiddenCategoryIds = userCategories.Select(uc => uc.CategoryId).ToList();

            // Validate that selected CategoryId is NOT in hidden categories
            if (hiddenCategoryIds.Contains(categoryId)) {
                throw new UnauthorizedAccessException("You do not have access to the selected category");
            }

            DateOnly startDate = request.StartDate ?? DateOnly.MinValue;
            DateOnly endDate = request.EndDate ?? DateOnly.MaxValue;

            int? projectId = ParseNullableInt(request.ProjectId);
            int? accountId = ParseNullableInt(request.AccountId);
            int? bookerId = ParseNullableInt(request.BookerId);
            int? locationId = ParseNullableInt(request.LocationId);

            int page = Math.Max(0, request.Page);
            int pageSize = Math.Clamp(request.PageSize, 1, 2500);

            var (openingDebit, openingCredit) = await _unitOfWork.JournalEntries.GetOpeningBalanceForUserReportAsync(
                startDate, projectId, categoryId, accountId);

            decimal openingBalance = openingDebit - openingCredit;

            decimal previousBalance = openingBalance;
            if (page > 0) {
                var previousEntriesBalance = await _unitOfWork.JournalEntries.GetPreviousEntriesBalanceForUserReportAsync(
                    startDate, endDate, projectId, categoryId, accountId, bookerId, locationId, page, pageSize);

                previousBalance = openingBalance + previousEntriesBalance;
            }

            var (entriesWithDC, totalCount) = await _unitOfWork.JournalEntries.GetEntriesForUserReportAsync(
                startDate, endDate, projectId, categoryId, accountId, bookerId, locationId, page, pageSize);

            // Filter out any entries with hidden categories (double-check)
            var filteredEntries = entriesWithDC.Where(e => !hiddenCategoryIds.Contains(e.Entry.CategoryId)).ToList();
            var filteredCount = totalCount - (entriesWithDC.Count - filteredEntries.Count);

            result.Entries = new List<UserReportEntryViewModel>();
            result.PreviousBalance = previousBalance;

            decimal runningBalance = previousBalance;

            // Track running qty balance per account when ShowQtyBalance is enabled
            Dictionary<int, int> accountQtyBalance = new();
            if (page > 0 && request.ShowQtyBalance) {
                accountQtyBalance = await _unitOfWork.JournalEntries.GetPreviousQtyBalancePerAccountAsync(
                    startDate, endDate, projectId, categoryId, accountId, bookerId, locationId, page, pageSize);
            }

            bool includeOpening = (page == 0);
            if (includeOpening) {
                result.Entries.Add(new UserReportEntryViewModel {
                    Id = 0,
                    JournalVoucherId = 0,
                    FormattedId = "OPENING",
                    TransactionDate = startDate,
                    CreditableDate = startDate,
                    Description = "Opening Balance",
                    AccountName = accountId.HasValue ? filteredEntries.FirstOrDefault().Entry?.Account?.AccountName ?? "" : "",
                    AccountId = accountId ?? 0,
                    Debit = 0,
                    Credit = 0,
                    Balance = openingBalance,
                    ProjectName = "",
                    ProjectId = projectId ?? 0,
                    CategoryName = "",
                    CategoryId = categoryId,
                    QtyBalance = 0
                });
            }

            foreach (var (entry, dcEntry) in filteredEntries) {
                decimal entryImpact = entry.Debit - entry.Credit;
                runningBalance += entryImpact;

                // Calculate running qty balance per account
                int qtyImpact = entry.Debit > 0 ? entry.Quantity : -entry.Quantity;
                if (!accountQtyBalance.ContainsKey(entry.AccountId))
                    accountQtyBalance[entry.AccountId] = 0;
                accountQtyBalance[entry.AccountId] += qtyImpact;

                string formattedId = $"{entry.JournalVoucher.ProjectId}-{entry.CategoryId}-{entry.AccountId}";

                result.Entries.Add(new UserReportEntryViewModel {
                    Id = entry.JournalEntryId,
                    JournalVoucherId = entry.JournalVoucherId,
                    FormattedId = formattedId,
                    TransactionDate = entry.JournalVoucher.EntryDate,
                    CreditableDate = entry.JournalVoucher.CreditableDate,
                    Description = FormatReportDescription(entry),
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
                    BookerName = entry.JournalVoucher.Booker?.BookerName ?? string.Empty,
                    LocationId = dcEntry?.LocationId,
                    LocationName = dcEntry?.Location?.LocationName ?? string.Empty,
                    QtyBalance = request.ShowQtyBalance ? accountQtyBalance[entry.AccountId] : 0,
                    Rate = entry.Rate
                });
            }

            result.TotalDebit = 0;
            result.TotalCredit = 0;
            result.NetBalance = runningBalance;

            result.TotalRecords = filteredCount + (includeOpening ? 1 : 0);
            result.Page = page;
            result.PageSize = pageSize;
            result.HasMoreData = (page + 1) * pageSize < filteredCount;

            return result;
        }

        public async Task<List<Project>> GetAllProjectsForUserAsync(int userId) {
            return await _unitOfWork.Projects.GetAllForUserAsync(userId);
        }

        public async Task<List<Category>> GetAllCategoriesAsync() {
            return await _unitOfWork.Categories.GetAllAsync();
        }

        public async Task<IEnumerable<Account>> GetAccountsByCategoryAsync(int categoryId) {
            try {
                return await _unitOfWork.Accounts.GetByCategoryIdAsync(categoryId);
            } catch (Exception ex) {
                throw new Exception($"Error retrieving accounts for category ID {categoryId}: {ex.Message}", ex);
            }
        }

        public async Task<IEnumerable<Category>> GetCategoriesByProjectAsync(int projectId) {
            return await _unitOfWork.Categories.GetByProjectIdAsync(projectId);
        }

        public async Task<IEnumerable<Category>> GetCategoriesByProjectForUserAsync(int projectId, int userId) {
            return await _unitOfWork.Categories.GetByProjectIdForUserAsync(projectId, userId);
        }

        public async Task<(bool Exists, bool IsBaseVoucher)> ValidateVoucherAsync(int journalVoucherId, VoucherType type) {
            await _approvalGuard.EnsureVoucherNotPendingAsync(journalVoucherId);
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null) {
                return (false, false);
            }

            if (type != VoucherType.JV) {

                var report = await GetSingleDocReportAsync(journalVoucherId, type);
                return (report != null, voucher.IsBaseVoucher);
            }

            return (true, voucher.IsBaseVoucher);
        }

        private int? ParseNullableInt(string? value) {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (int.TryParse(value, out int result))
                return result;

            return null;
        }

        #endregion

        public async Task<decimal> GetAccountBalanceBeforeVoucherAsync(int journalVoucherId) {
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

        public async Task<ProductReportViewModel> GetProductReportAsync(ProductReportRequest request) {
            if (string.IsNullOrEmpty(request.ProjectId)) {
                return new ProductReportViewModel();
            }

            int projectId = int.Parse(request.ProjectId);
            int? categoryId = !string.IsNullOrEmpty(request.CategoryId) ? int.Parse(request.CategoryId) : null;
            int? accountId = !string.IsNullOrEmpty(request.AccountId) ? int.Parse(request.AccountId) : null;
            int? locationId = !string.IsNullOrEmpty(request.LocationId) ? int.Parse(request.LocationId) : null;

            DateOnly startDate = request.StartDate ?? DateOnly.MinValue;
            DateOnly endDate = request.EndDate ?? DateOnly.MaxValue;

            int page = Math.Max(0, request.Page);
            int pageSize = Math.Clamp(request.PageSize, 1, 2500);

            // Select products from their complete as-of position before paging. A
            // product can have stock (including transit) without period activity.
            var allHistoricalEntries = await _unitOfWork.JournalEntries.GetAllEntriesForProductReportAsync(
                DateOnly.MinValue, endDate, projectId, categoryId, accountId, locationId);
            var transitMovements = locationId.HasValue
                ? new List<ProductTransitMovementDto>()
                : await _unitOfWork.JournalEntries.GetProductTransitMovementsAsync(
                    projectId, endDate, categoryId, accountId);
            var transitByProduct = transitMovements.GroupBy(x => x.ProductId)
                .ToDictionary(x => x.Key, x => x.Sum(movement => movement.Quantity));
            var productIds = allHistoricalEntries.Select(x => x.Entry.AccountId)
                .Distinct().OrderBy(id => id).ToList();
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

            foreach (var productId in productIds) {
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
                var prevCommercialDebitEntries = prevDebitEntries
                    .Where(x => !x.Entry.JournalVoucher.IsStockTransfer || x.Entry.Quantity == 0)
                    .ToList();
                var prevPhysicalCreditEntries = entriesBeforeStartDate.Where(x => x.Entry.Credit > 0).ToList();
                var prevSaleCreditEntries = prevPhysicalCreditEntries
                    .Where(x => !x.Entry.JournalVoucher.IsStockTransfer).ToList();

                int prevDebitQty = prevDebitEntries.Sum(x => x.Entry.Quantity);
                int prevCreditQty = prevPhysicalCreditEntries.Sum(x => x.Entry.Quantity);
                int prevQtyBalance = prevDebitQty - prevCreditQty;

                // Quantity-bearing transfer debits are internal receipts/returns, not
                // purchases. Quantity-zero product debits are landed charges and remain
                // part of the product's commercial cost.
                decimal prevTotalDebit = prevCommercialDebitEntries.Sum(x => x.Entry.Debit);
                decimal prevTotalCredit = prevSaleCreditEntries.Sum(x => x.Entry.Credit);

                var currentDebitEntries = entriesInDateRange.Where(x => x.Entry.Debit > 0).ToList();
                var currentCommercialDebitEntries = currentDebitEntries
                    .Where(x => !x.Entry.JournalVoucher.IsStockTransfer || x.Entry.Quantity == 0)
                    .ToList();
                var currentPhysicalCreditEntries = entriesInDateRange.Where(x => x.Entry.Credit > 0).ToList();
                var currentSaleCreditEntries = currentPhysicalCreditEntries
                    .Where(x => !x.Entry.JournalVoucher.IsStockTransfer).ToList();

                int currentPhysicalDebitQty = currentDebitEntries.Sum(x => x.Entry.Quantity);
                int currentPhysicalCreditQty = currentPhysicalCreditEntries.Sum(x => x.Entry.Quantity);
                decimal currentDebit = currentCommercialDebitEntries.Sum(x => x.Entry.Debit);
                decimal currentCredit = currentSaleCreditEntries.Sum(x => x.Entry.Credit);

                int totalProductDebitQty = prevDebitQty + currentPhysicalDebitQty;
                int totalProductCreditQty = prevCreditQty + currentPhysicalCreditQty;
                int qtyBalance = totalProductDebitQty - totalProductCreditQty;

                decimal prevAvgRate;
                decimal averageRate;
                decimal inventoryValue;
                decimal cogs;
                if (!productEntries.Any(x => x.Entry.JournalVoucher.IsStockTransfer))
                {
                    // Preserve the established report calculation exactly for historical
                    // products that have no transfer movements.
                    prevAvgRate = prevDebitQty > 0 ? prevTotalDebit / prevDebitQty : 0;
                    averageRate = totalProductDebitQty > 0
                        ? (prevTotalDebit + currentDebit) / totalProductDebitQty
                        : prevAvgRate;
                    cogs = averageRate * currentPhysicalCreditQty;
                    inventoryValue = averageRate * qtyBalance;
                }
                else
                {
                    // Keep the established period-average formula. An all-warehouse
                    // transfer is neutral to the cost base; within one warehouse its
                    // receipt/return supplies both quantity and posted cost.
                    var previousCostDebits = locationId.HasValue
                        ? prevDebitEntries
                        : prevCommercialDebitEntries;
                    var currentCostDebits = locationId.HasValue
                        ? currentDebitEntries
                        : currentCommercialDebitEntries;
                    int previousCostQuantity = previousCostDebits.Sum(x => x.Entry.Quantity);
                    int totalCostQuantity = previousCostQuantity +
                        currentCostDebits.Sum(x => x.Entry.Quantity);
                    decimal previousCost = previousCostDebits.Sum(x => x.Entry.Debit);
                    decimal totalCost = previousCost + currentCostDebits.Sum(x => x.Entry.Debit);
                    prevAvgRate = previousCostQuantity > 0
                        ? previousCost / previousCostQuantity : 0;
                    averageRate = totalCostQuantity > 0
                        ? totalCost / totalCostQuantity : prevAvgRate;
                    cogs = averageRate * currentSaleCreditEntries.Sum(x => x.Entry.Quantity);
                    // Preserve the posted transfer value when it differs from the
                    // report's average rate. Only ordinary outward quantity incurs COGS.
                    var allSaleQuantity = productEntries
                        .Where(x => x.Entry.Credit > 0 && !x.Entry.JournalVoucher.IsStockTransfer)
                        .Sum(x => x.Entry.Quantity);
                    inventoryValue = productEntries.Sum(x => x.Entry.Debit) -
                        productEntries.Where(x => x.Entry.Credit > 0 &&
                                                  x.Entry.JournalVoucher.IsStockTransfer)
                            .Sum(x => x.Entry.Credit) - averageRate * allSaleQuantity;
                }
                decimal margin = currentCredit - cogs;
                decimal marginPercentage = currentCredit > 0 ? (margin / currentCredit) * 100 : 0;

                var summary = new ProductSummaryViewModel {
                    ProductId = productId,
                    ProductName = productName,
                    PreviousDebitQty = prevDebitQty,
                    PreviousCreditQty = prevCreditQty,
                    PreviousBalance = prevQtyBalance,
                    PreviousDebit = prevTotalDebit,
                    PreviousCredit = prevTotalCredit,
                    PreviousAvgRate = prevAvgRate,
                    // Quantity columns describe physical warehouse movements, including
                    // transfers; monetary debit/credit columns remain commercial amounts.
                    TotalDebitQty = currentPhysicalDebitQty,
                    TotalCreditQty = currentPhysicalCreditQty,
                    QtyBalance = qtyBalance,
                    AverageRate = averageRate,
                    InventoryValue = inventoryValue,
                    PhysicalOutwardQuantity = currentPhysicalCreditQty,
                    TransferOutwardQuantity = currentPhysicalCreditEntries
                        .Where(x => x.Entry.JournalVoucher.IsStockTransfer).Sum(x => x.Entry.Quantity),
                    TransferInwardQuantity = currentDebitEntries
                        .Where(x => x.Entry.JournalVoucher.IsStockTransfer && x.Entry.Quantity > 0).Sum(x => x.Entry.Quantity),
                    InTransitQuantity = transitByProduct.GetValueOrDefault(productId),
                    TotalDebit = currentDebit,
                    TotalCredit = currentCredit,
                    Balance = currentDebit - currentCredit,
                    COGS = cogs,
                    Margin = margin,
                    MarginPercentage = marginPercentage,
                    HasPreviousBalance = prevDebitQty > 0 || prevCreditQty > 0
                };

                // Preserve period-activity rows while also showing carried stock.
                if (entriesInDateRange.Count == 0 && qtyBalance == 0 && summary.InTransitQuantity == 0)
                    continue;
                productSummaries.Add(summary);

                totalDebit += currentDebit;
                totalCredit += currentCredit;
                totalDebitQty += currentPhysicalDebitQty;
                totalCreditQty += currentPhysicalCreditQty;
                totalCOGS += cogs;
                totalMargin += margin;
            }

            if (request.ExcludeNilProducts) {
                productSummaries = productSummaries.Where(p => p.QtyBalance != 0 || p.InTransitQuantity != 0).ToList();

                // Recalculate totals from filtered products
                totalDebit = productSummaries.Sum(p => p.TotalDebit);
                totalCredit = productSummaries.Sum(p => p.TotalCredit);
                totalDebitQty = productSummaries.Sum(p => p.TotalDebitQty);
                totalCreditQty = productSummaries.Sum(p => p.TotalCreditQty);
                totalCOGS = productSummaries.Sum(p => p.COGS);
                totalMargin = productSummaries.Sum(p => p.Margin);
            }

            var totalCount = productSummaries.Count;
            decimal totalMarginPercentage = totalCredit > 0 ? (totalMargin / totalCredit) * 100 : 0;

            return new ProductReportViewModel {
                ProductSummaries = productSummaries.Skip(page * pageSize).Take(pageSize)
                    .OrderBy(p => p.ProductName).ToList(),
                Totals = new ProductReportTotalsViewModel {
                    TotalDebit = totalDebit,
                    TotalCredit = totalCredit,
                    Balance = totalDebit - totalCredit,
                    TotalDebitQty = totalDebitQty,
                    TotalCreditQty = totalCreditQty,
                    NetQuantity = totalDebitQty - totalCreditQty,
                    BalanceQuantity = productSummaries.Sum(p => p.QtyBalance),
                    TotalCOGS = totalCOGS,
                    TotalMargin = totalMargin,
                    MarginPercentage = totalMarginPercentage
                },
                TotalRecords = totalCount,
                Page = page,
                PageSize = pageSize,
                HasMoreData = (page + 1) * pageSize < totalCount
            };
        }

        public async Task<ProductStockBreakdownViewModel> GetProductStockBreakdownAsync(
            int projectId,
            int productId,
            DateOnly? endDate) {
            if (projectId <= 0)
                throw new ArgumentException("A valid project is required.", nameof(projectId));
            if (productId <= 0)
                throw new ArgumentException("A valid product is required.", nameof(productId));

            var product = await _unitOfWork.Accounts.GetByIdWithCategoryAsync(productId);
            if (product?.Category?.ProjectId != projectId || product.AccountGroupId != 2)
                throw new KeyNotFoundException("Product was not found in the selected project.");

            // PostgreSQL REPEATABLE READ gives the warehouse and transit queries
            // one committed snapshot during a concurrent transfer edit or delete.
            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead)
                : null;
            var movements = await _unitOfWork.JournalEntries.GetProductStockLocationMovementsAsync(
                projectId, productId, endDate ?? DateOnly.MaxValue);
            var transitMovements = await _unitOfWork.JournalEntries.GetProductTransitMovementsAsync(
                projectId, endDate ?? DateOnly.MaxValue, productId: productId);

            var locationNames = new Dictionary<int, string>();
            var movementsByLocation = new Dictionary<int, List<ProductStockLocationMovementDto>>();
            var unassignedMovements = new List<ProductStockLocationMovementDto>();
            var ambiguousMovements = new List<ProductStockLocationMovementDto>();
            var uniqueMovements = new List<ProductStockLocationMovementDto>();
            var assignmentIssueCount = 0;

            foreach (var entryGroup in movements.GroupBy(x => x.JournalEntryId)) {
                var movement = entryGroup.First();
                uniqueMovements.Add(movement);

                var assignments = entryGroup.Where(x => x.LocationId.HasValue).ToList();
                if (assignments.Count == 0) {
                    unassignedMovements.Add(movement);
                    if (HasInventoryImpact(movement))
                        assignmentIssueCount++;
                    continue;
                }

                if (assignments.Count != 1) {
                    ambiguousMovements.Add(movement);
                    if (HasInventoryImpact(movement))
                        assignmentIssueCount++;
                    continue;
                }

                var assignment = assignments[0];
                var locationId = assignment.LocationId!.Value;
                var locationName = string.IsNullOrWhiteSpace(assignment.LocationName)
                    ? $"Location {locationId}"
                    : assignment.LocationName;
                locationNames[locationId] = locationName;
                if (!movementsByLocation.TryGetValue(locationId, out var locationMovements)) {
                    locationMovements = new List<ProductStockLocationMovementDto>();
                    movementsByLocation[locationId] = locationMovements;
                }
                locationMovements.Add(movement);
            }

            var locations = new List<ProductStockLocationViewModel>();
            foreach (var location in movementsByLocation) {
                var quantity = location.Value.Sum(SignedQuantity);
                if (quantity == 0)
                    continue;
                locations.Add(new ProductStockLocationViewModel {
                    LocationId = location.Key,
                    LocationName = locationNames[location.Key],
                    Quantity = quantity
                });
            }
            locations = locations.OrderBy(x => x.LocationName).ToList();

            var totalQuantity = uniqueMovements.Sum(SignedQuantity);
            var unassignedQuantity = unassignedMovements.Sum(SignedQuantity);
            var ambiguousQuantity = ambiguousMovements.Sum(SignedQuantity);
            var categorizedQuantity = locations.Sum(x => x.Quantity) +
                                      unassignedQuantity + ambiguousQuantity;

            var result = new ProductStockBreakdownViewModel {
                ProductId = product.AccountId,
                ProductName = product.AccountName,
                AsOfDate = endDate,
                TotalQuantity = totalQuantity,
                InTransitQuantity = transitMovements.Sum(x => x.Quantity),
                TotalStockPosition = totalQuantity + transitMovements.Sum(x => x.Quantity),
                TransitRouteIssueCount = transitMovements.Count(x => x.HasRouteIssue),
                UnassignedQuantity = unassignedQuantity,
                AmbiguousQuantity = ambiguousQuantity,
                ReconciliationDifference = totalQuantity - categorizedQuantity,
                AssignmentIssueCount = assignmentIssueCount,
                Locations = locations,
                InTransitRoutes = transitMovements.OrderBy(x => x.DispatchDate)
                    .ThenBy(x => x.DispatchJournalVoucherId)
                    .Select(x => new ProductTransitRouteViewModel {
                        DispatchJournalVoucherId = x.DispatchJournalVoucherId,
                        DispatchDate = x.DispatchDate,
                        SourceLocationId = x.SourceLocationId,
                        SourceLocationName = x.SourceLocationName,
                        DestinationLocationId = x.DestinationLocationId,
                        DestinationLocationName = x.DestinationLocationName,
                        Quantity = x.Quantity,
                        HasRouteIssue = x.HasRouteIssue
                    }).ToList()
            };
            if (transaction != null)
                await transaction.CommitAsync();
            return result;
        }

        private static bool HasInventoryImpact(ProductStockLocationMovementDto movement) =>
            movement.Quantity != 0;

        private static int SignedQuantity(ProductStockLocationMovementDto movement) =>
            movement.Debit > 0 ? movement.Quantity : movement.Credit > 0 ? -movement.Quantity : 0;

        public async Task<List<Location>> GetAllLocationsAsync() {
            return await _unitOfWork.Locations.GetAllAsync();
        }

        public async Task<ProjectCartageSettings?> GetProjectCartageSettingsAsync(int projectId) {
            return await _unitOfWork.ProjectCartageSettings.GetByProjectIdAsync(projectId);
        }

        public async Task<JournalVoucher?> GetJournalVoucherAsync(int journalVoucherId) {
            return await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
        }

        public async Task<List<Booker>> GetAllBookersAsync() {
            return await _unitOfWork.Bookers.GetAllAsync();
        }

        #region Missed Entries

        public async Task<List<MissedEntryViewModel>> GetFilteredMissedEntriesAsync(
            DateOnly? startDate,
            DateOnly? endDate,
            int? projectId) {
            var entries = await _unitOfWork.MissedEntries.GetFilteredEntriesAsync(startDate, endDate, projectId);

            return entries.Select(e => new MissedEntryViewModel {
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
    int? projectId) {
            var entries = await _unitOfWork.MissedEntries.GetFilteredEntriesAsync(startDate, endDate, projectId);

            int totalMissedEntries = entries.Count;
            int totalMissedQuantity = entries.Sum(e => e.Quantity);
            decimal totalMissedValue = entries.Sum(e =>
            {
                var pkg = e.ProductAccount?.ProductData?.Pkg ?? 0;
                var baseAmount = (e.Rbp == "Y" && pkg > 0)
                    ? (decimal)pkg * e.Quantity * e.Rate
                    : (decimal)e.Quantity * e.Rate;
                return baseAmount - (baseAmount * e.Discount / 100m);
            });
            int uniqueCustomers = entries.Select(e => e.CustomerAccountId).Distinct().Count();
            int uniqueProducts = entries.Select(e => e.ProductAccountId).Distinct().Count();
            decimal averageQuantityPerEntry = totalMissedEntries > 0 ? Math.Round((decimal)totalMissedQuantity / totalMissedEntries, 2) : 0;
            decimal averageValuePerEntry = totalMissedEntries > 0 ? Math.Round(totalMissedValue / totalMissedEntries, 2) : 0;

            var customerGroups = entries
                .Where(e => e.CustomerAccountId > 0)
                .GroupBy(e => e.CustomerAccountId)
                .Select(g => new {
                    CustomerAccountId = g.Key,
                    Name = g.FirstOrDefault()?.CustomerAccount?.AccountName ?? "Unknown",
                    Quantity = g.Sum(x => x.Quantity),
                    EntryCount = g.Count()
                })
                .OrderByDescending(x => x.Quantity)
                .ToList();

            var topCustomerGroup = customerGroups.FirstOrDefault();

            var productGroups = entries
                .Where(e => e.ProductAccountId > 0)
                .GroupBy(e => e.ProductAccountId)
                .Select(g => new {
                    ProductAccountId = g.Key,
                    Name = g.FirstOrDefault()?.ProductAccount?.AccountName ?? "Unknown",
                    Quantity = g.Sum(x => x.Quantity),
                    EntryCount = g.Count()
                })
                .OrderByDescending(x => x.Quantity)
                .ToList();

            var topProductGroup = productGroups.FirstOrDefault();

            return new {
                totalMissedEntries,
                totalMissedQuantity,
                totalMissedValue,
                uniqueCustomers,
                uniqueProducts,
                averageQuantityPerEntry,
                averageValuePerEntry,
                topCustomer = topCustomerGroup?.Name ?? null,
                topCustomerMissedQuantity = topCustomerGroup?.Quantity ?? 0,
                topCustomerEntryCount = topCustomerGroup?.EntryCount ?? 0,
                topMissedProduct = topProductGroup?.Name ?? null,
                topMissedProductQuantity = topProductGroup?.Quantity ?? 0,
                topProductEntryCount = topProductGroup?.EntryCount ?? 0,
                top5Customers = customerGroups.Take(5).Select(c => new {
                    name = c.Name,
                    quantity = c.Quantity,
                    entryCount = c.EntryCount
                }),
                top5Products = productGroups.Take(5).Select(p => new {
                    name = p.Name,
                    quantity = p.Quantity,
                    entryCount = p.EntryCount
                })
            };
        }

        public async Task<List<object>> GetMissedEntriesByCustomerAsync(
            DateOnly? startDate,
            DateOnly? endDate,
            int? projectId) {
            var grouped = await _unitOfWork.MissedEntries.GetMissedEntriesByCustomerGroupedAsync(startDate, endDate, projectId);

            var result = grouped.Select(g => (object)new {
                customerAccountId = g.CustomerAccountId,
                customerName = g.CustomerName,
                entryCount = g.EntryCount,
                totalQuantity = g.TotalQuantity,
                totalValue = g.TotalValue,
                uniqueProductCount = g.UniqueProductCount,
                averageQuantityPerEntry = Math.Round(g.AverageQuantityPerEntry, 2)
            })
            .OrderByDescending(o => {
                dynamic d = o;
                return (long)d.totalQuantity;
            })
            .ToList();

            return result;
        }

        public async Task<List<object>> GetMissedEntriesByProductAsync(
            DateOnly? startDate,
            DateOnly? endDate,
            int? projectId) {
            var grouped = await _unitOfWork.MissedEntries.GetMissedEntriesByProductGroupedAsync(startDate, endDate, projectId);

            var result = grouped.Select(g => (object)new {
                productAccountId = g.ProductAccountId,
                productName = g.ProductName,
                entryCount = g.EntryCount,
                totalQuantity = g.TotalQuantity,
                totalValue = g.TotalValue,
                uniqueCustomerCount = g.UniqueCustomerCount,
                averageRate = Math.Round(g.AverageRate, 2),
                averageDiscount = Math.Round(g.AverageDiscount, 2)
            })
            .OrderByDescending(o => {
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
            int? projectId) {
            var entries = await _unitOfWork.MissedEntries.GetByCustomerAccountIdAsync(customerAccountId);

            var filtered = entries
                .Where(e => (!startDate.HasValue || e.MissedDate >= startDate.Value)
                         && (!endDate.HasValue || e.MissedDate <= endDate.Value)
                         && (!projectId.HasValue || e.ProjectId == projectId.Value))
                .Select(e => {
                    var pkg = e.ProductAccount?.ProductData?.Pkg ?? 0;
                    var baseAmount = (e.Rbp == "Y" && pkg > 0)
                        ? (decimal)pkg * e.Quantity * e.Rate
                        : (decimal)e.Quantity * e.Rate;
                    var totalValue = baseAmount - (baseAmount * e.Discount / 100m);
                    return new {
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
                        totalValue
                    };
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
            int pageSize) {
            var (entries, totalCount) = await _unitOfWork.MissedEntries.GetFilteredEntriesPaginatedAsync(
                startDate, endDate, projectId, page, pageSize);

            // Repository already returns lightweight MissedEntryViewModel, so no further projection needed
            var viewModels = entries ?? new List<MissedEntryViewModel>();

            return new MissedEntriesPagedResultViewModel {
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
