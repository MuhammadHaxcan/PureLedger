using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models
{
    public class Recipe
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RecipeId { get; set; }

        [Required]
        [StringLength(100)]
        public string RecipeName { get; set; } = string.Empty;

        [Required]
        [ForeignKey("Project")]
        public int ProjectId { get; set; }

        // Navigation property
        public virtual Project Project { get; set; } = null!;

        // Collection of recipe items
        public virtual ICollection<RecipeItem> RecipeItems { get; set; } = new List<RecipeItem>();
    }
}