using System.Security.Claims;
using PureLedger.Services.Auth;

namespace PureLedger.Middleware {
    /// <summary>
    /// Security middleware that:
    /// 1. Adds security headers to all responses
    /// 2. Validates user is still active for authenticated requests
    /// </summary>
    public class SecurityMiddleware {
        private readonly RequestDelegate _next;
        private readonly ILogger<SecurityMiddleware> _logger;

        public SecurityMiddleware(RequestDelegate next, ILogger<SecurityMiddleware> logger) {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(
            HttpContext context,
            IAuthService authService,
            ISystemAdminAuthService systemAdminAuthService) {
            // 1. Add security headers to all responses
            AddSecurityHeaders(context);

            // 2. Validate user is still active for authenticated requests
            if (context.User.Identity?.IsAuthenticated == true) {
                var validationResult = await ValidateUserAsync(context, authService, systemAdminAuthService);
                if (!validationResult) {
                    return; // Response already set in validation method
                }
            }

            await _next(context);
        }

        /// <summary>
        /// Adds security headers to the HTTP response
        /// </summary>
        private void AddSecurityHeaders(HttpContext context) {
            context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Add("X-Frame-Options", "DENY");
            context.Response.Headers.Add("X-XSS-Protection", "0"); // Disable legacy XSS filter, rely on CSP
            context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
            context.Response.Headers.Add("Permissions-Policy", "geolocation=(), microphone=(), camera=()");

            // Content Security Policy for HTTP - allows inline styles for React
            context.Response.Headers.Add("Content-Security-Policy",
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data: http:; " +
                "font-src 'self' data:; " +
                "connect-src 'self'; " +
                "frame-ancestors 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self'");
        }

        /// <summary>
        /// Validates that the user still exists and is active.
        /// Routes to appropriate auth service based on user_type claim.
        /// </summary>
        /// <returns>True if validation passes, False if validation fails</returns>
        private async Task<bool> ValidateUserAsync(
            HttpContext context,
            IAuthService authService,
            ISystemAdminAuthService systemAdminAuthService) {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
            var userType = context.User.FindFirst("user_type")?.Value;

            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId)) {
                // Handle SystemAdmin users - validate against master schema
                if (userType == "SystemAdmin") {
                    var admin = await systemAdminAuthService.GetByIdAsync(userId);

                    if (admin == null) {
                        _logger.LogWarning("Token validation failed: SystemAdmin {AdminId} not found", userId);
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(new { success = false, message = "Admin account not found." });
                        return false;
                    }

                    if (!admin.IsActive) {
                        _logger.LogWarning("Token validation failed: SystemAdmin {AdminId} is inactive", userId);
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(new { success = false, message = "Admin account is deactivated." });
                        return false;
                    }

                    return true; // SystemAdmin validated against master schema
                }

                // Handle tenant users - validate against tenant schema
                var user = await authService.GetUserByIdAsync(userId);

                if (user == null) {
                    // User no longer exists
                    _logger.LogWarning("Token validation failed: User {UserId} not found", userId);
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new { success = false, message = "Your account has been deleted or is no longer available. Please contact an administrator." });
                    return false;
                }

                // Check if user is still active
                if (!user.IsActive) {
                    _logger.LogWarning("Token validation failed: User {UserId} is inactive", userId);
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new { success = false, message = "Your account has been deactivated. Please contact an administrator." });
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Extension methods for SecurityMiddleware
    /// </summary>
    public static class SecurityMiddlewareExtensions {
        public static IApplicationBuilder UseSecurity(this IApplicationBuilder builder) {
            return builder.UseMiddleware<SecurityMiddleware>();
        }
    }
}
