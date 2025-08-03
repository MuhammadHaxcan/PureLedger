using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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

        // GET: /MetaData/Projects
        public async Task<IActionResult> Projects()
        {
            var projects = await _metaDataService.GetAllProjectsAsync();
            return View(projects);
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
            ViewBag.Projects = new SelectList(
                await _metaDataService.GetAllProjectsForCategoriesAsync(),
                "ProjectId", "ProjectName");
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

                ViewBag.Projects = new SelectList(
                    await _metaDataService.GetAllProjectsForCategoriesAsync(),
                    "ProjectId", "ProjectName", category.ProjectId);
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

                ViewBag.Projects = new SelectList(
                    await _metaDataService.GetAllProjectsForCategoriesAsync(),
                    "ProjectId", "ProjectName", category.ProjectId);
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

            ViewBag.Projects = new SelectList(
                await _metaDataService.GetAllProjectsForCategoriesAsync(),
                "ProjectId", "ProjectName", category.ProjectId);
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

                ViewBag.Projects = new SelectList(
                    await _metaDataService.GetAllProjectsForCategoriesAsync(),
                    "ProjectId", "ProjectName", category.ProjectId);
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

                ViewBag.Projects = new SelectList(
                    await _metaDataService.GetAllProjectsForCategoriesAsync(),
                    "ProjectId", "ProjectName", category.ProjectId);
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
            ViewBag.Projects = new SelectList(
                await _metaDataService.GetProjectsForAccountsAsync(),
                "ProjectId", "ProjectName");
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
                ViewBag.Projects = new SelectList(
                    await _metaDataService.GetProjectsForAccountsAsync(),
                    "ProjectId", "ProjectName", selectedProjectId);

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
                ViewBag.Projects = new SelectList(
                    await _metaDataService.GetProjectsForAccountsAsync(),
                    "ProjectId", "ProjectName", selectedProjectId);

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

        // POST: /MetaData/GetCategoriesByProject
        [HttpPost]
        public async Task<IActionResult> GetCategoriesByProject(int projectId)
        {
            var categories = await _metaDataService.GetCategoriesByProjectAsync(projectId);
            return Json(categories);
        }

        #endregion

        #region Tag

        // GET: /MetaData/Tags
        public async Task<IActionResult> Tags()
        {
            var tags = await _metaDataService.GetAllTagsAsync();
            return View(tags);
        }

        // GET: /MetaData/CreateTag
        public IActionResult CreateTag()
        {
            return View();
        }

        // POST: /MetaData/CreateTag
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTag(Tag tag)
        {
            var errors = await _validationService.ValidateTagAsync(tag);
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                    ModelState.AddModelError("", error);

                var tags = await _metaDataService.GetAllTagsAsync();
                return View("Tags", tags); 
            }

            var creationErrors = await _metaDataService.CreateTagAsync(tag);
            if (creationErrors.Count > 0)
            {
                foreach (var error in creationErrors)
                    ModelState.AddModelError("", error);

                var tags = await _metaDataService.GetAllTagsAsync();
                return View("Tags", tags);
            }

            TempData["SuccessMessage"] = "Tag created successfully.";
            return RedirectToAction(nameof(Tags));
        }

        // GET: /MetaData/DeleteTag/5
        public async Task<IActionResult> DeleteTag(int id)
        {
            var tag = await _metaDataService.GetAllTagsAsync()
                .ContinueWith(t => t.Result.FirstOrDefault(tag => tag.TagId == id));

            if (tag == null)
            {
                return NotFound();
            }

            if (await _metaDataService.IsTagInUseAsync(id))
            {
                TempData["ErrorMessage"] = "This tag is in use and cannot be deleted.";
                return RedirectToAction(nameof(Tags));
            }

            return View(tag);
        }

        // POST: /MetaData/DeleteTag/5
        [HttpPost, ActionName("DeleteTag")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTagConfirmed(int id)
        {
            if (await _metaDataService.IsTagInUseAsync(id))
            {
                TempData["ErrorMessage"] = "This tag is in use and cannot be deleted.";
                return RedirectToAction(nameof(Tags));
            }

            await _metaDataService.DeleteTagAsync(id);
            TempData["SuccessMessage"] = "Tag deleted successfully.";
            return RedirectToAction(nameof(Tags));
        }

        // GET: /MetaData/SearchTags
        public async Task<IActionResult> SearchTags(string term)
        {
            var tags = await _metaDataService.SearchTagsAsync(term);
            return Json(tags);
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
    }
}