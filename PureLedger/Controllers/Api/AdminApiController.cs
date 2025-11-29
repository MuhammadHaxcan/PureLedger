using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PureLedger.Models;
using PureLedger.Services.Interface;
using PureLedger.ViewModels;
using System.Security.Claims;
using System.IO;

namespace PureLedger.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminApiController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AdminApiController> _logger;

        // Map of controllers and their actions for access control
        private readonly Dictionary<string, List<string>> _controllerActions = new()
        {
            ["Home"] = new() { "Entry", "VoucherEntry", "ModifiedEntries", "BaseVouchers", "Recipes" },
            ["MetaData"] = new() { "Projects", "Categories", "Accounts", "Locations", "Bookers" },
            ["Reporting"] = new() { "SingleDoc", "AgingReport", "AccountReport", "MasterReport", "ProductReport", "MissedEntriesReport" },
            ["DeliveryChallan"] = new() { "ProcessDC" }
        };

        public AdminApiController(
            IAuthService authService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AdminApiController> logger)
        {
            _authService = authService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            try
            {
                var subroute = _httpContextAccessor.HttpContext?.Items["Subroute"]?.ToString() ?? "Default";
                var users = await _authService.GetUsersBySubrouteAsync(subroute);
                return Ok(new
                {
                    success = true,
                    data = users.Select(u => new
                    {
                        u.UserId,
                        u.Username,
                        u.FullName,
                        u.Email,
                        u.IsAdmin,
                        u.IsActive
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading users");
                return StatusCode(500, new { success = false, message = "Error loading users" });
            }
        }

        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

                // If trying to get self, return profile data
                if (id == currentUserId)
                {
                    var user = await _authService.GetUserByIdAsync(id);
                    if (user == null)
                    {
                        return NotFound(new { success = false, message = "User not found" });
                    }

                    return Ok(new
                    {
                        success = true,
                        data = new
                        {
                            user.UserId,
                            user.Username,
                            user.FullName,
                            user.Email,
                            user.IsAdmin,
                            user.IsActive
                        },
                        isProfile = true
                    });
                }

                var targetUser = await _authService.GetUserByIdAsync(id);
                if (targetUser == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                var userProjects = await _authService.GetUserProjectsAsync(targetUser.UserId);
                var controllerAccess = await _authService.GetUserControllerAccessAsync(targetUser.UserId);
                var actionAccess = await _authService.GetUserActionAccessAsync(targetUser.UserId);

                var accessPermissions = new List<string>();
                accessPermissions.AddRange(controllerAccess);
                foreach (var controllerActionPair in actionAccess)
                {
                    foreach (var action in controllerActionPair.Value)
                    {
                        accessPermissions.Add($"{controllerActionPair.Key}|{action}");
                    }
                }

                var allProjects = await _authService.GetUserProjectsAsync(currentUserId);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        targetUser.UserId,
                        targetUser.Username,
                        targetUser.FullName,
                        targetUser.Email,
                        targetUser.IsAdmin,
                        targetUser.IsActive,
                        ProjectIds = userProjects.Select(p => p.ProjectId).ToList(),
                        AccessPermissions = accessPermissions
                    },
                    projects = allProjects.Select(p => new { id = p.ProjectId, name = p.ProjectName }),
                    controllerActions = _controllerActions
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user {UserId}", id);
                return StatusCode(500, new { success = false, message = "Error loading user" });
            }
        }

        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] UserViewModel model)
        {
            try
            {
                if (model == null)
                {
                    return BadRequest(new { success = false, message = "Invalid request data" });
                }

                // Ensure AccessPermissions is properly set (even if empty array)
                if (model.AccessPermissions == null)
                {
                    model.AccessPermissions = new List<string>();
                }

                // Ensure ProjectIds is properly set (even if empty array)
                if (model.ProjectIds == null)
                {
                    model.ProjectIds = new List<int>();
                }

                // For new users, password is required
                if (model.UserId == 0 && string.IsNullOrWhiteSpace(model.Password))
                {
                    ModelState.AddModelError("Password", "Password is required for new users");
                }

                // Validate password confirmation if password is provided
                if (!string.IsNullOrWhiteSpace(model.Password) && model.Password != model.ConfirmPassword)
                {
                    ModelState.AddModelError("ConfirmPassword", "Passwords do not match");
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        success = false,
                        errors = ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)
                            .ToList()
                    });
                }

                var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

                _logger.LogInformation("Creating user with {PermissionCount} permissions and {ProjectCount} projects", 
                    model.AccessPermissions?.Count ?? 0, model.ProjectIds?.Count ?? 0);

                var result = await _authService.CreateUserAsync(model);
                if (result)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "User created successfully",
                        data = model
                    });
                }

                return BadRequest(new { success = false, message = "Error creating user. Username may already exist." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return StatusCode(500, new { success = false, message = "Error creating user" });
            }
        }

        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UserViewModel model)
        {
            try
            {
                if (id != model.UserId)
                {
                    return BadRequest(new { success = false, message = "User ID mismatch" });
                }

                var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

                // Prevent self-modification of admin status
                if (id == currentUserId)
                {
                    model.IsAdmin = User.IsInRole("Admin"); // Preserve current admin status
                }

                // Clear password validation if empty (matching MVC controller behavior)
                if (string.IsNullOrWhiteSpace(model.Password))
                {
                    // Clear validation errors for Password and ConfirmPassword
                    ModelState.Remove("Password");
                    ModelState.Remove("ConfirmPassword");
                    
                    // Make sure these are empty strings to avoid any issues
                    model.Password = string.Empty;
                    model.ConfirmPassword = string.Empty;
                }
                // If password is provided but doesn't match confirmation
                else if (!string.IsNullOrWhiteSpace(model.Password) && model.Password != model.ConfirmPassword)
                {
                    ModelState.AddModelError("ConfirmPassword", "Passwords do not match");
                }

                // Ensure AccessPermissions is properly set (even if empty array)
                if (model.AccessPermissions == null)
                {
                    model.AccessPermissions = new List<string>();
                }

                // Ensure ProjectIds is properly set (even if empty array)
                if (model.ProjectIds == null)
                {
                    model.ProjectIds = new List<int>();
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        success = false,
                        errors = ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)
                            .ToList()
                    });
                }

                _logger.LogInformation("Updating user {UserId} with {PermissionCount} permissions and {ProjectCount} projects", 
                    id, model.AccessPermissions?.Count ?? 0, model.ProjectIds?.Count ?? 0);

                var result = await _authService.UpdateUserAsync(model);
                if (result)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "User updated successfully",
                        data = model
                    });
                }

                return BadRequest(new { success = false, message = "Error updating user" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", id);
                return StatusCode(500, new { success = false, message = "Error updating user" });
            }
        }

        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

                // Prevent self-deletion
                if (id == currentUserId)
                {
                    return BadRequest(new { success = false, message = "You cannot delete your own account" });
                }

                var result = await _authService.DeleteUserAsync(id);
                if (result)
                {
                    return Ok(new { success = true, message = "User deleted successfully" });
                }

                return BadRequest(new { success = false, message = "Error deleting user. Make sure there's at least one admin user." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", id);
                return StatusCode(500, new { success = false, message = "Error deleting user" });
            }
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var user = await _authService.GetUserByUsernameAsync(username);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        user.UserId,
                        user.Username,
                        user.FullName,
                        user.Email,
                        user.IsAdmin,
                        user.IsActive
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading profile");
                return StatusCode(500, new { success = false, message = "Error loading profile" });
            }
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UserViewModel model)
        {
            try
            {
                var username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var user = await _authService.GetUserByUsernameAsync(username);
                if (user == null || user.UserId != model.UserId)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                // Don't allow changing username or admin status through profile
                model.Username = user.Username;
                model.IsAdmin = user.IsAdmin;
                model.IsActive = user.IsActive;

                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        success = false,
                        errors = ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)
                            .ToList()
                    });
                }

                var result = await _authService.UpdateUserAsync(model);
                if (result)
                {
                    return Ok(new { success = true, message = "Profile updated successfully", data = model });
                }

                return BadRequest(new { success = false, message = "Error updating profile" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile");
                return StatusCode(500, new { success = false, message = "Error updating profile" });
            }
        }

        [HttpGet("project-cartage-settings")]
        public async Task<IActionResult> GetProjectCartageSettings([FromQuery] int? projectId)
        {
            try
            {
                var projects = await _authService.GetAllProjectsAsync();
                var accounts = await _authService.GetAllAccountsAsync();
                var categories = await _authService.GetAllCategoriesAsync();

                ProjectCartageSettings? settings = null;
                if (projectId.HasValue)
                {
                    settings = await _authService.GetProjectCartageSettingsAsync(projectId.Value);
                }

                // Convert image byte array to base64 for frontend if settings exist
                string? projectImageBase64 = null;
                string? projectImageContentType = null;
                if (settings != null && settings.ProjectImage != null && settings.ProjectImage.Length > 0)
                {
                    projectImageBase64 = Convert.ToBase64String(settings.ProjectImage);
                    projectImageContentType = settings.ProjectImageContentType;
                    // Add data URL prefix for easy display
                    var contentType = !string.IsNullOrEmpty(settings.ProjectImageContentType) 
                        ? settings.ProjectImageContentType 
                        : "image/png";
                    projectImageBase64 = $"data:{contentType};base64,{projectImageBase64}";
                }

                object settingsData;
                if (settings != null)
                {
                    settingsData = new
                    {
                        id = settings.Id,
                        projectId = settings.ProjectId,
                        forExpenseAccountId = settings.ForExpenseAccountId ?? (int?)null,
                        cartageTransporterCategoryId = settings.CartageTransporterCategoryId ?? (int?)null,
                        projectImage = projectImageBase64,
                        projectImageContentType = projectImageContentType
                    };
                }
                else
                {
                    settingsData = new { projectId = projectId ?? 0 };
                }

                return Ok(new
                {
                    success = true,
                    data = settingsData,
                    projects = projects.Select(p => new { id = p.ProjectId, name = p.ProjectName }),
                    accounts = accounts.Select(a => new { id = a.AccountId, name = a.AccountName }),
                    categories = categories.Select(c => new { id = c.CategoryId, name = c.CategoryName })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading project cartage settings");
                return StatusCode(500, new { success = false, message = "Error loading settings" });
            }
        }

        [HttpPost("project-cartage-settings")]
        [Consumes("multipart/form-data", "application/json")]
        public async Task<IActionResult> SaveProjectCartageSettings([FromForm] ProjectCartageSettings? model, IFormFile? projectImage)
        {
            try
            {
                // Handle JSON body if model is null (from JSON request with base64 image)
                if (model == null && Request.ContentType?.Contains("application/json") == true)
                {
                    Request.EnableBuffering();
                    Request.Body.Position = 0;
                    using var reader = new StreamReader(Request.Body, leaveOpen: true);
                    var body = await reader.ReadToEndAsync();
                    Request.Body.Position = 0;
                    
                    if (!string.IsNullOrEmpty(body))
                    {
                        // Try to deserialize as ViewModel first (for base64 image)
                        var viewModel = System.Text.Json.JsonSerializer.Deserialize<ViewModels.ProjectCartageSettingsViewModel>(body, new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        
                        if (viewModel != null)
                        {
                            // Convert ViewModel to Model
                            model = new ProjectCartageSettings
                            {
                                Id = viewModel.Id,
                                ProjectId = viewModel.ProjectId,
                                ForExpenseAccountId = viewModel.ForExpenseAccountId,
                                CartageTransporterCategoryId = viewModel.CartageTransporterCategoryId
                            };
                            
                            // Handle base64 image
                            if (!string.IsNullOrEmpty(viewModel.ProjectImageBase64))
                            {
                                try
                                {
                                    // Remove data URL prefix if present
                                    var base64Data = viewModel.ProjectImageBase64.Contains(",") 
                                        ? viewModel.ProjectImageBase64.Split(',')[1] 
                                        : viewModel.ProjectImageBase64;
                                    
                                    model.ProjectImage = Convert.FromBase64String(base64Data);
                                    model.ProjectImageContentType = viewModel.ProjectImageContentType ?? "image/png";
                                }
                                catch (FormatException ex)
                                {
                                    _logger.LogWarning(ex, "Invalid base64 image data provided");
                                    return BadRequest(new { success = false, message = "Invalid image data format" });
                                }
                            }
                        }
                        else
                        {
                            // Fallback: try to deserialize as ProjectCartageSettings directly
                            model = System.Text.Json.JsonSerializer.Deserialize<ProjectCartageSettings>(body, new System.Text.Json.JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                        }
                    }
                }

                if (model == null)
                {
                    return BadRequest(new { success = false, message = "Invalid request data" });
                }

                // Handle image upload from FormData (IFormFile)
                if (projectImage != null)
                {
                    // Save image as byte array in database
                    using (var memoryStream = new MemoryStream())
                    {
                        await projectImage.CopyToAsync(memoryStream);
                        model.ProjectImage = memoryStream.ToArray();
                        model.ProjectImageContentType = projectImage.ContentType;
                    }
                }

                await _authService.SaveProjectCartageSettingsAsync(model);
                return Ok(new { success = true, message = "Project cartage settings updated successfully", data = model });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving project cartage settings");
                return StatusCode(500, new { success = false, message = "Error saving settings" });
            }
        }

        [HttpGet("categories-by-project/{projectId}")]
        public async Task<IActionResult> GetCategoriesByProject(int projectId)
        {
            try
            {
                var categories = await _authService.GetCategoriesByProjectAsync(projectId);
                return Ok(new
                {
                    success = true,
                    data = categories.Select(c => new { c.CategoryId, c.CategoryName })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading categories for project {ProjectId}", projectId);
                return StatusCode(500, new { success = false, message = "Error loading categories" });
            }
        }

        [HttpGet("accounts-by-category/{categoryId}")]
        public async Task<IActionResult> GetAccountsByCategory(int categoryId)
        {
            try
            {
                var accounts = await _authService.GetAccountsByCategoryAsync(categoryId);
                return Ok(new
                {
                    success = true,
                    data = accounts.Select(a => new { a.AccountId, a.AccountName })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading accounts for category {CategoryId}", categoryId);
                return StatusCode(500, new { success = false, message = "Error loading accounts" });
            }
        }

        [HttpGet("category-for-account/{accountId}")]
        public async Task<IActionResult> GetCategoryForAccount(int accountId)
        {
            try
            {
                var account = await _authService.GetAccountByIdAsync(accountId);
                return Ok(new
                {
                    success = true,
                    categoryId = account?.CategoryId ?? 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting category for account {AccountId}", accountId);
                return StatusCode(500, new { success = false, message = "Error getting category" });
            }
        }

        [HttpGet("project-cartage-settings-by-project/{projectId}")]
        public async Task<IActionResult> GetProjectCartageSettingsByProject(int projectId)
        {
            try
            {
                var settings = await _authService.GetProjectCartageSettingsAsync(projectId);
                if (settings == null)
                {
                    return Ok(new { success = true, data = (object?)null });
                }

                // Convert image byte array to base64 for frontend
                string? projectImageBase64 = null;
                if (settings.ProjectImage != null && settings.ProjectImage.Length > 0)
                {
                    projectImageBase64 = Convert.ToBase64String(settings.ProjectImage);
                    // Add data URL prefix for easy display
                    var contentType = !string.IsNullOrEmpty(settings.ProjectImageContentType) 
                        ? settings.ProjectImageContentType 
                        : "image/png";
                    projectImageBase64 = $"data:{contentType};base64,{projectImageBase64}";
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        id = settings.Id,
                        projectId = settings.ProjectId,
                        forExpenseAccountId = settings.ForExpenseAccountId ?? (int?)null,
                        cartageTransporterCategoryId = settings.CartageTransporterCategoryId ?? (int?)null,
                        projectImage = projectImageBase64,
                        projectImageContentType = settings.ProjectImageContentType
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cartage settings for project {ProjectId}", projectId);
                return StatusCode(500, new { success = false, message = "Error getting settings" });
            }
        }

        [HttpGet("controller-actions")]
        public IActionResult GetControllerActions()
        {
            return Ok(new { success = true, data = _controllerActions });
        }

        [HttpGet("projects")]
        public async Task<IActionResult> GetProjects()
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var projects = await _authService.GetUserProjectsAsync(currentUserId);
                return Ok(new
                {
                    success = true,
                    data = projects.Select(p => new { id = p.ProjectId, name = p.ProjectName })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading projects");
                return StatusCode(500, new { success = false, message = "Error loading projects" });
            }
        }
    }
}

