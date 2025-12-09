using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IAccountRepository
    {
        Task<List<Account>> GetAllWithDetailsAsync();
        Task<List<Account>> GetByCategoryIdAsync(int categoryId);
        Task<List<Account>> GetEnabledByCategoryIdAsync(int categoryId);
        Task<Account?> GetByIdAsync(int id);
        Task<Account?> GetByIdWithDetailsAsync(int id);
        Task<Account> AddAsync(Account account);
        Task<Account> UpdateAsync(Account account);
        Task<bool> DeleteAsync(Account account);
        Task<object?> GetProductDetailsAsync(int accountId);
        Task<List<Account>> GetAllAsync();
        Task<Account?> GetByIdWithCategoryAsync(int id);
        Task<List<Account>> GetByCategoryIdWithCategoryAsync(int categoryId);
        Task<List<Account>> GetAccountsWithEntriesInProjectAsync(int projectId);
        Task<IEnumerable<Account>> GetAccountsByCategoryAndProjectAsync(int categoryId, int projectId);
        Task<List<Account>> GetByProjectAsync(int projectId, int? categoryId = null);
        Task<List<int>> GetAssociatedJournalVoucherIds(int accountId);

        /// <summary>
        /// Checks if an account with the given name exists in the specified category (case-insensitive)
        /// </summary>
        Task<bool> ExistsByNameInCategoryAsync(string name, int categoryId, int? excludeId = null);

        /// <summary>
        /// Checks if the account has any associated journal entries
        /// </summary>
        Task<bool> HasAssociatedJournalEntriesAsync(int accountId);
    }
}