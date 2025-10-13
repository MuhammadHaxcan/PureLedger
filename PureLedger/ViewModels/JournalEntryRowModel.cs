namespace PureLedger.ViewModels
{
    public class JournalEntryRowModel
    {
        public int? CategoryId { get; set; }
        public int? AccountId { get; set; }
        public int? Pkg { get; set; } // <-- Add this line
        public string? Rbp { get; set; } = "Y";
        public string? Des { get; set; }
        public int? Qty { get; set; }
        public decimal? Rate { get; set; }
        public decimal? Percent { get; set; }
        public decimal? Disc { get; set; }
        public decimal? Debit { get; set; }
        public decimal? Credit { get; set; }
    }
}
    