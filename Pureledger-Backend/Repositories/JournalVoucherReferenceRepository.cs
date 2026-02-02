using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PureLedger.Repositories
{
    public class JournalVoucherReferenceRepository : IJournalVoucherReferenceRepository
    {
        private readonly TenantDbContext _context;

        public JournalVoucherReferenceRepository(TenantDbContext context)
        {
            _context = context;
        }

        public async Task<JournalVoucherReference> AddAsync(JournalVoucherReference reference)
        {
            try
            {
                var result = await _context.JournalVoucherReferences.AddAsync(reference);
                return result.Entity;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<JournalVoucherReference?> GetByIdAsync(int id)
        {
            try
            {
                return await _context.JournalVoucherReferences
                    .Include(r => r.MainVoucher)
                    .Include(r => r.ReferencedVoucher)
                    .FirstOrDefaultAsync(r => r.JournalVoucherReferenceId == id);
            }
            catch (Exception)
            {
                throw;
            }
        }



        public async Task<List<JournalVoucherReference>> GetByMainVoucherIdAsync(int mainVoucherId)
        {
            try
            {
                return await _context.JournalVoucherReferences
                    .Include(r => r.ReferencedVoucher)
                    .Where(r => r.MainVoucherId == mainVoucherId)
                    .ToListAsync();
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<List<JournalVoucherReference>> GetByReferencedVoucherIdAsync(int referencedVoucherId)
        {
            try
            {
                return await _context.JournalVoucherReferences
                    .Include(r => r.MainVoucher)
                    .Where(r => r.ReferencedVoucherId == referencedVoucherId)
                    .ToListAsync();
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<List<JournalVoucherReference>> GetByMainVoucherIdAndTypeAsync(int mainVoucherId, string referenceType)
        {
            try
            {
                return await _context.JournalVoucherReferences
                    .Include(r => r.ReferencedVoucher)
                    .Where(r => r.MainVoucherId == mainVoucherId && r.ReferenceType == referenceType)
                    .ToListAsync();
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<JournalVoucherReference> UpdateAsync(JournalVoucherReference reference)
        {
            try
            {
                var entity = _context.JournalVoucherReferences.Update(reference);
                return entity.Entity;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var reference = await _context.JournalVoucherReferences.FindAsync(id);
                if (reference == null)
                {
                    return false;
                }

                _context.JournalVoucherReferences.Remove(reference);
                return true;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<int> DeleteByVoucherIdAsync(int voucherId)
        {
            try
            {
                var references = await _context.JournalVoucherReferences
                    .Where(r => r.MainVoucherId == voucherId || r.ReferencedVoucherId == voucherId)
                    .ToListAsync();

                if (!references.Any())
                    return 0;

                _context.JournalVoucherReferences.RemoveRange(references);
                return references.Count;
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}