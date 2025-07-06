using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IAccountDataRepository
    {
        Task DeleteAsync(AccountData accountData);
    }
}