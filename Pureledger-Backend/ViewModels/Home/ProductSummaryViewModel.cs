namespace PureLedger.ViewModels.Home
{
    public class ProductSummaryViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal TotalQuantity { get; set; }
        public decimal UnprocessedQuantity { get; set; }
        public decimal ProcessedQuantity { get; set; }
        public int EntryCount { get; set; }
    }
}

