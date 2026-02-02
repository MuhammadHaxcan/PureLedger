using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PureLedger.Data;
using PureLedger.Models.Master;

namespace PureLedger.Services.Tenant
{
    /// <summary>
    /// In-memory caching service for tenant information.
    /// </summary>
    public class TenantCacheService : ITenantCacheService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TenantCacheService> _logger;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

        private const string CacheKeyPrefixSlug = "tenant_slug_";
        private const string CacheKeyPrefixSchema = "tenant_schema_";
        private const string CacheKeyPrefixId = "tenant_id_";

        public TenantCacheService(
            IServiceScopeFactory scopeFactory,
            IMemoryCache cache,
            ILogger<TenantCacheService> logger)
        {
            _scopeFactory = scopeFactory;
            _cache = cache;
            _logger = logger;
        }

        public async Task<TenantCacheEntry?> GetTenantBySlugAsync(string slug)
        {
            var cacheKey = CacheKeyPrefixSlug + slug.ToLowerInvariant();

            if (_cache.TryGetValue(cacheKey, out TenantCacheEntry? cached))
            {
                return cached;
            }

            using var scope = _scopeFactory.CreateScope();
            var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();

            var tenant = await masterDb.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Slug == slug.ToLowerInvariant());

            if (tenant == null)
            {
                return null;
            }

            var entry = MapToEntry(tenant);
            CacheEntry(entry);
            return entry;
        }

        public async Task<TenantCacheEntry?> GetTenantBySchemaNameAsync(string schemaName)
        {
            var cacheKey = CacheKeyPrefixSchema + schemaName.ToLowerInvariant();

            if (_cache.TryGetValue(cacheKey, out TenantCacheEntry? cached))
            {
                return cached;
            }

            using var scope = _scopeFactory.CreateScope();
            var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();

            var tenant = await masterDb.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.SchemaName == schemaName.ToLowerInvariant());

            if (tenant == null)
            {
                return null;
            }

            var entry = MapToEntry(tenant);
            CacheEntry(entry);
            return entry;
        }

        public async Task<TenantCacheEntry?> GetTenantByIdAsync(int tenantId)
        {
            var cacheKey = CacheKeyPrefixId + tenantId.ToString();

            if (_cache.TryGetValue(cacheKey, out TenantCacheEntry? cached))
            {
                return cached;
            }

            using var scope = _scopeFactory.CreateScope();
            var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();

            var tenant = await masterDb.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant == null)
            {
                return null;
            }

            var entry = MapToEntry(tenant);
            CacheEntry(entry);
            return entry;
        }

        public void InvalidateTenant(int tenantId)
        {
            _cache.Remove(CacheKeyPrefixId + tenantId.ToString());
            _logger.LogDebug("Invalidated cache for tenant ID {TenantId}", tenantId);
        }

        public void InvalidateTenantBySlug(string slug)
        {
            _cache.Remove(CacheKeyPrefixSlug + slug.ToLowerInvariant());
            _logger.LogDebug("Invalidated cache for tenant slug {Slug}", slug);
        }

        public void InvalidateAll()
        {
            // IMemoryCache doesn't have a clear all method, so we rely on expiration
            // In production, consider using a distributed cache with tagging support
            _logger.LogInformation("Cache invalidation requested - entries will expire naturally");
        }

        private TenantCacheEntry MapToEntry(Models.Master.Tenant tenant)
        {
            return new TenantCacheEntry
            {
                Id = tenant.Id,
                Name = tenant.Name,
                Slug = tenant.Slug,
                SchemaName = tenant.SchemaName,
                IsActive = tenant.IsActive
            };
        }

        private void CacheEntry(TenantCacheEntry entry)
        {
            var options = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_cacheExpiration);

            _cache.Set(CacheKeyPrefixId + entry.Id.ToString(), entry, options);
            _cache.Set(CacheKeyPrefixSlug + entry.Slug.ToLowerInvariant(), entry, options);
            _cache.Set(CacheKeyPrefixSchema + entry.SchemaName.ToLowerInvariant(), entry, options);
        }
    }
}
