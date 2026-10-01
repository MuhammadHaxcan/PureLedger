using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Services.Interface;

namespace PureLedger.Services;

public sealed class PaymentReceiptClassifier : IPaymentReceiptClassifier
{
    private readonly LedgerDbContext _context;

    public PaymentReceiptClassifier(LedgerDbContext context) => _context = context;

    public Task<bool> HasAnyInstrumentDetailsAsync(int journalVoucherId) =>
        _context.JournalEntryInstrumentDetails.AsNoTracking()
            .AnyAsync(x => x.JournalEntry.JournalVoucherId == journalVoucherId);

    public async Task<bool> IsStructurallyValidAsync(int journalVoucherId)
    {
        var voucher = await _context.JournalVouchers.AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.JournalEntries).ThenInclude(x => x.Account)
            .Include(x => x.JournalEntries).ThenInclude(x => x.Category)
            .Include(x => x.JournalEntries).ThenInclude(x => x.InstrumentDetail)
            .SingleOrDefaultAsync(x => x.JournalVoucherId == journalVoucherId);
        if (voucher == null || voucher.CreditableDate != voucher.EntryDate ||
            voucher.IsBaseVoucher || voucher.IsStockTransfer || voucher.PendingForCharges ||
            voucher.BookerId != null || voucher.JournalEntries.Count < 2)
            return false;
        if (await _context.DeliveryChallanEntries.AsNoTracking()
                .AnyAsync(x => x.JournalEntry.JournalVoucherId == journalVoucherId) ||
            await _context.DeliveryChallanVouchers.AsNoTracking().AnyAsync(x => x.JournalVoucherId == journalVoucherId) ||
            await _context.JournalVoucherReferences.AsNoTracking()
                .AnyAsync(x => x.MainVoucherId == journalVoucherId || x.ReferencedVoucherId == journalVoucherId))
            return false;

        if (voucher.JournalEntries.Any(x => x.InstrumentDetail == null))
            return false;
        var ordered = voucher.JournalEntries.OrderBy(x => x.JournalEntryId).ToList();
        if (ordered.Any(x => x.Account?.AccountGroupId != 1 ||
                             x.Account.CategoryId != x.CategoryId ||
                             x.Category?.ProjectId != voucher.ProjectId ||
                             x.Quantity != 0 || x.Rate != 0 || x.Discount != 0 ||
                             x.DiscountPercent != null || x.Rbp != "N" || !x.IsProcessed))
            return false;
        if (ordered.Any(x => !ValidInstrument(x.InstrumentDetail!)))
            return false;
        return Math.Abs(ordered.Sum(x => x.Debit) - ordered.Sum(x => x.Credit)) <= 0.01m &&
               ordered.All(x => x.Debit >= 0 && x.Credit >= 0 && (x.Debit > 0) ^ (x.Credit > 0));
    }

    public async Task<bool> IsApprovedAsync(int journalVoucherId)
    {
        if (!await IsStructurallyValidAsync(journalVoucherId)) return false;
        var voucher = await _context.JournalVouchers.AsNoTracking()
            .Where(x => x.JournalVoucherId == journalVoucherId)
            .Select(x => new { x.ProjectId, x.IsCompleted }).SingleOrDefaultAsync();
        return voucher?.IsCompleted == true &&
               await _context.ApprovalVouchers.AsNoTracking().AnyAsync(x =>
                   x.JournalVoucherId == journalVoucherId &&
                   x.VoucherType == ApprovalVoucherType.PaymentReceipt &&
                   x.Status == ApprovalStatuses.Approved && x.ProjectId == voucher.ProjectId &&
                   x.Entries.Count >= 2);
    }

    public async Task EnsureNotPaymentReceiptAsync(int journalVoucherId, string operation)
    {
        if (await HasAnyInstrumentDetailsAsync(journalVoucherId))
            throw new PaymentReceiptIntegrityException(
                $"Payment & Receipt vouchers cannot be {operation} through the generic voucher workflow.");
    }

    public async Task<VoucherType> ResolvePrintableTypeAsync(int journalVoucherId, VoucherType requestedType)
    {
        if (!await HasAnyInstrumentDetailsAsync(journalVoucherId))
        {
            if (requestedType == VoucherType.PaymentReceipt)
                throw new PaymentReceiptIntegrityException(
                    "The requested voucher is not a Payment & Receipt voucher.");
            return requestedType;
        }
        if (!await IsApprovedAsync(journalVoucherId))
            throw new PaymentReceiptIntegrityException(
                "This Payment & Receipt voucher is pending, incomplete, corrupt, or lacks approved provenance.");
        return VoucherType.PaymentReceipt;
    }

    private static bool ValidInstrument(JournalEntryInstrumentDetail detail) =>
        Enum.IsDefined(detail.InstrumentType) && (detail.InstrumentType == PaymentInstrumentType.Cash
            ? detail.ReferenceNumber == null && detail.NormalizedReferenceNumber == null && detail.InstrumentDate == null
            : !string.IsNullOrWhiteSpace(detail.ReferenceNumber) &&
              detail.ReferenceNumber == detail.ReferenceNumber.Trim() &&
              detail.NormalizedReferenceNumber == detail.ReferenceNumber.ToUpperInvariant() &&
              detail.InstrumentDate.HasValue);
}
