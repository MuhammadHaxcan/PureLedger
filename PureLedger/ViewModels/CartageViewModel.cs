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

    // Date fields changed to DateTimeOnly and made required
    [Required]
    public DateOnly DcDate { get; set; }
    
    public string DcRemarks { get; set; }
    public string DcStation { get; set; }
    public string DcOtherRef { get; set; }
    
    [Required]
    public int DcLocation { get; set; }
}