using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.Reporting;
using System.Text.Json;

namespace PureLedger.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly LedgerDbContext _context;
        private readonly ILogger<DashboardService> _logger;

        public DashboardService(LedgerDbContext context, ILogger<DashboardService> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Dashboard Settings

        public async Task<DashboardSettingsViewModel?> GetSettingsAsync(int projectId)
        {
            var settings = await _context.DashboardSettings
                .Include(ds => ds.Project)
                .FirstOrDefaultAsync(ds => ds.ProjectId == projectId);

            if (settings == null)
                return null;

            return new DashboardSettingsViewModel
            {
                DashboardSettingsId = settings.DashboardSettingsId,
                ProjectId = settings.ProjectId,
                ProjectName = settings.Project?.ProjectName ?? "",
                RevenueCategoryIds = ParseCategoryIds(settings.RevenueCategoryIds),
                PurchaseCategoryIds = ParseCategoryIds(settings.PurchaseCategoryIds),
                ExpenseCategoryIds = ParseCategoryIds(settings.ExpenseCategoryIds),
                UseCustomCalculations = settings.UseCustomCalculations
            };
        }

        public async Task<DashboardSettingsViewModel> SaveSettingsAsync(DashboardSettingsUpdateRequest request)
        {
            var settings = await _context.DashboardSettings
                .FirstOrDefaultAsync(ds => ds.ProjectId == request.ProjectId);

            if (settings == null)
            {
                settings = new DashboardSettings
                {
                    ProjectId = request.ProjectId
                };
                _context.DashboardSettings.Add(settings);
            }

            settings.RevenueCategoryIds = SerializeCategoryIds(request.RevenueCategoryIds);
            settings.PurchaseCategoryIds = SerializeCategoryIds(request.PurchaseCategoryIds);
            settings.ExpenseCategoryIds = SerializeCategoryIds(request.ExpenseCategoryIds);
            settings.UseCustomCalculations = request.UseCustomCalculations;

            await _context.SaveChangesAsync();

            var project = await _context.Projects.FindAsync(request.ProjectId);

            return new DashboardSettingsViewModel
            {
                DashboardSettingsId = settings.DashboardSettingsId,
                ProjectId = settings.ProjectId,
                ProjectName = project?.ProjectName ?? "",
                RevenueCategoryIds = request.RevenueCategoryIds,
                PurchaseCategoryIds = request.PurchaseCategoryIds,
                ExpenseCategoryIds = request.ExpenseCategoryIds,
                UseCustomCalculations = settings.UseCustomCalculations
            };
        }

        public async Task<DashboardSettingsCategoriesResponse> GetCategoriesForSettingsAsync(int projectId)
        {
            var categories = await _context.Categories
                .Include(c => c.AccountType)
                .Where(c => c.ProjectId == projectId)
                .OrderBy(c => c.CategoryName)
                .Select(c => new CategoryOption
                {
                    CategoryId = c.CategoryId,
                    CategoryName = c.CategoryName,
                    AccountTypeName = c.AccountType.AccountTypeName
                })
                .ToListAsync();

            return new DashboardSettingsCategoriesResponse
            {
                // Revenue: typically from Revenue account type (4) or products
                RevenueCategories = categories.Where(c => c.AccountTypeName == "Revenue" || c.AccountTypeName == "Assets").ToList(),
                // Purchase/COGS: typically from Expense account type (5) or products
                PurchaseCategories = categories.Where(c => c.AccountTypeName == "Expense" || c.AccountTypeName == "Assets").ToList(),
                // Expenses: typically from Expense account type (5)
                ExpenseCategories = categories.Where(c => c.AccountTypeName == "Expense").ToList()
            };
        }

        private static List<int> ParseCategoryIds(string? json)
        {
            if (string.IsNullOrEmpty(json))
                return new List<int>();

            try
            {
                return JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>();
            }
            catch
            {
                return new List<int>();
            }
        }

        private static string? SerializeCategoryIds(List<int>? ids)
        {
            if (ids == null || ids.Count == 0)
                return null;

            return JsonSerializer.Serialize(ids);
        }

        private async Task<DashboardSettings?> GetSettingsEntityAsync(int projectId)
        {
            return await _context.DashboardSettings
                .FirstOrDefaultAsync(ds => ds.ProjectId == projectId);
        }

        #endregion

        public async Task<DashboardResponse> GetDashboardDataAsync(int? projectId, int days = 30)
        {
            var response = new DashboardResponse();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var startDate = today.AddDays(-days);
            var thisMonthStart = new DateOnly(today.Year, today.Month, 1);
            var lastMonthStart = thisMonthStart.AddMonths(-1);
            var lastMonthEnd = thisMonthStart.AddDays(-1);

            try
            {
                response.KPIs = await GetKPIsAsync(projectId, today, thisMonthStart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting KPIs");
                response.KPIs = new DashboardKPIs();
            }

            try
            {
                response.Trends = await GetTrendsAsync(projectId, startDate, today);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting trends");
                response.Trends = new List<DailyTrendItem>();
            }

            try
            {
                response.TopProducts = await GetTopProductsAsync(projectId, startDate, today);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top products");
                response.TopProducts = new List<TopProductItem>();
            }

            try
            {
                response.TopCustomers = await GetTopCustomersAsync(projectId, startDate, today);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top customers");
                response.TopCustomers = new List<TopCustomerItem>();
            }

            try
            {
                response.CategoryDistribution = await GetCategoryDistributionAsync(projectId, startDate, today);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting category distribution");
                response.CategoryDistribution = new List<CategoryDistribution>();
            }

            try
            {
                response.ProjectPerformance = await GetProjectPerformanceAsync(thisMonthStart, today, lastMonthStart, lastMonthEnd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting project performance");
                response.ProjectPerformance = new List<ProjectPerformance>();
            }

            try
            {
                response.RecentActivity = await GetRecentActivityAsync(projectId, 20);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent activity");
                response.RecentActivity = new List<RecentActivity>();
            }

            try
            {
                response.Alerts = await GetAlertsAsync(projectId, today);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting alerts");
                response.Alerts = new List<DashboardAlert>();
            }

            return response;
        }

        private async Task<DashboardKPIs> GetKPIsAsync(int? projectId, DateOnly today, DateOnly thisMonthStart)
        {
            var kpis = new DashboardKPIs();

            // Check for custom settings
            DashboardSettings? settings = null;
            if (projectId.HasValue)
            {
                settings = await GetSettingsEntityAsync(projectId.Value);
            }

            // Build base query using joins instead of Include
            var baseQuery = from je in _context.JournalEntries
                            join jv in _context.JournalVouchers on je.JournalVoucherId equals jv.JournalVoucherId
                            join a in _context.Accounts on je.AccountId equals a.AccountId
                            where jv.IsCompleted
                            select new { je, jv, a };

            if (projectId.HasValue)
            {
                baseQuery = baseQuery.Where(x => x.jv.ProjectId == projectId.Value);
            }

            if (settings?.UseCustomCalculations == true)
            {
                // Custom category-based calculations
                var revenueCategoryIds = ParseCategoryIds(settings.RevenueCategoryIds);
                var purchaseCategoryIds = ParseCategoryIds(settings.PurchaseCategoryIds);
                var expenseCategoryIds = ParseCategoryIds(settings.ExpenseCategoryIds);

                // Total Revenue (sum of Credits from revenue categories)
                if (revenueCategoryIds.Count > 0)
                {
                    kpis.TotalRevenue = await baseQuery
                        .Where(x => revenueCategoryIds.Contains(x.je.CategoryId))
                        .Select(x => (decimal?)x.je.Credit)
                        .SumAsync() ?? 0;
                }

                // Total Purchases (sum of Debits from purchase categories)
                if (purchaseCategoryIds.Count > 0)
                {
                    kpis.TotalPurchases = await baseQuery
                        .Where(x => purchaseCategoryIds.Contains(x.je.CategoryId))
                        .Select(x => (decimal?)x.je.Debit)
                        .SumAsync() ?? 0;
                }

                // Expenses (sum of Debits from expense categories)
                decimal expenses = 0;
                if (expenseCategoryIds.Count > 0)
                {
                    expenses = await baseQuery
                        .Where(x => expenseCategoryIds.Contains(x.je.CategoryId))
                        .Select(x => (decimal?)x.je.Debit)
                        .SumAsync() ?? 0;
                }

                // Net Profit = Revenue - Purchases - Expenses
                kpis.NetProfit = kpis.TotalRevenue - kpis.TotalPurchases - expenses;
            }
            else
            {
                // Default: Product entries (AccountGroupId = 2) for revenue/purchases
                var productQuery = baseQuery.Where(x => x.a.AccountGroupId == 2);

                // Total Revenue (sum of Credits on products = sales)
                kpis.TotalRevenue = await productQuery.Select(x => (decimal?)x.je.Credit).SumAsync() ?? 0;

                // Total Purchases (sum of Debits on products = purchases/COGS)
                kpis.TotalPurchases = await productQuery.Select(x => (decimal?)x.je.Debit).SumAsync() ?? 0;

                // Net Profit = Revenue - Purchases (simplified)
                kpis.NetProfit = kpis.TotalRevenue - kpis.TotalPurchases;
            }

            // Profit Margin
            kpis.ProfitMargin = kpis.TotalRevenue > 0
                ? Math.Round((kpis.NetProfit / kpis.TotalRevenue) * 100, 2)
                : 0;

            // Outstanding Receivables (customer balances where Debit > Credit)
            var customerQuery = baseQuery.Where(x => x.a.AccountGroupId == 1);
            var customerBalancesList = await customerQuery
                .GroupBy(x => x.je.AccountId)
                .Select(g => g.Sum(x => x.je.Debit) - g.Sum(x => x.je.Credit))
                .ToListAsync();
            kpis.OutstandingReceivables = customerBalancesList.Where(b => b > 0).Sum();

            // Missed Entries Count
            var missedEntriesQuery = _context.MissedEntries.AsQueryable();
            if (projectId.HasValue)
            {
                missedEntriesQuery = missedEntriesQuery.Where(m => m.ProjectId == projectId.Value);
            }
            kpis.MissedEntriesCount = await missedEntriesQuery.CountAsync();

            // Transactions Today
            var todayVouchersQuery = _context.JournalVouchers
                .Where(jv => jv.EntryDate == today && jv.IsCompleted);
            if (projectId.HasValue)
            {
                todayVouchersQuery = todayVouchersQuery.Where(jv => jv.ProjectId == projectId.Value);
            }
            kpis.TransactionsToday = await todayVouchersQuery.CountAsync();

            // Transactions This Month
            var thisMonthVouchersQuery = _context.JournalVouchers
                .Where(jv => jv.EntryDate >= thisMonthStart && jv.EntryDate <= today && jv.IsCompleted);
            if (projectId.HasValue)
            {
                thisMonthVouchersQuery = thisMonthVouchersQuery.Where(jv => jv.ProjectId == projectId.Value);
            }
            kpis.TransactionsThisMonth = await thisMonthVouchersQuery.CountAsync();

            return kpis;
        }

        private async Task<List<DailyTrendItem>> GetTrendsAsync(int? projectId, DateOnly startDate, DateOnly endDate)
        {
            // Check for custom settings
            DashboardSettings? settings = null;
            if (projectId.HasValue)
            {
                settings = await GetSettingsEntityAsync(projectId.Value);
            }

            List<DailyTrendItem> dailyData;

            if (settings?.UseCustomCalculations == true)
            {
                var revenueCategoryIds = ParseCategoryIds(settings.RevenueCategoryIds);
                var purchaseCategoryIds = ParseCategoryIds(settings.PurchaseCategoryIds);
                var allCategoryIds = revenueCategoryIds.Union(purchaseCategoryIds).ToList();

                if (allCategoryIds.Count == 0)
                {
                    // Fall back to default if no categories selected
                    allCategoryIds = null;
                }

                var query = from je in _context.JournalEntries
                            join jv in _context.JournalVouchers on je.JournalVoucherId equals jv.JournalVoucherId
                            where jv.IsCompleted
                               && jv.EntryDate >= startDate
                               && jv.EntryDate <= endDate
                               && (allCategoryIds == null || allCategoryIds.Contains(je.CategoryId))
                            select new { je, jv, je.CategoryId };

                if (projectId.HasValue)
                {
                    query = query.Where(x => x.jv.ProjectId == projectId.Value);
                }

                // Get raw data and process in memory for complex category logic
                var rawData = await query.ToListAsync();

                dailyData = rawData
                    .GroupBy(x => x.jv.EntryDate)
                    .Select(g => new DailyTrendItem
                    {
                        Date = g.Key,
                        Revenue = g.Where(x => revenueCategoryIds.Contains(x.CategoryId)).Sum(x => x.je.Credit),
                        Purchases = g.Where(x => purchaseCategoryIds.Contains(x.CategoryId)).Sum(x => x.je.Debit),
                        TransactionCount = g.Count()
                    })
                    .OrderBy(d => d.Date)
                    .ToList();
            }
            else
            {
                // Default: AccountGroupId = 2 (products)
                var query = from je in _context.JournalEntries
                            join jv in _context.JournalVouchers on je.JournalVoucherId equals jv.JournalVoucherId
                            join a in _context.Accounts on je.AccountId equals a.AccountId
                            where jv.IsCompleted
                               && jv.EntryDate >= startDate
                               && jv.EntryDate <= endDate
                               && a.AccountGroupId == 2
                            select new { je, jv };

                if (projectId.HasValue)
                {
                    query = query.Where(x => x.jv.ProjectId == projectId.Value);
                }

                dailyData = await query
                    .GroupBy(x => x.jv.EntryDate)
                    .Select(g => new DailyTrendItem
                    {
                        Date = g.Key,
                        Revenue = g.Sum(x => x.je.Credit),
                        Purchases = g.Sum(x => x.je.Debit),
                        TransactionCount = g.Count()
                    })
                    .OrderBy(d => d.Date)
                    .ToListAsync();
            }

            // Fill in missing dates with zeros
            var result = new List<DailyTrendItem>();
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var existing = dailyData.FirstOrDefault(d => d.Date == date);
                result.Add(existing ?? new DailyTrendItem
                {
                    Date = date,
                    Revenue = 0,
                    Purchases = 0,
                    TransactionCount = 0
                });
            }

            return result;
        }

        private async Task<List<TopProductItem>> GetTopProductsAsync(int? projectId, DateOnly startDate, DateOnly endDate)
        {
            var query = from je in _context.JournalEntries
                        join jv in _context.JournalVouchers on je.JournalVoucherId equals jv.JournalVoucherId
                        join a in _context.Accounts on je.AccountId equals a.AccountId
                        where jv.IsCompleted
                           && jv.EntryDate >= startDate
                           && jv.EntryDate <= endDate
                           && a.AccountGroupId == 2
                        select new { je, jv, a };

            if (projectId.HasValue)
            {
                query = query.Where(x => x.jv.ProjectId == projectId.Value);
            }

            var productData = await query
                .GroupBy(x => new { x.je.AccountId, x.a.AccountName })
                .Select(g => new TopProductItem
                {
                    AccountId = g.Key.AccountId,
                    ProductName = g.Key.AccountName,
                    Revenue = g.Sum(x => x.je.Credit),
                    COGS = g.Sum(x => x.je.Debit),
                    Profit = g.Sum(x => x.je.Credit) - g.Sum(x => x.je.Debit),
                    Quantity = g.Sum(x => x.je.Quantity)
                })
                .OrderByDescending(p => p.Profit)
                .Take(10)
                .ToListAsync();

            // Calculate profit margin
            foreach (var product in productData)
            {
                product.ProfitMargin = product.Revenue > 0
                    ? Math.Round((product.Profit / product.Revenue) * 100, 2)
                    : 0;
            }

            return productData;
        }

        private async Task<List<TopCustomerItem>> GetTopCustomersAsync(int? projectId, DateOnly startDate, DateOnly endDate)
        {
            var query = from je in _context.JournalEntries
                        join jv in _context.JournalVouchers on je.JournalVoucherId equals jv.JournalVoucherId
                        join a in _context.Accounts on je.AccountId equals a.AccountId
                        where jv.IsCompleted
                           && jv.EntryDate >= startDate
                           && jv.EntryDate <= endDate
                           && a.AccountGroupId == 1
                        select new { je, jv, a };

            if (projectId.HasValue)
            {
                query = query.Where(x => x.jv.ProjectId == projectId.Value);
            }

            var customerData = await query
                .GroupBy(x => new { x.je.AccountId, x.a.AccountName })
                .Select(g => new
                {
                    AccountId = g.Key.AccountId,
                    CustomerName = g.Key.AccountName,
                    TransactionCount = g.Count(),
                    TotalValue = g.Sum(x => x.je.Debit),
                    LastTransactionDate = g.Max(x => x.jv.EntryDate)
                })
                .OrderByDescending(c => c.TotalValue)
                .Take(10)
                .ToListAsync();

            return customerData.Select(c => new TopCustomerItem
            {
                AccountId = c.AccountId,
                CustomerName = c.CustomerName,
                TransactionCount = c.TransactionCount,
                TotalValue = c.TotalValue,
                LastTransaction = c.LastTransactionDate
            }).ToList();
        }

        private async Task<List<CategoryDistribution>> GetCategoryDistributionAsync(int? projectId, DateOnly startDate, DateOnly endDate)
        {
            var query = from je in _context.JournalEntries
                        join jv in _context.JournalVouchers on je.JournalVoucherId equals jv.JournalVoucherId
                        join c in _context.Categories on je.CategoryId equals c.CategoryId
                        where jv.IsCompleted
                           && jv.EntryDate >= startDate
                           && jv.EntryDate <= endDate
                        select new { je, jv, c };

            if (projectId.HasValue)
            {
                query = query.Where(x => x.jv.ProjectId == projectId.Value);
            }

            var categoryData = await query
                .GroupBy(x => new { x.je.CategoryId, x.c.CategoryName })
                .Select(g => new CategoryDistribution
                {
                    CategoryId = g.Key.CategoryId,
                    CategoryName = g.Key.CategoryName,
                    TotalValue = g.Sum(x => x.je.Debit + x.je.Credit),
                    TransactionCount = g.Count()
                })
                .OrderByDescending(c => c.TotalValue)
                .ToListAsync();

            // Calculate percentages
            var total = categoryData.Sum(c => c.TotalValue);
            foreach (var category in categoryData)
            {
                category.Percentage = total > 0
                    ? Math.Round((category.TotalValue / total) * 100, 2)
                    : 0;
            }

            return categoryData;
        }

        private async Task<List<ProjectPerformance>> GetProjectPerformanceAsync(
            DateOnly thisMonthStart, DateOnly thisMonthEnd,
            DateOnly lastMonthStart, DateOnly lastMonthEnd)
        {
            // This month's revenue by project
            var thisMonthData = await (
                from je in _context.JournalEntries
                join jv in _context.JournalVouchers on je.JournalVoucherId equals jv.JournalVoucherId
                join p in _context.Projects on jv.ProjectId equals p.ProjectId
                join a in _context.Accounts on je.AccountId equals a.AccountId
                where jv.IsCompleted
                   && jv.EntryDate >= thisMonthStart
                   && jv.EntryDate <= thisMonthEnd
                   && a.AccountGroupId == 2
                group je by new { jv.ProjectId, p.ProjectName } into g
                select new
                {
                    ProjectId = g.Key.ProjectId,
                    ProjectName = g.Key.ProjectName,
                    Revenue = g.Sum(x => x.Credit)
                }
            ).ToListAsync();

            // Last month's revenue by project
            var lastMonthDataList = await (
                from je in _context.JournalEntries
                join jv in _context.JournalVouchers on je.JournalVoucherId equals jv.JournalVoucherId
                join a in _context.Accounts on je.AccountId equals a.AccountId
                where jv.IsCompleted
                   && jv.EntryDate >= lastMonthStart
                   && jv.EntryDate <= lastMonthEnd
                   && a.AccountGroupId == 2
                group je by jv.ProjectId into g
                select new
                {
                    ProjectId = g.Key,
                    Revenue = g.Sum(x => x.Credit)
                }
            ).ToListAsync();

            var lastMonthData = lastMonthDataList.ToDictionary(g => g.ProjectId, g => g.Revenue);

            var result = thisMonthData.Select(p =>
            {
                var lastMonthRevenue = lastMonthData.GetValueOrDefault(p.ProjectId, 0);
                var growth = lastMonthRevenue > 0
                    ? Math.Round(((p.Revenue - lastMonthRevenue) / lastMonthRevenue) * 100, 2)
                    : (p.Revenue > 0 ? 100 : 0);

                return new ProjectPerformance
                {
                    ProjectId = p.ProjectId,
                    ProjectName = p.ProjectName,
                    ThisMonthRevenue = p.Revenue,
                    LastMonthRevenue = lastMonthRevenue,
                    Growth = growth
                };
            })
            .OrderByDescending(p => p.ThisMonthRevenue)
            .Take(10)
            .ToList();

            return result;
        }

        private async Task<List<RecentActivity>> GetRecentActivityAsync(int? projectId, int count)
        {
            var query = _context.JournalVouchers
                .Include(jv => jv.Project)
                .Include(jv => jv.JournalEntries)
                    .ThenInclude(je => je.Account)
                .Where(jv => jv.IsCompleted)
                .AsQueryable();

            if (projectId.HasValue)
            {
                query = query.Where(jv => jv.ProjectId == projectId.Value);
            }

            var vouchers = await query
                .OrderByDescending(jv => jv.JournalVoucherId)
                .Take(count)
                .ToListAsync();

            return vouchers.Select(jv =>
            {
                var mainEntry = jv.JournalEntries.FirstOrDefault(je => je.Debit > 0 || je.Credit > 0);
                var voucherType = jv.IsBaseVoucher ? "Base" : "JV";

                return new RecentActivity
                {
                    JournalVoucherId = jv.JournalVoucherId,
                    VoucherType = voucherType,
                    EntryDate = jv.EntryDate,
                    AccountName = mainEntry?.Account?.AccountName ?? "Multiple",
                    Amount = mainEntry != null ? Math.Max(mainEntry.Debit, mainEntry.Credit) : jv.TotalDebit,
                    ProjectName = jv.Project?.ProjectName ?? ""
                };
            }).ToList();
        }

        private async Task<List<DashboardAlert>> GetAlertsAsync(int? projectId, DateOnly today)
        {
            var alerts = new List<DashboardAlert>();

            // Alert 1: Missed Entries
            var missedEntriesQuery = _context.MissedEntries.AsQueryable();
            if (projectId.HasValue)
            {
                missedEntriesQuery = missedEntriesQuery.Where(m => m.ProjectId == projectId.Value);
            }
            var missedCount = await missedEntriesQuery.CountAsync();

            if (missedCount > 0)
            {
                alerts.Add(new DashboardAlert
                {
                    Type = "danger",
                    Title = "Missed Entries",
                    Message = $"{missedCount} missed {(missedCount == 1 ? "entry" : "entries")} pending attention",
                    Link = "/reporting/missed-entries"
                });
            }

            // Alert 2: Overdue Receivables (>30 days)
            var thirtyDaysAgo = today.AddDays(-30);
            var overdueQuery = from je in _context.JournalEntries
                               join jv in _context.JournalVouchers on je.JournalVoucherId equals jv.JournalVoucherId
                               join a in _context.Accounts on je.AccountId equals a.AccountId
                               where jv.IsCompleted
                                  && a.AccountGroupId == 1
                                  && jv.CreditableDate < thirtyDaysAgo
                               select new { je, jv };

            if (projectId.HasValue)
            {
                overdueQuery = overdueQuery.Where(x => x.jv.ProjectId == projectId.Value);
            }

            var overdueBalances = await overdueQuery
                .GroupBy(x => x.je.AccountId)
                .Select(g => g.Sum(x => x.je.Debit) - g.Sum(x => x.je.Credit))
                .ToListAsync();

            var overdueBalance = overdueBalances.Where(b => b > 0).Sum();

            if (overdueBalance > 0)
            {
                alerts.Add(new DashboardAlert
                {
                    Type = "warning",
                    Title = "Overdue Receivables",
                    Message = $"Rs {overdueBalance:N2} overdue by more than 30 days",
                    Link = "/reporting/aging-report"
                });
            }

            // Alert 3: Pending Base Vouchers
            var pendingBaseVouchersQuery = _context.JournalVouchers
                .Where(jv => jv.IsBaseVoucher && !jv.IsCompleted);

            if (projectId.HasValue)
            {
                pendingBaseVouchersQuery = pendingBaseVouchersQuery.Where(jv => jv.ProjectId == projectId.Value);
            }

            var pendingBaseCount = await pendingBaseVouchersQuery.CountAsync();

            if (pendingBaseCount > 0)
            {
                alerts.Add(new DashboardAlert
                {
                    Type = "info",
                    Title = "Pending Base Vouchers",
                    Message = $"{pendingBaseCount} base {(pendingBaseCount == 1 ? "voucher" : "vouchers")} pending processing",
                    Link = "/home/base-vouchers"
                });
            }

            return alerts;
        }
    }
}
