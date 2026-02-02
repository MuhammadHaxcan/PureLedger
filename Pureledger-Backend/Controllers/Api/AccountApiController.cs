using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PureLedger.Services.Auth;
using PureLedger.ViewModels;
using PureLedger.Filters;
using LoginViewModel = PureLedger.ViewModels.Auth.LoginViewModel;
using LogoutViewModel = PureLedger.ViewModels.Auth.LogoutViewModel;
using RefreshTokenViewModel = PureLedger.ViewModels.Auth.RefreshTokenViewModel;
using RevokeTokenViewModel = PureLedger.ViewModels.Auth.RevokeTokenViewModel;

namespace PureLedger.Controllers.Api {
    [ApiController]
    [Route("api/[controller]")]
    public class AccountApiController : ControllerBase {
        private readonly IAuthService _authService;
        private readonly ILogger<AccountApiController> _logger;
        private readonly IConfiguration _configuration;

        // Cookie names from configuration
        private string AccessTokenCookieName => _configuration["Security:CookieSettings:AccessTokenCookieName"] ?? "accessToken";
        private string RefreshTokenCookieName => _configuration["Security:CookieSettings:RefreshTokenCookieName"] ?? "refreshToken";

        public AccountApiController(
            IAuthService authService,
            ILogger<AccountApiController> logger,
            IConfiguration configuration) {
            _authService = authService;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Sets authentication cookies (HttpOnly for security)
        /// </summary>
        private void SetAuthCookies(string accessToken, string refreshToken) {
            var accessTokenExpiryMinutes = int.Parse(_configuration["Security:CookieSettings:AccessTokenExpiryMinutes"] ?? "30");
            var refreshTokenExpiryDays = int.Parse(_configuration["Security:CookieSettings:RefreshTokenExpiryDays"] ?? "7");

            // Detect if request is HTTPS (via X-Forwarded-Proto from nginx)
            var isSecure = HttpContext.Request.IsHttps;

            // Access token cookie - HttpOnly to prevent XSS
            Response.Cookies.Append(AccessTokenCookieName, accessToken, new CookieOptions {
                HttpOnly = true,
                Secure = isSecure, // True for HTTPS, false for HTTP (development)
                SameSite = SameSiteMode.Lax,
                Path = "/api",
                Expires = DateTimeOffset.UtcNow.AddMinutes(accessTokenExpiryMinutes),
                IsEssential = true
            });

            // Refresh token cookie - HttpOnly to prevent XSS
            Response.Cookies.Append(RefreshTokenCookieName, refreshToken, new CookieOptions {
                HttpOnly = true,
                Secure = isSecure, // True for HTTPS, false for HTTP (development)
                SameSite = SameSiteMode.Lax,
                Path = "/api",
                Expires = DateTimeOffset.UtcNow.AddDays(refreshTokenExpiryDays),
                IsEssential = true
            });
        }

        /// <summary>
        /// Clears all authentication and XSRF cookies on logout
        /// </summary>
        private void ClearAuthCookies() {
            // Detect if request is HTTPS (via X-Forwarded-Proto from nginx)
            var isSecure = HttpContext.Request.IsHttps;

            // Clear access token cookie
            Response.Cookies.Delete(AccessTokenCookieName, new CookieOptions {
                HttpOnly = true,
                Secure = isSecure,
                SameSite = SameSiteMode.Lax,
                Path = "/api"
            });

            // Clear refresh token cookie
            Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions {
                HttpOnly = true,
                Secure = isSecure,
                SameSite = SameSiteMode.Lax,
                Path = "/api"
            });

            // Clear XSRF token cookie
            Response.Cookies.Delete("XSRF-TOKEN", new CookieOptions {
                HttpOnly = false,
                Secure = isSecure,
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginViewModel model) {
            if (!ModelState.IsValid) {
                return BadRequest(new { success = false, message = "Invalid input", errors = ModelState });
            }

            var normalizedUsername = model.Username?.Trim().ToLowerInvariant();
            model.Username = normalizedUsername;

            // Check if this is a tenant login (format: slug|username)
            if (model.Username?.Contains('|') == true) {
                return await TenantLogin(model.Username, model.Password);
            }

            // Legacy login (non-tenant)
            var isValid = await _authService.ValidateUserAsync(model);
            if (!isValid) {
                return Unauthorized(new { success = false, message = "Invalid username or password" });
            }

            var user = await _authService.GetUserByUsernameAsync(model.Username);
            if (user == null) {
                return Unauthorized(new { success = false, message = "User not found" });
            }

            // Generate JWT access token
            var accessToken = _authService.GenerateToken(
                user.UserId,
                user.Username,
                user.FullName,
                user.IsAdmin,
                user.Subroute
            );

            // Generate refresh token
            var refreshToken = _authService.GenerateRefreshToken();

            // Store refresh token in database
            await _authService.CreateRefreshTokenAsync(user.UserId, refreshToken);

            // Set tokens in HttpOnly cookies (XSS-safe)
            SetAuthCookies(accessToken, refreshToken);

            // Generate simple XSRF token for CSRF protection
            var xsrfToken = ValidateXsrfTokenAttribute.GenerateToken();
            ValidateXsrfTokenAttribute.SetXsrfCookie(HttpContext, xsrfToken);

            _logger.LogInformation($"User {model.Username} logged in via API");

            // Return user info without tokens (tokens are in HttpOnly cookies)
            return Ok(new {
                success = true,
                xsrfToken = xsrfToken, // Frontend stores this for CSRF protection
                user = new {
                    userId = user.UserId,
                    username = user.Username,
                    fullName = user.FullName,
                    email = user.Email,
                    isAdmin = user.IsAdmin,
                    subroute = user.Subroute
                }
            });
        }

        /// <summary>
        /// Handle tenant user login with format "slug|username"
        /// </summary>
        private async Task<IActionResult> TenantLogin(string fullUsername, string password) {
            var result = await _authService.ValidateTenantUserAsync(fullUsername, password);

            if (!result.Success) {
                return Unauthorized(new { success = false, message = result.ErrorMessage });
            }

            var user = result.User!;

            // Generate JWT access token with tenant claims
            var accessToken = _authService.GenerateTenantToken(
                user.UserId,
                user.Username,
                user.FullName,
                user.IsAdmin,
                user.Subroute,
                result.SchemaName,
                result.TenantName
            );

            // Generate refresh token
            var refreshToken = _authService.GenerateRefreshToken();

            // Store refresh token in database
            await _authService.CreateRefreshTokenAsync(user.UserId, refreshToken);

            // Set tokens in HttpOnly cookies (XSS-safe)
            SetAuthCookies(accessToken, refreshToken);

            // Generate simple XSRF token for CSRF protection
            var xsrfToken = ValidateXsrfTokenAttribute.GenerateToken();
            ValidateXsrfTokenAttribute.SetXsrfCookie(HttpContext, xsrfToken);

            _logger.LogInformation("Tenant user {FullUsername} logged in", fullUsername);

            // Return user info with tenant details
            return Ok(new {
                success = true,
                xsrfToken = xsrfToken,
                user = new {
                    userId = user.UserId,
                    username = user.Username,
                    fullName = user.FullName,
                    email = user.Email,
                    isAdmin = user.IsAdmin,
                    subroute = user.Subroute
                },
                tenant = new {
                    schemaName = result.SchemaName,
                    tenantName = result.TenantName,
                    tenantId = result.TenantId
                }
            });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] LogoutViewModel? model = null) {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId)) {
                return Unauthorized(new { success = false, message = "Invalid token" });
            }

