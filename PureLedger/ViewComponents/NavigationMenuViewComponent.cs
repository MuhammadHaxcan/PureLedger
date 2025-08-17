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
                // Get user's controller and action access
                var controllerAccess = await _authService.GetUserControllerAccessAsync(userId);
                var actionAccess = await _authService.GetUserActionAccessAsync(userId);
        
                // Get projects the user has access to
                var accessibleProjects = await _authService.GetUserProjectsAsync(userId);
        
                // Create a view model for the navigation menu
                var model = new NavigationViewModel
                {
                    IsAdmin = HttpContext.User.IsInRole("Admin"),
                    AccessibleControllers = controllerAccess,
                    AccessibleActions = actionAccess,
                    AccessibleProjects = accessibleProjects.Select(p => p.ProjectId).ToList()
                };
        
                return View(model);
            }
    
            // If user is not authenticated, return a model with no access
            return View(new NavigationViewModel 
            { 
                IsAdmin = false, 
                AccessibleControllers = new List<string>(),
                AccessibleActions = new Dictionary<string, List<string>>(),
                AccessibleProjects = new List<int>()
            });
        }
    }

    public class NavigationViewModel
    {
        public bool IsAdmin { get; set; }
        public List<string> AccessibleControllers { get; set; } = new List<string>();
        public Dictionary<string, List<string>> AccessibleActions { get; set; } = new Dictionary<string, List<string>>();
        public List<int> AccessibleProjects { get; set; } = new List<int>();

        // Helper method to check if user has access to a specific action
        public bool CanAccessAction(string controller, string action)
        {
            // Admin always has access
            if (IsAdmin)
                return true;
                
            //// Check controller-level access first
            //if (AccessibleControllers.Contains(controller))
            //    return true;
                
            // Check action-specific access
            if (AccessibleActions.TryGetValue(controller, out var actions))
            {
                return actions.Contains(action);
            }
                
            return false;
        }
        
        // Helper method to check if user has access to a specific project
        public bool CanAccessProject(int projectId)
        {
            return IsAdmin || AccessibleProjects.Contains(projectId);
        }
    }
}