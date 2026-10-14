using System.Text.RegularExpressions;
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

    private static readonly PaymentInstrumentType[] DefinedInstrumentTypes = Enum.GetValues<PaymentInstrumentType>();

    // A reference number containing anything outside ASCII is validated by the original in-memory rules,
    // because .NET and PostgreSQL disagree on Unicode whitespace trimming and upper-casing. Such vouchers are
    // rare, so the exact check only ever runs on that small subset; everything else is decided in SQL.
    private const string AsciiWhitespace = @"[\u0009\u000A\u000B\u000C\u000D\u0020]";
    private const string LeadingOrTrailingWhitespace = "^" + AsciiWhitespace + "|" + AsciiWhitespace + "$";
    private const string NonAscii = @"[^\u0001-\u007F]";

    public async Task<bool> IsStructurallyValidAsync(int journalVoucherId) =>
        await (await WhereStructurallyValidAsync(_context.JournalVouchers.AsNoTracking()
            .Where(x => x.JournalVoucherId == journalVoucherId))).AnyAsync();

    public async Task<bool> IsApprovedAsync(int journalVoucherId) =>
        await (await WhereApprovedAsync(_context.JournalVouchers.AsNoTracking()
            .Where(x => x.JournalVoucherId == journalVoucherId))).AnyAsync();

    public async Task<IQueryable<JournalVoucher>> WhereStructurallyValidAsync(IQueryable<JournalVoucher> vouchers)
    {
        var exact = new List<int>();
        foreach (var id in await VouchersWithNonAsciiReferencesAsync(vouchers))
            if (await IsStructurallyValidLegacyAsync(id)) exact.Add(id);
        return StructurallyValid(vouchers, exact);
    }

    public async Task<IQueryable<JournalVoucher>> WhereApprovedAsync(IQueryable<JournalVoucher> vouchers)
    {
        var exact = new List<int>();
        foreach (var id in await VouchersWithNonAsciiReferencesAsync(vouchers))
            if (await IsApprovedLegacyAsync(id)) exact.Add(id);
        return StructurallyValid(vouchers, exact, approvedOnly: true);
    }

    private Task<List<int>> VouchersWithNonAsciiReferencesAsync(IQueryable<JournalVoucher> vouchers) =>
        vouchers.Where(v => v.JournalEntries.Any(e => e.InstrumentDetail != null &&
                (e.InstrumentDetail.ReferenceNumber != null && Regex.IsMatch(e.InstrumentDetail.ReferenceNumber, NonAscii) ||
                 e.InstrumentDetail.NormalizedReferenceNumber != null && Regex.IsMatch(e.InstrumentDetail.NormalizedReferenceNumber, NonAscii))))
            .Select(v => v.JournalVoucherId).ToListAsync();

    // The one definition of a valid Payment & Receipt voucher for bulk use. It is expressed as a query so reports
    // can filter, count and page in the database instead of loading and checking each voucher separately.
    private IQueryable<JournalVoucher> StructurallyValid(
        IQueryable<JournalVoucher> vouchers, IReadOnlyCollection<int> exactIds, bool approvedOnly = false) =>
        vouchers.Where(v => exactIds.Contains(v.JournalVoucherId) || (
            v.CreditableDate == v.EntryDate &&
            !v.IsBaseVoucher && !v.IsStockTransfer && !v.PendingForCharges && v.BookerId == null &&
            v.JournalEntries.Count() >= 2 &&
            !_context.DeliveryChallanEntries.Any(x => x.JournalEntry.JournalVoucherId == v.JournalVoucherId) &&
            !_context.DeliveryChallanVouchers.Any(x => x.JournalVoucherId == v.JournalVoucherId) &&
            !_context.JournalVoucherReferences.Any(x =>
                x.MainVoucherId == v.JournalVoucherId || x.ReferencedVoucherId == v.JournalVoucherId) &&
            v.JournalEntries.All(e =>
                e.InstrumentDetail != null &&
                e.Account != null && e.Account.AccountGroupId == 1 && e.Account.CategoryId == e.CategoryId &&
                e.Category != null && e.Category.ProjectId == v.ProjectId &&
                e.Quantity == 0 && e.Rate == 0 && e.Discount == 0 && e.DiscountPercent == null &&
                e.Rbp == "N" && e.IsProcessed &&
                e.Debit >= 0 && e.Credit >= 0 && (e.Debit > 0) != (e.Credit > 0) &&
                DefinedInstrumentTypes.Contains(e.InstrumentDetail.InstrumentType) &&
                (e.InstrumentDetail.InstrumentType == PaymentInstrumentType.Cash
                    ? e.InstrumentDetail.ReferenceNumber == null &&
                      e.InstrumentDetail.NormalizedReferenceNumber == null &&
                      e.InstrumentDetail.InstrumentDate == null
                    : e.InstrumentDetail.ReferenceNumber != null && e.InstrumentDetail.ReferenceNumber != "" &&
                      !Regex.IsMatch(e.InstrumentDetail.ReferenceNumber, LeadingOrTrailingWhitespace) &&
                      !Regex.IsMatch(e.InstrumentDetail.ReferenceNumber, NonAscii) &&
                      e.InstrumentDetail.NormalizedReferenceNumber == e.InstrumentDetail.ReferenceNumber.ToUpper() &&
                      e.InstrumentDetail.InstrumentDate != null)) &&
            Math.Abs(v.JournalEntries.Sum(e => e.Debit) - v.JournalEntries.Sum(e => e.Credit)) <= 0.01m &&
            (!approvedOnly || (
                v.IsCompleted &&
                _context.ApprovalVouchers.Any(a =>
                    a.JournalVoucherId == v.JournalVoucherId &&
                    a.VoucherType == ApprovalVoucherType.PaymentReceipt &&
                    a.Status == ApprovalStatuses.Approved && a.ProjectId == v.ProjectId &&
                    a.Entries.Count >= 2)))));

    private async Task<bool> IsStructurallyValidLegacyAsync(int journalVoucherId)
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
        if (ordered.Any(x => !ValidInstrumentLegacy(x.InstrumentDetail!)))
            return false;
        return Math.Abs(ordered.Sum(x => x.Debit) - ordered.Sum(x => x.Credit)) <= 0.01m &&
               ordered.All(x => x.Debit >= 0 && x.Credit >= 0 && (x.Debit > 0) ^ (x.Credit > 0));
    }

    private async Task<bool> IsApprovedLegacyAsync(int journalVoucherId)
    {
        if (!await IsStructurallyValidLegacyAsync(journalVoucherId)) return false;
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

    private static bool ValidInstrumentLegacy(JournalEntryInstrumentDetail detail) =>
        Enum.IsDefined(detail.InstrumentType) && (detail.InstrumentType == PaymentInstrumentType.Cash
            ? detail.ReferenceNumber == null && detail.NormalizedReferenceNumber == null && detail.InstrumentDate == null
            : !string.IsNullOrWhiteSpace(detail.ReferenceNumber) &&
              detail.ReferenceNumber == detail.ReferenceNumber.Trim() &&
              detail.NormalizedReferenceNumber == detail.ReferenceNumber.ToUpperInvariant() &&
              detail.InstrumentDate.HasValue);
}
