using System.ComponentModel.DataAnnotations;

namespace PureLedger.ViewModels
{
    public class UserViewModel
    {
        public int UserId { get; set; }

        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        public string Username { get; set; } = string.Empty;

        // Remove MinimumLength and make it completely optional with no validation when empty
        [StringLength(100, ErrorMessage = "Password must not exceed 100 characters")]
        [DataType(DataType.Password)]
        // This marks the field as not required
        [RequiredIf("IsNew", true, ErrorMessage = "Password is required for new users")]
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

        // Property to indicate if this is a new user creation (not an edit)
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsNew => UserId == 0;

        public List<int> ProjectIds { get; set; } = new List<int>();
        
        // Categories to hide from this user (blacklist approach)
        public List<int> HiddenCategoryIds { get; set; } = new List<int>();
        
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

    // Custom validation attribute to conditionally make a field required
    public class RequiredIfAttribute : ValidationAttribute
    {
        private readonly string _propertyName;
        private readonly object _compareValue;

        public RequiredIfAttribute(string propertyName, object compareValue, string errorMessage = "")
        {
            _propertyName = propertyName;
            _compareValue = compareValue;
            ErrorMessage = errorMessage;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            // Get the property to compare with
            var property = validationContext.ObjectType.GetProperty(_propertyName);
            if (property == null)
                return ValidationResult.Success;

            // Get the property value to compare
            var propertyValue = property.GetValue(validationContext.ObjectInstance, null);
            
            // If the comparison property matches the expected value
            if (Equals(propertyValue, _compareValue))
            {
                // Validate that the current value is not empty
                if (value == null || (value is string stringValue && string.IsNullOrWhiteSpace(stringValue)))
                    return new ValidationResult(ErrorMessage);
            }

            return ValidationResult.Success;
        }
    }
}