namespace PureLedger.ViewModels.Home
{
    public class BaseVouchersSummaryViewModel
    {
        public int TotalBaseVouchers { get; set; }
        public int TotalUnprocessedEntries { get; set; }
        public decimal TotalUnprocessedQuantity { get; set; }
        public int TotalProcessedEntries { get; set; }
        public decimal TotalProcessedQuantity { get; set; }
        public decimal ProcessingRate { get; set; }
        public List<ProductSummaryViewModel> MostOrderedProducts { get; set; } = new();
        public List<ProductSummaryViewModel> MostPendingProducts { get; set; } = new();
        public CustomerSummaryViewModel? TopCustomer { get; set; }
        public VoucherSummaryViewModel? OldestPendingVoucher { get; set; }
    }
}

