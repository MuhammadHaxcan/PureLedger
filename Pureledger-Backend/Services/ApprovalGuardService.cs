using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Services.Interface;

namespace PureLedger.Services;

public sealed class ApprovalGuardService : IApprovalGuardService
{
    private readonly LedgerDbContext _context;
    public ApprovalGuardService(LedgerDbContext context) => _context = context;

    public Task<bool> IsVoucherPendingAsync(int journalVoucherId) =>
        _context.ApprovalVouchers.AsNoTracking().AnyAsync(a =>
            a.Status == ApprovalStatuses.Pending &&
            (a.JournalVoucherId == journalVoucherId ||
             a.JournalVoucherId != null &&
             _context.JournalVoucherReferences.Any(r =>
                 r.MainVoucherId == a.JournalVoucherId &&
                 r.ReferencedVoucherId == journalVoucherId &&
                 (r.ReferenceType == "ForCharges" || r.ReferenceType == "Cartage"))));

    public async Task EnsureVoucherNotPendingAsync(int journalVoucherId, bool lockVoucher = false)
    {
        if (lockVoucher)
        {
            var locked = await RowLocks.LockJournalVoucherAsync(_context, journalVoucherId);
            if (locked == null) throw new InvalidOperationException($"Voucher {journalVoucherId} was not found.");
        }
        if (await IsVoucherPendingAsync(journalVoucherId))
            throw new ApprovalConflictException("This voucher is pending approval and cannot be changed or printed.");
    }
}
