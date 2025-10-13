namespace PureLedger.ViewModels
{
    public class JournalEntryRowModel
    {
        public int CategoryId { get; set; }
        public int AccountId { get; set; }
        public string? Type { get; set; }
        public string? Rbp { get; set; } = "Y";
        public string? Des { get; set; }
        public int Qty { get; set; }
        public int Rate { get; set; }
        public int Percent { get; set; }
        public int Disc { get; set; }
        public int Debit { get; set; }
        public int Credit { get; set; }
    }
}
