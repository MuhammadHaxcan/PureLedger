using PureLedger.Data;
using PureLedger.Services.Tenant;
using System.Security.Claims;

namespace PureLedger.Middleware
{
    /// <summary>
    /// Middleware that extracts tenant information from JWT claims and sets the schema context.
    /// </summary>
    public class JwtTenantMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<JwtTenantMiddleware> _logger;

        // Paths that don't require tenant validation
        private static readonly HashSet<string> ExcludedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/api/AccountApi/login",
            "/api/AccountApi/refresh-token",
            "/api/AccountApi/check-auth",
            "/api/SystemAdminApi/login"
        };

        // Paths that are system admin only (no tenant context needed)
        private static readonly HashSet<string> SystemAdminPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/api/SystemAdminApi",
            "/api/system/tenants"
        };

        public JwtTenantMiddleware(RequestDelegate next, ILogger<JwtTenantMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ITenantSchemaProvider schemaProvider, ITenantCacheService cacheService)
        {
            var path = context.Request.Path.Value ?? string.Empty;

            // Skip tenant validation for excluded paths
            if (ExcludedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                await _next(context);
                return;
            }

            // Check if user is authenticated
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                await _next(context);
                return;
            }

            // Check if user is a system admin
            var userType = context.User.FindFirst("user_type")?.Value;
            if (userType == "SystemAdmin")
            {
                // System admins don't have tenant context - they manage all tenants
                _logger.LogDebug("System admin request - no tenant context");
                await _next(context);
                return;
            }

            // For tenant users, extract and validate tenant schema from JWT
            var tenantSchema = context.User.FindFirst("tenant_schema")?.Value;
            if (string.IsNullOrEmpty(tenantSchema))
            {
                _logger.LogWarning("No tenant_schema claim found in JWT for user {User}", context.User.Identity?.Name);

                // Allow the request to continue - the DbContext will use default behavior
                // This maintains backward compatibility with old tokens
                await _next(context);
                return;
            }

            // Validate schema name format
            if (!TenantSchemaValidator.IsValidSchemaName(tenantSchema))
            {
                _logger.LogError("Invalid tenant schema name in JWT: {Schema}", tenantSchema);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { success = false, message = "Invalid tenant context" });
                return;
            }

            // Get tenant from cache and verify it's active
            var tenant = await cacheService.GetTenantBySchemaNameAsync(tenantSchema);
            if (tenant == null)
            {
                _logger.LogError("Tenant not found for schema: {Schema}", tenantSchema);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { success = false, message = "Tenant not found" });
                return;
            }

            if (!tenant.IsActive)
            {
                _logger.LogWarning("Attempted access to deactivated tenant: {TenantName}", tenant.Name);
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new { success = false, message = "Tenant is deactivated" });
                return;
            }

            // Set tenant context for this request
            schemaProvider.SetTenant(tenant.SchemaName, tenant.Id, tenant.Name);
            _logger.LogDebug("Set tenant context: {TenantName} ({Schema})", tenant.Name, tenant.SchemaName);

            await _next(context);
        }
    }

    /// <summary>
    /// Extension methods for JwtTenantMiddleware.
    /// </summary>
    public static class JwtTenantMiddlewareExtensions
    {
        public static IApplicationBuilder UseJwtTenantMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<JwtTenantMiddleware>();
        }
    }
}
