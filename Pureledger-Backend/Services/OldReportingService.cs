using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.Reporting;

namespace PureLedger.Services
{
    public class OldReportingService : IOldReportingService
    {
        private sealed class QtyLedgerRow
        {
            public string AccountId { get; init; } = string.Empty;
            public int Qty { get; init; }
            public decimal Debit { get; init; }
        }

        private readonly IServiceProvider _serviceProvider;

        public OldReportingService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        private IOldLedgerDbContext GetContext(string? legacyDatabase = null)
        {
            if (string.Equals(legacyDatabase?.Trim(), "ghazi2", StringComparison.OrdinalIgnoreCase))
            {
                return _serviceProvider.GetRequiredService<OldLedgerDb2Context>();
            }
            return _serviceProvider.GetRequiredService<OldLedgerDbContext>();
        }

        public async Task<OldMasterReportResponse> GetOldMasterReportAsync(OldMasterReportRequest request, string? legacyDatabase = null)
        {
            using var context = GetContext(legacyDatabase);
            int page = Math.Max(0, request.Page);
            int pageSize = Math.Clamp(request.PageSize, 1, 2500);

            var filteredQuery = BuildFilteredQuery(request, context);
            int totalCount = await filteredQuery.CountAsync();

            var (totalDebit, totalCredit) = await GetAggregateTotalsAsync(filteredQuery);
            decimal openingBalance = await GetOpeningBalanceAsync(request, context);

            decimal previousBalance = openingBalance;
            if (page > 0)
            {
                previousBalance += await GetPreviousEntriesBalanceAsync(filteredQuery, page, pageSize);
            }

            var qtyBalances = request.ShowQtyBalance
                ? await GetPreviousQtyBalancesAsync(filteredQuery, page, pageSize)
                : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var pagedEntries = await ApplyOrdering(filteredQuery)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();

            bool includeOpening = page == 0;
            decimal runningBalance = previousBalance;
            var resultEntries = new List<OldMasterReportEntryViewModel>();

            if (includeOpening)
            {
                var openingDate = request.StartDate ?? DateOnly.MinValue;

                resultEntries.Add(new OldMasterReportEntryViewModel
                {
                    Id = 0,
                    MasterId = 0,
                    JournalVoucherId = 0,
                    FormattedId = "OPENING",
                    TransactionDate = openingDate,
                    CreditableDate = openingDate,
                    Description = "Opening Balance",
                    ProjectId = ParseNullableInt(NormalizeProjectId(request)),
                    ProjectName = ResolveOpeningProjectName(request, pagedEntries),
                    CategoryId = NormalizeCategoryId(request),
                    CategoryName = ResolveOpeningCategoryName(request, pagedEntries),
                    AccountId = NormalizeAccountId(request),
                    AccountName = ResolveOpeningAccountName(request, pagedEntries),
                    Quantity = 0,
                    Rate = 0,
                    Debit = 0,
                    Credit = 0,
                    Balance = openingBalance,
                    LocationName = ResolveOpeningLocationName(request, pagedEntries),
                    BookerId = ParseNullableInt(NormalizeBookerId(request)),
                    BookerName = ResolveOpeningBookerName(request, pagedEntries),
                    QtyBalance = 0
                });
            }

            foreach (var entry in pagedEntries)
            {
                decimal debit = entry.Debit ?? 0m;
                decimal credit = entry.Credit ?? 0m;
                int quantity = entry.Qty ?? 0;
                string accountKey = entry.AccountId ?? string.Empty;

                runningBalance += debit - credit;

                if (request.ShowQtyBalance)
                {
                    if (!qtyBalances.ContainsKey(accountKey))
                    {
                        qtyBalances[accountKey] = 0;
                    }

                    int qtyImpact = debit > 0 ? quantity : -quantity;
                    qtyBalances[accountKey] += qtyImpact;
                }

                resultEntries.Add(new OldMasterReportEntryViewModel
                {
                    Id = entry.Id,
                    MasterId = entry.MasterId,
                    JournalVoucherId = entry.MasterId,
                    FormattedId = BuildFormattedId(entry),
                    TransactionDate = entry.EntryDate ?? DateOnly.MinValue,
                    CreditableDate = entry.DueDate ?? entry.EntryDate ?? DateOnly.MinValue,
                    Description = entry.Des,
                    ProjectId = entry.ProjectId,
                    ProjectName = entry.ProjectName,
                    CategoryId = NormalizeLegacyHierarchyId(entry.CategoryId),
                    CategoryName = entry.CategoryName,
                    AccountId = NormalizeLegacyHierarchyId(entry.AccountId),
                    AccountName = entry.AccountName,
                    Quantity = quantity,
                    Rate = entry.Rate ?? 0m,
                    Debit = debit,
                    Credit = credit,
                    Balance = runningBalance,
                    LocationName = entry.Location,
                    BookerId = entry.BookerId,
                    BookerName = entry.BookerName,
                    QtyBalance = request.ShowQtyBalance
                        ? qtyBalances.GetValueOrDefault(accountKey)
                        : 0
                });
            }

            decimal netBalance = openingBalance + (totalDebit - totalCredit);

            return new OldMasterReportResponse
            {
                Entries = resultEntries,
                TotalDebit = totalDebit,
                TotalCredit = totalCredit,
                NetBalance = netBalance,
                PreviousBalance = previousBalance,
                TotalRecords = totalCount + (includeOpening ? 1 : 0),
                Page = page,
                PageSize = pageSize,
                HasMoreData = (page + 1) * pageSize < totalCount
            };
        }

