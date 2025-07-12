namespace PureLedger.ViewModels
{
    public class JournalVoucherEntryReportModel
    {
        public string? AccountName { get; set; }
        public string? Description { get; set; }
        public int Quantity { get; set; }
        public decimal Rate { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }

        public decimal LineTotal => Quantity * Rate;
    }
}
