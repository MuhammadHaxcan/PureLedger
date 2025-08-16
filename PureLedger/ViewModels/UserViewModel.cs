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
        
        public List<string> ControllerAccess { get; set; } = new List<string>();
    }
}