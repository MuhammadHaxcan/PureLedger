using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models
{
    public class JournalVoucherReference
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int JournalVoucherReferenceId { get; set; }

        [Required]
        [ForeignKey("MainVoucher")]
        public int MainVoucherId { get; set; }

        [Required]
        [ForeignKey("ReferencedVoucher")]
        public int ReferencedVoucherId { get; set; }

        [StringLength(50)]
        public string? ReferenceType { get; set; }

        // Navigation properties
        public virtual JournalVoucher MainVoucher { get; set; }
        public virtual JournalVoucher ReferencedVoucher { get; set; }
    }
}