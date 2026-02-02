namespace PureLedger.Services.Tenant
{
    /// <summary>
    /// Interface for tenant decommissioning operations.
    /// </summary>
    public interface ITenantDecommissioningService
    {
        /// <summary>
        /// Deactivates a tenant (soft delete - preserves data).
        /// </summary>
        /// <param name="tenantId">The tenant ID</param>
        /// <param name="performedBy">Username of the admin performing the operation</param>
        /// <returns>Result of the operation</returns>
        Task<TenantDecommissionResult> DeactivateTenantAsync(int tenantId, string performedBy);

        /// <summary>
        /// Reactivates a previously deactivated tenant.
        /// </summary>
        /// <param name="tenantId">The tenant ID</param>
        /// <param name="performedBy">Username of the admin performing the operation</param>
        /// <returns>Result of the operation</returns>
        Task<TenantDecommissionResult> ReactivateTenantAsync(int tenantId, string performedBy);

        /// <summary>
        /// Permanently deletes a tenant and all its data.
        /// WARNING: This operation is irreversible!
        /// </summary>
        /// <param name="tenantId">The tenant ID</param>
        /// <param name="performedBy">Username of the admin performing the operation</param>
        /// <param name="confirmDeletion">Must be true to confirm permanent deletion</param>
        /// <returns>Result of the operation</returns>
        Task<TenantDecommissionResult> DeleteTenantAsync(int tenantId, string performedBy, bool confirmDeletion);
    }

    /// <summary>
    /// Result of tenant decommissioning operation.
    /// </summary>
    public class TenantDecommissionResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }

        public static TenantDecommissionResult Succeeded()
        {
            return new TenantDecommissionResult { Success = true };
        }

        public static TenantDecommissionResult Failed(string errorMessage)
        {
            return new TenantDecommissionResult { Success = false, ErrorMessage = errorMessage };
        }
    }
}
