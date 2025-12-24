using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Cryptography;

namespace PureLedger.Filters {
    /// <summary>
    /// Action filter that validates XSRF tokens using simple double-submit cookie pattern.
    /// Compares the cookie value with the header value directly (no cryptographic linking).
    /// This is simpler and more reliable than the built-in antiforgery system.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class ValidateXsrfTokenAttribute : Attribute, IAsyncActionFilter {
        private const string CookieName = "XSRF-TOKEN";
        private const string HeaderName = "X-XSRF-TOKEN";

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next) {
            // Skip validation for unauthenticated requests
            if (context.HttpContext.User?.Identity?.IsAuthenticated != true) {
                await next();
                return;
            }

            // Only validate for state-changing methods
            var method = context.HttpContext.Request.Method;
            if (method != "POST" && method != "PUT" && method != "DELETE" && method != "PATCH") {
                await next();
                return;
            }

            // Skip validation for logout - allow logout even with invalid/missing XSRF token
            var path = context.HttpContext.Request.Path.Value?.ToLowerInvariant() ?? "";
            if (path.EndsWith("/accountapi/logout")) {
                await next();
                return;
            }

            var logger = context.HttpContext.RequestServices.GetService<ILogger<ValidateXsrfTokenAttribute>>();

            // Get cookie and header values
            var cookieValue = context.HttpContext.Request.Cookies[CookieName];
            var headerValue = context.HttpContext.Request.Headers[HeaderName].FirstOrDefault();

            // Check if we have both tokens
            if (string.IsNullOrEmpty(cookieValue)) {
                logger?.LogWarning("XSRF validation failed - missing cookie. Path: {Path}", context.HttpContext.Request.Path);

                // Generate new token and set cookie
                var newToken = GenerateToken();
                SetXsrfCookie(context.HttpContext, newToken);

                context.Result = new ObjectResult(new {
                    success = false,
                    message = "XSRF token cookie missing. Please try again.",
                    xsrfToken = newToken,
                    needsRefresh = true
                }) {
                    StatusCode = 400
                };
                return;
            }

            if (string.IsNullOrEmpty(headerValue)) {
                logger?.LogWarning("XSRF validation failed - missing header. Path: {Path}", context.HttpContext.Request.Path);

                context.Result = new ObjectResult(new {
                    success = false,
                    message = "XSRF token header missing.",
                    xsrfToken = cookieValue,
                    needsRefresh = true
                }) {
                    StatusCode = 400
                };
                return;
            }

            // Simple comparison - cookie and header must match exactly
            if (!string.Equals(cookieValue, headerValue, StringComparison.Ordinal)) {
                logger?.LogWarning(
                    "XSRF token mismatch for {Method} {Path}. Cookie length: {CookieLen}, Header length: {HeaderLen}",
                    method, context.HttpContext.Request.Path, cookieValue.Length, headerValue.Length);

                // Generate new token for retry
                var newToken = GenerateToken();
                SetXsrfCookie(context.HttpContext, newToken);

                context.Result = new ObjectResult(new {
                    success = false,
                    message = "Invalid XSRF token. Please try again.",
                    xsrfToken = newToken,
                    needsRefresh = true
                }) {
                    StatusCode = 400
                };
                return;
            }

            // Validation passed
            await next();
        }

        /// <summary>
        /// Generate a cryptographically secure random token
        /// </summary>
        public static string GenerateToken() {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        /// <summary>
        /// Set the XSRF cookie on the response
        /// </summary>
        public static void SetXsrfCookie(HttpContext context, string token) {
            // Detect if request is HTTPS (via X-Forwarded-Proto from nginx)
            var isSecure = context.Request.IsHttps;

            context.Response.Cookies.Append(CookieName, token, new CookieOptions {
                HttpOnly = false, // Must be readable by JavaScript
                Secure = isSecure, // True for HTTPS, false for HTTP (development)
                SameSite = SameSiteMode.Lax,
                Path = "/",
                IsEssential = true
            });
        }
    }
}
