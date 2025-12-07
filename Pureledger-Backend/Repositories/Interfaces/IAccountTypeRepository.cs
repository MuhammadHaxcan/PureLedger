using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IAccountTypeRepository
    {
        Task<List<AccountType>> GetAllAsync();
        Task<AccountType?> GetByIdAsync(int id);
    }
}