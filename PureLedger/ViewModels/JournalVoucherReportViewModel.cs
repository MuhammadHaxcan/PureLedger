using PureLedger.Models;

namespace PureLedger.ViewModels
{
    public class JournalVoucherReportViewModel
    {
        public int JvId { get; set; }
        public VoucherType VoucherType { get; set; }
        public string? ProjectName { get; set; }
        public string? BookerName { get; set; }
        public DateOnly CreatedDate { get; set; }

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

        // All Entries of that Entry
        public List<JournalVoucherEntryReportModel> Entries { get; set; } = new List<JournalVoucherEntryReportModel>();

        // FOR Charges and Cartage related entries and amounts
        public List<JournalVoucherEntryReportModel> ForChargesEntries { get; set; } = new List<JournalVoucherEntryReportModel>();
        public List<JournalVoucherEntryReportModel> CartageEntries { get; set; } = new List<JournalVoucherEntryReportModel>();
        public decimal ForChargesAmount { get; set; }
        public decimal CartageAmount { get; set; }

        // Totals as calculated properties
        public int TotalQuantity => Entries.Sum(e => e.Quantity);
        public decimal TotalAmount => Entries.Sum(e => e.LineTotal);
        public decimal TotalActualAmount => Entries.Sum(e => e.ActualAmount);

        // Effective amount after deducting FOR charges and cartage
        public decimal EffectiveAmount => TotalAmount - ForChargesAmount - CartageAmount;

        // Footer fields
        public string? AccountName { get; set; }
        public string? AuthorizedBy { get; set; }
        public string? ReceivedBy { get; set; }

        public JournalEntry? CartageEntry { get; set; }
    }
}