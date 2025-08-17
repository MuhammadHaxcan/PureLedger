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
                                context.Response.Redirect("/Account/AccessDenied");
                                return;
                            }
                        }
                    }
                    
                    // Check if user has access to this specific action
                    if (!string.IsNullOrEmpty(action))
                    {
                        // Check for action-specific permission ONLY - don't check controller permission
                        bool hasAccess = false;
                        
                        // First check for specific action permission
                        var actionPermission = await GetSpecificActionPermission(authService, userId, controller, action);
                        if (actionPermission)
                        {
                            hasAccess = true;
                        }

                        
                        if (!hasAccess)
                        {
                            _logger.LogWarning("Access denied: User {UserId} attempted to access {Controller}/{Action} without permission", 
                                userId, controller, action);
                            
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
                            return;
                        }
                    }
                    // Fall back to controller-level access if no action is specified
                    else
                    {
                        bool hasControllerAccess = await authService.HasControllerAccessAsync(userId, controller);
                        if (!hasControllerAccess)
                        {
                            _logger.LogWarning("Access denied: User {UserId} attempted to access controller {Controller} without permission", 
                                userId, controller);
                            
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
                            return;
                        }
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

        // Helper method to check for action-specific permission without controller fallback
        private async Task<bool> GetSpecificActionPermission(IAuthService authService, int userId, string controller, string action)
        {
            // Get all user action permissions for this controller
            var userActionsDict = await authService.GetUserActionAccessAsync(userId);
            if (userActionsDict.TryGetValue(controller, out var actions))
            {
                return actions.Contains(action);
            }
            return false;
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