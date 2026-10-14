using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PureLedger.Repositories;
using PureLedger.ViewModels.Reporting;

namespace PureLedger.Services;

public partial class ReportingService
{
    public async Task<AgingFifoResponse> GetAgingFifoAsync(AgingFifoRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ProjectId <= 0 || request.CategoryId <= 0 || request.AccountId <= 0 || request.ReferenceDate == default)
            throw new ArgumentException("Project, category, account and reference date are required.");
        if (request.Scope is not ("full" or "due")) throw new ArgumentException("Invalid FIFO scope.");
        if (request.View is not ("outstanding" or "credits" or "history" or "allocations")) throw new ArgumentException("Invalid FIFO view.");
        if (request.Page < 0 || request.PageSize is < 1 or > 250)
            throw new ArgumentException("Page must be nonnegative and page size must be between 1 and 250.");
        if (request.View == "allocations" && request.EntryId.GetValueOrDefault() <= 0)
            throw new ArgumentException("An entry is required to view allocations.");

        var account = await _context.Accounts.AsNoTracking()
            .Where(x => x.AccountId == request.AccountId && x.CategoryId == request.CategoryId &&
                x.Category!.ProjectId == request.ProjectId)
            .Select(x => new { x.AccountId, x.AccountName }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Account was not found in the selected project and category.");
        var query = AgingReportQuery.EligibleEntries(_context, request.CategoryId, request.ProjectId)
            .Where(x => x.AccountId == request.AccountId);
        if (request.Scope == "due") query = query.Where(x => x.JournalVoucher!.CreditableDate <= request.ReferenceDate);
        var movements = await query.OrderBy(x => x.JournalVoucher!.EntryDate)
            .ThenBy(x => x.JournalVoucherId).ThenBy(x => x.JournalEntryId)
            .Select(x => new AgingFifoMovement
            {
                JournalEntryId = x.JournalEntryId, JournalVoucherId = x.JournalVoucherId,
                EntryDate = x.JournalVoucher!.EntryDate, CreditableDate = x.JournalVoucher.CreditableDate,
                Description = x.Description, Debit = x.Debit, Credit = x.Credit
            }).ToListAsync(cancellationToken);

        // Replay ALL eligible entries before selecting a display page. Later credits can settle early debits.
        var result = AgingFifoCalculator.Calculate(movements, cancellationToken);
        var response = new AgingFifoResponse
        {
            AccountId = account.AccountId, AccountName = account.AccountName, Scope = request.Scope, View = request.View,
            LedgerVersion = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(movements)))),
            Page = request.Page, PageSize = request.PageSize,
            Summary = new AgingFifoSummary
            {
                TotalDebits = result.TotalDebits, TotalCredits = result.TotalCredits,
                OutstandingDebits = result.OutstandingDebits, UnappliedCredits = result.UnappliedCredits,
                EntryCount = result.Rows.Count, MixedEntryCount = result.MixedEntryCount
            }
        };
        var offset = (long)request.Page * request.PageSize;
        if (request.View == "allocations")
        {
            if (!result.Rows.Any(x => x.JournalEntryId == request.EntryId))
                throw new KeyNotFoundException("Entry was not found in this account and scope.");
            var allocations = result.Allocations.Where(x => x.DebitEntryId == request.EntryId || x.CreditEntryId == request.EntryId).ToList();
            response.TotalRecords = allocations.Count;
            if (offset < allocations.Count) response.Allocations = allocations.Skip((int)offset).Take(request.PageSize).ToList();
        }
        else
        {
            var rows = result.Rows.Where(x => request.View switch
            {
                "outstanding" => x.RemainingDebit > 0,
                "credits" => x.Credit > 0,
                _ => true
            }).ToList();
            response.TotalRecords = rows.Count;
            if (offset < rows.Count) response.Rows = rows.Skip((int)offset).Take(request.PageSize).ToList();
        }
        response.HasMoreData = offset + request.PageSize < response.TotalRecords;
        return response;
    }
}
