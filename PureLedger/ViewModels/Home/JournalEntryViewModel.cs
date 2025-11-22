using PureLedger.Models;

namespace PureLedger.ViewModels.Home
{
    public class JournalEntryViewModel
    {
        public int ProjectId { get; set; }
        public int CreditDays { get; set; }
        public DateOnly EntryDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
        public DateOnly DueDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
        public List<JournalEntryRowModel> Entries { get; set; } = new List<JournalEntryRowModel>();
        public bool IsBaseVoucher { get; set; } = false;
        public bool RequireQuantity { get; set; } = false;

        // Add these properties to JournalEntryViewModel
        public bool SaveAsRecipe { get; set; } = false;
        public string? RecipeName { get; set; }
        public int? BookerId { get; set; } // Change from string Booker to int? BookerId
        public List<Booker> Bookers { get; set; } = new List<Booker>(); // Add this for dropdown options

    }
}
