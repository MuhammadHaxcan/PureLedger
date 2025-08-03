using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Services
{
    public class CommonApiService : ICommonApiService
    {
        private readonly IProjectRepository _projectRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IJournalVoucherRepository _journalVoucherRepository;
        private readonly IJournalEntryRepository _journalEntryRepository;


        public CommonApiService(
            IProjectRepository projectRepository,
            ICategoryRepository categoryRepository,
            IAccountRepository accountRepository,
            IJournalVoucherRepository journalVoucherRepository,
            IJournalEntryRepository journalEntryRepository)
        {
            _projectRepository = projectRepository;
            _categoryRepository = categoryRepository;
            _accountRepository = accountRepository;
            _journalVoucherRepository = journalVoucherRepository;
            _journalEntryRepository = journalEntryRepository;
        }

        public async Task<IEnumerable<Project>> GetProjectsAsync()
            => await _projectRepository.GetAllAsync();

        public async Task<IEnumerable<Category>> GetCategoriesByProjectAsync(int projectId)
            => await _categoryRepository.GetByProjectIdAsync(projectId);

        public async Task<IEnumerable<Account>> GetAccountsByCategoryAsync(int categoryId)
            => await _accountRepository.GetByCategoryIdAsync(categoryId);

        public async Task<IEnumerable<Account>> GetAccountsByCategoryAndProjectAsync(int categoryId, int projectId)
        {
            var accounts = await _accountRepository.GetByCategoryIdAsync(categoryId);
            return accounts.Where(a => a.Category?.ProjectId == projectId);
        }

        public async Task<JournalEntry?> GetCartageEntryByJournalVoucherIdAsync(int journalVoucherId)
        {
            var voucher = await _journalVoucherRepository.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null || !voucher.CartageVoucherId.HasValue)
                return null;

            var cartageVoucherId = voucher.CartageVoucherId.Value;

            var entries = await _journalEntryRepository.GetByCartageId(cartageVoucherId);
            return entries; // or any specific logic to pick one
        }
    }
}
