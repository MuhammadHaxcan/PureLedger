using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Http;
using PureLedger.Data;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly SubrouteDbContextFactory _dbContextFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private LedgerDbContext _dbContext;

        // Add all required repositories
        private IProjectRepository _projects;
        private IJournalVoucherRepository _journalVouchers;
        private IJournalEntryRepository _journalEntries;
        private ICategoryRepository _categories;
        private IAccountRepository _accounts;
        private ITagRepository _tags;
        private IAccountGroupRepository _accountGroups;
        private IAccountDataRepository _accountDatas;
        private IProductDataRepository _productDatas;
        private IAccountTypeRepository _accountTypes;
        private ILocationRepository _locations;
        private IDeliveryChallanVoucherRepository _deliveryChallanVouchers;
        private IDeliveryChallanEntryRepository _deliveryChallanEntries;

        public UnitOfWork(
            SubrouteDbContextFactory dbContextFactory,
            IHttpContextAccessor httpContextAccessor,
            IProjectRepository projects,
            IJournalVoucherRepository journalVouchers,
            IJournalEntryRepository journalEntries,
            ICategoryRepository categories,
            IAccountRepository accounts,
            ITagRepository tags,
            IAccountGroupRepository accountGroups,
            IAccountDataRepository accountDatas,
            IProductDataRepository productDatas,
            IAccountTypeRepository accountTypes,
            ILocationRepository locations,
            IDeliveryChallanVoucherRepository deliveryChallanVouchers,
            IDeliveryChallanEntryRepository deliveryChallanEntries)
        {
            _dbContextFactory = dbContextFactory;
            _httpContextAccessor = httpContextAccessor;

            // Get the subroute from HttpContext
            var subrouteName =
                _httpContextAccessor.HttpContext?.Items["Subroute"]?.ToString() ?? "Default";

            // Log which subroute we're using
            Console.WriteLine($"UnitOfWork initializing with subroute: {subrouteName}");

            // Create the context with the appropriate connection
            _dbContext = _dbContextFactory.CreateDbContext(subrouteName);

            // Initialize repositories
            _projects = projects;
            _journalVouchers = journalVouchers;
            _journalEntries = journalEntries;
            _categories = categories;
            _accounts = accounts;
            _tags = tags;
            _accountGroups = accountGroups;
            _accountDatas = accountDatas;
            _productDatas = productDatas;
            _accountTypes = accountTypes;
            _locations = locations;
            _deliveryChallanVouchers = deliveryChallanVouchers;
            _deliveryChallanEntries = deliveryChallanEntries;
        }

        public LedgerDbContext DbContext => _dbContext;

        // Implement all required properties from IUnitOfWork interface
        public IProjectRepository Projects => _projects;
        public IJournalVoucherRepository JournalVouchers => _journalVouchers;
        public IJournalEntryRepository JournalEntries => _journalEntries;
        public ICategoryRepository Categories => _categories;
        public IAccountRepository Accounts => _accounts;
        public ITagRepository Tags => _tags;
        public IAccountGroupRepository AccountGroups => _accountGroups;
        public IAccountDataRepository AccountDatas => _accountDatas;
        public IProductDataRepository ProductDatas => _productDatas;
        public IAccountTypeRepository AccountTypes => _accountTypes;
        public ILocationRepository Locations => _locations;
        public IDeliveryChallanVoucherRepository DeliveryChallanVouchers => _deliveryChallanVouchers;
        public IDeliveryChallanEntryRepository DeliveryChallanEntries => _deliveryChallanEntries;

        public async Task<int> SaveChangesAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }

        // Implement the missing transaction method
        public async Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            return await _dbContext.Database.BeginTransactionAsync(isolationLevel);
        }

        public void Dispose()
        {
            _dbContext?.Dispose();
        }
    }
}