using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PureLedger.Filters {
    /// <summary>
    /// Action filter that validates XSRF tokens for authenticated requests.
    /// Skips validation for anonymous endpoints.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class ValidateXsrfTokenAttribute : Attribute, IAsyncActionFilter {
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

            var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
            var logger = context.HttpContext.RequestServices.GetService<ILogger<ValidateXsrfTokenAttribute>>();

            // Check if we have the required tokens before validation
            var hasXsrfCookie = context.HttpContext.Request.Cookies.ContainsKey("XSRF-TOKEN");
            var hasXsrfHeader = context.HttpContext.Request.Headers.ContainsKey("X-XSRF-TOKEN");

            if (!hasXsrfCookie || !hasXsrfHeader) {
                logger?.LogWarning(
                    "XSRF validation skipped - missing tokens. Cookie: {HasCookie}, Header: {HasHeader}, Path: {Path}",
                    hasXsrfCookie, hasXsrfHeader, context.HttpContext.Request.Path);

                // If missing tokens, generate new ones and return error asking to retry
                // This handles the edge case of first request after login
                if (!hasXsrfCookie) {
                    // Generate new tokens to set the cookie
                    var tokens = antiforgery.GetAndStoreTokens(context.HttpContext);
                    context.Result = new ObjectResult(new {
                        success = false,
                        message = "XSRF token cookie missing. Please refresh and try again.",
                        xsrfToken = tokens.RequestToken,
                        needsRefresh = true
                    }) {
                        StatusCode = 400
                    };
                    return;
                }

                context.Result = new UnauthorizedObjectResult(new {
                    success = false,
                    message = "Invalid or missing XSRF token header"
                });
                return;
            }

            try {
                await antiforgery.ValidateRequestAsync(context.HttpContext);
            } catch (AntiforgeryValidationException ex) {
                logger?.LogWarning(
                    "XSRF token validation failed for {Method} {Path}. Error: {Error}",
                    method, context.HttpContext.Request.Path, ex.Message);

                // Generate new tokens for the response to help recovery
                var tokens = antiforgery.GetAndStoreTokens(context.HttpContext);

                context.Result = new ObjectResult(new {
                    success = false,
                    message = "Invalid XSRF token. Please try again.",
                    xsrfToken = tokens.RequestToken,
                    needsRefresh = true
                }) {
                    StatusCode = 400
                };
                return;
            }

            await next();
        }
    }
}
