using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Services.Interface;

namespace PureLedger.Services;

/// <summary>
/// Reserves JV numbers for vouchers that are pending approval. A JV number is the identity value
/// of <c>JournalVouchers.JournalVoucherId</c>, so the number is drawn from that column's own
/// sequence; the sequence never hands the same value out again, which keeps ordinary vouchers
/// from colliding with a reserved one. EF Core has no LINQ equivalent of <c>nextval</c>, so this is
/// a fixed, parameterless statement (like <see cref="RowLocks"/>). Non-PostgreSQL providers (the
/// InMemory test provider) fall back to a plain LINQ read.
/// </summary>
internal static class JournalVoucherNumbers
{
    public static async Task<int> ReserveAsync(LedgerDbContext context)
    {
        if (context.Database.IsNpgsql())
        {
            return await context.Database
                .SqlQuery<int>($"""SELECT nextval(pg_get_serial_sequence('"JournalVouchers"', 'JournalVoucherId'))::int AS "Value" """)
                .SingleAsync();
        }

        var maxVoucher = await context.JournalVouchers.AsNoTracking()
            .MaxAsync(x => (int?)x.JournalVoucherId) ?? 0;
        var maxReserved = await context.ApprovalVouchers.AsNoTracking()
            .MaxAsync(x => x.ReservedJournalVoucherId) ?? 0;
        return Math.Max(maxVoucher, maxReserved) + 1;
    }

    /// <summary>
    /// Builds the voucher for an approval, using the reserved JV number when the request holds one.
    /// Fails with a conflict instead of a raw key violation if that id has since been taken
    /// (for example after a database restore rewound the sequence).
    /// </summary>
    public static async Task ApplyReservedIdAsync(LedgerDbContext context, ApprovalVoucher approval, JournalVoucher voucher)
    {
        if (!approval.ReservedJournalVoucherId.HasValue) return;
        var reserved = approval.ReservedJournalVoucherId.Value;
        if (await context.JournalVouchers.AsNoTracking().AnyAsync(x => x.JournalVoucherId == reserved))
            throw new ApprovalConflictException(
                $"The reserved voucher number JV-{reserved} is already in use. Reject this request and submit it again.");
        voucher.JournalVoucherId = reserved;
    }
}
