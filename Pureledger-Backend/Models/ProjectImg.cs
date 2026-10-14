using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models;

public class ProjectImg
{
    [Key]
    [ForeignKey(nameof(Project))]
    public int ProjectId { get; set; }

    [Required]
    [Column(TypeName = "text")]
    public string ImageDataUrl { get; set; } = string.Empty;

    public Project Project { get; set; } = null!;
}
