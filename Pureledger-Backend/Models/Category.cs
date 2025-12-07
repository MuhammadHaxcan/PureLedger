using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace PureLedger.Models
{
    [Index(nameof(CategoryName), nameof(ProjectId), IsUnique = true)]
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
        [StringLength(30, ErrorMessage = "Category name must be under 30 characters")]
        public string CategoryName { get; set; } = string.Empty;

        [ValidateNever]
        public Project? Project { get; set; }
        
        [ValidateNever]
        public ICollection<Account> Accounts { get; set; } = new List<Account>();
        
        [ValidateNever]
        public AccountType? AccountType { get; set; }
    }


}
