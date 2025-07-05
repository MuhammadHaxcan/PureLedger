namespace PureLedger.ViewModels
{
    public class JournalEntryViewModel
    {
        public int ProjectId { get; set; }
        public string? Booker { get; set; }
        public int CreditDays { get; set; }
        public DateOnly DueDate { get; set; }
        public List<JournalEntryRowModel> Entries { get; set; } = new List<JournalEntryRowModel>();
    }
}
