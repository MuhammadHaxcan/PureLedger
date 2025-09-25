using PureLedger.Models;

namespace PureLedger.ViewModels
{
    public class JournalVoucherEntryReportModel
    {
        public int AccountId { get; set; }
        public string? AccountName { get; set; }
        public string? Description { get; set; }
        public int Quantity { get; set; }
        public int PKG { get; set; } = 1;
        public decimal Rate { get; set; }
        public decimal Discount { get; set; }

        // Calculate net rate after discount
        public decimal NetRate => Rate - (Rate * Discount / 100);

        public decimal Debit { get; set; }
        public decimal Credit { get; set; }

        // Calculate line total (quantity * net rate)
        public decimal LineTotal => Credit;

        // RBP flag (Y or N)
        public string Rbp { get; set; } = "Y";

        // Actual amount calculation based on RBP, quantity and rate
        public decimal ActualAmount => Rbp == "Y"
            ? PKG * Quantity * NetRate
            : Credit > 0 ? Credit : Debit;
    }
}