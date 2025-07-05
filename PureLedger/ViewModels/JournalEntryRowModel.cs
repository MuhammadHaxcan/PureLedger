namespace PureLedger.ViewModels
{
    public class JournalEntryRowModel
    {
        public int CategoryId { get; set; }
        public int AccountId { get; set; }
        public string? Type { get; set; }
        public string? Rbp { get; set; } = "Y";
        public List<int>? Tags { get; set; } // Now a list of tag IDs
        public string? Des { get; set; }
        public int Qty { get; set; }
        public decimal Rate { get; set; }
        public decimal Percent { get; set; }
        public decimal Disc { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }
}
