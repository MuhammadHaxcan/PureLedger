namespace PureLedger.Data
{
    /// <summary>
    /// Scoped service that holds the current tenant's schema information for the request.
    /// </summary>
    public class TenantSchemaProvider : ITenantSchemaProvider
    {
        private string? _schemaName;
        private int? _tenantId;
        private string? _tenantName;

        public string? SchemaName => _schemaName;
        public int? TenantId => _tenantId;
        public string? TenantName => _tenantName;

        public bool HasTenant => !string.IsNullOrEmpty(_schemaName);

        public void SetTenant(string schemaName, int tenantId, string tenantName)
        {
            _schemaName = schemaName;
            _tenantId = tenantId;
            _tenantName = tenantName;
        }

        public void ClearTenant()
        {
            _schemaName = null;
            _tenantId = null;
            _tenantName = null;
        }
    }
}
