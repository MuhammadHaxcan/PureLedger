using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models
{
    public class AccountGroup
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AccountGroupId { get; set; }

        [Required]
        [StringLength(20)]
        public string AccountGroupName { get; set; }
    }
}