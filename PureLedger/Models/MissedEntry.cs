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

    [Range(0, 9999999999.9999, ErrorMessage = "Rate must be positive")]
    [Column(TypeName = "decimal(14,4)")]
    public decimal Rate { get; set; }

    [Range(0, 9999999999.999, ErrorMessage = "Discount must be positive")]
    [Column(TypeName = "decimal(14,4)")]
    public decimal Discount { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    // Navigation properties
    [ForeignKey("CustomerAccountId")]
    public virtual Account? CustomerAccount { get; set; }
    
    [ForeignKey("ProductAccountId")]
    public virtual Account? ProductAccount { get; set; }
    
    public virtual Project? Project { get; set; }
}