using System.ComponentModel.DataAnnotations;

namespace PureLedger.ViewModels.Reporting;

public sealed class AgingFifoRequest
{
    [Range(1, int.MaxValue)] public int ProjectId { get; set; }
    [Range(1, int.MaxValue)] public int CategoryId { get; set; }
    [Range(1, int.MaxValue)] public int AccountId { get; set; }
    public DateOnly ReferenceDate { get; set; }
    public string Scope { get; set; } = "full";
    public string View { get; set; } = "outstanding";
    public int? EntryId { get; set; }
    [Range(0, int.MaxValue)] public int Page { get; set; }
    [Range(1, 250)] public int PageSize { get; set; } = 100;
}

public class AgingFifoMovement
{
    public int JournalEntryId { get; set; }
    public int JournalVoucherId { get; set; }
    public DateOnly EntryDate { get; set; }
    public DateOnly CreditableDate { get; set; }
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public sealed class AgingFifoRow : AgingFifoMovement
{
    public decimal RemainingDebit { get; set; }
    public decimal UnappliedCredit { get; set; }
    public decimal SettledDebit => Debit - RemainingDebit;
    public decimal AppliedCredit => Credit - UnappliedCredit;
    public decimal RunningBalance { get; set; }
    public int AllocationCount { get; set; }
}

public sealed class AgingFifoAllocation
{
    public int DebitEntryId { get; set; }
    public int CreditEntryId { get; set; }
    public decimal Amount { get; set; }
    public int DebitVoucherId { get; set; }
    public int CreditVoucherId { get; set; }
    public DateOnly DebitEntryDate { get; set; }
    public DateOnly CreditEntryDate { get; set; }
    public string? DebitDescription { get; set; }
    public string? CreditDescription { get; set; }
}

public sealed class AgingFifoSummary
{
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    public decimal OutstandingDebits { get; set; }
    public decimal UnappliedCredits { get; set; }
    public decimal NetBalance => TotalDebits - TotalCredits;
    public int EntryCount { get; set; }
    public int MixedEntryCount { get; set; }
}

public sealed class AgingFifoResponse
{
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string View { get; set; } = string.Empty;
    public string LedgerVersion { get; set; } = string.Empty;
    public AgingFifoSummary Summary { get; set; } = new();
    public List<AgingFifoRow> Rows { get; set; } = new();
    public List<AgingFifoAllocation> Allocations { get; set; } = new();
    public int TotalRecords { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMoreData { get; set; }
}
