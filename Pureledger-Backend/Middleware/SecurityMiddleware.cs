using System.Security.Claims;
using PureLedger.Services.Auth;

namespace PureLedger.Middleware
{
    /// <summary>
    /// Consolidated security middleware that:
    /// 1. Adds security headers to all responses
    /// 2. Validates SecurityStamp in JWT tokens to ensure tokens are invalidated when passwords change
    /// </summary>
    public class SecurityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SecurityMiddleware> _logger;

        public SecurityMiddleware(RequestDelegate next, ILogger<SecurityMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IAuthService authService)
        {
            // 1. Add security headers to all responses
            AddSecurityHeaders(context);

            // 2. Validate SecurityStamp for authenticated requests
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var validationResult = await ValidateSecurityStampAsync(context, authService);
                if (!validationResult)
                {
                    return; // Response already set in validation method
                }
            }

            await _next(context);
        }

        /// <summary>
        /// Adds security headers to the HTTP response
        /// </summary>
        private void AddSecurityHeaders(HttpContext context)
        {
            context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Add("X-Frame-Options", "DENY");
            context.Response.Headers.Add("X-XSS-Protection", "0"); // Disable legacy XSS filter, rely on CSP
            context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
            context.Response.Headers.Add("Permissions-Policy", "geolocation=(), microphone=(), camera=()");

            // Strict Content Security Policy - removed unsafe-inline and unsafe-eval for XSS protection
            context.Response.Headers.Add("Content-Security-Policy",
                "default-src 'self'; " +
                "script-src 'self'; " +  // Removed unsafe-inline and unsafe-eval
                "style-src 'self'; " +   // Removed unsafe-inline
                "img-src 'self' data: https:; " +
                "font-src 'self' data:; " +
                "connect-src 'self'; " +
                "frame-ancestors 'none'; " +  // Prevent clickjacking
                "base-uri 'self'; " +         // Prevent base tag hijacking
                "form-action 'self'");        // Restrict form submissions
        }

        /// <summary>
        /// Validates that the SecurityStamp in the JWT token matches the current user's SecurityStamp.
        /// This ensures that when a user's password is changed, all existing JWT tokens become invalid.
        /// </summary>
        /// <returns>True if validation passes, False if validation fails</returns>
        private async Task<bool> ValidateSecurityStampAsync(HttpContext context, IAuthService authService)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
            var securityStampClaim = context.User.FindFirst("SecurityStamp");

            _logger.LogInformation(
                "SecurityStamp validation for {Path}: UserIdClaim={UserId}, SecurityStampClaim={StampClaim}",
                context.Request.Path,
                userIdClaim?.Value ?? "(null)",
                securityStampClaim?.Value?.Substring(0, Math.Min(8, securityStampClaim?.Value?.Length ?? 0)) + "..." ?? "(null)");

            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                // Get the current user from database
                var user = await authService.GetUserByIdAsync(userId);

                if (user == null)
                {
                    // User no longer exists
                    _logger.LogWarning("Token validation failed: User {UserId} not found", userId);
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new { success = false, message = "Your account has been deleted or is no longer available. Please contact an administrator." });
                    return false;
                }

                _logger.LogInformation(
                    "SecurityStamp comparison for user {UserId}: Token={TokenStamp}, Database={DbStamp}, Match={Match}",
                    userId,
                    securityStampClaim?.Value?.Substring(0, Math.Min(8, securityStampClaim?.Value?.Length ?? 0)) + "...",
                    user.SecurityStamp?.Substring(0, Math.Min(8, user.SecurityStamp?.Length ?? 0)) + "...",
                    securityStampClaim?.Value == user.SecurityStamp);

                // Validate SecurityStamp
                if (securityStampClaim == null || securityStampClaim.Value != user.SecurityStamp)
                {
                    // SecurityStamp mismatch - token is no longer valid
                    _logger.LogWarning(
                        "Token validation FAILED: SecurityStamp mismatch for user {UserId}. Token stamp: {TokenStamp}, DB stamp: {DbStamp}",
                        userId,
                        securityStampClaim?.Value ?? "(null)",
                        user.SecurityStamp ?? "(null)");
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new { success = false, message = "Your session has expired due to security changes. Please log in again." });
                    return false;
                }

                // Check if user is still active
                if (!user.IsActive)
                {
                    _logger.LogWarning("Token validation failed: User {UserId} is inactive", userId);
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new { success = false, message = "Your account has been deactivated. Please contact an administrator." });
                    return false;
                }

                _logger.LogInformation("SecurityStamp validation PASSED for user {UserId}", userId);
            }

            return true;
        }
    }

    /// <summary>
    /// Extension methods for SecurityMiddleware
    /// </summary>
    public static class SecurityMiddlewareExtensions
    {
        public static IApplicationBuilder UseSecurity(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SecurityMiddleware>();
        }
    }
}
