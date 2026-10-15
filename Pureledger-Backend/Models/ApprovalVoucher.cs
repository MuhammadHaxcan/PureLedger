using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace PureLedger.Models;

public static class ApprovalStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string PostedNormally = "PostedNormally";
}

public static class ApprovalRequestKinds
{
    public const string New = "New";
    public const string Revision = "Revision";
}

public class ApprovalVoucher
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ApprovalVoucherId { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ApprovalVoucherType VoucherType { get; set; } = ApprovalVoucherType.ManualExpense;
    [MaxLength(30)] public string Status { get; set; } = ApprovalStatuses.Pending;
    [MaxLength(20)] public string RequestKind { get; set; } = ApprovalRequestKinds.New;
    public int? JournalVoucherId { get; set; }
    // JV number drawn from the JournalVouchers identity sequence when a New request is
    // submitted for approval, so the number can be shown while the voucher is pending.
    // No JournalVoucher row exists until approval, which inserts it with this exact id.
    public int? ReservedJournalVoucherId { get; set; }
    public int ProjectId { get; set; }
    public int? BookerId { get; set; }
    public int CreditDays { get; set; }
    public DateOnly SubmittedDate { get; set; }
    public DateOnly? RequestedEntryDate { get; set; }
    public Guid? SnapshotVersion { get; set; }
    public bool HadDuplicateWarningsAtSubmission { get; set; }
    public bool HadDuplicateWarningsAtDecision { get; set; }
    public int PostedByUserId { get; set; }
    public DateOnly? DecisionDate { get; set; }
    public int? DecisionByUserId { get; set; }
    [MaxLength(1000)] public string? RejectionReason { get; set; }
    public ICollection<ApprovalVoucherEntry> Entries { get; set; } = new List<ApprovalVoucherEntry>();
}

public class ApprovalVoucherEntry
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ApprovalVoucherEntryId { get; set; }
    public int ApprovalVoucherId { get; set; }
    public int? OriginalJournalEntryId { get; set; }
    // Expense snapshots retain their established ordering value. Payment & Receipt
    // snapshots deliberately leave this null because P&R has no line-number concept.
    public int? LineNumber { get; set; }
    public int AccountId { get; set; }
    public int CategoryId { get; set; }
    [MaxLength(500)] public string? Description { get; set; }
    [Column(TypeName = "decimal(14,4)")] public decimal Debit { get; set; }
    [Column(TypeName = "decimal(14,4)")] public decimal Credit { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PaymentInstrumentType? InstrumentType { get; set; }
    [MaxLength(100)] public string? ReferenceNumber { get; set; }
    [MaxLength(100)] public string? NormalizedReferenceNumber { get; set; }
    public DateOnly? InstrumentDate { get; set; }
    public ApprovalVoucher ApprovalVoucher { get; set; } = null!;
}
