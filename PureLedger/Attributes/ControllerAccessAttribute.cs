using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PureLedger.Services;
using System.Security.Claims;

namespace PureLedger.Attributes
{
    public class ControllerAccessAttribute : ActionFilterAttribute
    {
        // Define a list of "utility" API endpoints that should be accessible 
        // when a user has access to any part of the controller
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

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Skip check for Account controller and admin users
            if (context.Controller.GetType().Name == "AccountController" ||
                context.HttpContext.User.IsInRole("Admin"))
            {
                await next();
                return;
            }

            var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
            {
                var controllerName = context.RouteData.Values["controller"]?.ToString();
                var actionName = context.RouteData.Values["action"]?.ToString();

                if (!string.IsNullOrEmpty(controllerName) && !string.IsNullOrEmpty(actionName))
                {
                    // Get the AuthService from DI
                    var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();

                    // Check if this is a utility API endpoint - FIX: Added null check for utilityActions
                    bool isUtilityEndpoint = ApiUtilityEndpoints.TryGetValue(controllerName, out var utilityActions) &&
                                            utilityActions != null &&
                                            utilityActions.Contains(actionName);

                    // Check project access if projectId is in route data or action parameters
                    if (context.RouteData.Values.TryGetValue("projectId", out var projectIdObj) ||
                        context.ActionArguments.TryGetValue("projectId", out projectIdObj))
                    {
                        if (projectIdObj != null && int.TryParse(projectIdObj.ToString(), out int projectId))
                        {
                            bool hasProjectAccess = await authService.HasProjectAccessAsync(userId, projectId);
                            if (!hasProjectAccess)
                            {
                                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                                return;
                            }
                        }
                    }

                    // Check for controller-level access first
                    bool hasControllerAccess = await authService.HasControllerAccessAsync(userId, controllerName);
                    if (hasControllerAccess)
                    {
                        await next();
                        return;
                    }

                    // If no controller-level access, check action-specific access
                    bool hasActionAccess = await authService.HasActionAccessAsync(userId, controllerName, actionName);
                    if (hasActionAccess)
                    {
                        await next();
                        return;
                    }

                    // Special case for utility endpoints: Check if user has access to ANY action in this controller
                    // OR if the request is coming from a controller they have access to
                    if (isUtilityEndpoint)
                    {
                        // Check if user has access to any action in current controller
                        bool hasAnyActionInController = await HasAccessToAnyActionInControllerAsync(authService, userId, controllerName);
                        
                        if (hasAnyActionInController)
                        {
                            await next();
                            return;
                        }
                    }
                }
            }

            // Check if this is an API request
            bool isApiRequest = context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                               context.HttpContext.Request.Path.ToString().Contains("/api/", StringComparison.OrdinalIgnoreCase);

            if (isApiRequest)
            {
                // For API requests, return 403 Forbidden status
                context.Result = new StatusCodeResult(403);
            }
            else
            {
                // For regular requests, redirect to access denied
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
            }
        }

        private async Task<bool> HasAccessToAnyActionInControllerAsync(IAuthService authService, int userId, string controllerName)
        {
            try
            {
                // Get all user action permissions
                var userActionAccess = await authService.GetUserActionAccessAsync(userId);

                // Check if the user has access to any action in the specified controller
                if (userActionAccess != null && userActionAccess.TryGetValue(controllerName, out var actions))
                {
                    return actions != null && actions.Count > 0;
                }

                return false;
            }
            catch (Exception)
            {
                // In case of any exception, fail safely by denying access
                return false;
            }
        }
    }
}