        public async Task<OldMasterReportInitResponse> GetOldMasterReportInitAsync(string? legacyDatabase = null)
        {
            using var context = GetContext(legacyDatabase);

            var projects = await context.JournalEntries
                .AsNoTracking()
                .Where(e => e.ProjectId.HasValue && !string.IsNullOrWhiteSpace(e.ProjectName))
                .Select(e => new { e.ProjectId, e.ProjectName })
                .Distinct()
                .OrderBy(e => e.ProjectName)
                .ToListAsync();

            var categories = await context.JournalEntries
                .AsNoTracking()
                .Where(e => !string.IsNullOrWhiteSpace(e.CategoryName))
                .Select(e => e.CategoryName!)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync();

            var categoryOptions = categories
                .Select(name => new OldReportLookupOption
                {
                    Id = name,
                    Name = name
                })
                .ToList();

            var accounts = await context.JournalEntries
                .AsNoTracking()
                .Where(e => !string.IsNullOrWhiteSpace(e.AccountName))
                .Select(e => e.AccountName!)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync();

            var accountOptions = accounts
                .Select(name => new OldReportLookupOption
                {
                    Id = name,
                    Name = name
                })
                .ToList();

            var bookers = await context.JournalEntries
                .AsNoTracking()
                .Where(e => !string.IsNullOrWhiteSpace(e.BookerName))
                .Select(e => new { e.BookerId, e.BookerName })
                .Distinct()
                .OrderBy(e => e.BookerName)
                .ToListAsync();

            var locations = await context.JournalEntries
                .AsNoTracking()
                .Where(e => !string.IsNullOrWhiteSpace(e.Location))
                .Select(e => e.Location!)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync();

            return new OldMasterReportInitResponse
            {
                Projects = projects
                    .Select(p => new OldReportLookupOption
                    {
                        Id = p.ProjectId!.Value.ToString(),
                        Name = p.ProjectName!
                    })
                    .ToList(),
                Categories = categoryOptions,
                Accounts = accountOptions,
                Bookers = bookers
                    .Select(b => new OldReportLookupOption
                    {
                        Id = b.BookerId?.ToString() ?? $"name:{b.BookerName}",
                        Name = b.BookerName!
                    })
                    .ToList(),
                Locations = locations
                    .Select(location => new OldReportLookupOption
                    {
                        Id = location,
                        Name = location
                    })
                    .ToList(),
                ProjectNames = projects
                    .Select(p => p.ProjectName!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name)
                    .ToList(),
                CategoryNames = categories,
                BookerNames = bookers
                    .Select(b => b.BookerName!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name)
                    .ToList(),
                AccountIds = accounts,
                LocationsLegacy = locations
            };
        }

        public async Task<List<OldReportLookupOption>> GetCategoriesByProjectAsync(int projectId, string? legacyDatabase = null)
        {
            using var context = GetContext(legacyDatabase);

            var categories = await context.JournalEntries
                .AsNoTracking()
                .Where(e => e.ProjectId == projectId && !string.IsNullOrWhiteSpace(e.CategoryName))
                .Select(e => e.CategoryName!)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync();

            return categories
                .Select(name =>
                {
                    return new OldReportLookupOption
                    {
                        Id = name,
                        Name = name
                    };
                })
                .ToList();
        }

