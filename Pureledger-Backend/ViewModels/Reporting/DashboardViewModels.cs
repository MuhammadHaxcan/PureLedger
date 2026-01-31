namespace PureLedger.ViewModels.Reporting
{
    public class DashboardKPIs
    {
        public decimal TotalRevenue { get; set; }
        public decimal TotalPurchases { get; set; }
        public decimal NetProfit { get; set; }
        public decimal ProfitMargin { get; set; }
        public decimal OutstandingReceivables { get; set; }
        public int MissedEntriesCount { get; set; }
        public int TransactionsToday { get; set; }
        public int TransactionsThisMonth { get; set; }
    }

    public class DailyTrendItem
    {
        public DateOnly Date { get; set; }
        public decimal Revenue { get; set; }
        public decimal Purchases { get; set; }
        public int TransactionCount { get; set; }
    }

    public class TopProductItem
    {
        public int AccountId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public decimal COGS { get; set; }
        public decimal Profit { get; set; }
        public decimal ProfitMargin { get; set; }
        public int Quantity { get; set; }
    }

    public class TopCustomerItem
    {
        public int AccountId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int TransactionCount { get; set; }
        public decimal TotalValue { get; set; }
        public DateOnly? LastTransaction { get; set; }
    }

    public class CategoryDistribution
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public decimal TotalValue { get; set; }
        public int TransactionCount { get; set; }
        public decimal Percentage { get; set; }
    }

    public class ProjectPerformance
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public decimal ThisMonthRevenue { get; set; }
        public decimal LastMonthRevenue { get; set; }
        public decimal Growth { get; set; }
    }

    public class RecentActivity
    {
        public int JournalVoucherId { get; set; }
        public string VoucherType { get; set; } = string.Empty;
        public DateOnly EntryDate { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string ProjectName { get; set; } = string.Empty;
    }

    public class DashboardAlert
    {
        public string Type { get; set; } = "info"; // "warning", "danger", "info"
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Link { get; set; }
    }

    public class DashboardResponse
    {
        public DashboardKPIs KPIs { get; set; } = new DashboardKPIs();
        public List<DailyTrendItem> Trends { get; set; } = new List<DailyTrendItem>();
        public List<TopProductItem> TopProducts { get; set; } = new List<TopProductItem>();
        public List<TopCustomerItem> TopCustomers { get; set; } = new List<TopCustomerItem>();
        public List<CategoryDistribution> CategoryDistribution { get; set; } = new List<CategoryDistribution>();
        public List<ProjectPerformance> ProjectPerformance { get; set; } = new List<ProjectPerformance>();
        public List<RecentActivity> RecentActivity { get; set; } = new List<RecentActivity>();
        public List<DashboardAlert> Alerts { get; set; } = new List<DashboardAlert>();
    }

    // Dashboard Settings ViewModels
    public class DashboardSettingsViewModel
    {
        public int DashboardSettingsId { get; set; }
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public List<int> RevenueCategoryIds { get; set; } = new List<int>();
        public List<int> PurchaseCategoryIds { get; set; } = new List<int>();
        public List<int> ExpenseCategoryIds { get; set; } = new List<int>();
        public bool UseCustomCalculations { get; set; }
    }

    public class DashboardSettingsUpdateRequest
    {
        public int ProjectId { get; set; }
        public List<int> RevenueCategoryIds { get; set; } = new List<int>();
        public List<int> PurchaseCategoryIds { get; set; } = new List<int>();
        public List<int> ExpenseCategoryIds { get; set; } = new List<int>();
        public bool UseCustomCalculations { get; set; }
    }

    public class CategoryOption
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string AccountTypeName { get; set; } = string.Empty;
    }

    public class DashboardSettingsCategoriesResponse
    {
        public List<CategoryOption> RevenueCategories { get; set; } = new List<CategoryOption>();
        public List<CategoryOption> PurchaseCategories { get; set; } = new List<CategoryOption>();
        public List<CategoryOption> ExpenseCategories { get; set; } = new List<CategoryOption>();
    }
}
