using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PureLedger.Models
{
    public class JournalEntryTag
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int JournalEntryTagId { get; set; }

        [Required]
        [ForeignKey("JournalEntry")]
        public int JournalEntryId { get; set; }

        [Required]
        [ForeignKey("Tag")]
        public int TagId { get; set; }

        // Navigation properties
        public virtual JournalEntry JournalEntry { get; set; }
        public virtual Tag Tag { get; set; }
    }
}
