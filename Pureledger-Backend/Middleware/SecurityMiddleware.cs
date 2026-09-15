using System.Security.Claims;
using PureLedger.Services.Auth;
using PureLedger.Services.Interface;

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

        public async Task InvokeAsync(HttpContext context, IAuthService authService, IApiErrorResponseFactory errorResponseFactory) {
            // 1. Add security headers to all responses
            AddSecurityHeaders(context);

            // 2. Validate user is still active for authenticated requests
            if (context.User.Identity?.IsAuthenticated == true) {
                var validationResult = await ValidateUserAsync(context, authService, errorResponseFactory);
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
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["X-XSS-Protection"] = "0"; // Disable legacy XSS filter, rely on CSP
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

            // Content Security Policy for HTTP - allows inline styles for React
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data: http:; " +
                "font-src 'self' data:; " +
                "connect-src 'self'; " +
                "frame-ancestors 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self'";
        }

        /// <summary>
        /// Validates that the user still exists and is active.
        /// </summary>
        /// <returns>True if validation passes, False if validation fails</returns>
        private async Task<bool> ValidateUserAsync(HttpContext context, IAuthService authService, IApiErrorResponseFactory errorResponseFactory) {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId)) {
                // Get the current user from database
                var user = await authService.GetUserByIdAsync(userId);

                if (user == null) {
                    // User no longer exists
                    _logger.LogWarning("Token validation failed: User {UserId} not found", userId);
                    await WriteSecurityErrorAsync(
                        context,
                        errorResponseFactory,
                        "Your account has been deleted or is no longer available. Please contact an administrator.",
                        "USER_NOT_FOUND");
                    return false;
                }

                // Check if user is still active
                if (!user.IsActive) {
                    _logger.LogWarning("Token validation failed: User {UserId} is inactive", userId);
                    await WriteSecurityErrorAsync(
                        context,
                        errorResponseFactory,
                        "Your account has been deactivated. Please contact an administrator.",
                        "USER_INACTIVE");
                    return false;
                }
            }

            return true;
        }

        private static async Task WriteSecurityErrorAsync(
            HttpContext context,
            IApiErrorResponseFactory errorResponseFactory,
            string message,
            string errorCode) {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(errorResponseFactory.Create(
                context,
                StatusCodes.Status401Unauthorized,
                message,
                new[] { message },
                errorCode));
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
