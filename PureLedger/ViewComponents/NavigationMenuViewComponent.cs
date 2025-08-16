using Microsoft.AspNetCore.Mvc;
using PureLedger.Services;
using System.Security.Claims;

namespace PureLedger.ViewComponents
{
    public class NavigationMenuViewComponent : ViewComponent
    {
        private readonly IAuthService _authService;

        public NavigationMenuViewComponent(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var userId = 0;
            var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out userId))
            {
                // Get user's controller access
                var controllerAccess = await _authService.GetUserControllerAccessAsync(userId);
                
                // Create a view model for the navigation menu
                var model = new NavigationViewModel
                {
                    IsAdmin = HttpContext.User.IsInRole("Admin"),
                    AccessibleControllers = controllerAccess
                };
                
                return View(model);
            }
            
            // If user is not authenticated, return a model with no access
            return View(new NavigationViewModel { IsAdmin = false, AccessibleControllers = new List<string>() });
        }
    }

    public class NavigationViewModel
    {
        public bool IsAdmin { get; set; }
        public List<string> AccessibleControllers { get; set; } = new List<string>();
    }
}