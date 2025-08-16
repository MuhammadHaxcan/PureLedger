using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        // Common controller names in the application
        private readonly string[] _availableControllers = new[]
        {
            "Home",
            "MetaData",
            "Reporting",
            "DeliveryChallan",
            "Account"
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
            ViewBag.Controllers = _availableControllers;
            return View(new UserViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserViewModel model, string[] controllerAccess)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            ViewBag.Projects = await _authService.GetUserProjectsAsync(userId);
            ViewBag.Controllers = _availableControllers;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.ControllerAccess = controllerAccess?.ToList() ?? new List<string>();
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

            // Get user's projects and controller access
            var userProjects = await _authService.GetUserProjectsAsync(user.UserId);
            var controllerAccess = await _authService.GetUserControllerAccessAsync(user.UserId);

            ViewBag.Projects = await _authService.GetUserProjectsAsync(currentUserId);
            ViewBag.Controllers = _availableControllers;

            var model = new UserViewModel
            {
                UserId = user.UserId,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                IsAdmin = user.IsAdmin,
                IsActive = user.IsActive,
                ProjectIds = userProjects.Select(p => p.ProjectId).ToList(),
                ControllerAccess = controllerAccess
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserViewModel model, string[] controllerAccess)
        {
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            ViewBag.Projects = await _authService.GetUserProjectsAsync(currentUserId);
            ViewBag.Controllers = _availableControllers;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.ControllerAccess = controllerAccess?.ToList() ?? new List<string>();
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
    }
}