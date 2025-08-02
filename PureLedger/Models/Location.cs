using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models
{
    [Index(nameof(LocationName), IsUnique = true)]
    public class Location
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LocationId { get; set; }

        [Required(ErrorMessage = "Location name is required")]
        [StringLength(20, ErrorMessage = "Location name cannot exceed 20 characters")]
        public string LocationName { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Invalid phone number format")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
        public string? Phone { get; set; }

        [StringLength(20, ErrorMessage = "Region name cannot exceed 20 characters")]
        public string? Region { get; set; }
    }
}
