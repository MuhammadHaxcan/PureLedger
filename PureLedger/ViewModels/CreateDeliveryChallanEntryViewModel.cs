using System;
using System.ComponentModel.DataAnnotations;

namespace PureLedger.ViewModels
{
    public class CreateDeliveryChallanEntryViewModel
    {
        [Required]
        public int JournalEntryId { get; set; }
        
        [Required]
        public int LocationId { get; set; }
        
        [StringLength(500)]
        public string? Remarks { get; set; }
        
        [StringLength(200)]
        public string? OtherReference { get; set; }
        
        [Required]
        public DateOnly DCDate { get; set; }
        
        // Add these new properties
        public bool IncludeForCharges { get; set; }
        
        [Range(0, int.MaxValue)]
        public int ForAmount { get; set; }
    }
}