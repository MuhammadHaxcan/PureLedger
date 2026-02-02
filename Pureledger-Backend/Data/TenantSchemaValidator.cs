using System.Text.RegularExpressions;

namespace PureLedger.Data
{
    /// <summary>
    /// Validates tenant schema names and slugs to ensure they meet PostgreSQL requirements
    /// and follow the expected naming convention.
    /// </summary>
    public static class TenantSchemaValidator
    {
        // Schema name pattern: tenant_ followed by 1-50 lowercase alphanumeric characters or underscores
        private static readonly Regex SchemaNamePattern = new Regex(
            @"^tenant_[a-z0-9_]{1,50}$",
            RegexOptions.Compiled);

        // Slug pattern: 1-50 lowercase alphanumeric characters or underscores
        private static readonly Regex SlugPattern = new Regex(
            @"^[a-z0-9_]{1,50}$",
            RegexOptions.Compiled);

        // Reserved schema names that cannot be used
        private static readonly HashSet<string> ReservedSchemas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "public",
            "master",
            "pg_catalog",
            "information_schema",
            "pg_toast",
            "pg_temp_1",
            "pg_toast_temp_1"
        };

        // Reserved slugs that cannot be used
        private static readonly HashSet<string> ReservedSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "admin",
            "system",
            "master",
            "public",
            "root",
            "sys",
            "api",
            "www",
            "app"
        };

        /// <summary>
        /// Validates a schema name.
        /// </summary>
        /// <param name="schemaName">The schema name to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool IsValidSchemaName(string schemaName)
        {
            if (string.IsNullOrWhiteSpace(schemaName))
                return false;

            if (ReservedSchemas.Contains(schemaName))
                return false;

            return SchemaNamePattern.IsMatch(schemaName);
        }

        /// <summary>
        /// Validates a tenant slug.
        /// </summary>
        /// <param name="slug">The slug to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool IsValidSlug(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return false;

            if (ReservedSlugs.Contains(slug))
                return false;

            return SlugPattern.IsMatch(slug);
        }

        /// <summary>
        /// Converts a slug to a schema name.
        /// </summary>
        /// <param name="slug">The slug to convert</param>
        /// <returns>The schema name (e.g., "prime" -> "tenant_prime")</returns>
        public static string SlugToSchemaName(string slug)
        {
            return $"tenant_{slug.ToLowerInvariant()}";
        }

        /// <summary>
        /// Extracts the slug from a schema name.
        /// </summary>
        /// <param name="schemaName">The schema name</param>
        /// <returns>The slug (e.g., "tenant_prime" -> "prime")</returns>
        public static string? SchemaNameToSlug(string schemaName)
        {
            if (string.IsNullOrWhiteSpace(schemaName) || !schemaName.StartsWith("tenant_"))
                return null;

            return schemaName.Substring(7);
        }

        /// <summary>
        /// Validates a schema name and returns detailed error information.
        /// </summary>
        /// <param name="schemaName">The schema name to validate</param>
        /// <param name="errorMessage">The error message if validation fails</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool TryValidateSchemaName(string schemaName, out string? errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(schemaName))
            {
                errorMessage = "Schema name cannot be empty.";
                return false;
            }

            if (ReservedSchemas.Contains(schemaName))
            {
                errorMessage = $"Schema name '{schemaName}' is reserved and cannot be used.";
                return false;
            }

            if (!SchemaNamePattern.IsMatch(schemaName))
            {
                errorMessage = "Schema name must start with 'tenant_' followed by 1-50 lowercase letters, numbers, or underscores.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates a slug and returns detailed error information.
        /// </summary>
        /// <param name="slug">The slug to validate</param>
        /// <param name="errorMessage">The error message if validation fails</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool TryValidateSlug(string slug, out string? errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(slug))
            {
                errorMessage = "Slug cannot be empty.";
                return false;
            }

            if (ReservedSlugs.Contains(slug))
            {
                errorMessage = $"Slug '{slug}' is reserved and cannot be used.";
                return false;
            }

            if (!SlugPattern.IsMatch(slug))
            {
                errorMessage = "Slug must be 1-50 lowercase letters, numbers, or underscores.";
                return false;
            }

            return true;
        }
    }
}
