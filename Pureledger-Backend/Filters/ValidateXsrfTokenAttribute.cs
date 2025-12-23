using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PureLedger.Filters
{
    /// <summary>
    /// Action filter that validates XSRF tokens for authenticated requests.
    /// Skips validation for anonymous endpoints and endpoints marked with [IgnoreAntiforgeryToken].
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class ValidateXsrfTokenAttribute : Attribute, IAsyncActionFilter
    {
        // Endpoints that should skip XSRF validation (e.g., logout needs to work even with stale tokens)
        private static readonly HashSet<string> SkipValidationEndpoints = new(StringComparer.OrdinalIgnoreCase)
        {
            "/api/AccountApi/logout"
        };

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Skip validation for unauthenticated requests
            if (context.HttpContext.User?.Identity?.IsAuthenticated != true)
            {
                await next();
                return;
            }

            // Only validate for state-changing methods
            var method = context.HttpContext.Request.Method;
            if (method != "POST" && method != "PUT" && method != "DELETE" && method != "PATCH")
            {
                await next();
                return;
            }

            // Skip validation for certain endpoints (like logout) that need to work even with stale tokens
            var path = context.HttpContext.Request.Path.Value;
            if (!string.IsNullOrEmpty(path) && SkipValidationEndpoints.Contains(path))
            {
                await next();
                return;
            }

            // Check if action or controller has [IgnoreAntiforgeryToken] attribute
            var hasIgnoreAttribute = context.ActionDescriptor.EndpointMetadata
                .Any(m => m is IgnoreAntiforgeryTokenAttribute);
            if (hasIgnoreAttribute)
            {
                await next();
                return;
            }

            var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
            var logger = context.HttpContext.RequestServices.GetService<ILogger<ValidateXsrfTokenAttribute>>();

            // Check if we have the required tokens before validation
            var hasXsrfCookie = context.HttpContext.Request.Cookies.ContainsKey("XSRF-TOKEN");
            var hasXsrfHeader = context.HttpContext.Request.Headers.ContainsKey("X-XSRF-TOKEN");

            // Get token values for logging (truncated for security)
            var cookieValue = context.HttpContext.Request.Cookies["XSRF-TOKEN"];
            var headerValue = context.HttpContext.Request.Headers["X-XSRF-TOKEN"].FirstOrDefault();
            var cookiePreview = !string.IsNullOrEmpty(cookieValue) && cookieValue.Length > 20
                ? cookieValue.Substring(0, 20) + "..." : cookieValue ?? "(null)";
            var headerPreview = !string.IsNullOrEmpty(headerValue) && headerValue.Length > 20
                ? headerValue.Substring(0, 20) + "..." : headerValue ?? "(null)";

            // Log all cookies present in request for debugging
            var allCookies = string.Join(", ", context.HttpContext.Request.Cookies.Keys);
            logger?.LogInformation(
                "XSRF Check for {Method} {Path}: Cookie present: {HasCookie} ({CookiePreview}), Header present: {HasHeader} ({HeaderPreview}), All cookies: [{AllCookies}], IsHttps: {IsHttps}, Scheme: {Scheme}",
                method, context.HttpContext.Request.Path, hasXsrfCookie, cookiePreview, hasXsrfHeader, headerPreview,
                allCookies, context.HttpContext.Request.IsHttps, context.HttpContext.Request.Scheme);

            if (!hasXsrfCookie || !hasXsrfHeader)
            {
                logger?.LogWarning(
                    "XSRF validation failed - missing tokens. Cookie: {HasCookie}, Header: {HasHeader}, Path: {Path}",
                    hasXsrfCookie, hasXsrfHeader, context.HttpContext.Request.Path);

                // If missing tokens, generate new ones and return error asking to retry
                // This handles the edge case of first request after login
                if (!hasXsrfCookie)
                {
                    // Generate new tokens to set the cookie
                    var tokens = antiforgery.GetAndStoreTokens(context.HttpContext);
                    logger?.LogInformation("Generated new XSRF tokens. New request token: {TokenPreview}...",
                        tokens.RequestToken?.Substring(0, Math.Min(20, tokens.RequestToken?.Length ?? 0)));

                    context.Result = new ObjectResult(new
                    {
                        success = false,
                        message = "XSRF token cookie missing. Please refresh and try again.",
                        xsrfToken = tokens.RequestToken,
                        needsRefresh = true
                    })
                    {
                        StatusCode = 400
                    };
                    return;
                }

                context.Result = new UnauthorizedObjectResult(new
                {
                    success = false,
                    message = "Invalid or missing XSRF token header"
                });
                return;
            }

            try
            {
                await antiforgery.ValidateRequestAsync(context.HttpContext);
                logger?.LogInformation("XSRF validation successful for {Method} {Path}", method, context.HttpContext.Request.Path);
            }
            catch (AntiforgeryValidationException ex)
            {
                logger?.LogWarning(
                    "XSRF token validation FAILED for {Method} {Path}. Error: {Error}. Cookie: {CookiePreview}, Header: {HeaderPreview}",
                    method, context.HttpContext.Request.Path, ex.Message, cookiePreview, headerPreview);

                // Generate new tokens for the response to help recovery
                var tokens = antiforgery.GetAndStoreTokens(context.HttpContext);
                logger?.LogInformation("Generated new XSRF tokens after failure. New request token: {TokenPreview}...",
                    tokens.RequestToken?.Substring(0, Math.Min(20, tokens.RequestToken?.Length ?? 0)));

                context.Result = new ObjectResult(new
                {
                    success = false,
                    message = "Invalid XSRF token. Please try again.",
                    xsrfToken = tokens.RequestToken,
                    needsRefresh = true
                })
                {
                    StatusCode = 400
                };
                return;
            }

            await next();
        }
    }
}
