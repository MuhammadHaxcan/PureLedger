using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models
{
    [Index(nameof(BookerName), IsUnique = true)]
    public class Booker
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BookerID { get; set; }

        [Required(ErrorMessage = "Booker name is required")]
        [StringLength(100, ErrorMessage = "Booker name must be under 100 characters")]
        public string BookerName { get; set; }

        [Required]
        public bool IsEnabled { get; set; } = true; // Default to enabled (true = enabled, false = disabled)
    }
}
