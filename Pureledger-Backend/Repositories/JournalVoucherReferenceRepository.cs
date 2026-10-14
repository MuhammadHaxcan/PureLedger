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
        private readonly LedgerDbContext _context;

        public JournalVoucherReferenceRepository(LedgerDbContext context)
        {
            _context = context;
        }

        public JournalVoucherReference Add(JournalVoucherReference reference)
        {
            return _context.JournalVoucherReferences.Add(reference).Entity;
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

        public JournalVoucherReference Update(JournalVoucherReference reference)
        {
            return _context.JournalVoucherReferences.Update(reference).Entity;
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
    }
}
