using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IAccountRepository
    {
        Task<List<Account>> GetAllWithDetailsAsync();
        Task<List<Account>> GetByCategoryIdAsync(int categoryId);
        Task<Account?> GetByIdAsync(int id);
        Task<Account?> GetByIdWithDetailsAsync(int id);
        Task<Account> AddAsync(Account account);
        Task<Account> UpdateAsync(Account account);
        Task<object?> GetProductDetailsAsync(int accountId);
        Task<List<Account>> GetAllAsync();
        Task<Account?> GetByIdWithCategoryAsync(int id);
        Task<List<Account>> GetByCategoryIdWithCategoryAsync(int categoryId);

    }
}