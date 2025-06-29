using System.ComponentModel.DataAnnotations;

namespace PureLedger.Models
{
    public class Tag
    {
        [Key]
        public int TagId { get; set; }
        [Required]
        public string TagName { get; set; }
    }
}
