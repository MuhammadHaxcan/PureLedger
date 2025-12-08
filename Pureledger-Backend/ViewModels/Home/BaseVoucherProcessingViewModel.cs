using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.ViewModels.Home
{
    public class BaseVoucherProcessingViewModel
    {
        public int BaseVoucherId { get; set; }
        public int ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public DateOnly EntryDate { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public List<BaseVoucherEntryViewModel> Entries { get; set; } = new();
        
        // New properties for account entry
        public int AccountEntryAmount { get; set; }
        public bool AccountEntryIsDebit { get; set; }
        public bool AccountEntryIncluded { get; set; } = true;
        public DateOnly CreditableDate { get; set; }
        public int? BookerId { get; set; }

    }

    public class BaseVoucherEntryViewModel
    {
        public int EntryId { get; set; }
        public string? Description { get; set; }
        public string? AccountName { get; set; }
        public string? CategoryName { get; set; }
        public int AccountId { get; set; }
        public int CategoryId { get; set; } // Added to match JournalEntryRowModel
        public int AvailableQuantity { get; set; }
        public int ProcessQuantity { get; set; }
        public int Pkg { get; set; }
        public decimal Rate { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public int AccountGroupId { get; set; }
        public decimal Percent { get; set; } // Added to match JournalEntryRowModel
        public decimal Disc { get; set; } // Added to match JournalEntryRowModel
        public string? Rbp { get; set; } // Added to match JournalEntryRowModel
        public string? Type { get; set; } // Added to match JournalEntryRowModel
        public List<int>? Tags { get; set; } // Added to match JournalEntryRowModel
        public bool IsProcessed { get; set; }

        [NotMapped]
        public bool IsFullQuantity => ProcessQuantity == AvailableQuantity;

        [NotMapped]
        public bool IsPartialQuantity => ProcessQuantity > 0 && ProcessQuantity < AvailableQuantity;

        // For account entries
        public int AccountEntryAmount { get; set; }
        public bool AccountEntryIsDebit { get; set; }
    }
}