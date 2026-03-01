using System;
using System.ComponentModel.DataAnnotations;

namespace PureLedger.ViewModels.DeliveryChallan
{
    public class DeliveryChallanViewModels
    {
    }

    public class ProcessPendingForChargesViewModel
    {
        [Required]
        public int JournalVoucherId { get; set; }
        
        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "FOR charges amount must be zero or greater")]
        public int ForAmount { get; set; }
        
        [Required]
        public DateOnly DcDate { get; set; }
        
        public int? ForAccountId { get; set; }

        [StringLength(200)]
        public string? OtherRef { get; set; }
    }
}
