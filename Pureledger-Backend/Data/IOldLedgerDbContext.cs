using Microsoft.EntityFrameworkCore;
using PureLedger.Models;

namespace PureLedger.Data
{
    public interface IOldLedgerDbContext : IDisposable
    {
        DbSet<LegacyJournalEntry> JournalEntries { get; set; }
    }
}
