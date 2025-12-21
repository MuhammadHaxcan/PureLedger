namespace PureLedger.ViewModels.DeliveryChallan;
using System.ComponentModel.DataAnnotations;

public class CartageViewModel
{
    [Required(ErrorMessage = "Journal Voucher ID is required")]
    public int JournalVoucherId { get; set; }

    [Required(ErrorMessage = "Entry ID is required")]
    public int EntryId { get; set; }

    [Required(ErrorMessage = "Transporter is required")]
    public int TransporterId { get; set; }

    [Required(ErrorMessage = "Amount is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Amount must be greater than zero")]
    public int Amount { get; set; }

    public bool PostToCustomer { get; set; }

    public string? Reference { get; set; }

    [Required(ErrorMessage = "DC Date is required")]
    public DateOnly DcDate { get; set; }

    public string? DcRemarks { get; set; }

    public string? DcStation { get; set; }

    public string? DcOtherRef { get; set; }

    [Required(ErrorMessage = "Location is required")]
    public int DcLocation { get; set; }

    public bool IncludeForCharges { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "FOR charges amount must be zero or greater")]
    public int ForAmount { get; set; }
    
    public int? ForAccountId { get; set; }
}