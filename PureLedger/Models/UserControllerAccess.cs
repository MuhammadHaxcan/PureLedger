using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models
{
    public class UserControllerAccess
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserControllerAccessId { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        [StringLength(50)]
        public string Controller { get; set; } = string.Empty;

        [Required]
        public bool HasAccess { get; set; } = true;

        // Navigation property
        public virtual User User { get; set; } = null!;
    }
}