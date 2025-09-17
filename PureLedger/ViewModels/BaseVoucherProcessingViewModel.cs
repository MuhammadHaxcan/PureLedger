using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.ViewModels
{
    public class BaseVoucherProcessingViewModel
    {
        public int BaseVoucherId { get; set; }
        public int ProjectId { get; set; }
        public List<BaseVoucherEntryViewModel> Entries { get; set; } = new List<BaseVoucherEntryViewModel>();
    }

    public class BaseVoucherEntryViewModel
    {
        public int EntryId { get; set; }
        public string? Description { get; set; }
        public string? AccountName { get; set; }
        public int AvailableQuantity { get; set; }
        
        [Range(0, int.MaxValue, ErrorMessage = "Quantity must be positive")]
        public int ProcessQuantity { get; set; }
        public int Rate { get; set; }
        
        [NotMapped]
        public bool IsProcessed => AvailableQuantity <= 0;
        
        [NotMapped]
        public bool IsFullQuantity => ProcessQuantity == AvailableQuantity;
        
        [NotMapped]
        public bool IsPartialQuantity => ProcessQuantity > 0 && ProcessQuantity < AvailableQuantity;
    }
}