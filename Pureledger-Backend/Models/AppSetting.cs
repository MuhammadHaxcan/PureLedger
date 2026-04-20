using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models
{
    public class AppSetting
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(256)]
        public string SettingKey { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? SettingValue { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
