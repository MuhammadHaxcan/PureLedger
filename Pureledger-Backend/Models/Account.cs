using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace PureLedger.Models
{
    [Index(nameof(AccountName), nameof(CategoryId), IsUnique = true)] 
    public class Account
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AccountId { get; set; }

        [Required(ErrorMessage = "Category is required")]
        [ForeignKey("Category")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Account group is required")]
        [ForeignKey("AccountGroup")]
        public int AccountGroupId { get; set; }

        [Required(ErrorMessage = "Account name is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Account name must be between 2-50 characters")]
        public string AccountName { get; set; }

        [Required]
        public bool IsEnabled { get; set; } = true; // Default to enabled (true = enabled, false = disabled)

        public int? LinkedBookerId { get; set; }
        public virtual Booker? LinkedBooker { get; set; }


        public virtual AccountData? AccountData { get; set; }
        public virtual ProductData? ProductData { get; set; }
        public virtual Category? Category { get; set; }
        public virtual AccountGroup? AccountGroup { get; set; }
    }

}
