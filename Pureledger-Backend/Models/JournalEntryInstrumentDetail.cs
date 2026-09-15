using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace PureLedger.Models;

public sealed class JournalEntryInstrumentDetail
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int JournalEntryInstrumentDetailId { get; set; }

    public int JournalEntryId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PaymentInstrumentType InstrumentType { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    [MaxLength(100)]
    public string? NormalizedReferenceNumber { get; set; }

    public DateOnly? InstrumentDate { get; set; }

    public JournalEntry JournalEntry { get; set; } = null!;
}
