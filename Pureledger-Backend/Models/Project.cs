using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models
{
    [Index(nameof(ProjectName), IsUnique = true)]
    public class Project
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProjectId { get; set; }

        [Required(ErrorMessage = "Project name is required")]
        [StringLength(100, ErrorMessage = "Project name must be under 100 characters")]
        public string ProjectName { get; set; }

        public ProjectImg? ProjectImg { get; set; }

        public ICollection<Category> Categories { get; set; } = new List<Category>();
    }
}
