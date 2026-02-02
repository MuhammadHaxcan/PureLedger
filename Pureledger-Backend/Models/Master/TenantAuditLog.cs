using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models.Master
{
    [Table("TenantAuditLogs", Schema = "master")]
    public class TenantAuditLog
    {
        [Key]
        public long Id { get; set; }

        public int? TenantId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Operation { get; set; } = string.Empty;

        [MaxLength(63)]
        public string? SchemaName { get; set; }

        [Required]
        [MaxLength(100)]
        public string PerformedBy { get; set; } = string.Empty;

        public string? Details { get; set; }

        public bool IsSuccess { get; set; }

        [MaxLength(1000)]
        public string? ErrorMessage { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Navigation property
        [ForeignKey("TenantId")]
        public virtual Tenant? Tenant { get; set; }
    }
}
