using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IAccountGroupRepository
    {
        Task<List<AccountGroup>> GetAllAsync();
        Task<AccountGroup?> GetByIdAsync(int id);
    }
}