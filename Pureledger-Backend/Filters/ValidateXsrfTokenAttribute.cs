using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PureLedger.Filters
{
    /// <summary>
    /// Action filter that validates XSRF tokens for authenticated requests.
    /// Skips validation for anonymous endpoints.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class ValidateXsrfTokenAttribute : Attribute, IAsyncActionFilter
    {
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

            var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();

            try
            {
                await antiforgery.ValidateRequestAsync(context.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                var logger = context.HttpContext.RequestServices.GetService<ILogger<ValidateXsrfTokenAttribute>>();
                logger?.LogWarning("XSRF token validation failed for {Method} {Path}",
                    method, context.HttpContext.Request.Path);

                context.Result = new UnauthorizedObjectResult(new
                {
                    success = false,
                    message = "Invalid or missing XSRF token"
                });
                return;
            }

            await next();
        }
    }
}
