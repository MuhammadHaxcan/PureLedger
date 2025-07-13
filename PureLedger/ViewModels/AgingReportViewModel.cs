namespace PureLedger.ViewModels
{
    public class AgingReportViewModel
    {
        public string AccountId { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal NetBalance { get; set; }
    }

    public class AgingReportRequest
    {
        public DateOnly ReferenceDate { get; set; }
        public string? ProjectId { get; set; }
        public string CategoryId { get; set; } = string.Empty;
    }
}