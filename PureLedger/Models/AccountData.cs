using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models
{
    public class AccountData
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AccountDataId { get; set; }

        [Required(ErrorMessage = "Account reference is required")]
        [ForeignKey("Account")]
        public int AccountId { get; set; }

        [Phone(ErrorMessage = "Invalid phone number format")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
        public string? PhoneNumber { get; set; }

        [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
        public string? Address { get; set; }

        [StringLength(20, ErrorMessage = "Area cannot exceed 20 characters")]
        public string? Area { get; set; }

        [StringLength(20, ErrorMessage = "City cannot exceed 20 characters")]
        public string? City { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(50, ErrorMessage = "Email cannot exceed 50 characters")]
        public string? Email { get; set; }

        // Navigation property
        public virtual Account? Account { get; set; }
    }
}
