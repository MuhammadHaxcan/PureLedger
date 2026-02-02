namespace PureLedger.Services.Tenant
{
    /// <summary>
    /// Interface for managing tenant database migrations.
    /// </summary>
    public interface ITenantMigrationService
    {
        /// <summary>
        /// Gets all tenants that have pending migrations.
        /// </summary>
        /// <returns>List of tenant IDs with pending migrations</returns>
        Task<List<TenantMigrationStatus>> GetTenantsWithPendingMigrationsAsync();

        /// <summary>
        /// Applies pending migrations to a specific tenant.
        /// </summary>
        /// <param name="tenantId">The tenant ID</param>
        /// <param name="performedBy">Username of the admin performing the operation</param>
        /// <returns>Result of the migration operation</returns>
        Task<TenantMigrationResult> UpgradeTenantAsync(int tenantId, string performedBy);

        /// <summary>
        /// Applies pending migrations to all active tenants.
        /// </summary>
        /// <param name="performedBy">Username of the admin performing the operation</param>
        /// <returns>Results for each tenant</returns>
        Task<List<TenantMigrationResult>> UpgradeAllTenantsAsync(string performedBy);

        /// <summary>
        /// Gets the current migration version for a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant ID</param>
        /// <returns>The current migration version or null if no migrations applied</returns>
        Task<string?> GetCurrentMigrationVersionAsync(int tenantId);
    }

    /// <summary>
    /// Status of migrations for a tenant.
    /// </summary>
    public class TenantMigrationStatus
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string SchemaName { get; set; } = string.Empty;
        public string? CurrentVersion { get; set; }
        public int PendingMigrationCount { get; set; }
        public List<string> PendingMigrations { get; set; } = new List<string>();
    }

    /// <summary>
    /// Result of a tenant migration operation.
    /// </summary>
    public class TenantMigrationResult
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int MigrationsApplied { get; set; }
        public string? NewVersion { get; set; }
    }
}
