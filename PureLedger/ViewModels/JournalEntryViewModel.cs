namespace PureLedger.ViewModels
{
    public class JournalEntryViewModel
    {
        public int ProjectId { get; set; }
        public string? Booker { get; set; }
        public int CreditDays { get; set; }
        public DateOnly EntryDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
        public DateOnly DueDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
        public List<JournalEntryRowModel> Entries { get; set; } = new List<JournalEntryRowModel>();
        public bool IsBaseVoucher { get; set; } = false;

        // Add these properties to JournalEntryViewModel
        public bool SaveAsRecipe { get; set; } = false;
        public string? RecipeName { get; set; }
    }
}
