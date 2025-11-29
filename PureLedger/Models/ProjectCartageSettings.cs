using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models
{
    public class ProjectCartageSettings : IValidatableObject
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Project")]
        public int ProjectId { get; set; }

        // Make navigation properties nullable if they are optional
        public virtual Project? Project { get; set; }

        [ForeignKey("Account")]
        public int? ForExpenseAccountId { get; set; }

        // Mark as nullable
        public virtual Account? ForExpenseAccount { get; set; }

        [ForeignKey("Category")]
        public int? CartageTransporterCategoryId { get; set; }

        // Mark as nullable
        public virtual Category? CartageTransporterCategory { get; set; }

        // Store image as byte array in database
        [Column(TypeName = "bytea")]
        public byte[]? ProjectImage { get; set; }
        
        // Store image content type (e.g., "image/png", "image/jpeg")
        [StringLength(50)]
        public string? ProjectImageContentType { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // At least one of ForExpenseAccountId or CartageTransporterCategoryId must be set
            if (!ForExpenseAccountId.HasValue && !CartageTransporterCategoryId.HasValue)
            {
                yield return new ValidationResult(
                    "At least one of FOR Expense Account or Cartage Transporter Category must be selected.",
                    new[] { nameof(ForExpenseAccountId), nameof(CartageTransporterCategoryId) }
                );
            }
        }
    }
}
