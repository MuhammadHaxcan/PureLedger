using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models.Master
{
    [Table("Tenants", Schema = "master")]
    public class Tenant
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Slug { get; set; } = string.Empty;

        [Required]
        [MaxLength(63)]
        public string SchemaName { get; set; } = string.Empty;

        public bool IsActive { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ActivatedAt { get; set; }

        public DateTime? DeactivatedAt { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        // Navigation property
        public virtual ICollection<TenantAuditLog> AuditLogs { get; set; } = new List<TenantAuditLog>();
    }
}
