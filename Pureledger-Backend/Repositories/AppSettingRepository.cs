using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class AppSettingRepository : IAppSettingRepository
    {
        private readonly LedgerDbContext _dbContext;

        public AppSettingRepository(LedgerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<string?> GetValueAsync(string key)
        {
            var setting = await _dbContext.AppSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SettingKey == key);
            return setting?.SettingValue;
        }

        public async Task SetValueAsync(string key, string value)
        {
            var existing = await _dbContext.AppSettings
                .FirstOrDefaultAsync(s => s.SettingKey == key);

            if (existing != null)
            {
                existing.SettingValue = value;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var setting = new AppSetting
                {
                    SettingKey = key,
                    SettingValue = value,
                    UpdatedAt = DateTime.UtcNow
                };
                _dbContext.AppSettings.Add(setting);
            }

            await _dbContext.SaveChangesAsync();
        }
    }
}
