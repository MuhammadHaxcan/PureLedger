using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using PureLedger.Models;

namespace PureLedger.ViewModels.PaymentReceipt;

public sealed class PaymentReceiptLineRequest
{
    public int? OriginalJournalEntryId { get; set; }
    public int CategoryId { get; set; }
    public int AccountId { get; set; }
    [MaxLength(500)] public string? Description { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonRequired]
    public PaymentInstrumentType InstrumentType { get; set; }
    [MaxLength(100)] public string? ReferenceNumber { get; set; }
    public DateOnly? InstrumentDate { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public class PaymentReceiptSubmissionRequest
{
    public int ProjectId { get; set; }
    public DateOnly EntryDate { get; set; }
    public byte[]? VoucherVersion { get; set; }
    public List<PaymentReceiptLineRequest> Entries { get; set; } = new();
}

public sealed class PaymentReceiptApprovalEditRequest : PaymentReceiptSubmissionRequest
{
    public Guid SnapshotVersion { get; set; }
}

public sealed class PaymentReceiptDuplicateLookupRequest
{
    public int? ApprovalVoucherId { get; set; }
    public int? JournalVoucherId { get; set; }
    public List<PaymentReceiptLineRequest> Entries { get; set; } = new();
}

public sealed class PaymentReceiptDuplicateWarning
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PaymentInstrumentType InstrumentType { get; init; }
    public int RowIndex { get; init; }
    public string ReferenceNumber { get; init; } = string.Empty;
    public string MatchedReference { get; init; } = string.Empty;
    [JsonIgnore] public bool MatchesCurrentRequest { get; init; }
    [JsonIgnore] public bool MatchesExistingVoucher { get; init; }
    [JsonIgnore] public bool MatchesPendingRequest { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class PaymentReceiptReportRequest
{
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? ProjectId { get; set; }
    public int? CategoryId { get; set; }
    public int? AccountId { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PaymentInstrumentType? InstrumentType { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
