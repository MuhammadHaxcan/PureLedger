using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PureLedger.Models
{
    [Index(nameof(Username), IsUnique = true)]
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserId { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string FullName { get; set; } = string.Empty;

        [EmailAddress]
        [StringLength(100)]
        public string? Email { get; set; }

        public bool IsAdmin { get; set; } = false;

        [StringLength(20)]
        public string Subroute { get; set; } = "Default";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? LastLoginDate { get; set; }

        // Navigation property
        public virtual ICollection<UserProject> UserProjects { get; set; } = new List<UserProject>();

        // Navigation property for controller access
        public virtual ICollection<UserControllerAccess> ControllerAccess { get; set; } = new List<UserControllerAccess>();
    }
}