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
        private readonly LedgerDbContext _dbContext;

        // Add all required repositories as lazy-initialized properties
        private IProjectRepository? _projects;
        private IJournalVoucherRepository? _journalVouchers;
        private IJournalEntryRepository? _journalEntries;
        private ICategoryRepository? _categories;
        private IAccountRepository? _accounts;
        private IAccountGroupRepository? _accountGroups;
        private IAccountDataRepository? _accountDatas;
        private IProductDataRepository? _productDatas;
        private IAccountTypeRepository? _accountTypes;
        private ILocationRepository? _locations;
        private IDeliveryChallanVoucherRepository? _deliveryChallanVouchers;
        private IDeliveryChallanEntryRepository? _deliveryChallanEntries;
        private IJournalVoucherReferenceRepository? _journalVoucherReferences;
        private IUserRepository? _users;
        private IUserControllerAccessRepository? _userControllerAccess;
        private IUserProjectRepository? _userProjects;
        private IUserCategoryRepository? _userCategories;
        private IRefreshTokenRepository? _refreshTokens;
        private IProjectCartageSettingsRepository? _projectCartageSettings;
        private IMissedEntryRepository? _missedEntries;
        private IRecipeRepository? _recipes; // Changed to same pattern
        private IRecipeItemRepository? _recipeItems; // Changed to same pattern
        private IBookerRepository? _bookers;
        private ISignatureRepository? _signatures;
        private IAppSettingRepository? _appSettings;

        public UnitOfWork(LedgerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public LedgerDbContext DbContext => _dbContext;

        // All repositories share this context and only stage changes.
        // Services control transaction scope and call SaveChangesAsync explicitly.
        public IProjectRepository Projects => _projects ??= new ProjectRepository(_dbContext);
        public IJournalVoucherRepository JournalVouchers => _journalVouchers ??= new JournalVoucherRepository(_dbContext);
        public IJournalEntryRepository JournalEntries => _journalEntries ??= new JournalEntryRepository(_dbContext);
        public ICategoryRepository Categories => _categories ??= new CategoryRepository(_dbContext);
        public IAccountRepository Accounts => _accounts ??= new AccountRepository(_dbContext);
        public IAccountGroupRepository AccountGroups => _accountGroups ??= new AccountGroupRepository(_dbContext);
        public IAccountDataRepository AccountDatas => _accountDatas ??= new AccountDataRepository(_dbContext);
        public IProductDataRepository ProductDatas => _productDatas ??= new ProductDataRepository(_dbContext);
        public IAccountTypeRepository AccountTypes => _accountTypes ??= new AccountTypeRepository(_dbContext);
        public ILocationRepository Locations => _locations ??= new LocationRepository(_dbContext);
        public IDeliveryChallanVoucherRepository DeliveryChallanVouchers => _deliveryChallanVouchers ??= new DeliveryChallanVoucherRepository(_dbContext);
        public IDeliveryChallanEntryRepository DeliveryChallanEntries => _deliveryChallanEntries ??= new DeliveryChallanEntryRepository(_dbContext);
        public IJournalVoucherReferenceRepository JournalVoucherReferences => _journalVoucherReferences ??= new JournalVoucherReferenceRepository(_dbContext);
        public IUserRepository Users => _users ??= new UserRepository(_dbContext);
        public IUserControllerAccessRepository UserControllerAccess => _userControllerAccess ??= new UserControllerAccessRepository(_dbContext);
        public IUserProjectRepository UserProjects => _userProjects ??= new UserProjectRepository(_dbContext);
        public IUserCategoryRepository UserCategories => _userCategories ??= new UserCategoryRepository(_dbContext);
        public IRefreshTokenRepository RefreshTokens => _refreshTokens ??= new RefreshTokenRepository(_dbContext);
        public IProjectCartageSettingsRepository ProjectCartageSettings => _projectCartageSettings ??= new ProjectCartageSettingsRepository(_dbContext);
        public IMissedEntryRepository MissedEntries => _missedEntries ??= new MissedEntryRepository(_dbContext);
        public IRecipeRepository Recipes => _recipes ??= new RecipeRepository(_dbContext);
        public IRecipeItemRepository RecipeItems => _recipeItems ??= new RecipeItemRepository(_dbContext);
        
        public IBookerRepository Bookers => _bookers ??= new BookerRepository(_dbContext);
        public ISignatureRepository Signatures => _signatures ??= new SignatureRepository(_dbContext);
        public IAppSettingRepository AppSettings => _appSettings ??= new AppSettingRepository(_dbContext);
        public Task<int> SaveChangesAsync() => _dbContext.SaveChangesAsync();

        public Task<bool> IsStockTransferVoucherAsync(int journalVoucherId)
        {
            return _dbContext.JournalVouchers
                .AnyAsync(v => v.JournalVoucherId == journalVoucherId && v.IsStockTransfer);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted) =>
            _dbContext.Database.BeginTransactionAsync(isolationLevel);

        public void Dispose()
        {
            _dbContext?.Dispose();
        }
    }
}
