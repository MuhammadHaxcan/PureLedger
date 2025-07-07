using System.Threading.Tasks;

namespace PureLedger.Repositories.Interfaces
{
    public interface IUnitOfWork
    {
        IProjectRepository Projects { get; }
        IJournalVoucherRepository JournalVouchers { get; }
        IJournalEntryRepository JournalEntries { get; }
        ICategoryRepository Categories { get; }
        IAccountRepository Accounts { get; }
        ITagRepository Tags { get; }
        IAccountGroupRepository AccountGroups { get; }
        IAccountDataRepository AccountDatas { get; }
        IProductDataRepository ProductDatas { get; }
        IAccountTypeRepository AccountTypes { get; }
        ILocationRepository Locations { get; }

        Task<int> SaveChangesAsync();
    }
}