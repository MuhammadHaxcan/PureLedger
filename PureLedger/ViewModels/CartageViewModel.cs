namespace PureLedger.ViewModels;
using System.ComponentModel.DataAnnotations;

public class CartageViewModel
{
    [Required]
    public int JournalVoucherId { get; set; }
    
    [Required]
    public int EntryId { get; set; }
    
    [Required]
    public int TransporterId { get; set; }
    
    [Required]
    [Range(0.01, double.MaxValue)]
    public int Amount { get; set; }
    
    public bool PostToCustomer { get; set; }
    
    public string? Reference { get; set; } // Fixed the typo from "Referece"
    
    [Required]
    public DateOnly DcDate { get; set; }
    
    public string? DcRemarks { get; set; }
    public string? DcStation { get; set; }
    public string? DcOtherRef { get; set; }
    
    [Required]
    public int DcLocation { get; set; }
}