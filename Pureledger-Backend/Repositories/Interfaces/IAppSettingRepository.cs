using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IAppSettingRepository
    {
        Task<string?> GetValueAsync(string key);
        Task SetValueAsync(string key, string value);
    }
}
