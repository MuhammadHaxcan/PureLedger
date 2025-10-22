using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PureLedger.Attributes;
using PureLedger.Models;
using PureLedger.Services;
using PureLedger.Services.Interface;

namespace PureLedger.Controllers
{
    [ControllerAccess]
    public class MetaDataController : Controller
    {
        private readonly MetaDataValidationService _validationService;
        private readonly IMetaDataService _metaDataService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<MetaDataController> _logger;

        public MetaDataController(
            MetaDataValidationService validationService,
            IMetaDataService metaDataService,
            ICurrentUserService currentUserService,
            ILogger<MetaDataController> logger)
        {
            _validationService = validationService;
            _metaDataService = metaDataService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        #region Project

        // GET: /MetaData/Projects
        public async Task<IActionResult> Projects()
        {
            var userId = _currentUserService.GetCurrentUserId();
            if (userId.HasValue)
            {
                var projects = await _metaDataService.GetAllProjectsForUserAsync(userId.Value);
                return View(projects);
            }
            return View(new List<Project>());
        }

        // GET: /MetaData/CreateProject
        public IActionResult CreateProject()
        {
            return View();
        }

        // POST: /MetaData/CreateProject
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProject(Project project)
        {
            var errors = await _validationService.ValidateProjectAsync(project);
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    ModelState.AddModelError("", error);
                }
                return View(project);
            }

            var creationErrors = await _metaDataService.CreateProjectAsync(project);
            if (creationErrors.Count > 0)
            {
                foreach (var error in creationErrors)
                {
                    ModelState.AddModelError("", error);
                }
                return View(project);
            }

            TempData["SuccessMessage"] = "Project created successfully.";
            return RedirectToAction(nameof(Projects));
        }

        // GET: /MetaData/EditProject/5
        public async Task<IActionResult> EditProject(int id)
        {
            var project = await _metaDataService.GetProjectByIdAsync(id);
            if (project == null)
            {
                return NotFound();
            }
            return View(project);
        }