        public async Task<List<OldReportLookupOption>> GetAccountsByCategoryAsync(string categoryName, string? legacyDatabase = null)
        {
            using var context = GetContext(legacyDatabase);

            var accounts = await context.JournalEntries
                .AsNoTracking()
                .Where(e => !string.IsNullOrWhiteSpace(e.AccountName)
                    && e.CategoryName != null && e.CategoryName == categoryName)
                .Select(e => e.AccountName!)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync();

            return accounts
                .Select(name => new OldReportLookupOption
                {
                    Id = name,
                    Name = name
                })
                .ToList();
        }

        private IQueryable<LegacyJournalEntry> BuildFilteredQuery(OldMasterReportRequest request, IOldLedgerDbContext context)
        {
            var query = context.JournalEntries
                .AsNoTracking()
                .AsQueryable();

            query = ApplyProjectFilter(query, request);
            query = ApplyCategoryFilter(query, request);
            query = ApplyAccountFilter(query, request);
            query = ApplyBookerFilter(query, request);
            query = ApplyLocationFilter(query, request);

            if (request.StartDate.HasValue)
            {
                query = query.Where(e => e.EntryDate.HasValue && e.EntryDate.Value >= request.StartDate.Value);
            }

            if (request.EndDate.HasValue)
            {
                query = query.Where(e => e.EntryDate.HasValue && e.EntryDate.Value <= request.EndDate.Value);
            }

            // Exclude vouchers where any entry has dc='no'
            query = ApplyDcNoExclusionFilter(query, context);

            return query;
        }

        private IQueryable<LegacyJournalEntry> BuildPreStartQuery(OldMasterReportRequest request, IOldLedgerDbContext context)
        {
            var query = context.JournalEntries
                .AsNoTracking()
                .AsQueryable();

            query = ApplyProjectFilter(query, request);
            query = ApplyCategoryFilter(query, request);
            query = ApplyAccountFilter(query, request);

            if (request.StartDate.HasValue)
            {
                query = query.Where(e => e.EntryDate.HasValue && e.EntryDate.Value < request.StartDate.Value);
            }
            else
            {
                query = query.Where(_ => false);
            }

            // Exclude vouchers where any entry has dc='no'
            query = ApplyDcNoExclusionFilter(query, context);

            return query;
        }

        private IQueryable<LegacyJournalEntry> ApplyProjectFilter(IQueryable<LegacyJournalEntry> query, OldMasterReportRequest request)
        {
            string? normalizedProjectId = NormalizeProjectId(request);
            if (!string.IsNullOrWhiteSpace(normalizedProjectId) && int.TryParse(normalizedProjectId, out int projectId))
            {
                return query.Where(e => e.ProjectId == projectId);
            }

            string? legacyProjectName = NormalizeText(request.ProjectName);
            if (!string.IsNullOrWhiteSpace(legacyProjectName))
            {
                return query.Where(e => e.ProjectName == legacyProjectName);
            }

            return query;
        }

        private IQueryable<LegacyJournalEntry> ApplyCategoryFilter(IQueryable<LegacyJournalEntry> query, OldMasterReportRequest request)
        {
            string? categoryName = NormalizeText(request.CategoryName);
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                return query.Where(e => e.CategoryName == categoryName);
            }

            var categoryIds = BuildLegacyHierarchyFilterCandidates(request.CategoryId);
            if (categoryIds.Count > 0)
            {
                return query.Where(e => e.CategoryId != null && categoryIds.Contains(e.CategoryId));
            }

            return query;
        }

        private IQueryable<LegacyJournalEntry> ApplyAccountFilter(IQueryable<LegacyJournalEntry> query, OldMasterReportRequest request)
        {
            string? accountName = NormalizeText(request.AccountName);
            if (!string.IsNullOrWhiteSpace(accountName))
            {
                return query.Where(e => e.AccountName == accountName);
            }

            var accountIds = BuildLegacyHierarchyFilterCandidates(request.AccountId);
            if (accountIds.Count > 0)
            {
                query = query.Where(e => e.AccountId != null && accountIds.Contains(e.AccountId));
            }

            return query;
        }

