using System;
using PureLedger.Helpers;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace PureLedger.Models
{
    public class DeliveryChallanVoucher
    {
        public const int StationMaxLength = 250;

        [Key]
        public int DeliveryChallanVoucherId { get; set; }

        [Required]
        [ForeignKey("JournalVoucher")]
        public int JournalVoucherId { get; set; }

        [Required]
        public DateOnly DCDate { get; set; } = BusinessClock.Today;

        [StringLength(StationMaxLength)]
        public string? Station { get; set; }

        public DateOnly? FinalDeliveryDate { get; set; }

        public bool IsCompleted { get; set; } = false;

        // Navigation
        public virtual JournalVoucher JournalVoucher { get; set; }
        public virtual ICollection<DeliveryChallanEntry> Entries { get; set; } = new List<DeliveryChallanEntry>();
    }
}