        // POST: /MetaData/EditProject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProject(Project project)
        {
            var errors = await _validationService.ValidateProjectAsync(project);
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    ModelState.AddModelError("", error);
                }
                return View(project);
            }

            var updateErrors = await _metaDataService.UpdateProjectAsync(project);
            if (updateErrors.Count > 0)
            {
                foreach (var error in updateErrors)
                {
                    ModelState.AddModelError("", error);
                }
                return View(project);
            }

            TempData["SuccessMessage"] = "Project updated successfully.";
            return RedirectToAction(nameof(Projects));
        }

        #endregion

        #region Category

        // GET: /MetaData/Categories
        public async Task<IActionResult> Categories()
        {
            var categories = await _metaDataService.GetAllCategoriesAsync();
            return View(categories);
        }

        // GET: /MetaData/CreateCategory
        public async Task<IActionResult> CreateCategory()
        {
            var userId = _currentUserService.GetCurrentUserId();
            var projects = userId.HasValue
                ? await _metaDataService.GetAllProjectsForCategoriesForUserAsync(userId.Value)
                : new List<Project>();
                
            ViewBag.Projects = new SelectList(projects, "ProjectId", "ProjectName");
            ViewBag.AccountTypes = new SelectList(
                await _metaDataService.GetAllAccountTypesAsync(),
                "AccountTypeId", "AccountTypeName");
            return View();
        }

        // POST: /MetaData/CreateCategory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(Category category)
        {
            var errors = await _validationService.ValidateCategoryAsync(category);
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    ModelState.AddModelError("", error);
                }

                var userId = _currentUserService.GetCurrentUserId();
                var projects = userId.HasValue
                    ? await _metaDataService.GetAllProjectsForCategoriesForUserAsync(userId.Value)
                    : new List<Project>();
                    
                ViewBag.Projects = new SelectList(projects, "ProjectId", "ProjectName", category.ProjectId);
                ViewBag.AccountTypes = new SelectList(
                    await _metaDataService.GetAllAccountTypesAsync(),
                    "AccountTypeId", "AccountTypeName", category.AccountTypeId);

                return View(category);
            }

            var creationErrors = await _metaDataService.CreateCategoryAsync(category);
            if (creationErrors.Count > 0)
            {
                foreach (var error in creationErrors)
                {
                    ModelState.AddModelError("", error);
                }

                var userId = _currentUserService.GetCurrentUserId();
                var projects = userId.HasValue
                    ? await _metaDataService.GetAllProjectsForCategoriesForUserAsync(userId.Value)
                    : new List<Project>();
                    
                ViewBag.Projects = new SelectList(projects, "ProjectId", "ProjectName", category.ProjectId);
                ViewBag.AccountTypes = new SelectList(
                    await _metaDataService.GetAllAccountTypesAsync(),
                    "AccountTypeId", "AccountTypeName", category.AccountTypeId);

                return View(category);
            }

            TempData["SuccessMessage"] = "Category created successfully.";
            return RedirectToAction(nameof(Categories));
        }

        // GET: /MetaData/EditCategory/5
        public async Task<IActionResult> EditCategory(int id)
        {
            var category = await _metaDataService.GetCategoryByIdAsync(id);
            if (category == null)
            {
                return NotFound();
            }

            var userId = _currentUserService.GetCurrentUserId();
            var projects = userId.HasValue
                ? await _metaDataService.GetAllProjectsForCategoriesForUserAsync(userId.Value)
                : new List<Project>();
                
            ViewBag.Projects = new SelectList(projects, "ProjectId", "ProjectName", category.ProjectId);
            ViewBag.AccountTypes = new SelectList(
                await _metaDataService.GetAllAccountTypesAsync(),
                "AccountTypeId", "AccountTypeName", category.AccountTypeId);

            return View(category);
        }

        // POST: /MetaData/EditCategory/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(Category category)
        {
            var errors = await _validationService.ValidateCategoryAsync(category);
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    ModelState.AddModelError("", error);
                }

                var userId = _currentUserService.GetCurrentUserId();
                var projects = userId.HasValue
                    ? await _metaDataService.GetAllProjectsForCategoriesForUserAsync(userId.Value)
                    : new List<Project>();
                    
                ViewBag.Projects = new SelectList(projects, "ProjectId", "ProjectName", category.ProjectId);
                ViewBag.AccountTypes = new SelectList(
                    await _metaDataService.GetAllAccountTypesAsync(),
                    "AccountTypeId", "AccountTypeName", category.AccountTypeId);

                return View(category);
            }

            var updateErrors = await _metaDataService.UpdateCategoryAsync(category);
            if (updateErrors.Count > 0)
            {
                foreach (var error in updateErrors)
                    ModelState.AddModelError("", error);

                var userId = _currentUserService.GetCurrentUserId();
                var projects = userId.HasValue
                    ? await _metaDataService.GetAllProjectsForCategoriesForUserAsync(userId.Value)
                    : new List<Project>();
                    
                ViewBag.Projects = new SelectList(projects, "ProjectId", "ProjectName", category.ProjectId);
                ViewBag.AccountTypes = new SelectList(
                    await _metaDataService.GetAllAccountTypesAsync(),
                    "AccountTypeId", "AccountTypeName", category.AccountTypeId);

                return View(category);
            }

            TempData["SuccessMessage"] = "Category updated successfully.";
            return RedirectToAction(nameof(Categories));
        }

        #endregion

        #region Account

        // GET: /MetaData/Accounts
        public async Task<IActionResult> Accounts()
        {
            var accounts = await _metaDataService.GetAllAccountsAsync();
            return View(accounts);
        }

        // GET: /MetaData/CreateAccount
        public async Task<IActionResult> CreateAccount()
        {
            var userId = _currentUserService.GetCurrentUserId();
            var projects = userId.HasValue
                ? await _metaDataService.GetProjectsForAccountsForUserAsync(userId.Value)
                : new List<Project>();
                
            ViewBag.Projects = new SelectList(projects, "ProjectId", "ProjectName");
            ViewBag.Categories = new SelectList(new List<object>(), "categoryId", "categoryName");
            ViewBag.AccountGroups = new SelectList(
                await _metaDataService.GetAccountGroupsAsync(),
                "AccountGroupId", "AccountGroupName");

            return View();
        }

        // POST: /MetaData/CreateAccount
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAccount(Account account)
        {
            var errors = await _validationService.ValidateAccountAsync(account);
            int.TryParse(Request.Form["ProjectId"], out int selectedProjectId);

            if (errors.Count > 0)
            {
                var userIdForProjects = _currentUserService.GetCurrentUserId();
                var projectsForAccounts = userIdForProjects.HasValue
                    ? await _metaDataService.GetProjectsForAccountsForUserAsync(userIdForProjects.Value)
                    : new List<Project>();
                    
                ViewBag.Projects = new SelectList(projectsForAccounts, "ProjectId", "ProjectName", selectedProjectId);

                var categories = selectedProjectId > 0
                    ? await _metaDataService.GetCategoriesByProjectAsync(selectedProjectId)
                    : new List<object>();
                ViewBag.Categories = new SelectList(categories, "categoryId", "categoryName", account.CategoryId);

                ViewBag.AccountGroups = new SelectList(
                    await _metaDataService.GetAccountGroupsAsync(),
                    "AccountGroupId", "AccountGroupName", account.AccountGroupId);

                foreach (var error in errors)
                {
                    ModelState.AddModelError("", error);
                }
                return View(account);
            }

            var creationErrors = await _metaDataService.CreateAccountAsync(account);
            if (creationErrors.Count > 0)
            {
                foreach (var error in creationErrors)
                {
                    ModelState.AddModelError("", error);
                }
                var userIdForProjects = _currentUserService.GetCurrentUserId();
                var projectsForAccounts = userIdForProjects.HasValue
                    ? await _metaDataService.GetProjectsForAccountsForUserAsync(userIdForProjects.Value)
                    : new List<Project>();
                    
                ViewBag.Projects = new SelectList(projectsForAccounts, "ProjectId", "ProjectName", selectedProjectId);

                var categories = selectedProjectId > 0
                    ? await _metaDataService.GetCategoriesByProjectAsync(selectedProjectId)
                    : new List<object>();
                ViewBag.Categories = new SelectList(categories, "categoryId", "categoryName", account.CategoryId);
                //ViewBag.Categories = new SelectList(
                //    await _metaDataService.GetCategoriesForAccountsAsync(),
                //    "CategoryId", "CategoryName", account.CategoryId);
                ViewBag.AccountGroups = new SelectList(
                    await _metaDataService.GetAccountGroupsAsync(),
                    "AccountGroupId", "AccountGroupName", account.AccountGroupId);
                //ViewBag.Projects = await _metaDataService.GetProjectsForAccountsAsync();

                return View(account);
            }

            TempData["SuccessMessage"] = "Account created successfully.";
            return RedirectToAction(nameof(Accounts));
        }

        // GET: /MetaData/EditAccount/5
        public async Task<IActionResult> EditAccount(int id)
        {
            var account = await _metaDataService.GetAccountByIdAsync(id);
            if (account == null)
            {
                return NotFound();
            }

            ViewBag.Categories = new SelectList(
                await _metaDataService.GetCategoriesForAccountsAsync(),
                "CategoryId", "CategoryName", account.CategoryId);
            ViewBag.AccountGroups = new SelectList(
                await _metaDataService.GetAccountGroupsAsync(),
                "AccountGroupId", "AccountGroupName", account.AccountGroupId);

            return View(account);
        }

        // POST: /MetaData/EditAccount/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAccount(Account account)
        {
            var errors = await _validationService.ValidateAccountAsync(account);
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    ModelState.AddModelError("", error);
                }

                ViewBag.Categories = new SelectList(
                    await _metaDataService.GetCategoriesForAccountsAsync(),
                    "CategoryId", "CategoryName", account.CategoryId);
                ViewBag.AccountGroups = new SelectList(
                    await _metaDataService.GetAccountGroupsAsync(),
                    "AccountGroupId", "AccountGroupName", account.AccountGroupId);

                return View(account);
            }

            var updateErrors = await _metaDataService.UpdateAccountAsync(account);
            if (updateErrors.Count > 0)
            {
                foreach (var error in updateErrors)
                {
                    ModelState.AddModelError("", error);
                }

                ViewBag.Categories = new SelectList(
                    await _metaDataService.GetCategoriesForAccountsAsync(),
                    "CategoryId", "CategoryName", account.CategoryId);
                ViewBag.AccountGroups = new SelectList(
                    await _metaDataService.GetAccountGroupsAsync(),
                    "AccountGroupId", "AccountGroupName", account.AccountGroupId);

                return View(account);
            }

            TempData["SuccessMessage"] = "Account updated successfully.";
            return RedirectToAction(nameof(Accounts));
        }

        // POST: /MetaData/DeleteAccount/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount(int id)
        {
            var (success, associatedVoucherIds) = await _metaDataService.DeleteAccountAsync(id);
            
            if (!success)
            {
                if (associatedVoucherIds.Any())
                {
                    TempData["ErrorMessage"] = $"This account cannot be deleted because it is associated with journal voucher(s): {string.Join(", ", associatedVoucherIds.Take(5))}";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to delete the account.";
                }
                return RedirectToAction(nameof(Accounts));
            }

            TempData["SuccessMessage"] = "Account deleted successfully.";
            return RedirectToAction(nameof(Accounts));
        }

        // GET: /MetaData/CanDeleteAccount/5
        [HttpGet]
        public async Task<IActionResult> CanDeleteAccount(int id)
        {
            // Check if account exists
            var account = await _metaDataService.GetAccountByIdAsync(id);
            if (account == null)
            {
                return Json(new { canDelete = false, message = "Account not found." });
            }

            // Check for associated journal entries
            var associatedVoucherIds = await _metaDataService.GetAssociatedVoucherIdsAsync(id);
            
            if (associatedVoucherIds.Any())
            {
                // Take just first few to avoid making message too long
                var idsToShow = associatedVoucherIds.Count > 5 
                    ? associatedVoucherIds.Take(5).ToList() 
                    : associatedVoucherIds;
                    
                string idList = string.Join(", ", idsToShow);
                string message = $"This account cannot be deleted because it is associated with journal voucher(s): {idList}";
                
                if (associatedVoucherIds.Count > 5)
                {
                    message += $" and {associatedVoucherIds.Count - 5} more";
                }
                
                return Json(new { canDelete = false, message });
            }
            
            return Json(new { canDelete = true });
        }

        // POST: /MetaData/GetCategoriesByProject
        [HttpGet]
        [HttpPost]
        public async Task<IActionResult> GetCategoriesByProject(int projectId)
                {
            var categories = await _metaDataService.GetCategoriesByProjectAsync(projectId);
            return Json(categories);
        }

        #endregion

        #region Location

        // GET: /MetaData/Locations
        public async Task<IActionResult> Locations()
        {
            var locations = await _metaDataService.GetAllLocationsAsync();
            return View(locations);
        }

        // GET: /MetaData/CreateLocation
        public IActionResult CreateLocation()
        {
            return View();
        }

        // POST: /MetaData/CreateLocation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLocation(Location location)
        {
            var errors = await _validationService.ValidateLocationAsync(location);
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                    ModelState.AddModelError("", error);

                return View(location);
            }

            var creationErrors = await _metaDataService.CreateLocationAsync(location);
            if (creationErrors.Count > 0)
            {
                foreach (var error in creationErrors)
                    ModelState.AddModelError("", error);

                return View(location);
            }

            TempData["SuccessMessage"] = "Location created successfully.";
            return RedirectToAction(nameof(Locations));
        }


        // GET: /MetaData/EditLocation/5
        public async Task<IActionResult> EditLocation(int id)
        {
            var location = await _metaDataService.GetLocationByIdAsync(id);
            if (location == null)
            {
                return NotFound();
            }
            return View(location);
        }

        // POST: /MetaData/EditLocation/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLocation(Location location)
        {
            var errors = await _validationService.ValidateLocationAsync(location);
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    ModelState.AddModelError("", error);
                }
                return View(location);
            }

            var updateErrors = await _metaDataService.UpdateLocationAsync(location);
            if (updateErrors.Count > 0)
            {
                foreach (var error in updateErrors)
                {
                    ModelState.AddModelError("", error);
                }
                return View(location);
            }

            TempData["SuccessMessage"] = "Location updated successfully.";
            return RedirectToAction(nameof(Locations));
        }

        #endregion

        #region Booker

        // Add these methods to MetaDataController class
        #region Booker

        public async Task<IActionResult> Bookers()
        {
            var bookers = await _metaDataService.GetAllBookersAsync();
            return View(bookers);
        }

        public IActionResult CreateBooker()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBooker(Booker booker)
        {
            if (!ModelState.IsValid)
            {
                return View(booker);
            }

            var errors = await _metaDataService.CreateBookerAsync(booker);
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    ModelState.AddModelError("", error);
                }
                return View(booker);
            }

            TempData["SuccessMessage"] = "Booker created successfully.";
            return RedirectToAction(nameof(Bookers));
        }

        public async Task<IActionResult> EditBooker(int id)
        {
            var booker = await _metaDataService.GetBookerByIdAsync(id);
            if (booker == null)
            {
                return NotFound();
            }
            return View(booker);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBooker(Booker booker)
        {
            if (!ModelState.IsValid)
            {
                return View(booker);
            }

            var errors = await _metaDataService.UpdateBookerAsync(booker);
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    ModelState.AddModelError("", error);
                }
                return View(booker);
            }

            TempData["SuccessMessage"] = "Booker updated successfully.";
            return RedirectToAction(nameof(Bookers));
        }

        #endregion

        #endregion
    }
}