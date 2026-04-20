using PureLedger.Repositories.Interfaces;
using PureLedger.Services.Interface;

namespace PureLedger.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly IUnitOfWork _unitOfWork;

        public SettingsService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<string?> GetSettingAsync(string key)
        {
            return await _unitOfWork.AppSettings.GetValueAsync(key);
        }

        public async Task SetSettingAsync(string key, string value)
        {
            await _unitOfWork.AppSettings.SetValueAsync(key, value);
        }
    }
}
