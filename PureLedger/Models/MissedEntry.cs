using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PureLedger.Models;

public class MissedEntry
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int MissedEntryId { get; set; }

    [Required]
    [ForeignKey("CustomerAccount")]
    public int CustomerAccountId { get; set; }
    
    [Required]
    [ForeignKey("ProductAccount")]
    public int ProductAccountId { get; set; }
    
    [Required]
    [ForeignKey("Project")]
    public int ProjectId { get; set; }
    
    [Required]
    public DateOnly MissedDate { get; set; }
    
    [Required]
    public int Quantity { get; set; }
    
    public int Rate { get; set; }
    
    public int Discount { get; set; }
    
    [MaxLength(500)]
    public string? Description { get; set; }

    // Navigation properties
    [ForeignKey("CustomerAccountId")]
    public virtual Account CustomerAccount { get; set; }
    
    [ForeignKey("ProductAccountId")]
    public virtual Account ProductAccount { get; set; }
    
    public virtual Project Project { get; set; }
}