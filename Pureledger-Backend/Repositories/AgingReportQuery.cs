using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;

namespace PureLedger.Repositories;

internal static class AgingReportQuery
{
    // Shared by the account summary and FIFO detail so their eligible monetary entries stay identical.
    internal static IQueryable<JournalEntry> EligibleEntries(LedgerDbContext context, int categoryId, int? projectId)
    {
        var query = context.JournalEntries.AsNoTracking().Where(entry =>
            entry.CategoryId == categoryId && entry.Account!.CategoryId == categoryId &&
            entry.JournalVoucher!.IsCompleted && !entry.JournalVoucher.IsBaseVoucher);
        if (projectId.HasValue) query = query.Where(entry => entry.JournalVoucher!.ProjectId == projectId.Value);
        return query;
    }
}
