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
        [Range(1, int.MaxValue, ErrorMessage = "FOR charges amount must be greater than zero")]
        public int ForAmount { get; set; }
        
        [Required]
        public DateOnly DcDate { get; set; }
    }
}
