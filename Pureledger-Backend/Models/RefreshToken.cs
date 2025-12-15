using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models
{
    public class RefreshToken
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RefreshTokenId { get; set; }

        [Required]
        [StringLength(500)]
        public string Token { get; set; } = string.Empty;

        [Required]
        public int UserId { get; set; }

        [Required]
        public DateTime ExpiryDate { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public bool IsRevoked { get; set; } = false;

        public DateTime? RevokedDate { get; set; }

        [StringLength(200)]
        public string? ReplacedByToken { get; set; }

        [StringLength(50)]
        public string? CreatedByIp { get; set; }

        [StringLength(50)]
        public string? RevokedByIp { get; set; }

        // Navigation property
        public virtual User User { get; set; } = null!;

        // Helper properties
        [NotMapped]
        public bool IsExpired => DateTime.UtcNow >= ExpiryDate;

        [NotMapped]
        public bool IsActive => !IsRevoked && !IsExpired;
    }
}
