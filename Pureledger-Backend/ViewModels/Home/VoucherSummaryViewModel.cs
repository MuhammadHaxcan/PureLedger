namespace PureLedger.ViewModels.Home
{
    public class VoucherSummaryViewModel
    {
        public int VoucherId { get; set; }
        public DateOnly EntryDate { get; set; }
        public int DaysPending { get; set; }
        public int UnprocessedCount { get; set; }
    }
}

