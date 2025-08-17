using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PureLedger.Services;
using System.Security.Claims;

namespace PureLedger.Attributes
{
    public class ControllerAccessAttribute : ActionFilterAttribute
    {
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
                    
                    // Strict action-specific access check
                    bool hasAccess = await authService.HasActionAccessAsync(userId, controllerName, actionName);
                    if (hasAccess)
                    {
                        await next();
                        return;
                    }
                }
            }
            
            // User doesn't have access - redirect to access denied
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
        }
    }
}