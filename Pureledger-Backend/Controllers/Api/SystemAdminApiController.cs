using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PureLedger.Services.Auth;
using PureLedger.Filters;
using System.Security.Claims;

namespace PureLedger.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class SystemAdminApiController : ControllerBase
    {
        private readonly ISystemAdminAuthService _authService;
        private readonly ILogger<SystemAdminApiController> _logger;
        private readonly IConfiguration _configuration;

        // Cookie names from configuration
        private string AccessTokenCookieName => _configuration["Security:CookieSettings:AccessTokenCookieName"] ?? "accessToken";
        private string RefreshTokenCookieName => _configuration["Security:CookieSettings:RefreshTokenCookieName"] ?? "refreshToken";

        public SystemAdminApiController(
            ISystemAdminAuthService authService,
            ILogger<SystemAdminApiController> logger,
            IConfiguration configuration)
        {
            _authService = authService;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Sets authentication cookies (HttpOnly for security)
        /// </summary>
        private void SetAuthCookies(string accessToken)
        {
            var accessTokenExpiryMinutes = int.Parse(_configuration["Security:CookieSettings:AccessTokenExpiryMinutes"] ?? "30");
            var isSecure = HttpContext.Request.IsHttps;

            // Access token cookie - HttpOnly to prevent XSS
            Response.Cookies.Append(AccessTokenCookieName, accessToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = isSecure,
                SameSite = SameSiteMode.Lax,
                Path = "/api",
                Expires = DateTimeOffset.UtcNow.AddMinutes(accessTokenExpiryMinutes),
                IsEssential = true
            });
        }

        /// <summary>
        /// Clears all authentication cookies on logout
        /// </summary>
        private void ClearAuthCookies()
        {
            var isSecure = HttpContext.Request.IsHttps;

            Response.Cookies.Delete(AccessTokenCookieName, new CookieOptions
            {
                HttpOnly = true,
                Secure = isSecure,
                SameSite = SameSiteMode.Lax,
                Path = "/api"
            });

            Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
            {
                HttpOnly = true,
                Secure = isSecure,
                SameSite = SameSiteMode.Lax,
                Path = "/api"
            });

            Response.Cookies.Delete("XSRF-TOKEN", new CookieOptions
            {
                HttpOnly = false,
                Secure = isSecure,
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });
        }

        /// <summary>
        /// TEMPORARY: Reset sysadmin password - REMOVE IN PRODUCTION
        /// </summary>
        [HttpPost("reset-sysadmin-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetSysadminPassword([FromBody] ResetPasswordRequest model)
        {
            if (string.IsNullOrEmpty(model?.NewPassword))
            {
                return BadRequest(new { success = false, message = "New password is required" });
            }

            var admin = await _authService.GetByUsernameAsync("sysadmin");
            if (admin == null)
            {
                return NotFound(new { success = false, message = "Sysadmin not found" });
            }

            // Generate new hash
            var newHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);

            // Update via direct SQL since we don't have an update method
            using var scope = HttpContext.RequestServices.CreateScope();
            var masterDb = scope.ServiceProvider.GetRequiredService<PureLedger.Data.MasterDbContext>();

            admin.PasswordHash = newHash;
            masterDb.Update(admin);
            await masterDb.SaveChangesAsync();

            _logger.LogWarning("Sysadmin password was reset via temporary endpoint");

            return Ok(new { success = true, message = "Password reset successfully. New hash: " + newHash.Substring(0, 30) + "..." });
        }

        /// <summary>
        /// System admin login endpoint.
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] SystemAdminLoginRequest model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid input" });
            }

            var isValid = await _authService.ValidateCredentialsAsync(model.Username, model.Password);
            if (!isValid)
            {
                _logger.LogWarning("Failed system admin login attempt for user: {Username}", model.Username);
                return Unauthorized(new { success = false, message = "Invalid username or password" });
            }

            var admin = await _authService.GetByUsernameAsync(model.Username);
            if (admin == null)
            {
                return Unauthorized(new { success = false, message = "Admin not found" });
            }

            // Update last login
            await _authService.UpdateLastLoginAsync(admin.Id);

            // Generate JWT token
            var accessToken = _authService.GenerateToken(admin);

            // Set token in HttpOnly cookie
            SetAuthCookies(accessToken);

            // Generate XSRF token
            var xsrfToken = ValidateXsrfTokenAttribute.GenerateToken();
            ValidateXsrfTokenAttribute.SetXsrfCookie(HttpContext, xsrfToken);

            _logger.LogInformation("System admin {Username} logged in", model.Username);

            return Ok(new
            {
                success = true,
                xsrfToken = xsrfToken,
                admin = new
                {
                    id = admin.Id,
                    username = admin.Username,
                    fullName = admin.FullName,
                    email = admin.Email
                }
            });
        }

        /// <summary>
        /// System admin logout endpoint.
        /// </summary>
        [HttpPost("logout")]
        [Authorize(Roles = "SystemAdmin")]
        public IActionResult Logout()
        {
            var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            ClearAuthCookies();

            _logger.LogInformation("System admin {AdminId} logged out", adminId);

            return Ok(new { success = true, message = "Logged out successfully" });
        }

        /// <summary>
        /// Get current system admin info.
        /// </summary>
        [HttpGet("me")]
        [Authorize(Roles = "SystemAdmin")]
        public async Task<IActionResult> GetCurrentAdmin()
        {
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (adminIdClaim == null || !int.TryParse(adminIdClaim.Value, out int adminId))
            {
                return Unauthorized(new { success = false, message = "Invalid token" });
            }

            var admin = await _authService.GetByIdAsync(adminId);
            if (admin == null)
            {
                return NotFound(new { success = false, message = "Admin not found" });
            }

            return Ok(new
            {
                success = true,
                admin = new
                {
                    id = admin.Id,
                    username = admin.Username,
                    fullName = admin.FullName,
                    email = admin.Email,
                    isActive = admin.IsActive,
                    lastLoginAt = admin.LastLoginAt
                }
            });
        }
    }

    /// <summary>
    /// Request model for system admin login.
    /// </summary>
    public class SystemAdminLoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request model for password reset.
    /// </summary>
    public class ResetPasswordRequest
    {
        public string NewPassword { get; set; } = string.Empty;
    }
}
