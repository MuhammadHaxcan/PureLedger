using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models
{
    public class UserCategory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserCategoryId { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        [ForeignKey("Category")]
        public int CategoryId { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual Category Category { get; set; } = null!;
    }
}

