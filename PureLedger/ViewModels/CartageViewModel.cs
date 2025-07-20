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
}