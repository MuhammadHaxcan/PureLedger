using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models {
    public class ProjectCartageSettings : IValidatableObject {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Project")]
        public int ProjectId { get; set; }

        // Make navigation properties nullable if they are optional
        public virtual Project? Project { get; set; }

        [ForeignKey("Category")]
        public int? ForExpenseCategoryId { get; set; }

        // Mark as nullable
        public virtual Category? ForExpenseCategory { get; set; }

        [ForeignKey("Category")]
        public int? CartageTransporterCategoryId { get; set; }

        // Mark as nullable
        public virtual Category? CartageTransporterCategory { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) {
            // At least one of ForExpenseCategoryId or CartageTransporterCategoryId must be set
            if (!ForExpenseCategoryId.HasValue && !CartageTransporterCategoryId.HasValue) {
                yield return new ValidationResult(
                    "At least one of FOR Expense Category or Cartage Transporter Category must be selected.",
                    new[] { nameof(ForExpenseCategoryId), nameof(CartageTransporterCategoryId) }
                );
            }
        }
    }
}
