using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PureLedger.Services.Auth;
using PureLedger.ViewModels;
using LoginViewModel = PureLedger.ViewModels.Auth.LoginViewModel;
using LogoutViewModel = PureLedger.ViewModels.Auth.LogoutViewModel;
using RefreshTokenViewModel = PureLedger.ViewModels.Auth.RefreshTokenViewModel;
using RevokeTokenViewModel = PureLedger.ViewModels.Auth.RevokeTokenViewModel;

namespace PureLedger.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountApiController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AccountApiController> _logger;

        public AccountApiController(
            IAuthService authService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AccountApiController> logger)
        {
            _authService = authService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginViewModel model)
         {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid input", errors = ModelState });
            }

            var normalizedUsername = model.Username?.Trim().ToLowerInvariant();
            model.Username = normalizedUsername;

            var isValid = await _authService.ValidateUserAsync(model);
            if (!isValid)
            {
                return Unauthorized(new { success = false, message = "Invalid username or password" });
            }

            var user = await _authService.GetUserByUsernameAsync(model.Username);
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "User not found" });
            }

            // Generate JWT access token
            var accessToken = _authService.GenerateToken(
                user.UserId,
                user.Username,
                user.FullName,
                user.IsAdmin,
                user.Subroute,
                user.SecurityStamp
            );

            // Generate refresh token
            var refreshToken = _authService.GenerateRefreshToken();
            var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

            // Store refresh token in database
            await _authService.CreateRefreshTokenAsync(user.UserId, refreshToken, ipAddress);

            _logger.LogInformation($"User {model.Username} logged in via API");

            return Ok(new
            {
                success = true,
                token = accessToken,
                refreshToken = refreshToken,
                user = new
                {
                    userId = user.UserId,
                    username = user.Username,
                    fullName = user.FullName,
                    email = user.Email,
                    isAdmin = user.IsAdmin,
                    subroute = user.Subroute
                }
            });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] LogoutViewModel model)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { success = false, message = "Invalid token" });
            }

            var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

            // Revoke the specific refresh token if provided
            if (!string.IsNullOrEmpty(model?.RefreshToken))
            {
                await _authService.RevokeRefreshTokenAsync(model.RefreshToken, ipAddress);
            }
            else
            {
                // Revoke all user tokens if no specific token provided
                await _authService.RevokeAllUserTokensAsync(userId, ipAddress);
            }

            _logger.LogInformation($"User {userId} logged out via API");
            return Ok(new { success = true, message = "Logged out successfully" });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { success = false, message = "Invalid token" });
            }

            var user = await _authService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            return Ok(new
            {
                success = true,
                user = new
                {
                    userId = user.UserId,
                    username = user.Username,
                    fullName = user.FullName,
                    email = user.Email,
                    isAdmin = user.IsAdmin,
                    subroute = user.Subroute
                }
            });
        }

        [HttpGet("access")]
        [Authorize]
        public async Task<IActionResult> GetUserAccess() {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId)) {
                return Unauthorized(new { success = false, message = "Invalid token" });
            }

            var controllers = await _authService.GetUserControllerAccessAsync(userId);
            var actions = await _authService.GetUserActionAccessAsync(userId);

            return Ok(new {
                success = true,
                controllers,
                actions
            });
        }

        [HttpPost("refresh-token")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenViewModel model)
        {
            if (string.IsNullOrEmpty(model?.AccessToken) || string.IsNullOrEmpty(model?.RefreshToken))
            {
                return BadRequest(new { success = false, message = "Invalid token request" });
            }

            // Extract claims from expired access token
            var principal = _authService.GetPrincipalFromExpiredToken(model.AccessToken);
            if (principal == null)
            {
                return Unauthorized(new { success = false, message = "Invalid access token" });
            }

            var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { success = false, message = "Invalid token claims" });
            }

            // Validate refresh token
            var isValidRefreshToken = await _authService.ValidateRefreshTokenAsync(model.RefreshToken);
            if (!isValidRefreshToken)
            {
                return Unauthorized(new { success = false, message = "Invalid or expired refresh token" });
            }

            // Get user information
            var user = await _authService.GetUserByIdAsync(userId);
            if (user == null || !user.IsActive)
            {
                return Unauthorized(new { success = false, message = "User not found or inactive" });
            }

            // Generate new access token
            var newAccessToken = _authService.GenerateToken(
                user.UserId,
                user.Username,
                user.FullName,
                user.IsAdmin,
                user.Subroute,
                user.SecurityStamp
            );

            // Generate new refresh token
            var newRefreshToken = _authService.GenerateRefreshToken();
            var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

            // Revoke old refresh token and create new one
            await _authService.RevokeRefreshTokenAsync(model.RefreshToken, ipAddress);
            await _authService.CreateRefreshTokenAsync(user.UserId, newRefreshToken, ipAddress);

            _logger.LogInformation($"Token refreshed for user {user.Username}");

            return Ok(new
            {
                success = true,
                token = newAccessToken,
                refreshToken = newRefreshToken
            });
        }

        [HttpPost("revoke-token")]
        [Authorize]
        public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenViewModel model)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { success = false, message = "Invalid token" });
            }

            var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

            if (!string.IsNullOrEmpty(model?.RefreshToken))
            {
                var result = await _authService.RevokeRefreshTokenAsync(model.RefreshToken, ipAddress);
                if (!result)
                {
                    return BadRequest(new { success = false, message = "Token not found or already revoked" });
                }
            }
            else
            {
                await _authService.RevokeAllUserTokensAsync(userId, ipAddress);
            }

            return Ok(new { success = true, message = "Token revoked successfully" });
        }

        [HttpGet("navigation-menu")]
        [Authorize]
        public async Task<IActionResult> GetNavigationMenu()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { success = false, message = "Invalid token" });
            }

            var isAdmin = User.IsInRole("Admin");

            // Optimized: Run all async calls in parallel
            var controllerAccessTask = _authService.GetUserControllerAccessAsync(userId);
            var actionAccessTask = _authService.GetUserActionAccessAsync(userId);
            var accessibleProjectsTask = _authService.GetUserProjectsAsync(userId);

            await Task.WhenAll(controllerAccessTask, actionAccessTask, accessibleProjectsTask);

            var controllerAccess = await controllerAccessTask;
            var actionAccess = await actionAccessTask;
            var accessibleProjects = await accessibleProjectsTask;

            // Build navigation structure similar to NavigationMenuViewComponent
            var navigationMap = new List<object>();

            // Define controller structure with icons
            var controllerDefinitions = new Dictionary<string, (string title, string icon, string[] actions)>
            {
                ["Home"] = ("Home", "fas fa-home", new[] { "Entry", "VoucherEntry", "ModifiedEntries", "BaseVouchers", "Recipes" }),
                ["MetaData"] = ("Meta Data", "fas fa-database", new[] { "Projects", "Categories", "Accounts", "Locations", "Bookers" }),
                ["Reporting"] = ("Reporting", "fas fa-chart-bar", new[] { "SingleDoc", "AgingReport", "AccountReport", "MasterReport", "ProductReport", "MissedEntriesReport" }),
                ["DeliveryChallan"] = ("Delivery Challan", "fas fa-truck", new[] { "ProcessDC" })
            };

            foreach (var kvp in controllerDefinitions)
            {
                var controllerName = kvp.Key;
                var (title, icon, actions) = kvp.Value;
                var accessibleActions = new List<object>();

                // Check if user has access to this controller
                bool hasControllerAccess = isAdmin || controllerAccess.Contains(controllerName);
                
                if (hasControllerAccess)
                {
                    // User has full controller access, include all actions
                    foreach (var action in actions)
                    {
                        accessibleActions.Add(new
                        {
                            action = action,
                            title = System.Text.RegularExpressions.Regex.Replace(action, "([A-Z])", " $1").Trim(),
                            icon = GetActionIcon(controllerName, action)
                        });
                    }
                }
                else
                {
                    // Check action-level access
                    if (actionAccess.TryGetValue(controllerName, out var userActions))
                    {
                        foreach (var action in actions)
                        {
                            if (userActions.Contains(action))
                            {
                                accessibleActions.Add(new
                                {
                                    action = action,
                                    title = System.Text.RegularExpressions.Regex.Replace(action, "([A-Z])", " $1").Trim(),
                                    icon = GetActionIcon(controllerName, action)
                                });
                            }
                        }
                    }
                }

                // Only add controller to navigation if user has access to at least one action
                if (accessibleActions.Count > 0)
                {
                    navigationMap.Add(new
                    {
                        controller = controllerName,
                        title = title,
                        icon = icon,
                        actions = accessibleActions
                    });
                }
            }

            return Ok(new
            {
                success = true,
                navigation = navigationMap,
                isAdmin,
                accessibleProjects = accessibleProjects.Select(p => p.ProjectId).ToList()
            });
        }

        private string GetActionIcon(string controller, string action)
        {
            var iconMap = new Dictionary<string, Dictionary<string, string>>
            {
                ["Home"] = new Dictionary<string, string>
                {
                    ["Entry"] = "fas fa-plus-circle",
                    ["VoucherEntry"] = "fas fa-file-invoice",
                    ["ModifiedEntries"] = "fas fa-history",
                    ["BaseVouchers"] = "fas fa-layer-group",
                    ["Recipes"] = "fas fa-book-open"
                },
                ["MetaData"] = new Dictionary<string, string>
                {
                    ["Projects"] = "fas fa-project-diagram",
                    ["Categories"] = "fas fa-tags",
                    ["Accounts"] = "fas fa-address-book",
                    ["Locations"] = "fas fa-map-marker-alt",
                    ["Bookers"] = "fas fa-user-tie"
                },
                ["Reporting"] = new Dictionary<string, string>
                {
                    ["SingleDoc"] = "fas fa-file-alt",
                    ["AgingReport"] = "fas fa-clock",
                    ["AccountReport"] = "fas fa-chart-pie",
                    ["MasterReport"] = "fas fa-file-alt",
                    ["ProductReport"] = "fas fa-box",
                    ["MissedEntriesReport"] = "fas fa-exclamation-circle"
                },
                ["DeliveryChallan"] = new Dictionary<string, string>
                {
                    ["ProcessDC"] = "fas fa-truck-loading"
                }
            };

            return iconMap.TryGetValue(controller, out var actions) && actions.TryGetValue(action, out var icon)
                ? icon
                : "fas fa-circle";
        }
    }
}

