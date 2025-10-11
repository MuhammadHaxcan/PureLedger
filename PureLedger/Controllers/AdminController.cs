using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PureLedger.Models;
using PureLedger.Services;
using PureLedger.ViewModels;
using System.Security.Claims;

namespace PureLedger.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AdminController> _logger;
        
        // Map of controllers and their actions for access control
        private readonly Dictionary<string, List<string>> _controllerActions = new()
        {
            ["Home"] = new() { "Entry", "VoucherEntry", "ModifiedEntries" , "MissedEntries", "BaseVouchers" },
            ["MetaData"] = new() { "Projects", "Categories", "Accounts", "Tags", "Locations" },
            ["Reporting"] = new() { "SingleDoc", "AgingReport", "AccountReport", "MasterReport", "ProductReport" },
            ["DeliveryChallan"] = new() { "Index" }
        };

        public AdminController(
            IAuthService authService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AdminController> logger)
        {
            _authService = authService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            // Get the current subroute
            var subroute = _httpContextAccessor.HttpContext?.Items["Subroute"]?.ToString() ?? "Default";
            var users = await _authService.GetUsersBySubrouteAsync(subroute);
            return View(users);
        }

        public async Task<IActionResult> Create()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            ViewBag.Projects = await _authService.GetUserProjectsAsync(userId);
            ViewBag.ControllerActions = _controllerActions;
            return View(new UserViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserViewModel model, string[] accessPermissions)
        {
            // Update the model with access permissions
            model.AccessPermissions = accessPermissions?.ToList() ?? new List<string>();
            
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            ViewBag.Projects = await _authService.GetUserProjectsAsync(userId);
            ViewBag.ControllerActions = _controllerActions;
            
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _authService.CreateUserAsync(model);
            if (result)
            {
                TempData["SuccessMessage"] = "User created successfully";
                return RedirectToAction(nameof(Index));
            }
            
            ModelState.AddModelError("", "Error creating user. Username may already exist.");
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            
            // If trying to edit self, go to profile edit
            if (id == currentUserId)
            {
                return RedirectToAction("EditProfile");
            }
            
            var user = await _authService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            
            // Get user's projects and access permissions
            var userProjects = await _authService.GetUserProjectsAsync(user.UserId);
            var controllerAccess = await _authService.GetUserControllerAccessAsync(user.UserId);
            var actionAccess = await _authService.GetUserActionAccessAsync(user.UserId);
            
            ViewBag.Projects = await _authService.GetUserProjectsAsync(currentUserId);
            ViewBag.ControllerActions = _controllerActions;
            
            // Build access permissions list
            var accessPermissions = new List<string>();
            
            // Add controller-level access
            accessPermissions.AddRange(controllerAccess);
            
            // Add action-level access
            foreach (var controllerActionPair in actionAccess)
            {
                foreach (var action in controllerActionPair.Value)
                {
                    accessPermissions.Add($"{controllerActionPair.Key}|{action}");
                }
            }
            
            var model = new UserViewModel
            {
                UserId = user.UserId,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                IsAdmin = user.IsAdmin,
                IsActive = user.IsActive,
                ProjectIds = userProjects.Select(p => p.ProjectId).ToList(),
                AccessPermissions = accessPermissions
            };
            
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserViewModel model, string[] accessPermissions)
        {
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            ViewBag.Projects = await _authService.GetUserProjectsAsync(currentUserId);
            ViewBag.ControllerActions = _controllerActions;
            
            // Clear password validation errors if password is empty
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
            
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            
            model.AccessPermissions = accessPermissions?.ToList() ?? new List<string>();
            
            var result = await _authService.UpdateUserAsync(model);
            if (result)
            {
                TempData["SuccessMessage"] = "User updated successfully";
                return RedirectToAction(nameof(Index));
            }
            
            ModelState.AddModelError("", "Error updating user");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

            // Prevent self-deletion
            if (id == currentUserId)
            {
                TempData["ErrorMessage"] = "You cannot delete your own account";
                return RedirectToAction(nameof(Index));
            }

            var result = await _authService.DeleteUserAsync(id);
            if (result)
            {
                TempData["SuccessMessage"] = "User deleted successfully";
            }
            else
            {
                TempData["ErrorMessage"] = "Error deleting user. Make sure there's at least one admin user.";
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> EditProfile()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login", "Account");
            }

            var user = await _authService.GetUserByUsernameAsync(username);
            if (user == null)
            {
                return NotFound();
            }

            var model = new UserViewModel
            {
                UserId = user.UserId,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                IsAdmin = user.IsAdmin,
                IsActive = user.IsActive
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(UserViewModel model)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login", "Account");
            }

            var user = await _authService.GetUserByUsernameAsync(username);
            if (user == null || user.UserId != model.UserId)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Don't allow changing username or admin status through profile
            model.Username = user.Username;
            model.IsAdmin = user.IsAdmin;
            model.IsActive = user.IsActive;

            var result = await _authService.UpdateUserAsync(model);
            if (result)
            {
                TempData["SuccessMessage"] = "Profile updated successfully";
                return RedirectToAction("Entry", "Home");
            }

            ModelState.AddModelError("", "Error updating profile");
            return View(model);
        }

        // GET: /Admin/ProjectCartageSettings
        [HttpGet]
        public async Task<IActionResult> ProjectCartageSettings(int? projectId)
        {
            var projects = await _authService.GetAllProjectsAsync();
            var accounts = await _authService.GetAllAccountsAsync();
            var categories = await _authService.GetAllCategoriesAsync();

            ProjectCartageSettings settings = null;
            if (projectId.HasValue)
            {
                settings = await _authService.GetProjectCartageSettingsAsync(projectId.Value);
            }

            ViewBag.Projects = new SelectList(projects, "ProjectId", "ProjectName", projectId);
            ViewBag.Accounts = new SelectList(accounts, "AccountId", "AccountName", settings?.ForExpenseAccountId);
            ViewBag.Categories = new SelectList(categories, "CategoryId", "CategoryName", settings?.CartageTransporterCategoryId);

            return View(settings ?? new ProjectCartageSettings { ProjectId = projectId ?? 0 });
        }

        // POST: /Admin/ProjectCartageSettings
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProjectCartageSettings(ProjectCartageSettings model)
        {
            await _authService.SaveProjectCartageSettingsAsync(model);
            TempData["SuccessMessage"] = "Project cartage settings updated successfully.";
            return RedirectToAction(nameof(ProjectCartageSettings), new { projectId = model.ProjectId });
        }

        [HttpGet]
        public async Task<JsonResult> GetCategoriesByProject(int projectId)
        {
            var categories = await _authService.GetCategoriesByProjectAsync(projectId);
            return Json(categories.Select(c => new { c.CategoryId, c.CategoryName }));
        }

        [HttpGet]
        public async Task<JsonResult> GetAccountsByCategory(int categoryId)
        {
            var accounts = await _authService.GetAccountsByCategoryAsync(categoryId);
            return Json(accounts.Select(a => new { a.AccountId, a.AccountName }));
        }

        [HttpGet]
        public async Task<JsonResult> GetCategoryForAccount(int accountId)
        {
            try {
                var account = await _authService.GetAccountByIdAsync(accountId);
                return Json(new { categoryId = account?.CategoryId ?? 0 });
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error getting category for account {AccountId}", accountId);
                return Json(new { categoryId = 0 });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetProjectCartageSettingsByProject(int projectId)
        {
            try {
                var settings = await _authService.GetProjectCartageSettingsAsync(projectId);
                if (settings == null) {
                    return Json(null);
                }
                
                // Create a simplified anonymous object without navigation properties to avoid circular references
                var result = new {
                    id = settings.Id,
                    projectId = settings.ProjectId,
                    forExpenseAccountId = settings.ForExpenseAccountId,
                    cartageTransporterCategoryId = settings.CartageTransporterCategoryId
                };
                
                return Json(result);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error getting cartage settings for project {ProjectId}: {Message}", projectId, ex.Message);
                return Json(null);
            }
        }
    }
}