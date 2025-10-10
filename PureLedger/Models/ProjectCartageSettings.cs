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
        public virtual Project Project { get; set; }

        [ForeignKey("Account")]
        public int? ForExpenseAccountId { get; set; }
        public virtual Account ForExpenseAccount { get; set; }

        [ForeignKey("Category")]
        public int? CartageTransporterCategoryId { get; set; }
        public virtual Category CartageTransporterCategory { get; set; }

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