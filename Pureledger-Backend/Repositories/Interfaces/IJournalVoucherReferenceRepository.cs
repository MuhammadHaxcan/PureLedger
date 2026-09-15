using PureLedger.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PureLedger.Repositories.Interfaces
{
    public interface IJournalVoucherReferenceRepository
    {
        JournalVoucherReference Add(JournalVoucherReference reference);
        Task<JournalVoucherReference?> GetByIdAsync(int id);
        Task<List<JournalVoucherReference>> GetByMainVoucherIdAsync(int mainVoucherId);
        Task<List<JournalVoucherReference>> GetByReferencedVoucherIdAsync(int referencedVoucherId);
        Task<List<JournalVoucherReference>> GetByMainVoucherIdAndTypeAsync(int mainVoucherId, string referenceType);
        JournalVoucherReference Update(JournalVoucherReference reference);
        Task<bool> DeleteAsync(int id);
        Task<int> DeleteByVoucherIdAsync(int voucherId);
    }
}
