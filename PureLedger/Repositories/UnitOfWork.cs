using PureLedger.Data;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly LedgerDbContext _context;
        
        public IProjectRepository Projects { get; }
        public IJournalVoucherRepository JournalVouchers { get; }
        public IJournalEntryRepository JournalEntries { get; }
        public ICategoryRepository Categories { get; }
        public IAccountRepository Accounts { get; }
        public ITagRepository Tags { get; }
        public IAccountGroupRepository AccountGroups { get; }
        public IAccountDataRepository AccountDatas { get; }
        public IProductDataRepository ProductDatas { get; }
        public IAccountTypeRepository AccountTypes { get; }
        public ILocationRepository Locations { get; }

        public UnitOfWork(
            LedgerDbContext context,
            IProjectRepository projectRepository,
            IJournalVoucherRepository journalVoucherRepository,
            IJournalEntryRepository journalEntryRepository,
            ICategoryRepository categoryRepository,
            IAccountRepository accountRepository,
            ITagRepository tagRepository,
            IAccountGroupRepository accountGroupRepository,
            IAccountDataRepository accountDataRepository,
            IProductDataRepository productDataRepository,
            IAccountTypeRepository accountTypeRepository,
            ILocationRepository locationRepository)
        {
            _context = context;
            Projects = projectRepository;
            JournalVouchers = journalVoucherRepository;
            JournalEntries = journalEntryRepository;
            Categories = categoryRepository;
            Accounts = accountRepository;
            Tags = tagRepository;
            AccountGroups = accountGroupRepository;
            AccountDatas = accountDataRepository;
            ProductDatas = productDataRepository;
            AccountTypes = accountTypeRepository;
            Locations = locationRepository;
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}