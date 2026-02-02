using Microsoft.EntityFrameworkCore;
using Npgsql;
using PureLedger.Data;
using PureLedger.Models.Master;

namespace PureLedger.Services.Tenant
{
    /// <summary>
    /// Service for managing tenant database migrations.
    /// </summary>
    public class TenantMigrationService : ITenantMigrationService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TenantMigrationService> _logger;

        public TenantMigrationService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<TenantMigrationService> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<List<TenantMigrationStatus>> GetTenantsWithPendingMigrationsAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
            var connectionString = _configuration.GetConnectionString("LedgerDb");

            var tenants = await masterDb.Tenants
                .Where(t => t.IsActive)
                .ToListAsync();

            var results = new List<TenantMigrationStatus>();

            foreach (var tenant in tenants)
            {
                var status = new TenantMigrationStatus
                {
                    TenantId = tenant.Id,
                    TenantName = tenant.Name,
                    SchemaName = tenant.SchemaName
                };

                try
                {
                    await using var connection = new NpgsqlConnection(connectionString);
                    await connection.OpenAsync();

                    // Check if __EFMigrationsHistory table exists in the schema
                    await using var checkCommand = connection.CreateCommand();
                    checkCommand.CommandText = $@"
                        SELECT EXISTS (
                            SELECT FROM information_schema.tables
                            WHERE table_schema = '{tenant.SchemaName}'
                            AND table_name = '__EFMigrationsHistory'
                        )";

                    var historyExists = (bool)(await checkCommand.ExecuteScalarAsync() ?? false);

                    if (historyExists)
                    {
                        // Get current migration version
                        await using var versionCommand = connection.CreateCommand();
                        versionCommand.CommandText = $@"
                            SELECT ""MigrationId""
                            FROM ""{tenant.SchemaName}"".""__EFMigrationsHistory""
                            ORDER BY ""MigrationId"" DESC
                            LIMIT 1";

                        status.CurrentVersion = (string?)await versionCommand.ExecuteScalarAsync();
                    }

                    // For now, we assume no pending migrations if the schema exists
                    // In a production system, you'd compare against the model's migrations
                    status.PendingMigrationCount = 0;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking migrations for tenant {TenantName}", tenant.Name);
                    status.PendingMigrationCount = -1; // Indicates error
                }

                results.Add(status);
            }

            return results;
        }

        public async Task<TenantMigrationResult> UpgradeTenantAsync(int tenantId, string performedBy)
        {
            using var scope = _scopeFactory.CreateScope();
            var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();

            var tenant = await masterDb.Tenants.FindAsync(tenantId);
            if (tenant == null)
            {
                return new TenantMigrationResult
                {
                    TenantId = tenantId,
                    Success = false,
                    ErrorMessage = "Tenant not found"
                };
            }

            var result = new TenantMigrationResult
            {
                TenantId = tenantId,
                TenantName = tenant.Name
            };

            var auditLog = new TenantAuditLog
            {
                TenantId = tenantId,
                Operation = "MIGRATE",
                SchemaName = tenant.SchemaName,
                PerformedBy = performedBy,
                IsSuccess = false
            };

            try
            {
                var connectionString = _configuration.GetConnectionString("LedgerDb");

                // Apply migrations to tenant schema
                await ApplyMigrationsAsync(tenant.SchemaName, connectionString);

                result.Success = true;
                result.MigrationsApplied = 1; // Simplified - would track actual count in production
                result.NewVersion = await GetCurrentMigrationVersionAsync(tenantId);

                auditLog.IsSuccess = true;
                auditLog.Details = $"Migrations applied successfully. New version: {result.NewVersion}";

                _logger.LogInformation("Successfully applied migrations to tenant {TenantName}", tenant.Name);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;

                auditLog.IsSuccess = false;
                auditLog.ErrorMessage = ex.Message;

                _logger.LogError(ex, "Failed to apply migrations to tenant {TenantName}", tenant.Name);
            }

            masterDb.TenantAuditLogs.Add(auditLog);
            await masterDb.SaveChangesAsync();

            return result;
        }

        public async Task<List<TenantMigrationResult>> UpgradeAllTenantsAsync(string performedBy)
        {
            using var scope = _scopeFactory.CreateScope();
            var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();

            var tenants = await masterDb.Tenants
                .Where(t => t.IsActive)
                .ToListAsync();

            var results = new List<TenantMigrationResult>();

            foreach (var tenant in tenants)
            {
                var result = await UpgradeTenantAsync(tenant.Id, performedBy);
                results.Add(result);
            }

            return results;
        }

        public async Task<string?> GetCurrentMigrationVersionAsync(int tenantId)
        {
            using var scope = _scopeFactory.CreateScope();
            var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
            var connectionString = _configuration.GetConnectionString("LedgerDb");

            var tenant = await masterDb.Tenants.FindAsync(tenantId);
            if (tenant == null)
            {
                return null;
            }

            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();

                await using var command = connection.CreateCommand();
                command.CommandText = $@"
                    SELECT ""MigrationId""
                    FROM ""{tenant.SchemaName}"".""__EFMigrationsHistory""
                    ORDER BY ""MigrationId"" DESC
                    LIMIT 1";

                return (string?)await command.ExecuteScalarAsync();
            }
            catch
            {
                return null;
            }
        }

        private async Task ApplyMigrationsAsync(string schemaName, string connectionString)
        {
            // Create a new DbContext with the tenant schema
            var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            using var dbContext = new TenantDbContext(optionsBuilder.Options);

            await using var connection = dbContext.Database.GetDbConnection();
            await connection.OpenAsync();

            // Set the search path to the tenant schema
            await using var command = connection.CreateCommand();
            command.CommandText = $"SET search_path TO \"{schemaName}\", public";
            await command.ExecuteNonQueryAsync();

            // Apply any pending migrations
            // Note: In production, you might want to use dbContext.Database.Migrate()
            // For now, we use EnsureCreated which is simpler but doesn't track migrations
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                await dbContext.Database.MigrateAsync();
            }
        }
    }
}
