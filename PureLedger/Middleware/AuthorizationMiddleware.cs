using Microsoft.AspNetCore.Http;
using PureLedger.Services;
using System.Security.Claims;

namespace PureLedger.Middleware
{
    public class AuthorizationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuthorizationMiddleware> _logger;

        public AuthorizationMiddleware(RequestDelegate next, ILogger<AuthorizationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IAuthService authService)
        {
            // Skip authentication checks for Account controller and static files
            var controller = context.Request.RouteValues["controller"]?.ToString();
            var action = context.Request.RouteValues["action"]?.ToString();
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
            
            // Always allow access to login-related paths and static files
            if (string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/lib/") || 
                path.StartsWith("/js/") ||
                path.StartsWith("/css/") ||
                path.StartsWith("/favicon.ico"))
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
                    if (context.User.IsInRole("Admin"))
                    {
                        await _next(context);
                        return;
                    }

                    // Check project ID if present in route or query string
                    if (context.Request.RouteValues.TryGetValue("projectId", out var projectIdObj) || 
                        context.Request.Query.TryGetValue("projectId", out var projectIdQuery))
                    {
                        string projectIdStr = (projectIdObj ?? projectIdQuery.FirstOrDefault()) as string;
                        if (int.TryParse(projectIdStr, out int projectId))
                        {
                            // Check if user has access to this project
                            bool hasProjectAccess = await authService.HasProjectAccessAsync(userId, projectId);
                            if (!hasProjectAccess)
                            {
                                _logger.LogWarning("Access denied: User {UserId} attempted to access project {ProjectId} without permission", userId, projectId);
                                await HandleAccessDenied(context);
                                return;
                            }
                        }
                    }
                    
                    // Check access permissions - simplified approach
                    bool hasAccess = false;
                    
                    if (!string.IsNullOrEmpty(action))
                    {
                        // Check for specific action access first
                        hasAccess = await authService.HasActionAccessAsync(userId, controller, action);
                        
                        // If no specific action access, check controller-level access
                        if (!hasAccess)
                        {
                            hasAccess = await authService.HasControllerAccessAsync(userId, controller);
                        }
                    }
                    else
                    {
                        // No action specified, check controller-level access
                        hasAccess = await authService.HasControllerAccessAsync(userId, controller);
                    }

                    if (!hasAccess)
                    {
                        _logger.LogWarning("Access denied: User {UserId} attempted to access {Controller}/{Action} without permission", 
                            userId, controller, action ?? "default");
                        
                        await HandleAccessDenied(context);
                        return;
                    }

                    // Log successful access for debugging
                    _logger.LogDebug("Access granted: User {UserId} accessing {Controller}/{Action}", 
                        userId, controller, action ?? "default");
                }
            }
            else if (!string.IsNullOrEmpty(controller) && !string.IsNullOrEmpty(action))
            {
                // User is not authenticated, redirect to login
                // But store the return URL so they can be redirected back after login
                var returnUrl = $"/{controller}/{action}";
                if (context.Request.QueryString.HasValue)
                {
                    returnUrl += context.Request.QueryString.Value;
                }
                
                _logger.LogInformation("Redirecting unauthenticated user to login from {Path}", context.Request.Path);
                context.Response.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}");
                return;
            }

            await _next(context);
        }

        private async Task HandleAccessDenied(HttpContext context)
        {
            // Check if this is an AJAX request
            if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Access denied" });
            }
            else
            {
                context.Response.Redirect("/Account/AccessDenied");
            }
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