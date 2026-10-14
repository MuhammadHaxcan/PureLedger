using System.ComponentModel.DataAnnotations;

namespace PureLedger.ViewModels.Auth
{
    /// <summary>
    /// View model for user login requests
    /// </summary>
    public class LoginViewModel
    {
        // Keep the JSON property name for existing clients; accepts a username or an email address.
        [Required(ErrorMessage = "Username or email is required")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    /// <summary>
    /// View model for user logout requests
    /// </summary>
    public class LogoutViewModel
    {
        public string? RefreshToken { get; set; }
    }

    /// <summary>
    /// View model for refresh token requests.
    /// Note: Tokens are primarily sent via HttpOnly cookies.
    /// Body fields are optional for backward compatibility.
    /// </summary>
    public class RefreshTokenViewModel
    {
        public string? AccessToken { get; set; }

        public string? RefreshToken { get; set; }
    }

    /// <summary>
    /// View model for token revocation requests
    /// </summary>
    public class RevokeTokenViewModel
    {
        public string? RefreshToken { get; set; }
    }
}
