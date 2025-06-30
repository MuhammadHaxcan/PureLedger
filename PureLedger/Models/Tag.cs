using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PureLedger.Models
{
    public class Tag
    {
        [Key]
        public int TagId { get; set; }
        [Required]
        public string TagName { get; set; }

        // Many-to-many navigation property
        public virtual ICollection<JournalEntryTag> JournalEntryTags { get; set; } = new List<JournalEntryTag>();
    }
}