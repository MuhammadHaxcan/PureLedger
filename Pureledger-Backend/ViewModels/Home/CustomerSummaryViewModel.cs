namespace PureLedger.ViewModels.Home
{
    public class CustomerSummaryViewModel
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int VoucherCount { get; set; }
        public int TotalEntries { get; set; }
    }
}