            // Try to get refresh token from cookie first, then from body
            var refreshToken = Request.Cookies[RefreshTokenCookieName] ?? model?.RefreshToken;

            // Revoke the specific refresh token if provided
            if (!string.IsNullOrEmpty(refreshToken)) {
                await _authService.RevokeRefreshTokenAsync(refreshToken);
            } else {
                // Revoke all user tokens if no specific token provided
                await _authService.RevokeAllUserTokensAsync(userId);
            }

            // Clear authentication cookies
            ClearAuthCookies();

            _logger.LogInformation($"User {userId} logged out via API");
            return Ok(new { success = true, message = "Logged out successfully" });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser() {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId)) {
                return Unauthorized(new { success = false, message = "Invalid token" });
            }

            var user = await _authService.GetUserByIdAsync(userId);
            if (user == null) {
                return NotFound(new { success = false, message = "User not found" });
            }

            // Extract tenant claims from JWT
            var tenantSchema = User.FindFirst("tenant_schema")?.Value;
            var tenantName = User.FindFirst("tenant_name")?.Value;
            var userType = User.FindFirst("user_type")?.Value ?? "TenantUser";

            var response = new {
                success = true,
                user = new {
                    userId = user.UserId,
                    username = user.Username,
                    fullName = user.FullName,
                    email = user.Email,
                    isAdmin = user.IsAdmin,
                    subroute = user.Subroute
                },
                tenant = !string.IsNullOrEmpty(tenantSchema) ? new {
                    schemaName = tenantSchema,
                    tenantName = tenantName
                } : null,
                userType = userType
            };

