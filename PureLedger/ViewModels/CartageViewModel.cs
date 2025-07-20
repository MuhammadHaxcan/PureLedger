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
    public decimal Amount { get; set; }
    
    public bool PostToCustomer { get; set; }
    public string Referece { get; set; }

    // Add these fields
    public string DcDate { get; set; }
    public string DcRemarks { get; set; }
    public string DcStation { get; set; }
    public string DcOtherRef { get; set; }
    public int DcLocation { get; set; }
}