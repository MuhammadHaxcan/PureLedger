using System.ComponentModel.DataAnnotations;

namespace PureLedger.ViewModels
{
    public class UserViewModel
    {
        public int UserId { get; set; }

        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        public string Username { get; set; } = string.Empty;

        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Compare("Password", ErrorMessage = "Passwords do not match")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full name is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 50 characters")]
        public string FullName { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(100)]
        public string? Email { get; set; }

        public bool IsAdmin { get; set; }

        public bool IsActive { get; set; } = true;

        public List<int> ProjectIds { get; set; } = new List<int>();
        
        // Simplified access control - store controller-action pairs together
        // Format: "Controller" for controller-level access, "Controller|Action" for action-level access
        public List<string> AccessPermissions { get; set; } = new List<string>();
        
        // Helper methods for access checking
        public bool HasControllerAccess(string controller)
        {
            return AccessPermissions.Contains(controller);
        }
        
        public bool HasActionAccess(string controller, string action)
        {
            return AccessPermissions.Contains($"{controller}|{action}");
        }
        
        public bool HasAnyAccess(string controller, string? action = null)
        {
            if (string.IsNullOrEmpty(action))
                return HasControllerAccess(controller);
            
            return HasActionAccess(controller, action) || HasControllerAccess(controller);
        }
        
        // Get all controllers that have access (either controller-level or action-level)
        public List<string> GetAccessibleControllers()
        {
            var controllers = new HashSet<string>();
            
            foreach (var permission in AccessPermissions)
            {
                var parts = permission.Split('|');
                controllers.Add(parts[0]);
            }
            
            return controllers.ToList();
        }
        
        // Get actions for a specific controller
        public List<string> GetControllerActions(string controller)
        {
            return AccessPermissions
                .Where(p => p.StartsWith($"{controller}|"))
                .Select(p => p.Split('|')[1])
                .ToList();
        }
    }
}