using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;

namespace PureLedger.Services;

/// <summary>
/// The only place in the runtime code that uses handwritten SQL.
/// These are lock-only statements, not data queries. EF Core has no LINQ equivalent of
/// <c>SELECT ... FOR UPDATE</c>, and faking one with a no-op UPDATE would add a write to every
/// lock and weaken the lock mode. Values are passed through <c>FromSqlInterpolated</c>, so they are
/// parameterized. Non-PostgreSQL providers (the InMemory test provider) fall back to a plain LINQ read.
/// </summary>
internal static class RowLocks
{
    public static Task<JournalVoucher?> LockJournalVoucherAsync(LedgerDbContext context, int journalVoucherId) =>
        context.Database.IsNpgsql()
            ? context.JournalVouchers
                .FromSqlInterpolated($"SELECT * FROM \"JournalVouchers\" WHERE \"JournalVoucherId\" = {journalVoucherId} FOR UPDATE")
                .SingleOrDefaultAsync()
            : context.JournalVouchers.SingleOrDefaultAsync(x => x.JournalVoucherId == journalVoucherId);

    public static Task<ApprovalVoucher?> LockApprovalVoucherAsync(LedgerDbContext context, int approvalVoucherId) =>
        context.Database.IsNpgsql()
            ? context.ApprovalVouchers
                .FromSqlInterpolated($"SELECT * FROM \"ApprovalVouchers\" WHERE \"ApprovalVoucherId\" = {approvalVoucherId} FOR UPDATE")
                .SingleOrDefaultAsync()
            : context.ApprovalVouchers.SingleOrDefaultAsync(x => x.ApprovalVoucherId == approvalVoucherId);
}
