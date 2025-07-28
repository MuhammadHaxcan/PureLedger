using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace PureLedger.Models
{
    [Index(nameof(CategoryName), IsUnique = true)]
    public class Category
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CategoryId { get; set; }

        [Required]
        [ForeignKey("Project")]
        public int ProjectId { get; set; }

        [Required]
        [ForeignKey("AccountType")]
        public int AccountTypeId { get; set; }

        [Required(ErrorMessage = "Category name is required")]
        [StringLength(20, ErrorMessage = "Category name must be under 20 characters")]
        public string CategoryName { get; set; }

        public Project Project { get; set; }
        public ICollection<Account> Accounts { get; set; } = new List<Account>();
        public AccountType AccountType { get; set; }
    }


}
