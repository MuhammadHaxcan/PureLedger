using System.ComponentModel.DataAnnotations;
using PureLedger.Helpers;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models
{
    public class DeliveryChallanEntry
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DeliveryChallanEntryId { get; set; }

        [Required]
        [ForeignKey("DeliveryChallanVoucher")]
        public int DeliveryChallanVoucherId { get; set; }

        [Required]
        [ForeignKey("JournalEntry")]
        public int JournalEntryId { get; set; }

        [Required]
        [ForeignKey("Location")]
        public int LocationId { get; set; }

        [Required]
        public DateOnly EntryDate { get; set; } = BusinessClock.Today;

        [StringLength(500)]
        public string? Remarks { get; set; }

        [StringLength(200)]
        public string? OtherReference { get; set; }

        // Navigation
        public virtual DeliveryChallanVoucher DeliveryChallanVoucher { get; set; }
        public virtual JournalEntry JournalEntry { get; set; }
        public virtual Location Location { get; set; }
    }
}
