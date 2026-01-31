using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models
{
    public class DashboardSettings
    {
        [Key]
        public int DashboardSettingsId { get; set; }

        [Required]
        [ForeignKey("Project")]
        public int ProjectId { get; set; }

        // Categories for Revenue (Credits from these categories)
        // Stored as JSON array: [1, 5, 12]
        [Column(TypeName = "text")]
        public string? RevenueCategoryIds { get; set; }

        // Categories for Purchases/COGS (Debits from these categories)
        [Column(TypeName = "text")]
        public string? PurchaseCategoryIds { get; set; }

        // Categories to subtract from profit (Expenses)
        [Column(TypeName = "text")]
        public string? ExpenseCategoryIds { get; set; }

        // Toggle: use custom settings or default logic
        public bool UseCustomCalculations { get; set; } = false;

        public virtual Project? Project { get; set; }
    }
}
