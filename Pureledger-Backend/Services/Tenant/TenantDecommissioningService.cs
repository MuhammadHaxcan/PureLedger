using Microsoft.EntityFrameworkCore;
using Npgsql;
using PureLedger.Data;
using PureLedger.Models.Master;

namespace PureLedger.Services.Tenant
{
    /// <summary>
    /// Service for tenant decommissioning operations.
    /// </summary>
    public class TenantDecommissioningService : ITenantDecommissioningService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TenantDecommissioningService> _logger;
        private readonly ITenantCacheService _cacheService;

        public TenantDecommissioningService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<TenantDecommissioningService> logger,
            ITenantCacheService cacheService)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
            _cacheService = cacheService;
        }

        public async Task<TenantDecommissionResult> DeactivateTenantAsync(int tenantId, string performedBy)
        {
            using var scope = _scopeFactory.CreateScope();
            var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();

            var tenant = await masterDb.Tenants.FindAsync(tenantId);
            if (tenant == null)
            {
                return TenantDecommissionResult.Failed("Tenant not found.");
            }

            if (!tenant.IsActive)
            {
                return TenantDecommissionResult.Failed("Tenant is already deactivated.");
            }

            var auditLog = new TenantAuditLog
            {
                TenantId = tenantId,
                Operation = "DEACTIVATE",
                SchemaName = tenant.SchemaName,
                PerformedBy = performedBy,
                IsSuccess = false
            };

            try
            {
                tenant.IsActive = false;
                tenant.DeactivatedAt = DateTime.UtcNow;
                await masterDb.SaveChangesAsync();

                auditLog.IsSuccess = true;
                auditLog.Details = "Tenant deactivated successfully";
                masterDb.TenantAuditLogs.Add(auditLog);
                await masterDb.SaveChangesAsync();

                // Invalidate cache
                _cacheService.InvalidateTenant(tenantId);

                _logger.LogInformation("Deactivated tenant {TenantName} by {PerformedBy}", tenant.Name, performedBy);

                return TenantDecommissionResult.Succeeded();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deactivate tenant {TenantId}", tenantId);

                auditLog.IsSuccess = false;
                auditLog.ErrorMessage = ex.Message;
                masterDb.TenantAuditLogs.Add(auditLog);
                await masterDb.SaveChangesAsync();

                return TenantDecommissionResult.Failed($"Failed to deactivate tenant: {ex.Message}");
            }
        }

        public async Task<TenantDecommissionResult> ReactivateTenantAsync(int tenantId, string performedBy)
        {
            using var scope = _scopeFactory.CreateScope();
            var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();

            var tenant = await masterDb.Tenants.FindAsync(tenantId);
            if (tenant == null)
            {
                return TenantDecommissionResult.Failed("Tenant not found.");
            }

            if (tenant.IsActive)
            {
                return TenantDecommissionResult.Failed("Tenant is already active.");
            }

            var auditLog = new TenantAuditLog
            {
                TenantId = tenantId,
                Operation = "REACTIVATE",
                SchemaName = tenant.SchemaName,
                PerformedBy = performedBy,
                IsSuccess = false
            };

            try
            {
                tenant.IsActive = true;
                tenant.ActivatedAt = DateTime.UtcNow;
                tenant.DeactivatedAt = null;
                await masterDb.SaveChangesAsync();

                auditLog.IsSuccess = true;
                auditLog.Details = "Tenant reactivated successfully";
                masterDb.TenantAuditLogs.Add(auditLog);
                await masterDb.SaveChangesAsync();

                // Invalidate cache
                _cacheService.InvalidateTenant(tenantId);

                _logger.LogInformation("Reactivated tenant {TenantName} by {PerformedBy}", tenant.Name, performedBy);

                return TenantDecommissionResult.Succeeded();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reactivate tenant {TenantId}", tenantId);

                auditLog.IsSuccess = false;
                auditLog.ErrorMessage = ex.Message;
                masterDb.TenantAuditLogs.Add(auditLog);
                await masterDb.SaveChangesAsync();

                return TenantDecommissionResult.Failed($"Failed to reactivate tenant: {ex.Message}");
            }
        }

        public async Task<TenantDecommissionResult> DeleteTenantAsync(int tenantId, string performedBy, bool confirmDeletion)
        {
            if (!confirmDeletion)
            {
                return TenantDecommissionResult.Failed("Deletion must be explicitly confirmed.");
            }

            using var scope = _scopeFactory.CreateScope();
            var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
            var connectionString = _configuration.GetConnectionString("LedgerDb");

            var tenant = await masterDb.Tenants.FindAsync(tenantId);
            if (tenant == null)
            {
                return TenantDecommissionResult.Failed("Tenant not found.");
            }

            var schemaName = tenant.SchemaName;
            var tenantName = tenant.Name;

            var auditLog = new TenantAuditLog
            {
                TenantId = null, // Set to null since tenant will be deleted
                Operation = "DELETE",
                SchemaName = schemaName,
                PerformedBy = performedBy,
                IsSuccess = false,
                Details = $"Deleting tenant: {tenantName} (ID: {tenantId})"
            };

            try
            {
                // Drop the schema (CASCADE removes all tables)
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();

                await using var dropCommand = connection.CreateCommand();
                dropCommand.CommandText = $"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE";
                await dropCommand.ExecuteNonQueryAsync();

                _logger.LogInformation("Dropped schema {SchemaName} for tenant {TenantName}", schemaName, tenantName);

                // Delete tenant record
                masterDb.Tenants.Remove(tenant);
                await masterDb.SaveChangesAsync();

                auditLog.IsSuccess = true;
                auditLog.Details = $"Tenant {tenantName} and schema {schemaName} deleted permanently";
                masterDb.TenantAuditLogs.Add(auditLog);
                await masterDb.SaveChangesAsync();

                // Invalidate cache
                _cacheService.InvalidateTenant(tenantId);

                _logger.LogWarning("PERMANENTLY DELETED tenant {TenantName} (ID: {TenantId}) by {PerformedBy}",
                    tenantName, tenantId, performedBy);

                return TenantDecommissionResult.Succeeded();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete tenant {TenantId}", tenantId);

                auditLog.IsSuccess = false;
                auditLog.ErrorMessage = ex.Message;
                masterDb.TenantAuditLogs.Add(auditLog);
                await masterDb.SaveChangesAsync();

                return TenantDecommissionResult.Failed($"Failed to delete tenant: {ex.Message}");
            }
        }
    }
}
