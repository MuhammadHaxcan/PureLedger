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
        public string? Builty { get; set; }
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
    }
}
