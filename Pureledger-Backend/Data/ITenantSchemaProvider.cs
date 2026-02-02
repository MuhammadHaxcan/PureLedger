namespace PureLedger.Data
{
    /// <summary>
    /// Interface for providing the current tenant's schema name.
    /// This is used by the TenantSchemaInterceptor to set the PostgreSQL search_path.
    /// </summary>
    public interface ITenantSchemaProvider
    {
        /// <summary>
        /// Gets the current tenant's schema name.
        /// Returns null if no tenant context is set (e.g., system admin operations).
        /// </summary>
        string? SchemaName { get; }

        /// <summary>
        /// Gets the current tenant's ID.
        /// Returns null if no tenant context is set.
        /// </summary>
        int? TenantId { get; }

        /// <summary>
        /// Gets the current tenant's display name.
        /// Returns null if no tenant context is set.
        /// </summary>
        string? TenantName { get; }

        /// <summary>
        /// Sets the tenant context for the current request.
        /// </summary>
        /// <param name="schemaName">The tenant's schema name (e.g., "tenant_prime")</param>
        /// <param name="tenantId">The tenant's ID</param>
        /// <param name="tenantName">The tenant's display name</param>
        void SetTenant(string schemaName, int tenantId, string tenantName);

        /// <summary>
        /// Clears the tenant context.
        /// </summary>
        void ClearTenant();

        /// <summary>
        /// Checks if a tenant context is currently set.
        /// </summary>
        bool HasTenant { get; }
    }
}
