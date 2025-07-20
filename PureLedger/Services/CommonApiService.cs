using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Services
{
    public class CommonApiService : ICommonApiService
    {
        private readonly IProjectRepository _projectRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IAccountRepository _accountRepository;

        public CommonApiService(
            IProjectRepository projectRepository,
            ICategoryRepository categoryRepository,
            IAccountRepository accountRepository)
        {
            _projectRepository = projectRepository;
            _categoryRepository = categoryRepository;
            _accountRepository = accountRepository;
        }

        public async Task<IEnumerable<Project>> GetProjectsAsync()
            => await _projectRepository.GetAllAsync();

        public async Task<IEnumerable<Category>> GetCategoriesByProjectAsync(int projectId)
            => await _categoryRepository.GetByProjectIdAsync(projectId);

        public async Task<IEnumerable<Account>> GetAccountsByCategoryAsync(int categoryId)
            => await _accountRepository.GetByCategoryIdAsync(categoryId);
    }
}
