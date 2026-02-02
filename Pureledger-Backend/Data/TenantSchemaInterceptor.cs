using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PureLedger.Data
{
    /// <summary>
    /// EF Core DbConnectionInterceptor that sets the PostgreSQL search_path
    /// to the current tenant's schema before executing queries.
    /// </summary>
    public class TenantSchemaInterceptor : DbConnectionInterceptor
    {
        private readonly ITenantSchemaProvider _schemaProvider;
        private readonly ILogger<TenantSchemaInterceptor> _logger;

        public TenantSchemaInterceptor(ITenantSchemaProvider schemaProvider, ILogger<TenantSchemaInterceptor> logger)
        {
            _schemaProvider = schemaProvider;
            _logger = logger;
        }

        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            SetSearchPath(connection);
            base.ConnectionOpened(connection, eventData);
        }

        public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            await SetSearchPathAsync(connection, cancellationToken);
            await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        }

        private void SetSearchPath(DbConnection connection)
        {
            var schemaName = _schemaProvider.SchemaName;
            if (string.IsNullOrEmpty(schemaName))
            {
                _logger.LogDebug("No tenant schema set, using public schema");
                return;
            }

            // Validate schema name to prevent SQL injection
            if (!TenantSchemaValidator.IsValidSchemaName(schemaName))
            {
                _logger.LogError("Invalid schema name attempted: {SchemaName}", schemaName);
                throw new InvalidOperationException($"Invalid schema name: {schemaName}");
            }

            try
            {
                using var command = connection.CreateCommand();
                // Use quoted identifier to safely include the schema name
                command.CommandText = $"SET search_path TO \"{schemaName}\", public;";
                command.ExecuteNonQuery();
                _logger.LogDebug("Set search_path to {SchemaName}", schemaName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set search_path to {SchemaName}", schemaName);
                throw;
            }
        }

        private async Task SetSearchPathAsync(DbConnection connection, CancellationToken cancellationToken)
        {
            var schemaName = _schemaProvider.SchemaName;
            if (string.IsNullOrEmpty(schemaName))
            {
                _logger.LogDebug("No tenant schema set, using public schema");
                return;
            }

            // Validate schema name to prevent SQL injection
            if (!TenantSchemaValidator.IsValidSchemaName(schemaName))
            {
                _logger.LogError("Invalid schema name attempted: {SchemaName}", schemaName);
                throw new InvalidOperationException($"Invalid schema name: {schemaName}");
            }

            try
            {
                using var command = connection.CreateCommand();
                // Use quoted identifier to safely include the schema name
                command.CommandText = $"SET search_path TO \"{schemaName}\", public;";
                await command.ExecuteNonQueryAsync(cancellationToken);
                _logger.LogDebug("Set search_path to {SchemaName}", schemaName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set search_path to {SchemaName}", schemaName);
                throw;
            }
        }
    }
}
