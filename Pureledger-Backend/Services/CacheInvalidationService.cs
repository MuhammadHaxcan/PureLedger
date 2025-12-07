using Microsoft.Extensions.Caching.Memory;
using PureLedger.Helpers;

namespace PureLedger.Services
{
    public interface ICacheInvalidationService
    {
        void InvalidateCategoriesForProject(int projectId);
        void InvalidateAccountsForCategory(int categoryId);
    }

    public class CacheInvalidationService : ICacheInvalidationService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<CacheInvalidationService> _logger;
        
        private static readonly HashSet<string> _activeCategoryKeys = new();
        private static readonly HashSet<string> _activeAccountKeys = new();
        private static readonly object _lock = new();

        public CacheInvalidationService(IMemoryCache cache, ILogger<CacheInvalidationService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public void InvalidateCategoriesForProject(int projectId)
        {
            var key = CacheKeys.CategoriesByProject(projectId);
            _cache.Remove(key);
            _cache.Remove(CacheKeys.AllCategories);
            
            lock (_lock)
            {
                _activeCategoryKeys.Remove(key);
            }
            
            _logger.LogDebug("Invalidated category cache for project {ProjectId}", projectId);
        }

        public void InvalidateAccountsForCategory(int categoryId)
        {
            var key = CacheKeys.AccountsByCategory(categoryId);
            _cache.Remove(key);
            _cache.Remove(CacheKeys.AllAccounts);
            
            lock (_lock)
            {
                _activeAccountKeys.Remove(key);
            }
            
            _logger.LogDebug("Invalidated account cache for category {CategoryId}", categoryId);
        }

        public static void RegisterCategoryKey(string key)
        {
            lock (_lock)
            {
                _activeCategoryKeys.Add(key);
            }
        }

        public static void RegisterAccountKey(string key)
        {
            lock (_lock)
            {
                _activeAccountKeys.Add(key);
            }
        }
    }
}

