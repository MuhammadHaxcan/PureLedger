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
        public int DeliveryChallanVoucherId { get; set; }

        [Required]
        [ForeignKey("JournalVoucher")]
        public int JournalVoucherId { get; set; }

        [Required]
        public DateOnly DCDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        public DateOnly? FinalDeliveryDate { get; set; }

        public bool IsCompleted { get; set; } = false;

        // Navigation
        public virtual JournalVoucher JournalVoucher { get; set; }
        public virtual ICollection<DeliveryChallanEntry> Entries { get; set; } = new List<DeliveryChallanEntry>();
    }
}