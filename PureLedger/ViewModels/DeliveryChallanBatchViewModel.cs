using System.ComponentModel.DataAnnotations;

namespace PureLedger.ViewModels
{
    public class DeliveryChallanBatchViewModel
    {
        [Required]
        public int JournalVoucherId { get; set; }
        
        public string DCDate { get; set; }
        
        [Required]
        public int LocationId { get; set; }
        
        public int Quantity { get; set; }
        
        [StringLength(500)]
        public string Remarks { get; set; }
        
        [StringLength(200)]
        public string OtherReference { get; set; }
        
        [Required]
        public List<int> SelectedEntries { get; set; }
    }
}