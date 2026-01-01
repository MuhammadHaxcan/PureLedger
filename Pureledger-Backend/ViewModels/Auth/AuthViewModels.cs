using System.ComponentModel.DataAnnotations;

namespace PureLedger.ViewModels.Auth
{
    /// <summary>
    /// View model for user login requests
    /// </summary>
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username is required")]
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
