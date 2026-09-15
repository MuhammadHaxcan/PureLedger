using PureLedger.ViewModels.Home;

namespace PureLedger.ViewModels.Approval;

public sealed class ApprovalSubmissionResult
{
    public bool IsPending { get; init; }
    public int? ApprovalVoucherId { get; init; }
    public int? JournalVoucherId { get; init; }
    public int ProjectId { get; init; }
}

public sealed class ApprovalDecisionRequest
{
    public string? Reason { get; set; }
    public Guid? SnapshotVersion { get; set; }
}

public sealed class ApprovalEditRequest
{
    public int ProjectId { get; set; }
    public int CreditDays { get; set; }
    public int? BookerId { get; set; }
    public DateOnly EntryDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly DueDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public bool IsBaseVoucher { get; set; }
    public bool SaveAsRecipe { get; set; }
    public string? RecipeName { get; set; }
    public List<JournalEntryRowModel> Entries { get; set; } = new();

    public JournalEntryViewModel ToJournalEntryViewModel() => new()
    {
        ProjectId = ProjectId,
        CreditDays = CreditDays,
        BookerId = BookerId,
        EntryDate = EntryDate,
        DueDate = DueDate,
        IsBaseVoucher = IsBaseVoucher,
        SaveAsRecipe = SaveAsRecipe,
        RecipeName = RecipeName,
        Entries = Entries
    };
}
