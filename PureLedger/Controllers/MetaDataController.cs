using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PureLedger.Models;
using PureLedger.Services;

namespace PureLedger.Controllers
{
    public class MetaDataController : Controller
    {
        private readonly MetaDataValidationService _validationService;
        private readonly IMetaDataService _metaDataService;
        private readonly ILogger<MetaDataController> _logger;

        public MetaDataController(
            MetaDataValidationService validationService,
            IMetaDataService metaDataService,
            ILogger<MetaDataController> logger)
        {
            _validationService = validationService;
            _metaDataService = metaDataService;
            _logger = logger;
        }

        #region Project

        // GET: /MetaData/Project
        [HttpGet]
        public async Task<IActionResult> Project()
        {
            var projects = await _metaDataService.GetAllProjectsAsync();
            return View(projects);
        }

        // POST: /MetaData/Project
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Project(Project project)
        {
            var errors = await _validationService.ValidateProjectAsync(project);
            if (errors.Count == 0)
            {
                await _metaDataService.CreateProjectAsync(project);
                return RedirectToAction(nameof(Project));
            }
            foreach (var error in errors)
            {
                ModelState.AddModelError("", error);
            }
            var projects = await _metaDataService.GetAllProjectsAsync();
            return View(projects);
        }

        // GET: /MetaData/EditProject/5
        [HttpGet]
        public async Task<IActionResult> EditProject(int id)
        {
            var project = await _metaDataService.GetProjectByIdAsync(id);
            if (project == null) return NotFound();
            return View(project);
        }

        // POST: /MetaData/EditProject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProject(Project project)
        {
            var errors = await _validationService.ValidateProjectAsync(project);
            if (errors.Count == 0)
            {
                await _metaDataService.UpdateProjectAsync(project);
                return RedirectToAction(nameof(Project));
            }
            foreach (var error in errors)
            {
                ModelState.AddModelError("", error);
            }
            return View(project);
        }

        #endregion

        #region Category

        // GET: /MetaData/Category
        [HttpGet]
        public async Task<IActionResult> Category()
        {
            var categories = await _metaDataService.GetAllCategoriesAsync();
            ViewBag.Projects = await _metaDataService.GetAllProjectsForCategoriesAsync();
            ViewBag.AccountTypes = await _metaDataService.GetAllAccountTypesAsync();
            return View(categories);
        }

        // POST: /MetaData/Category
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Category(Category category)
        {
            var errors = await _validationService.ValidateCategoryAsync(category);
            if (errors.Count == 0)
            {
                await _metaDataService.CreateCategoryAsync(category);
                return RedirectToAction(nameof(Category));
            }
            foreach (var error in errors)
            {
                ModelState.AddModelError("", error);
            }
            var categories = await _metaDataService.GetAllCategoriesAsync();
            ViewBag.Projects = await _metaDataService.GetAllProjectsForCategoriesAsync();
            ViewBag.AccountTypes = await _metaDataService.GetAllAccountTypesAsync();
            return View(categories);
        }

        // GET: /MetaData/EditCategory/5
        [HttpGet]
        public async Task<IActionResult> EditCategory(int id)
        {
            var category = await _metaDataService.GetCategoryByIdAsync(id);
            if (category == null) return NotFound();
            ViewBag.Projects = await _metaDataService.GetAllProjectsForCategoriesAsync();
            ViewBag.AccountTypes = await _metaDataService.GetAllAccountTypesAsync();
            return View(category);
        }

        // POST: /MetaData/EditCategory/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(Category category)
        {
            var errors = await _validationService.ValidateCategoryAsync(category);
            if (errors.Count == 0)
            {
                await _metaDataService.UpdateCategoryAsync(category);
                return RedirectToAction(nameof(Category));
            }
            foreach (var error in errors)
            {
                ModelState.AddModelError("", error);
            }
            ViewBag.Projects = await _metaDataService.GetAllProjectsForCategoriesAsync();
            ViewBag.AccountTypes = await _metaDataService.GetAllAccountTypesAsync();
            return View(category);
        }

        #endregion

        #region Account

        // GET: /MetaData/Account
        [HttpGet]
        public async Task<IActionResult> Account()
        {
            var accounts = await _metaDataService.GetAllAccountsAsync();
            ViewBag.Categories = await _metaDataService.GetCategoriesForAccountsAsync();
            ViewBag.AccountGroups = await _metaDataService.GetAccountGroupsAsync();
            ViewBag.Projects = await _metaDataService.GetProjectsForAccountsAsync();
            return View(accounts);
        }

