using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using PureLedger.Services;
using PureLedger.ViewModels;
using System.Security.Claims;

namespace PureLedger.Controllers
{
    [AllowAnonymous] // Apply to the entire controller
    public class AccountController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            IAuthService authService,
            ILogger<AccountController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = "/")
        {
            // Check if user is already logged in
            if (User.Identity?.IsAuthenticated == true)
            {
                // Make sure returnUrl doesn't cause a loop
                if (string.IsNullOrEmpty(returnUrl) || returnUrl.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase))
                {
                    returnUrl = "/Home/Entry";
                }
                return Redirect(returnUrl);
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = "/")
        {
            // Prevent infinite loop on unsuccessful login attempts
            if (string.IsNullOrEmpty(returnUrl) || returnUrl.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase))
            {
                returnUrl = "/Home/Entry";
            }
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var isValid = await _authService.ValidateUserAsync(model);
            if (!isValid)
            {
                ModelState.AddModelError("", "Invalid username or password");
                return View(model);
            }

            var user = await _authService.GetUserByUsernameAsync(model.Username);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found");
                return View(model);
            }

            // Create claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.GivenName, user.FullName),
                new Claim("Subroute", user.Subroute)
            };

            // Add role claim
            if (user.IsAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            }
            else
            {
                claims.Add(new Claim(ClaimTypes.Role, "User"));
            }

            // Create identity and principal
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            // Set authentication properties
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
            };

            // Sign in user
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProperties);

            _logger.LogInformation($"User {model.Username} logged in");

            // Redirect to returnUrl or default
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Entry", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}