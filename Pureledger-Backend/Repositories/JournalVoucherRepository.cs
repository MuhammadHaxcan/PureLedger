using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class JournalVoucherRepository : IJournalVoucherRepository
    {
        private readonly LedgerDbContext _context;

        public JournalVoucherRepository(LedgerDbContext context)
        {
            _context = context;
        }

        public async Task<List<JournalVoucher>> GetAllWithEntriesAsync()
        {
            return await _context.JournalVouchers
                .AsNoTracking()
                .AsSplitQuery()
                .Where(v => !_context.ApprovalVouchers.Any(a =>
                    a.Status == ApprovalStatuses.Pending &&
                    a.JournalVoucherId.HasValue &&
                    (a.JournalVoucherId.Value == v.JournalVoucherId ||
                     _context.JournalVoucherReferences.Any(r =>
                         r.MainVoucherId == a.JournalVoucherId.Value &&
                         r.ReferencedVoucherId == v.JournalVoucherId &&
                         (r.ReferenceType == "ForCharges" || r.ReferenceType == "Cartage")))))
                .Include(jv => jv.JournalEntries)
                    .ThenInclude(e => e.Account)
                .OrderByDescending(jv => jv.EntryDate)
                .ToListAsync();
        }

        public async Task<JournalVoucher?> GetWithEntriesAsync(int id)
        {
            return await _context.JournalVouchers
                .AsSplitQuery()
                .Include(jv => jv.Project)
                .Include(jv => jv.JournalEntries)
                    .ThenInclude(e => e.Account)
                .Include(jv => jv.JournalEntries)
                    .ThenInclude(e => e.Category)
                .Include(jv => jv.JournalEntries)
                    .ThenInclude(e => e.InstrumentDetail)
                .FirstOrDefaultAsync(jv => jv.JournalVoucherId == id);
        }

        public JournalVoucher Add(JournalVoucher voucher)
        {
            _context.JournalVouchers.Add(voucher);
            return voucher;
        }

        public JournalVoucher Update(JournalVoucher voucher)
        {
            _context.JournalVouchers.Update(voucher);
            return voucher;
        }

        public async Task<List<JournalVoucher>> GetFilteredVouchersAsync(
            DateOnly? sinceDate,
            DateOnly? toDate,
            int? projectId,
            bool includeEntries)
        {
            IQueryable<JournalVoucher> query = _context.JournalVouchers
                // Delivery Challan/FOR/cartage selectors must conservatively omit
                // every instrument-linked voucher, including a partial/corrupt graph.
                .Where(jv => !jv.JournalEntries.Any(je => je.InstrumentDetail != null))
                .Include(jv => jv.Project);
                
            if (includeEntries)
            {
                query = query.Include(jv => jv.JournalEntries)
                    .ThenInclude(je => je.Account);
            }
            
            if (sinceDate.HasValue)
            {
                query = query.Where(jv => jv.EntryDate >= sinceDate.Value);
            }
            
            if (toDate.HasValue)
            {
                query = query.Where(jv => jv.EntryDate <= toDate.Value);
            }
            
            if (projectId.HasValue)
            {
                query = query.Where(jv => jv.ProjectId == projectId.Value);
            }
            
            return await query.OrderByDescending(jv => jv.EntryDate).ToListAsync();
        }

        public async Task<(List<JournalVoucher> Vouchers, int TotalCount)> GetVouchersWithModifiedEntriesPaginatedAsync(int? projectId, int page, int pageSize)
        {
            page = Math.Max(0, page);
            pageSize = Math.Clamp(pageSize, 1, 2500);

            var query = _context.Set<JournalVoucher>()
                .AsNoTracking()
                .Include(v => v.JournalEntries)
                    .ThenInclude(e => e.Category)
                .Include(v => v.JournalEntries)
                    .ThenInclude(e => e.Account)
                .Where(v => !_context.ApprovalVouchers.Any(a =>
                    a.Status == ApprovalStatuses.Pending &&
                    a.JournalVoucherId.HasValue &&
                    (a.JournalVoucherId.Value == v.JournalVoucherId ||
                     _context.JournalVoucherReferences.Any(r =>
                         r.MainVoucherId == a.JournalVoucherId.Value &&
                         r.ReferencedVoucherId == v.JournalVoucherId &&
                         (r.ReferenceType == "ForCharges" || r.ReferenceType == "Cartage")))))
                .Where(v => v.JournalEntries.Any(e => e.IsEdited));

            if (projectId.HasValue)
            {
                query = query.Where(v => v.ProjectId == projectId.Value);
            }

            var totalCount = await query.CountAsync();

            var vouchers = await query
                .OrderByDescending(v => v.EntryDate)
                .ThenByDescending(v => v.JournalVoucherId)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (vouchers, totalCount);
        }

        public async Task<bool> DeleteAsync(int journalVoucherId)
        {
            if (await _context.JournalEntryInstrumentDetails.AsNoTracking()
                    .AnyAsync(x => x.JournalEntry.JournalVoucherId == journalVoucherId))
                throw new InvalidOperationException(
                    "Payment & Receipt vouchers cannot be deleted through the generic voucher workflow.");
            var voucher = await _context.JournalVouchers
                .Include(jv => jv.JournalEntries)
                .FirstOrDefaultAsync(jv => jv.JournalVoucherId == journalVoucherId);

            if (voucher == null)
                return false;

            // Remove related entries first if needed (cascade delete may handle this)
            if (voucher.JournalEntries != null)
                _context.JournalEntries.RemoveRange(voucher.JournalEntries);

            _context.JournalVouchers.Remove(voucher);
            return true;
        }

        public async Task<(List<JournalVoucher> Vouchers, int TotalCount)> GetAllBaseVouchersPaginatedAsync(
            int? projectId, DateOnly? startDate, DateOnly? endDate, bool hideFullyProcessed, int page, int pageSize)
        {
            page = Math.Max(0, page);
            pageSize = Math.Clamp(pageSize, 1, 2500);

            var baseQuery = _context.JournalVouchers
                .AsNoTracking()
                .Where(v => v.IsBaseVoucher);

            if (projectId.HasValue)
            {
                baseQuery = baseQuery.Where(v => v.ProjectId == projectId.Value);
            }

            if (startDate.HasValue)
            {
                baseQuery = baseQuery.Where(v => v.EntryDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                baseQuery = baseQuery.Where(v => v.EntryDate <= endDate.Value);
            }

            if (hideFullyProcessed)
            {
                baseQuery = baseQuery.Where(v => v.JournalEntries.Any(e => !e.IsProcessed && e.Quantity > 0));
            }

            var query = baseQuery
                .Include(v => v.Project)
                .Include(v => v.JournalEntries)
                    .ThenInclude(e => e.Account);

            var totalCount = await baseQuery.CountAsync();

            var vouchers = await query
                .OrderByDescending(v => v.EntryDate)
                .ThenByDescending(v => v.JournalVoucherId)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (vouchers, totalCount);
        }

        public async Task<JournalVoucher?> GetBaseVoucherWithUnprocessedEntriesAsync(int id)
        {
            return await _context.JournalVouchers
                .AsSplitQuery()
                .Where(v => v.IsBaseVoucher && v.JournalVoucherId == id)
                .Include(v => v.Project)
                .Include(v => v.JournalEntries)
                    .ThenInclude(e => e.Account)
                .Include(v => v.JournalEntries)
                    .ThenInclude(e => e.Category)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Batch fetch multiple vouchers with their entries - eliminates N+1 query pattern
        /// </summary>
        public async Task<List<JournalVoucher>> GetByIdsWithEntriesAsync(List<int> voucherIds)
        {
            if (voucherIds == null || !voucherIds.Any())
                return new List<JournalVoucher>();

            return await _context.JournalVouchers
                .AsNoTracking()
                .AsSplitQuery()
                .Include(jv => jv.JournalEntries)
                    .ThenInclude(e => e.Account)
                .Include(jv => jv.JournalEntries)
                    .ThenInclude(e => e.Category)
                .Where(jv => 
                    voucherIds.Contains(jv.JournalVoucherId) &&
                    jv.IsCompleted &&
                    !jv.IsBaseVoucher)
                .ToListAsync();
        }

        /// <summary>
        /// Get all base vouchers with entries for summary statistics calculation
        /// </summary>
        public async Task<List<JournalVoucher>> GetBaseVouchersSummaryDataAsync(int? projectId, DateOnly? startDate = null, DateOnly? endDate = null)
        {
            IQueryable<JournalVoucher> baseQuery = _context.JournalVouchers
                .AsNoTracking()
                .Where(v => v.IsBaseVoucher);

            if (projectId.HasValue)
            {
                baseQuery = baseQuery.Where(v => v.ProjectId == projectId.Value);
            }

            if (startDate.HasValue)
            {
                baseQuery = baseQuery.Where(v => v.EntryDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                baseQuery = baseQuery.Where(v => v.EntryDate <= endDate.Value);
            }

            var query = baseQuery
                .AsSplitQuery()
                .Include(v => v.Project)
                .Include(v => v.JournalEntries)
                    .ThenInclude(e => e.Account)
                .Include(v => v.JournalEntries)
                    .ThenInclude(e => e.Category);

            return await query
                .OrderByDescending(v => v.EntryDate)
                .ThenByDescending(v => v.JournalVoucherId)
                .ToListAsync();
        }
    }
}