            return Ok(response);
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
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenViewModel? model = null) {
            // Get refresh token from cookie first, then from body (for backward compatibility)
            var refreshToken = Request.Cookies[RefreshTokenCookieName] ?? model?.RefreshToken;

            if (string.IsNullOrEmpty(refreshToken)) {
                return Unauthorized(new { success = false, message = "Missing refresh token" });
            }

            // Validate refresh token and get associated user
            var refreshTokenEntity = await _authService.GetRefreshTokenAsync(refreshToken);
            if (refreshTokenEntity == null || refreshTokenEntity.IsRevoked || refreshTokenEntity.ExpiryDate < DateTime.UtcNow) {
                // Clear invalid cookies
                ClearAuthCookies();
                return Unauthorized(new { success = false, message = "Invalid or expired refresh token" });
            }

            // Get user information from the refresh token
            var user = await _authService.GetUserByIdAsync(refreshTokenEntity.UserId);
            if (user == null || !user.IsActive) {
                ClearAuthCookies();
                return Unauthorized(new { success = false, message = "User not found or inactive" });
            }

            // Extract tenant claims from the expired access token (if present)
            string? tenantSchema = null;
            string? tenantName = null;
            var expiredAccessToken = Request.Cookies[AccessTokenCookieName];
            if (!string.IsNullOrEmpty(expiredAccessToken)) {
                var principal = _authService.GetPrincipalFromExpiredToken(expiredAccessToken);
                if (principal != null) {
                    tenantSchema = principal.FindFirst("tenant_schema")?.Value;
                    tenantName = principal.FindFirst("tenant_name")?.Value;
                }
            }

            // Generate new access token with tenant claims preserved
            var newAccessToken = _authService.GenerateTenantToken(
                user.UserId,
                user.Username,
                user.FullName,
                user.IsAdmin,
                user.Subroute,
                tenantSchema,
                tenantName
            );

            // Generate new refresh token
            var newRefreshToken = _authService.GenerateRefreshToken();

            // Revoke old refresh token and create new one
            await _authService.RevokeRefreshTokenAsync(refreshToken);
            await _authService.CreateRefreshTokenAsync(user.UserId, newRefreshToken);

            // Set new tokens in HttpOnly cookies
            SetAuthCookies(newAccessToken, newRefreshToken);

            // Generate new simple XSRF token
            var xsrfToken = ValidateXsrfTokenAttribute.GenerateToken();
            ValidateXsrfTokenAttribute.SetXsrfCookie(HttpContext, xsrfToken);

            _logger.LogInformation($"Token refreshed for user {user.Username}");

            return Ok(new {
                success = true,
                xsrfToken = xsrfToken // Return new XSRF token
            });
        }

        [HttpPost("revoke-token")]
        [Authorize]
        public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenViewModel model) {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId)) {
                return Unauthorized(new { success = false, message = "Invalid token" });
            }

            if (!string.IsNullOrEmpty(model?.RefreshToken)) {
                var result = await _authService.RevokeRefreshTokenAsync(model.RefreshToken);
                if (!result) {
                    return BadRequest(new { success = false, message = "Token not found or already revoked" });
                }
            } else {
                await _authService.RevokeAllUserTokensAsync(userId);
            }

            return Ok(new { success = true, message = "Token revoked successfully" });
        }

        [HttpGet("navigation-menu")]
        [Authorize]
        public async Task<IActionResult> GetNavigationMenu() {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId)) {
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
            var controllerDefinitions = new Dictionary<string, (string title, string icon, string[] actions)> {
                ["Home"] = ("Home", "fas fa-home", new[] { "Entry", "VoucherEntry", "ModifiedEntries", "BaseVouchers", "Recipes" }),
                ["MetaData"] = ("Meta Data", "fas fa-database", new[] { "Projects", "Categories", "Accounts", "Locations", "Bookers" }),
                ["Reporting"] = ("Reporting", "fas fa-chart-bar", new[] { "SingleDoc", "AgingReport", "AccountReport", "MasterReport", "ProductReport", "MissedEntriesReport" }),
                ["DeliveryChallan"] = ("Delivery Challan", "fas fa-truck", new[] { "ProcessDC" })
            };

            foreach (var kvp in controllerDefinitions) {
                var controllerName = kvp.Key;
                var (title, icon, actions) = kvp.Value;
                var accessibleActions = new List<object>();

                // Check if user has access to this controller
                bool hasControllerAccess = isAdmin || controllerAccess.Contains(controllerName);

                if (hasControllerAccess) {
                    // User has full controller access, include all actions
                    foreach (var action in actions) {
                        accessibleActions.Add(new {
                            action = action,
                            title = System.Text.RegularExpressions.Regex.Replace(action, "([A-Z])", " $1").Trim(),
                            icon = GetActionIcon(controllerName, action)
                        });
                    }
                } else {
                    // Check action-level access
                    if (actionAccess.TryGetValue(controllerName, out var userActions)) {
                        foreach (var action in actions) {
                            if (userActions.Contains(action)) {
                                accessibleActions.Add(new {
                                    action = action,
                                    title = System.Text.RegularExpressions.Regex.Replace(action, "([A-Z])", " $1").Trim(),
                                    icon = GetActionIcon(controllerName, action)
                                });
                            }
                        }
                    }
                }

                // Only add controller to navigation if user has access to at least one action
                if (accessibleActions.Count > 0) {
                    navigationMap.Add(new {
                        controller = controllerName,
                        title = title,
                        icon = icon,
                        actions = accessibleActions
                    });
                }
            }

            return Ok(new {
                success = true,
                navigation = navigationMap,
                isAdmin,
                accessibleProjects = accessibleProjects.Select(p => p.ProjectId).ToList()
            });
        }

        /// <summary>
        /// Get XSRF token for CSRF protection.
        /// Call this endpoint to get a fresh XSRF token when needed.
        /// </summary>
        [HttpGet("xsrf-token")]
        [Authorize]
        public IActionResult GetXsrfToken() {
            // Check if there's already a valid XSRF cookie
            var existingToken = Request.Cookies["XSRF-TOKEN"];
            if (!string.IsNullOrEmpty(existingToken)) {
                // Return the existing token to maintain consistency
                return Ok(new {
                    success = true,
                    xsrfToken = existingToken
                });
            }

            // Generate new token if none exists
            var xsrfToken = ValidateXsrfTokenAttribute.GenerateToken();
            ValidateXsrfTokenAttribute.SetXsrfCookie(HttpContext, xsrfToken);
            return Ok(new {
                success = true,
                xsrfToken = xsrfToken
            });
        }

        /// <summary>
        /// Check if user is authenticated (has valid cookies).
        /// Useful for frontend to verify auth state on page load.
        /// </summary>
        [HttpGet("check-auth")]
        [AllowAnonymous]
        public IActionResult CheckAuth() {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null) {
                return Ok(new { success = true, isAuthenticated = false });
            }

            return Ok(new { success = true, isAuthenticated = true });
        }

        private string GetActionIcon(string controller, string action) {
            var iconMap = new Dictionary<string, Dictionary<string, string>> {
                ["Home"] = new Dictionary<string, string> {
                    ["Entry"] = "fas fa-plus-circle",
                    ["VoucherEntry"] = "fas fa-file-invoice",
                    ["ModifiedEntries"] = "fas fa-history",
                    ["BaseVouchers"] = "fas fa-layer-group",
                    ["Recipes"] = "fas fa-book-open"
                },
                ["MetaData"] = new Dictionary<string, string> {
                    ["Projects"] = "fas fa-project-diagram",
                    ["Categories"] = "fas fa-tags",
                    ["Accounts"] = "fas fa-address-book",
                    ["Locations"] = "fas fa-map-marker-alt",
                    ["Bookers"] = "fas fa-user-tie"
                },
                ["Reporting"] = new Dictionary<string, string> {
                    ["SingleDoc"] = "fas fa-file-alt",
                    ["AgingReport"] = "fas fa-clock",
                    ["AccountReport"] = "fas fa-chart-pie",
                    ["MasterReport"] = "fas fa-file-alt",
                    ["ProductReport"] = "fas fa-box",
                    ["MissedEntriesReport"] = "fas fa-exclamation-circle"
                },
                ["DeliveryChallan"] = new Dictionary<string, string> {
                    ["ProcessDC"] = "fas fa-truck-loading"
                }
            };

            return iconMap.TryGetValue(controller, out var actions) && actions.TryGetValue(action, out var icon)
                ? icon
                : "fas fa-circle";
        }
    }
}

