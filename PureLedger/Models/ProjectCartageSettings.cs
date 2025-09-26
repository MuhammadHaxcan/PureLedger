using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models
{
    public class ProjectCartageSettings
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Project")]
        public int ProjectId { get; set; }
        public virtual Project Project { get; set; }

        [Required]
        [ForeignKey("Account")]
        public int ForExpenseAccountId { get; set; }
        public virtual Account ForExpenseAccount { get; set; }

        [Required]
        [ForeignKey("Category")]
        public int CartageTransporterCategoryId { get; set; }
        public virtual Category CartageTransporterCategory { get; set; }
    }
}