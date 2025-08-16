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
            // Skip check for admin users
            if (context.HttpContext.User.IsInRole("Admin"))
            {
                await next();
                return;
            }

            var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
            {
                var controllerName = context.RouteData.Values["controller"]?.ToString();
                
                if (!string.IsNullOrEmpty(controllerName))
                {
                    // Get the AuthService from DI
                    var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
                    
                    // Check if user has access to this controller
                    bool hasAccess = await authService.HasControllerAccessAsync(userId, controllerName);
                    
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