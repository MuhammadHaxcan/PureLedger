using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace PureLedger.Models
{
    public class DeliveryChallanVoucher
    {
        [Key]
        public int DeliveryChallanId { get; set; }

        [Required]
        public int JournalVoucherId { get; set; }

        [Required]
        public DateOnly DCDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);

        // Final delivery date (not used for now as per requirements, but included for future use)
        public DateOnly? FinalDeliveryDate { get; set; }

        [StringLength(200)]
        public string? ReferenceNumber { get; set; }

        public bool IsCompleted { get; set; } = false;

        [ForeignKey("JournalVoucherId")]
        public virtual JournalVoucher? JournalVoucher { get; set; }

        // One-to-many relationship with DeliveryChallanEntry
        public virtual ICollection<DeliveryChallanEntry> DeliveryChallanEntries { get; set; } = new List<DeliveryChallanEntry>();

    }

    public class DeliveryChallanEntry
    {
        [Key]
        public int DCEntryId { get; set; }

        [Required]
        public int DeliveryChallanId { get; set; }

        [Required]
        public int JournalEntryId { get; set; }

        [Required]
        public int LocationId { get; set; }

        [Required]
        public DateOnly EntryDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);

        [StringLength(500)]
        public string? Remarks { get; set; }

        [StringLength(200)]
        public string? OtherReference { get; set; }

        // Foreign key relationships
        [ForeignKey("DeliveryChallanId")]
        public virtual DeliveryChallanVoucher? DeliveryChallanVoucher { get; set; }

        [ForeignKey("JournalEntryId")]
        public virtual JournalEntry? JournalEntry { get; set; }

        [ForeignKey("LocationId")]
        public virtual Location? Location { get; set; }
    }
}