        private IQueryable<LegacyJournalEntry> ApplyBookerFilter(IQueryable<LegacyJournalEntry> query, OldMasterReportRequest request)
        {
            string? bookerId = NormalizeBookerId(request);
            if (!string.IsNullOrWhiteSpace(bookerId))
            {
                if (int.TryParse(bookerId, out int parsedBookerId))
                {
                    return query.Where(e => e.BookerId == parsedBookerId);
                }

                string normalizedBookerName = StripBookerNamePrefix(bookerId);
                return query.Where(e => e.BookerName == normalizedBookerName);
            }

            string? legacyBookerName = NormalizeText(request.BookerName);
            if (!string.IsNullOrWhiteSpace(legacyBookerName))
            {
                return query.Where(e => e.BookerName == legacyBookerName);
            }

            return query;
        }

        private IQueryable<LegacyJournalEntry> ApplyLocationFilter(IQueryable<LegacyJournalEntry> query, OldMasterReportRequest request)
        {
            string? locationId = NormalizeLocationId(request);
            if (!string.IsNullOrWhiteSpace(locationId))
            {
                return query.Where(e => e.Location == locationId);
            }

            string? legacyLocation = NormalizeText(request.Location);
            if (!string.IsNullOrWhiteSpace(legacyLocation))
            {
                return query.Where(e => e.Location == legacyLocation);
            }

            return query;
        }

        private IQueryable<LegacyJournalEntry> ApplyDcNoExclusionFilter(IQueryable<LegacyJournalEntry> query, IOldLedgerDbContext context)
        {
            var excludedMasterIds = context.JournalEntries
                .Where(e => e.Dc == "no")
                .Select(e => e.MasterId)
                .Distinct()
                .ToList();

            if (excludedMasterIds.Count == 0)
                return query;

            return query.Where(e => !excludedMasterIds.Contains(e.MasterId));
        }

        private static IQueryable<LegacyJournalEntry> ApplyOrdering(IQueryable<LegacyJournalEntry> query)
        {
            return query
                .OrderBy(e => e.EntryDate ?? DateOnly.MinValue)
                .ThenBy(e => e.MasterId)
                .ThenBy(e => e.Id);
        }

