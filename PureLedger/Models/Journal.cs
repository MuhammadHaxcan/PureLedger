using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace PureLedger.Models
{
    public class JournalVoucher
    {
        [Key]
        public int JvId { get; set; }

        [Required]
        public int ProjectId { get; set; }

        [Required]
        public DateOnly EntryDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
        public DateOnly? CreditableDate { get; set; }

        public bool IsProcessed { get; set; }

        [NotMapped]
        public decimal TotalDebit => JournalEntries?.Sum(e => e.Debit) ?? 0;

        [NotMapped]
        public decimal TotalCredit => JournalEntries?.Sum(e => e.Credit) ?? 0;

        [ForeignKey("ProjectId")]
        public virtual Project? Project { get; set; }

        public virtual ICollection<JournalEntry> JournalEntries { get; set; } = new List<JournalEntry>();
    }

    public class JournalEntry
    {
        [Key]
        public int EntryId { get; set; }

        [Required]
        public int JournalVoucherId { get; set; }

        [Required]
        public int AccountId { get; set; }

        [Required]
        public int CategoryId { get; set; }
        public int? LocationId { get; set; } 

        public bool IsProcessed { get; set; } = false;

        public bool IsEdited { get; set; } = false;
        [StringLength(500)]
        public string? Description { get; set; }

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Rate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? DiscountPercent { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Discount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Debit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Credit { get; set; }

        [StringLength(1)]
        public string? Rbp { get; set; } = "Y";

        [ForeignKey("AccountId")]
        public virtual Account? Account { get; set; }

        [ForeignKey("CategoryId")]
        public virtual Category? Category { get; set; }

        [ForeignKey("JournalVoucherId")]
        public virtual JournalVoucher? JournalVoucher { get; set; }
        [ForeignKey("LocationId")]
        public virtual Location? Location { get; set; }

        // Many-to-many navigation property
        public virtual ICollection<JournalEntryTag> JournalEntryTags { get; set; } = new List<JournalEntryTag>();
    }

    // Join entity for many-to-many relationship
    public class JournalEntryTag
    {
        [Key]
        public int JournalEntryTagId { get; set; }
        public int JournalEntryId { get; set; }
        public JournalEntry JournalEntry { get; set; }

        public Tag Tag { get; set; }
    }
}