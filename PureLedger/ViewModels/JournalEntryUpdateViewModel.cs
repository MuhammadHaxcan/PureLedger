using System.ComponentModel.DataAnnotations;
using PureLedger.Models;
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

            [Range(typeof(decimal), "0", "9999999999.9999")]
            public decimal Rate { get; set; }

            [Range(typeof(decimal), "0", "9999999999.9999")]
            public decimal Discount { get; set; }

            [Range(0, 100)]
            public int DiscountPercent { get; set; }

            [Range(typeof(decimal), "0", "9999999999.9999")]
            public decimal Debit { get; set; }

            [Range(typeof(decimal), "0", "9999999999.9999")]
            public decimal Credit { get; set; }

            // Read-only properties for display purposes
            public string? AccountName { get; set; }
            public int ProjectId { get; set; } 
            public int CategoryId { get; set; }
            public int AccountId { get; set; }  
            public int AccountGroupId { get; set; }
            public int? DeliveryChallanEntryId { get; set; }
            public int? LocationId { get; set; }
            public List<Location>? Locations { get; set; }
        }
    }
}
