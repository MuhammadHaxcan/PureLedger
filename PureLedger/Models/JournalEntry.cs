using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models
{
    public class JournalEntry
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int JournalEntryId { get; set; }

        [Required(ErrorMessage = "Journal voucher reference is required")]
        [ForeignKey("JournalVoucher")]
        public int JournalVoucherId { get; set; }

        [Required(ErrorMessage = "Account is required")]
        [ForeignKey("Account")]
        public int AccountId { get; set; }

        [Required(ErrorMessage = "Category is required")]
        [ForeignKey("Category")]
        public int CategoryId { get; set; }

        public bool IsProcessed { get; set; } = false;
        public bool IsEdited { get; set; } = false;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Quantity must be positive")]
        public int Quantity { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Rate must be positive")]
        public int Rate { get; set; }

        [Range(0, 100, ErrorMessage = "Discount percentage must be between 0-100")]
        public int? DiscountPercent { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Discount must be positive")]
        public int Discount { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Debit amount must be positive")]
        public int Debit { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Credit amount must be positive")]
        public int Credit { get; set; }

        [StringLength(1)]
        [RegularExpression("Y|N", ErrorMessage = "RBP must be 'Y' or 'N'")]
        public string Rbp { get; set; } = "N";

        public DateOnly? ModifiedDateTime { get; set; } // <-- Add this line

        // Navigation properties
        public virtual Account Account { get; set; }
        public virtual Category Category { get; set; }
        public virtual JournalVoucher JournalVoucher { get; set; }
    }
}
