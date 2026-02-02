using PureLedger.Models.Master;

namespace PureLedger.Services.Tenant
{
    /// <summary>
    /// Interface for caching tenant information to reduce database lookups.
    /// </summary>
    public interface ITenantCacheService
    {
        /// <summary>
        /// Gets tenant information by slug.
        /// </summary>
        /// <param name="slug">The tenant slug (e.g., "prime")</param>
        /// <returns>Cached tenant info or null if not found</returns>
        Task<TenantCacheEntry?> GetTenantBySlugAsync(string slug);

        /// <summary>
        /// Gets tenant information by schema name.
        /// </summary>
        /// <param name="schemaName">The tenant schema name (e.g., "tenant_prime")</param>
        /// <returns>Cached tenant info or null if not found</returns>
        Task<TenantCacheEntry?> GetTenantBySchemaNameAsync(string schemaName);

        /// <summary>
        /// Gets tenant information by ID.
        /// </summary>
        /// <param name="tenantId">The tenant ID</param>
        /// <returns>Cached tenant info or null if not found</returns>
        Task<TenantCacheEntry?> GetTenantByIdAsync(int tenantId);

        /// <summary>
        /// Invalidates the cache for a specific tenant.
        /// </summary>
        /// <param name="tenantId">The tenant ID to invalidate</param>
        void InvalidateTenant(int tenantId);

        /// <summary>
        /// Invalidates the cache for a specific tenant by slug.
        /// </summary>
        /// <param name="slug">The tenant slug to invalidate</param>
        void InvalidateTenantBySlug(string slug);

        /// <summary>
        /// Invalidates all cached tenant data.
        /// </summary>
        void InvalidateAll();
    }

    /// <summary>
    /// Cached tenant information.
    /// </summary>
    public class TenantCacheEntry
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string SchemaName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