        private async Task<(decimal TotalDebit, decimal TotalCredit)> GetAggregateTotalsAsync(IQueryable<LegacyJournalEntry> query)
        {
            var totals = await query
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalDebit = g.Sum(e => e.Debit ?? 0m),
                    TotalCredit = g.Sum(e => e.Credit ?? 0m)
                })
                .FirstOrDefaultAsync();

            return (totals?.TotalDebit ?? 0m, totals?.TotalCredit ?? 0m);
        }

        private async Task<decimal> GetOpeningBalanceAsync(OldMasterReportRequest request, IOldLedgerDbContext context)
        {
            if (!request.StartDate.HasValue)
            {
                return 0m;
            }

            var openingQuery = BuildPreStartQuery(request, context);
            var totals = await GetAggregateTotalsAsync(openingQuery);
            return totals.TotalDebit - totals.TotalCredit;
        }

        private async Task<decimal> GetPreviousEntriesBalanceAsync(
            IQueryable<LegacyJournalEntry> filteredQuery,
            int page,
            int pageSize)
        {
            if (page <= 0)
            {
                return 0m;
            }

            int takeCount = page * pageSize;
            var previousEntries = await ApplyOrdering(filteredQuery)
                .Take(takeCount)
                .Select(e => new
                {
                    Debit = e.Debit ?? 0m,
                    Credit = e.Credit ?? 0m
                })
                .ToListAsync();

            return previousEntries.Sum(e => e.Debit - e.Credit);
        }

        private async Task<Dictionary<string, int>> GetPreviousQtyBalancesAsync(
            IQueryable<LegacyJournalEntry> filteredQuery,
            int page,
            int pageSize)
        {
            var balances = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (page > 0)
            {
                int takeCount = page * pageSize;
                var previousEntries = await ApplyOrdering(filteredQuery)
                    .Take(takeCount)
                    .Where(e => !string.IsNullOrEmpty(e.AccountId))
                    .Select(e => new QtyLedgerRow
                    {
                        AccountId = e.AccountId!,
                        Qty = e.Qty ?? 0,
                        Debit = e.Debit ?? 0m
                    })
                    .ToListAsync();

                ApplyQtyImpacts(previousEntries, balances);
            }

            return balances;
        }

        private static void ApplyQtyImpacts(IEnumerable<QtyLedgerRow> entries, Dictionary<string, int> balances)
        {
            foreach (var entry in entries)
            {
                if (!balances.ContainsKey(entry.AccountId))
                {
                    balances[entry.AccountId] = 0;
                }

                balances[entry.AccountId] += entry.Debit > 0 ? entry.Qty : -entry.Qty;
            }
        }

        private static string? NormalizeText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string? NormalizeProjectId(OldMasterReportRequest request)
        {
            return NormalizeText(request.ProjectId);
        }

        private static string? NormalizeCategoryId(OldMasterReportRequest request)
        {
            return NormalizeLegacyHierarchyId(NormalizeText(request.CategoryId));
        }

        private static string? NormalizeAccountId(OldMasterReportRequest request)
        {
            return NormalizeLegacyHierarchyId(NormalizeText(request.AccountId));
        }

        private static string? NormalizeBookerId(OldMasterReportRequest request)
        {
            return NormalizeText(request.BookerId);
        }

        private static string? NormalizeLocationId(OldMasterReportRequest request)
        {
            return NormalizeText(request.LocationId);
        }

        private static int? ParseNullableInt(string? value)
        {
            return int.TryParse(value, out int parsedValue) ? parsedValue : null;
        }

        private static string StripBookerNamePrefix(string bookerId)
        {
            return bookerId.StartsWith("name:", StringComparison.OrdinalIgnoreCase)
                ? bookerId.Substring("name:".Length)
                : bookerId;
        }

        private static string? ResolveOpeningProjectName(OldMasterReportRequest request, List<LegacyJournalEntry> entries)
        {
            return NormalizeText(request.ProjectName)
                ?? entries.FirstOrDefault()?.ProjectName;
        }

        private static string? ResolveOpeningCategoryName(OldMasterReportRequest request, List<LegacyJournalEntry> entries)
        {
            return NormalizeText(request.CategoryName)
                ?? entries.FirstOrDefault()?.CategoryName;
        }

        private static string? ResolveOpeningAccountName(OldMasterReportRequest request, List<LegacyJournalEntry> entries)
        {
            return NormalizeText(request.AccountName)
                ?? entries.FirstOrDefault()?.AccountName;
        }

        private static string BuildFormattedId(LegacyJournalEntry entry)
        {
            return NormalizeLegacyHierarchyId(entry.AccountId)
                ?? NormalizeLegacyHierarchyId(entry.CategoryId)
                ?? entry.MasterId.ToString();
        }

        private static string? NormalizeLegacyHierarchyId(string? rawId)
        {
            var normalized = NormalizeText(rawId);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            var parts = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 3)
            {
                return normalized;
            }

            var reduced = parts.Where((_, index) => index != 1);
            return string.Join("-", reduced);
        }

        private static string? ExpandLegacyHierarchyId(string? normalizedId)
        {
            var normalized = NormalizeText(normalizedId);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            var parts = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
            {
                return normalized;
            }

            var expanded = parts.Take(1)
                .Concat(new[] { "1" })
                .Concat(parts.Skip(1));

            return string.Join("-", expanded);
        }

        private static List<string> BuildLegacyHierarchyFilterCandidates(string? requestedId)
        {
            var candidates = new List<string>();

            void AddCandidate(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                if (!candidates.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    candidates.Add(value);
                }
            }

            var rawId = NormalizeText(requestedId);
            var normalizedId = NormalizeLegacyHierarchyId(rawId);
            var expandedId = ExpandLegacyHierarchyId(normalizedId);

            AddCandidate(rawId);
            AddCandidate(normalizedId);
            AddCandidate(expandedId);

            return candidates;
        }

        private static string? ResolveOpeningLocationName(OldMasterReportRequest request, List<LegacyJournalEntry> entries)
        {
            return NormalizeText(request.Location)
                ?? NormalizeText(request.LocationId)
                ?? entries.FirstOrDefault()?.Location;
        }

        private static string? ResolveOpeningBookerName(OldMasterReportRequest request, List<LegacyJournalEntry> entries)
        {
            return NormalizeText(request.BookerName)
                ?? entries.FirstOrDefault()?.BookerName;
        }
    }
}
