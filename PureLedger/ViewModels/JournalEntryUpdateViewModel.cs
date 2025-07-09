using System.ComponentModel.DataAnnotations;

namespace PureLedger.ViewModels
{
    namespace PureLedger.ViewModels
    {
        public class JournalEntryUpdateViewModel
        {
            [Required]
            public int EntryId { get; set; }

            public string? Description { get; set; }

            [Range(0, int.MaxValue)]
            public int Quantity { get; set; }

            [Range(0, double.MaxValue)]
            public decimal Rate { get; set; }

            [Range(0, double.MaxValue)]
            public decimal Discount { get; set; }

            [Range(0, 100)]
            public decimal DiscountPercent { get; set; }

            [Range(0, double.MaxValue)]
            public decimal Debit { get; set; }

            [Range(0, double.MaxValue)]
            public decimal Credit { get; set; }

            // Read-only properties for display purposes
            public string? AccountName { get; set; }
            public int ProjectId { get; set; } 
            public int CategoryId { get; set; }
            public int AccountId { get; set; }
        }
    }
}
