using PureLedger.Models;

namespace PureLedger.ViewModels
{
    public class JournalVoucherReportViewModel
    {
        public int JvId { get; set; }
        public string? VoucherType { get; set; } // DC or JV
        public string? ProjectName { get; set; }
        public string? BookerName { get; set; }
        public DateOnly CreatedDate { get; set; }

        // Additional Fields for Delivery Challan
        public bool IsPartial { get; set; } // Check if Partial DC

        
        
        // Particular Customer Data
        public string? CustomerAccountName { get; set; } // Customer account name (from JournalEntries with Quantity == 0)
        public string? CustomerAddress { get; set; }
        public string? CustomerCity { get; set; }
        public string? CustomerPhone { get; set; }


        //Particular Document Detail
        public DateOnly? DueDate { get; set; }
        public string? Adda { get; set; }
        public int? Builty { get; set; }
        public string? Remarks { get; set; }



        // All Entreis of that Entry
        public List<JournalVoucherEntryReportModel> Entries { get; set; } = new List<JournalVoucherEntryReportModel>();

        // Totals
        public int TotalQuantity { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalActualAmount { get; set; }

        // Footer fields
        public string? AccountName { get; set; }
        public string? AuthorizedBy { get; set; }
        public string? ReceivedBy { get; set; }

        public JournalEntry? CartageEntry { get; set; }
        
        // Add these properties to JournalVoucherReportViewModel
        public decimal ForChargesAmount { get; set; }
        public List<JournalVoucherEntryReportModel> ForChargesEntries { get; set; } = new();

        // Add these new properties
        public decimal CartageAmount { get; set; }
        public List<JournalVoucherEntryReportModel> CartageEntries { get; set; } = new();
        
        // Add a calculated property for effective total amount
        public decimal EffectiveAmount => TotalAmount - ForChargesAmount - CartageAmount;
    }
}
