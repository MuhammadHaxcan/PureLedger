using Microsoft.AspNetCore.Http;
using PureLedger.Services;
using System.Security.Claims;

namespace PureLedger.Middleware
{
    public class AuthorizationMiddleware
    {
        private readonly RequestDelegate _next;

        public AuthorizationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IAuthService authService)
        {
            // Skip authentication checks for Account controller
            var controller = context.Request.RouteValues["controller"]?.ToString();
            var action = context.Request.RouteValues["action"]?.ToString();
            
            // Skip auth checks for login-related paths
            if (string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase) ||
                context.Request.Path.StartsWithSegments("/Account/Login"))
            {
                await _next(context);
                return;
            }

            // Check if user is authenticated
            if (context.User.Identity?.IsAuthenticated == true)
            {
                // Get user ID and controller name
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId) && !string.IsNullOrEmpty(controller))
                {
                    // Check if user is admin (admins have access to all controllers)
                    if (!context.User.IsInRole("Admin"))
                    {
                        // Check if user has access to the controller
                        bool hasAccess = await authService.HasControllerAccessAsync(userId, controller);
                        if (!hasAccess)
                        {
                            context.Response.Redirect("/Account/AccessDenied");
                            return;
                        }
                    }
                }
            }

            await _next(context);
        }
    }

    public static class AuthorizationMiddlewareExtensions
    {
        public static IApplicationBuilder UseAuthorizationMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AuthorizationMiddleware>();
        }
    }
}