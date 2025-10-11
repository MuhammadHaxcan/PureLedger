using Microsoft.AspNetCore.Http;
using PureLedger.Services;
using System.Security.Claims;

namespace PureLedger.Middleware
{
    public class AuthorizationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuthorizationMiddleware> _logger;
        
        // Define utility endpoints that should be accessible when user has any access to the controller
        private static readonly Dictionary<string, HashSet<string>> ApiUtilityEndpoints = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Home"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "GetCategoriesByProject",
                "GetAccountsByCategory",
                "GetProductDetails"
            },
            ["DeliveryChallan"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "GetCategoriesByProject",
                "GetAccountsByCategory",
                "GetLocations"
            },
            ["Reporting"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "GetCategoriesByProject",
                "GetAccountsByCategory"
            }
        };

        public AuthorizationMiddleware(RequestDelegate next, ILogger<AuthorizationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IAuthService authService)
        {
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

            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId) && !string.IsNullOrEmpty(controller))
                {
                    // Check if user is admin
                    if (context.User.IsInRole("Admin"))
                    {
                        await _next(context);
                        return;
                    }

                    // Check project access if projectId is present
                    if (context.Request.RouteValues.TryGetValue("projectId", out var projectIdObj) || 
                        context.Request.Query.TryGetValue("projectId", out var projectIdQuery))
                    {
                        string projectIdStr = (projectIdObj ?? projectIdQuery.FirstOrDefault()) as string;
                        if (int.TryParse(projectIdStr, out int projectId))
                        {
                            bool hasProjectAccess = await authService.HasProjectAccessAsync(userId, projectId);
                            if (!hasProjectAccess)
                            {
                                _logger.LogWarning("Access denied: User {UserId} attempted to access project {ProjectId} without permission", userId, projectId);
                                await HandleAccessDenied(context);
                                return;
                            }
                        }
                    }
                    
                    // Check if this is a utility endpoint
                    bool isUtilityEndpoint = !string.IsNullOrEmpty(action) && 
                                           ApiUtilityEndpoints.TryGetValue(controller, out var utilityActions) &&
                                           utilityActions.Contains(action);

                    if (isUtilityEndpoint)
                    {
                        // For utility endpoints, check if user has access to ANY action in this controller
                        var userActionAccess = await authService.GetUserActionAccessAsync(userId);
                        bool hasAnyActionAccess = userActionAccess != null && 
                                                userActionAccess.TryGetValue(controller, out var actions) &&
                                                actions != null &&
                                                actions.Count > 0;

                        if (hasAnyActionAccess)
                        {
                            await _next(context);
                            return;
                        }
                    }
                    else
                    {
                        // Regular access check logic
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

                        if (hasAccess)
                        {
                            await _next(context);
                            return;
                        }
                    }

                    _logger.LogWarning("Access denied: User {UserId} attempted to access {Controller}/{Action} without permission", 
                        userId, controller, action ?? "default");
                    
                    await HandleAccessDenied(context);
                    return;
                }
            }
            else if (!string.IsNullOrEmpty(controller) && !string.IsNullOrEmpty(action))
            {
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