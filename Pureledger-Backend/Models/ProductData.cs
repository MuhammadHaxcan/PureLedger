using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models
{
    public class ProductData
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProductDataId { get; set; }

        [Required(ErrorMessage = "Account reference is required")]
        [ForeignKey("Account")]
        public int AccountId { get; set; }

        [StringLength(15, ErrorMessage = "Package form cannot exceed 15 characters")]
        public string? PkgForm { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Package must be a positive number")]
        public int? Pkg { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Sale rate must be a positive number")]
        public int? SaleRate { get; set; }

        // Navigation property
        public virtual Account? Account { get; set; }
    }
}
