namespace PureLedger.Services.Tenant
{
    /// <summary>
    /// Interface for tenant provisioning operations.
    /// </summary>
    public interface ITenantProvisioningService
    {
        /// <summary>
        /// Creates a new tenant with the specified name and slug.
        /// </summary>
        /// <param name="name">The tenant's display name</param>
        /// <param name="slug">The tenant's slug (used for schema name and login prefix)</param>
        /// <param name="performedBy">Username of the admin performing the operation</param>
        /// <returns>Result containing tenant ID and credentials if successful</returns>
        Task<TenantProvisioningResult> CreateTenantAsync(string name, string slug, string performedBy);

        /// <summary>
        /// Checks if a tenant with the given slug already exists.
        /// </summary>
        /// <param name="slug">The slug to check</param>
        /// <returns>True if a tenant with this slug exists</returns>
        Task<bool> SlugExistsAsync(string slug);
    }

    /// <summary>
    /// Result of tenant provisioning operation.
    /// </summary>
    public class TenantProvisioningResult
    {
        public bool Success { get; set; }
        public int? TenantId { get; set; }
        public string? SchemaName { get; set; }
        public string? AdminUsername { get; set; }
        public string? AdminPassword { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> Errors { get; set; } = new List<string>();

        public static TenantProvisioningResult Succeeded(int tenantId, string schemaName, string adminUsername, string adminPassword)
        {
            return new TenantProvisioningResult
            {
                Success = true,
                TenantId = tenantId,
                SchemaName = schemaName,
                AdminUsername = adminUsername,
                AdminPassword = adminPassword
            };
        }

        public static TenantProvisioningResult Failed(string errorMessage)
        {
            return new TenantProvisioningResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Errors = new List<string> { errorMessage }
            };
        }

        public static TenantProvisioningResult Failed(List<string> errors)
        {
            return new TenantProvisioningResult
            {
                Success = false,
                ErrorMessage = errors.FirstOrDefault(),
                Errors = errors
            };
        }
    }
}
