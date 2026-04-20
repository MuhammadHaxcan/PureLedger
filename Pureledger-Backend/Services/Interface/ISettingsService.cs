namespace PureLedger.Services.Interface
{
    public interface ISettingsService
    {
        Task<string?> GetSettingAsync(string key);
        Task SetSettingAsync(string key, string value);
    }
}
