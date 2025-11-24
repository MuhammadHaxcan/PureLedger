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
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int JournalVoucherId { get; set; }

        [Required(ErrorMessage = "Project is required")]
        [ForeignKey("Project")]
        public int ProjectId { get; set; }

        [Required(ErrorMessage = "Entry date is required")]
        public DateOnly EntryDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);

        [Required(ErrorMessage = "Creditable date is required")]
        [DateAfter("EntryDate", ErrorMessage = "Creditable date must be after entry date")]
        public DateOnly CreditableDate { get; set; }
        
        public bool IsCompleted { get; set; }
        
        // New property to identify base vouchers
        public bool IsBaseVoucher { get; set; } = false;

        [NotMapped]
        public decimal TotalDebit => JournalEntries?.Sum(e => e.Debit) ?? 0;

        [NotMapped]
        public decimal TotalCredit => JournalEntries?.Sum(e => e.Credit) ?? 0;
        
        // Computed property to check if this voucher has derived vouchers
        [NotMapped]
        public bool HasDerivedVouchers => OutgoingReferences?.Any(r => r.ReferenceType == "DerivedVoucher") ?? false;

        [ForeignKey("Booker")]
        public int? BookerId { get; set; }

        // Add navigation property
        public virtual Booker? Booker { get; set; }

        // Navigation properties
        public virtual Project? Project { get; set; }
        public virtual ICollection<JournalEntry> JournalEntries { get; set; } = new List<JournalEntry>();
        
        // Reference collections
        public virtual ICollection<JournalVoucherReference> OutgoingReferences { get; set; } = new List<JournalVoucherReference>();
        public virtual ICollection<JournalVoucherReference> IncomingReferences { get; set; } = new List<JournalVoucherReference>();
    }
    
    public class DateAfterAttribute : ValidationAttribute
    {
        private readonly string _comparisonProperty;

        public DateAfterAttribute(string comparisonProperty)
        {
            _comparisonProperty = comparisonProperty;
        }

        protected override ValidationResult? IsValid(object value, ValidationContext validationContext)
        {
            if (value is not DateOnly currentValue)
                return new ValidationResult("Invalid date value");

            var property = validationContext.ObjectType.GetProperty(_comparisonProperty);

            if (property == null)
                return new ValidationResult($"Unknown property: {_comparisonProperty}");

            var comparisonValue = (DateOnly)property.GetValue(validationContext.ObjectInstance)!;

            return currentValue > comparisonValue
                ? ValidationResult.Success
                : new ValidationResult(ErrorMessage);
        }
    }
}