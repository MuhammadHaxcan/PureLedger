using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models
{
    public class RecipeItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RecipeItemId { get; set; }

        [Required]
        [ForeignKey("Recipe")]
        public int RecipeId { get; set; }

        [Required]
        [ForeignKey("Category")]
        public int CategoryId { get; set; }

        [Required]
        [ForeignKey("Account")]
        public int AccountId { get; set; }

        // Navigation properties
        public virtual Recipe Recipe { get; set; } = null!;
        public virtual Category Category { get; set; } = null!;
        public virtual Account Account { get; set; } = null!;
    }
}