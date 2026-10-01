namespace PureLedger.Services.Interface;

public enum PaymentReceiptApprovalCheckpoint
{
    VoucherSaved,
    EntriesSaved,
    InstrumentDetailsSaved
}

/// <summary>
/// Provides deterministic approval checkpoints for rollback verification and
/// operational fault testing. The production implementation is a no-op.
/// </summary>
public interface IPaymentReceiptApprovalCheckpoint
{
    Task ReachedAsync(PaymentReceiptApprovalCheckpoint checkpoint);
}

public sealed class NoOpPaymentReceiptApprovalCheckpoint : IPaymentReceiptApprovalCheckpoint
{
    public Task ReachedAsync(PaymentReceiptApprovalCheckpoint checkpoint) => Task.CompletedTask;
}
