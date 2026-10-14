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
        Account Add(Account account);
        Account Update(Account account);
        bool Delete(Account account);
        Task<object?> GetProductDetailsAsync(int accountId);
        Task<List<Account>> GetAllAsync();
        Task<Account?> GetByIdWithCategoryAsync(int id);
        Task<List<Account>> GetByProjectAsync(int projectId, int? categoryId = null);
        Task<List<int>> GetAssociatedJournalVoucherIds(int accountId);
        /// <summary>Ids of pending approval requests whose rows use the account.</summary>
        Task<List<int>> GetPendingApprovalIdsAsync(int accountId);

        /// <summary>
        /// Checks if an account with the given name exists in the specified category (case-insensitive)
        /// </summary>
        Task<bool> ExistsByNameInCategoryAsync(string name, int categoryId, int? excludeId = null);

        Task<bool> HasLinkedBookerAsync(int bookerId);
    }
}
