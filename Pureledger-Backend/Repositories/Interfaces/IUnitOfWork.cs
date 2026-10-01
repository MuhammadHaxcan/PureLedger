using System.Data;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;

namespace PureLedger.Repositories.Interfaces
{
    public interface IUnitOfWork
    {
        IProjectRepository Projects { get; }
        IJournalVoucherRepository JournalVouchers { get; }
        IJournalEntryRepository JournalEntries { get; }
        ICategoryRepository Categories { get; }
        IAccountRepository Accounts { get; }
        IAccountGroupRepository AccountGroups { get; }
        IAccountDataRepository AccountDatas { get; }
        IProductDataRepository ProductDatas { get; }
        IAccountTypeRepository AccountTypes { get; }
        ILocationRepository Locations { get; }
        IDeliveryChallanVoucherRepository DeliveryChallanVouchers { get; }
        IDeliveryChallanEntryRepository DeliveryChallanEntries { get; }
        IJournalVoucherReferenceRepository JournalVoucherReferences { get; }
        IUserRepository Users { get; }
        IUserControllerAccessRepository UserControllerAccess { get; }
        IUserProjectRepository UserProjects { get; }
        IUserCategoryRepository UserCategories { get; }
        IRefreshTokenRepository RefreshTokens { get; }
        IProjectCartageSettingsRepository ProjectCartageSettings { get; }
        IMissedEntryRepository MissedEntries { get; }
        IRecipeRepository Recipes { get; }
        IRecipeItemRepository RecipeItems { get; }
        IBookerRepository Bookers { get; }
        ISignatureRepository Signatures { get; }
        IAppSettingRepository AppSettings { get; }

        /// <summary>
        /// Persists all mutations staged by repositories sharing this unit of work.
        /// Repository mutation methods never save independently.
        /// </summary>
        Task<int> SaveChangesAsync();
        Task<bool> IsStockTransferVoucherAsync(int journalVoucherId);
        
        // Add transaction support
        Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);
    }
}
