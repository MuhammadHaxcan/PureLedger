using Microsoft.EntityFrameworkCore;
using Npgsql;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Models.Master;

namespace PureLedger.Services.Tenant
{
    /// <summary>
    /// Service for provisioning new tenants.
    /// </summary>
    public class TenantProvisioningService : ITenantProvisioningService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TenantProvisioningService> _logger;
        private readonly ITenantCacheService _cacheService;

        private const string DefaultAdminPassword = "Admin@123";

        public TenantProvisioningService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<TenantProvisioningService> logger,
            ITenantCacheService cacheService)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
            _cacheService = cacheService;
        }

        public async Task<bool> SlugExistsAsync(string slug)
        {
            using var scope = _scopeFactory.CreateScope();
            var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();

            var normalizedSlug = slug.ToLowerInvariant();
            return await masterDb.Tenants.AnyAsync(t => t.Slug == normalizedSlug);
        }

        public async Task<TenantProvisioningResult> CreateTenantAsync(string name, string slug, string performedBy)
        {
            var normalizedSlug = slug.ToLowerInvariant().Trim();
            var schemaName = TenantSchemaValidator.SlugToSchemaName(normalizedSlug);

            // Validate inputs
            if (!TenantSchemaValidator.TryValidateSlug(normalizedSlug, out var slugError))
            {
                return TenantProvisioningResult.Failed(slugError!);
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return TenantProvisioningResult.Failed("Tenant name is required.");
            }

            using var scope = _scopeFactory.CreateScope();
            var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
            var connectionString = _configuration.GetConnectionString("LedgerDb");

            // Check if slug already exists
            if (await masterDb.Tenants.AnyAsync(t => t.Slug == normalizedSlug))
            {
                return TenantProvisioningResult.Failed($"A tenant with slug '{normalizedSlug}' already exists.");
            }

            // Audit log will be updated with tenant ID after tenant is created
            var auditLog = new TenantAuditLog
            {
                TenantId = null, // Will be set after tenant is created
                Operation = "CREATE",
                SchemaName = schemaName,
                PerformedBy = performedBy,
                IsSuccess = false
            };

            try
            {
                // Acquire advisory lock to prevent concurrent tenant creation
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();

                await using var lockCommand = connection.CreateCommand();
                lockCommand.CommandText = "SELECT pg_advisory_lock(12345)"; // Arbitrary lock ID for tenant creation
                await lockCommand.ExecuteNonQueryAsync();

                try
                {
                    // 1. Create tenant record (inactive initially)
                    var tenant = new Models.Master.Tenant
                    {
                        Name = name.Trim(),
                        Slug = normalizedSlug,
                        SchemaName = schemaName,
                        IsActive = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    masterDb.Tenants.Add(tenant);
                    await masterDb.SaveChangesAsync();

                    // Update audit log with the generated tenant ID
                    auditLog.TenantId = tenant.Id;

                    _logger.LogInformation("Created tenant record for {TenantName} ({Slug})", name, normalizedSlug);

                    // 2. Create schema
                    await using var createSchemaCommand = connection.CreateCommand();
                    createSchemaCommand.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{schemaName}\"";
                    await createSchemaCommand.ExecuteNonQueryAsync();

                    _logger.LogInformation("Created schema {SchemaName}", schemaName);

                    // 3. Apply migrations to the new schema
                    await ApplyMigrationsToSchemaAsync(schemaName, connectionString);

                    _logger.LogInformation("Applied migrations to schema {SchemaName}", schemaName);

                    // 4. Create admin user in the tenant schema
                    var adminUsername = $"{normalizedSlug}|admin";
                    var adminPasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultAdminPassword);

                    await CreateTenantAdminUserAsync(connection, schemaName, adminPasswordHash);

                    _logger.LogInformation("Created admin user {AdminUsername} for tenant {TenantName}", adminUsername, name);

                    // 5. Activate tenant
                    tenant.IsActive = true;
                    tenant.ActivatedAt = DateTime.UtcNow;
                    await masterDb.SaveChangesAsync();

                    // Update audit log
                    auditLog.IsSuccess = true;
                    auditLog.Details = $"Tenant created successfully with schema {schemaName}";
                    masterDb.TenantAuditLogs.Add(auditLog);
                    await masterDb.SaveChangesAsync();

                    // Invalidate cache
                    _cacheService.InvalidateTenantBySlug(normalizedSlug);

                    _logger.LogInformation("Successfully provisioned tenant {TenantName} ({Slug})", name, normalizedSlug);

                    return TenantProvisioningResult.Succeeded(tenant.Id, schemaName, adminUsername, DefaultAdminPassword);
                }
                finally
                {
                    // Release advisory lock
                    await using var unlockCommand = connection.CreateCommand();
                    unlockCommand.CommandText = "SELECT pg_advisory_unlock(12345)";
                    await unlockCommand.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to provision tenant {TenantName} ({Slug})", name, normalizedSlug);

                // Record failure in audit log
                auditLog.IsSuccess = false;
                auditLog.ErrorMessage = ex.Message;
                auditLog.Details = $"Failed at: {ex.Source}";

                try
                {
                    masterDb.TenantAuditLogs.Add(auditLog);
                    await masterDb.SaveChangesAsync();
                }
                catch
                {
                    // Ignore audit log save failure
                }

                return TenantProvisioningResult.Failed($"Failed to create tenant: {ex.Message}");
            }
        }

        private async Task ApplyMigrationsToSchemaAsync(string schemaName, string connectionString)
        {
            // Create a new DbContext to generate the SQL script
            var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            using var dbContext = new TenantDbContext(optionsBuilder.Options);

            // Generate the create script
            var sql = dbContext.Database.GenerateCreateScript();

            // The generated SQL uses unqualified table names
            // We need to wrap it with SET search_path so tables are created in the tenant schema
            var wrappedSql = $@"
                SET search_path TO ""{schemaName}"";
                {sql}
                SET search_path TO public;
            ";

            // Execute the SQL using a direct connection
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = wrappedSql;
            command.CommandTimeout = 120; // 2 minutes timeout for large schemas

            _logger.LogInformation("Executing schema creation SQL for {SchemaName}", schemaName);

            try
            {
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute schema creation SQL for {SchemaName}. SQL length: {SqlLength}", schemaName, sql.Length);
                throw;
            }
        }

        private async Task CreateTenantAdminUserAsync(NpgsqlConnection connection, string schemaName, string passwordHash)
        {
            // Insert admin user into tenant schema
            await using var command = connection.CreateCommand();
            command.CommandText = $@"
                INSERT INTO ""{schemaName}"".""Users""
                (""Username"", ""PasswordHash"", ""FullName"", ""Email"", ""IsAdmin"", ""Subroute"", ""IsActive"", ""CreatedDate"")
                VALUES
                (@username, @passwordHash, @fullName, @email, true, 'Default', true, @createdDate)";

            command.Parameters.AddWithValue("username", "admin");
            command.Parameters.AddWithValue("passwordHash", passwordHash);
            command.Parameters.AddWithValue("fullName", "Tenant Administrator");
            command.Parameters.AddWithValue("email", "admin@tenant.local");
            command.Parameters.AddWithValue("createdDate", DateTime.UtcNow);

            await command.ExecuteNonQueryAsync();

            // Get the user ID
            await using var getIdCommand = connection.CreateCommand();
            getIdCommand.CommandText = $@"SELECT ""UserId"" FROM ""{schemaName}"".""Users"" WHERE ""Username"" = 'admin'";
            var userId = Convert.ToInt32(await getIdCommand.ExecuteScalarAsync());

            // Add all controller access for admin
            var controllers = new[] { "Home", "MetaData", "Reporting", "DeliveryChallan" };
            foreach (var controller in controllers)
            {
                await using var accessCommand = connection.CreateCommand();
                accessCommand.CommandText = $@"
                    INSERT INTO ""{schemaName}"".""UserControllerAccess""
                    (""UserId"", ""Controller"", ""HasAccess"")
                    VALUES
                    (@userId, @controller, true)";

                accessCommand.Parameters.AddWithValue("userId", userId);
                accessCommand.Parameters.AddWithValue("controller", controller);

                await accessCommand.ExecuteNonQueryAsync();
            }
        }
    }
}
