using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models
{
    public class Signature
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        // Store signature image as base64 encoded string in database
        [Column(TypeName = "text")]
        [Required]
        public string SignatureImage { get; set; } = string.Empty;

        public bool IsActive { get; set; } = false;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}