        // POST: /MetaData/Account
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Account(Account account)
        {
            var errors = await _validationService.ValidateAccountAsync(account);
            if (errors.Count == 0)
            {
                await _metaDataService.CreateAccountAsync(account);
                return RedirectToAction(nameof(Account));
            }
            foreach (var error in errors)
            {
                ModelState.AddModelError("", error);
            }
            var accounts = await _metaDataService.GetAllAccountsAsync();
            ViewBag.Categories = await _metaDataService.GetCategoriesForAccountsAsync();
            ViewBag.AccountGroups = await _metaDataService.GetAccountGroupsAsync();
            ViewBag.Projects = await _metaDataService.GetProjectsForAccountsAsync();
            return View(accounts);
        }

        // GET: /MetaData/EditAccount/5
        [HttpGet]
        public async Task<IActionResult> EditAccount(int id)
        {
            var account = await _metaDataService.GetAccountByIdAsync(id);
            if (account == null) return NotFound();
            ViewBag.Categories = await _metaDataService.GetCategoriesForAccountsAsync();
            ViewBag.AccountGroups = await _metaDataService.GetAccountGroupsAsync();
            return View(account);
        }

        // POST: /MetaData/EditAccount/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAccount(Account account)
        {
            var errors = await _validationService.ValidateAccountAsync(account);
            if (errors.Count == 0)
            {
                await _metaDataService.UpdateAccountAsync(account);
                return RedirectToAction(nameof(Account));
            }
            foreach (var error in errors)
            {
                ModelState.AddModelError("", error);
            }
            ViewBag.Categories = await _metaDataService.GetCategoriesForAccountsAsync();
            ViewBag.AccountGroups = await _metaDataService.GetAccountGroupsAsync();
            return View(account);
        }

        #endregion

        #region Tag

        // GET: /MetaData/Tag
        [HttpGet]
        public async Task<IActionResult> Tag()
        {
            var tags = await _metaDataService.GetAllTagsAsync();
            return View(tags);
        }

        // POST: /MetaData/Tag
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Tag(Tag tag)
        {
            var errors = await _validationService.ValidateTagAsync(tag);
            if (errors.Count == 0)
            {
                await _metaDataService.CreateTagAsync(tag);
                TempData["Success"] = "Tag added successfully.";
                return RedirectToAction(nameof(Tag));
            }
            foreach (var error in errors)
            {
                ModelState.AddModelError("", error);
            }
            var tags = await _metaDataService.GetAllTagsAsync();
            return View(tags);
        }

        // AJAX: Delete Tag
        [HttpPost]
        public async Task<IActionResult> DeleteTag(int id)
        {
            if (await _metaDataService.IsTagInUseAsync(id))
            {
                return Json(new { success = false, message = "This tag is in use and cannot be deleted." });
            }
            var success = await _metaDataService.DeleteTagAsync(id);
            return Json(new { success });
        }

        [HttpGet]
        public async Task<IActionResult> SearchTags(string term)
        {
            var tags = await _metaDataService.SearchTagsAsync(term);
            return Json(tags);
        }

        #endregion

        #region Location

        // GET: /MetaData/Location
        [HttpGet]
        public async Task<IActionResult> Location()
        {
            var locations = await _metaDataService.GetAllLocationsAsync();
            return View(locations);
        }

        // POST: /MetaData/Location
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Location(Location location)
        {
            var errors = await _validationService.ValidateLocationAsync(location);
            if (errors.Count == 0)
            {
                await _metaDataService.CreateLocationAsync(location);
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new
                    {
                        success = true,
                        location = location,
                        message = "Location added successfully."
                    });
                }
                TempData["Success"] = "Location added successfully.";
                return RedirectToAction(nameof(Location));
            }
            foreach (var error in errors)
            {
                ModelState.AddModelError("", error);
            }
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var modelErrors = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .ToDictionary(
                        k => k.Key,
                        v => v.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );
                return Json(new
                {
                    success = false,
                    errors = modelErrors
                });
            }
            var locations = await _metaDataService.GetAllLocationsAsync();
            return View(locations);
        }

        // GET: /MetaData/GetAllLocations - AJAX Endpoint
        [HttpGet]
        public async Task<IActionResult> GetAllLocations()
        {
            var locations = await _metaDataService.GetAllLocationsAsync();
            return Json(locations);
        }

        #endregion
    }
}