using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models
{
    public class UserProject
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserProjectId { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        [ForeignKey("Project")]
        public int ProjectId { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual Project Project { get; set; } = null!;
    }
}