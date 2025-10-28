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

            // Check if category is associated with any accounts
            var associatedAccountIds = await _metaDataService.GetAssociatedAccountIdsAsync(id);
            bool hasAssociations = associatedAccountIds.Any();

            var userId = _currentUserService.GetCurrentUserId();
            var projects = userId.HasValue
                ? await _metaDataService.GetAllProjectsForCategoriesForUserAsync(userId.Value)
                : new List<Project>();

            ViewBag.Projects = new SelectList(projects, "ProjectId", "ProjectName", category.ProjectId);
            ViewBag.AccountTypes = new SelectList(
                await _metaDataService.GetAllAccountTypesAsync(),
                "AccountTypeId", "AccountTypeName", category.AccountTypeId);

            // Pass the association status to the view
            ViewBag.HasAccountAssociations = hasAssociations;
            ViewBag.AssociatedAccountCount = associatedAccountIds.Count;
            
            if (hasAssociations)
            {
                // Get account names for better user experience
                var allAccounts = await _metaDataService.GetAllAccountsAsync();
                var associatedAccounts = allAccounts.Where(a => associatedAccountIds.Contains(a.AccountId))
                                                   .Select(a => a.AccountName)
                                                   .ToList();

                var accountsToShow = associatedAccounts.Count > 5 
                    ? associatedAccounts.Take(5).ToList() 
                    : associatedAccounts;
                ViewBag.AssociatedAccountNames = string.Join(", ", accountsToShow);
                
                if (associatedAccounts.Count > 5)
                {
                    ViewBag.AssociatedAccountMessage = $"This category is associated with {associatedAccounts.Count} accounts (showing first 5: {ViewBag.AssociatedAccountNames} and {associatedAccounts.Count - 5} more). Only the category name can be changed.";
                }
                else
                {
                    ViewBag.AssociatedAccountMessage = $"This category is associated with account(s): {ViewBag.AssociatedAccountNames}. Only the category name can be changed.";
                }
            }

            return View(category);
        }

        // POST: /MetaData/EditCategory/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(Category category)
        {
            // Check if category is associated with accounts
            var associatedAccountIds = await _metaDataService.GetAssociatedAccountIdsAsync(category.CategoryId);
            bool hasAssociations = associatedAccountIds.Any();

            // If category has associations, get the original category data to preserve ProjectId and AccountTypeId
            if (hasAssociations)
            {
                var originalCategory = await _metaDataService.GetCategoryByIdAsync(category.CategoryId);
                if (originalCategory != null)
                {
                    // Preserve the original ProjectId and AccountTypeId
                    category.ProjectId = originalCategory.ProjectId;
                    category.AccountTypeId = originalCategory.AccountTypeId;
                }
                
                // Add a custom validation message if user tried to change restricted fields
                if (Request.Form["ProjectId"] != originalCategory?.ProjectId.ToString() || 
                    Request.Form["AccountTypeId"] != originalCategory?.AccountTypeId.ToString())
                {
                    ModelState.AddModelError("", "Project and Account Type cannot be changed because this category is associated with accounts.");
                }
            }

            var errors = await _validationService.ValidateCategoryAsync(category);
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    ModelState.AddModelError("", error);
                }

                // Reload ViewBag data for the form
                var userId = _currentUserService.GetCurrentUserId();
                var projects = userId.HasValue
                    ? await _metaDataService.GetAllProjectsForCategoriesForUserAsync(userId.Value)
                    : new List<Project>();

                ViewBag.Projects = new SelectList(projects, "ProjectId", "ProjectName", category.ProjectId);
                ViewBag.AccountTypes = new SelectList(
                    await _metaDataService.GetAllAccountTypesAsync(),
                    "AccountTypeId", "AccountTypeName", category.AccountTypeId);

                // Pass association status back to view
                ViewBag.HasAccountAssociations = hasAssociations;
                ViewBag.AssociatedAccountCount = associatedAccountIds.Count;
                
                if (hasAssociations)
                {
                    var allAccounts = await _metaDataService.GetAllAccountsAsync();
                    var associatedAccounts = allAccounts.Where(a => associatedAccountIds.Contains(a.AccountId))
                                                       .Select(a => a.AccountName)
                                                       .ToList();

                    var accountsToShow = associatedAccounts.Count > 5 
                        ? associatedAccounts.Take(5).ToList() 
                        : associatedAccounts;
                    ViewBag.AssociatedAccountNames = string.Join(", ", accountsToShow);
                    
                    if (associatedAccounts.Count > 5)
                    {
                        ViewBag.AssociatedAccountMessage = $"This category is associated with {associatedAccounts.Count} accounts (showing first 5: {ViewBag.AssociatedAccountNames} and {associatedAccounts.Count - 5} more). Only the category name can be changed.";
                    }
                    else
                    {
                        ViewBag.AssociatedAccountMessage = $"This category is associated with account(s): {ViewBag.AssociatedAccountNames}. Only the category name can be changed.";
                    }
                }

                return View(category);
            }

            var updateErrors = await _metaDataService.UpdateCategoryAsync(category);
            if (updateErrors.Count > 0)
            {
                foreach (var error in updateErrors)
                    ModelState.AddModelError("", error);

                // Reload ViewBag data for the form
                var userId = _currentUserService.GetCurrentUserId();
                var projects = userId.HasValue
                    ? await _metaDataService.GetAllProjectsForCategoriesForUserAsync(userId.Value)
                    : new List<Project>();

                ViewBag.Projects = new SelectList(projects, "ProjectId", "ProjectName", category.ProjectId);
                ViewBag.AccountTypes = new SelectList(
                    await _metaDataService.GetAllAccountTypesAsync(),
                    "AccountTypeId", "AccountTypeName", category.AccountTypeId);

                // Pass association status back to view
                ViewBag.HasAccountAssociations = hasAssociations;
                ViewBag.AssociatedAccountCount = associatedAccountIds.Count;
                
                if (hasAssociations)
                {
                    var allAccounts = await _metaDataService.GetAllAccountsAsync();
                    var associatedAccounts = allAccounts.Where(a => associatedAccountIds.Contains(a.AccountId))
                                                       .Select(a => a.AccountName)
                                                       .ToList();

                    var accountsToShow = associatedAccounts.Count > 5 
                        ? associatedAccounts.Take(5).ToList() 
                        : associatedAccounts;
                    ViewBag.AssociatedAccountNames = string.Join(", ", accountsToShow);
                    
                    if (associatedAccounts.Count > 5)
                    {
                        ViewBag.AssociatedAccountMessage = $"This category is associated with {associatedAccounts.Count} accounts (showing first 5: {ViewBag.AssociatedAccountNames} and {associatedAccounts.Count - 5} more). Only the category name can be changed.";
                    }
                    else
                    {
                        ViewBag.AssociatedAccountMessage = $"This category is associated with account(s): {ViewBag.AssociatedAccountNames}. Only the category name can be changed.";
                    }
                }

                return View(category);
            }

            TempData["SuccessMessage"] = "Category updated successfully.";
            return RedirectToAction(nameof(Categories));
        }

        // GET: /MetaData/CanDeleteCategory/5
        [HttpGet]
        public async Task<IActionResult> CanDeleteCategory(int id)
        {
            // Check if category exists
            var category = await _metaDataService.GetCategoryByIdAsync(id);
            if (category == null)
            {
                return Json(new { canDelete = false, message = "Category not found." });
            }

            // Check for associated accounts
            var associatedAccountIds = await _metaDataService.GetAssociatedAccountIdsAsync(id);

            if (associatedAccountIds.Any())
            {
                // Get account names for better user experience
                var allAccounts = await _metaDataService.GetAllAccountsAsync();
                var associatedAccounts = allAccounts.Where(a => associatedAccountIds.Contains(a.AccountId))
                                                   .Select(a => a.AccountName)
                                                   .ToList();

                // Take just first few to avoid making message too long
                var accountsToShow = associatedAccounts.Count > 5
                    ? associatedAccounts.Take(5).ToList()
                    : associatedAccounts;

                string accountList = string.Join(", ", accountsToShow);
                string message = $"This category cannot be deleted because it is associated with account(s): {accountList}";

                if (associatedAccounts.Count > 5)
                {
                    message += $" and {associatedAccounts.Count - 5} more";
                }

                return Json(new { canDelete = false, message });
            }

            return Json(new { canDelete = true });
        }

        // POST: /MetaData/DeleteCategory/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var (success, associatedAccountIds) = await _metaDataService.DeleteCategoryAsync(id);

            if (!success)
            {
                if (associatedAccountIds.Any())
                {
                    // Get account names for better user experience
                    var allAccounts = await _metaDataService.GetAllAccountsAsync();
                    var associatedAccounts = allAccounts.Where(a => associatedAccountIds.Contains(a.AccountId))
                                                       .Select(a => a.AccountName)
                                                       .Take(5)
                                                       .ToList();

                    TempData["ErrorMessage"] = $"This category cannot be deleted because it is associated with account(s): {string.Join(", ", associatedAccounts)}";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to delete the category.";
                }
                return RedirectToAction(nameof(Categories));
            }

            TempData["SuccessMessage"] = "Category deleted successfully.";
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

            // Check if account is associated with any journal vouchers
            var associatedVoucherIds = await _metaDataService.GetAssociatedVoucherIdsAsync(id);
            bool hasAssociations = associatedVoucherIds.Any();

            ViewBag.Categories = new SelectList(
                await _metaDataService.GetCategoriesForAccountsAsync(),
                "CategoryId", "CategoryName", account.CategoryId);
            ViewBag.AccountGroups = new SelectList(
                await _metaDataService.GetAccountGroupsAsync(),
                "AccountGroupId", "AccountGroupName", account.AccountGroupId);

            // Pass the association status to the view
            ViewBag.HasJournalAssociations = hasAssociations;
            ViewBag.AssociatedVoucherCount = associatedVoucherIds.Count;
            
            if (hasAssociations)
            {
                // Provide information about the associations
                var voucherIdsToShow = associatedVoucherIds.Count > 5 
                    ? associatedVoucherIds.Take(5).ToList() 
                    : associatedVoucherIds;
                ViewBag.AssociatedVoucherIds = string.Join(", ", voucherIdsToShow);
                
                if (associatedVoucherIds.Count > 5)
                {
                    ViewBag.AssociatedVoucherMessage = $"This account is associated with {associatedVoucherIds.Count} journal vouchers (showing first 5: {ViewBag.AssociatedVoucherIds} and {associatedVoucherIds.Count - 5} more). Category and Account Group cannot be changed.";
                }
                else
                {
                    ViewBag.AssociatedVoucherMessage = $"This account is associated with journal voucher(s): {ViewBag.AssociatedVoucherIds}. Category and Account Group cannot be changed.";
                }
            }

            return View(account);
        }

        // POST: /MetaData/EditAccount/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAccount(Account account)
        {
            // Check if account is associated with journal vouchers
            var associatedVoucherIds = await _metaDataService.GetAssociatedVoucherIdsAsync(account.AccountId);
            bool hasAssociations = associatedVoucherIds.Any();

            // If account has associations, get the original account data to preserve CategoryId and AccountGroupId
            if (hasAssociations)
            {
                var originalAccount = await _metaDataService.GetAccountByIdAsync(account.AccountId);
                if (originalAccount != null)
                {
                    // Preserve the original CategoryId and AccountGroupId
                    account.CategoryId = originalAccount.CategoryId;
                    account.AccountGroupId = originalAccount.AccountGroupId;
                }
                
                // Add a custom validation message if user tried to change restricted fields
                if (Request.Form["CategoryId"] != originalAccount?.CategoryId.ToString() || 
                    Request.Form["AccountGroupId"] != originalAccount?.AccountGroupId.ToString())
                {
                    ModelState.AddModelError("", "Category and Account Group cannot be changed because this account is associated with journal vouchers.");
                }
            }

            var errors = await _validationService.ValidateAccountAsync(account);
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    ModelState.AddModelError("", error);
                }

                // Reload ViewBag data for the form
                ViewBag.Categories = new SelectList(
                    await _metaDataService.GetCategoriesForAccountsAsync(),
                    "CategoryId", "CategoryName", account.CategoryId);
                ViewBag.AccountGroups = new SelectList(
                    await _metaDataService.GetAccountGroupsAsync(),
                    "AccountGroupId", "AccountGroupName", account.AccountGroupId);

                // Pass association status back to view
                ViewBag.HasJournalAssociations = hasAssociations;
                ViewBag.AssociatedVoucherCount = associatedVoucherIds.Count;
                
                if (hasAssociations)
                {
                    var voucherIdsToShow = associatedVoucherIds.Count > 5 
                        ? associatedVoucherIds.Take(5).ToList() 
                        : associatedVoucherIds;
                    ViewBag.AssociatedVoucherIds = string.Join(", ", voucherIdsToShow);
                    
                    if (associatedVoucherIds.Count > 5)
                    {
                        ViewBag.AssociatedVoucherMessage = $"This account is associated with {associatedVoucherIds.Count} journal vouchers (showing first 5: {ViewBag.AssociatedVoucherIds} and {associatedVoucherIds.Count - 5} more). Category and Account Group cannot be changed.";
                    }
                    else
                    {
                        ViewBag.AssociatedVoucherMessage = $"This account is associated with journal voucher(s): {ViewBag.AssociatedVoucherIds}. Category and Account Group cannot be changed.";
                    }
                }

                return View(account);
            }

            var updateErrors = await _metaDataService.UpdateAccountAsync(account);
            if (updateErrors.Count > 0)
            {
                foreach (var error in updateErrors)
                {
                    ModelState.AddModelError("", error);
                }

                // Reload ViewBag data for the form
                ViewBag.Categories = new SelectList(
                    await _metaDataService.GetCategoriesForAccountsAsync(),
                    "CategoryId", "CategoryName", account.CategoryId);
                ViewBag.AccountGroups = new SelectList(
                    await _metaDataService.GetAccountGroupsAsync(),
                    "AccountGroupId", "AccountGroupName", account.AccountGroupId);

                // Pass association status back to view
                ViewBag.HasJournalAssociations = hasAssociations;
                ViewBag.AssociatedVoucherCount = associatedVoucherIds.Count;
                
                if (hasAssociations)
                {
                    var voucherIdsToShow = associatedVoucherIds.Count > 5 
                        ? associatedVoucherIds.Take(5).ToList() 
                        : associatedVoucherIds;
                    ViewBag.AssociatedVoucherIds = string.Join(", ", voucherIdsToShow);
                    
                    if (associatedVoucherIds.Count > 5)
                    {
                        ViewBag.AssociatedVoucherMessage = $"This account is associated with {associatedVoucherIds.Count} journal vouchers (showing first 5: {ViewBag.AssociatedVoucherIds} and {associatedVoucherIds.Count - 5} more). Category and Account Group cannot be changed.";
                    }
                    else
                    {
                        ViewBag.AssociatedVoucherMessage = $"This account is associated with journal voucher(s): {ViewBag.AssociatedVoucherIds}. Category and Account Group cannot be changed.";
                    }
                }

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

        // Add these methods to the Account region

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAccountStatus(int id)
        {
            try
            {
                var errors = await _metaDataService.ToggleAccountStatusAsync(id);

                if (errors.Any())
                {
                    TempData["ErrorMessage"] = string.Join("; ", errors);
                }
                else
                {
                    // Get the updated account to show the correct message
                    var account = await _metaDataService.GetAccountByIdAsync(id);
                    if (account != null)
                    {
                        TempData["SuccessMessage"] = $"Account '{account.AccountName}' has been {(account.IsEnabled ? "enabled" : "disabled")} successfully.";
                    }
                    else
                    {
                        TempData["SuccessMessage"] = "Account status updated successfully.";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling account status for ID {AccountId}", id);
                TempData["ErrorMessage"] = "An error occurred while updating the account status.";
            }

            return RedirectToAction(nameof(Accounts));
        }

        // Also add this AJAX endpoint for real-time updates
        [HttpPost]
        public async Task<IActionResult> ToggleAccountStatusAjax(int id)
        {
            try
            {
                var errors = await _metaDataService.ToggleAccountStatusAsync(id);

                if (errors.Any())
                {
                    return Json(new { success = false, message = string.Join("; ", errors) });
                }

                // Get the updated account status
                var account = await _metaDataService.GetAccountByIdAsync(id);
                if (account != null)
                {
                    return Json(new
                    {
                        success = true,
                        isEnabled = account.IsEnabled,
                        message = $"Account '{account.AccountName}' has been {(account.IsEnabled ? "enabled" : "disabled")} successfully."
                    });
                }

                return Json(new { success = true, message = "Account status updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling account status for ID {AccountId}", id);
                return Json(new { success = false, message = "An error occurred while updating the account status." });
            }

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

        // Add this method to the Booker region
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBookerStatus(int id)
        {
            try
            {
                var errors = await _metaDataService.ToggleBookerStatusAsync(id);
                
                if (errors.Any())
                {
                    TempData["ErrorMessage"] = string.Join("; ", errors);
                }
                else
                {
                    // Get the updated booker to show the correct message
                    var booker = await _metaDataService.GetBookerByIdAsync(id);
                    if (booker != null)
                    {
                        TempData["SuccessMessage"] = $"Booker '{booker.BookerName}' has been {(booker.IsEnabled ? "enabled" : "disabled")} successfully.";
                    }
                    else
                    {
                        TempData["SuccessMessage"] = "Booker status updated successfully.";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling booker status for ID {BookerId}", id);
                TempData["ErrorMessage"] = "An error occurred while updating the booker status.";
            }

            return RedirectToAction(nameof(Bookers));
        }

        // Also add this AJAX endpoint for real-time updates
        [HttpPost]
        public async Task<IActionResult> ToggleBookerStatusAjax(int id)
        {
            try
            {
                var errors = await _metaDataService.ToggleBookerStatusAsync(id);
                
                if (errors.Any())
                {
                    return Json(new { success = false, message = string.Join("; ", errors) });
                }

                // Get the updated booker status
                var booker = await _metaDataService.GetBookerByIdAsync(id);
                if (booker != null)
                {
                    return Json(new { 
                        success = true, 
                        isEnabled = booker.IsEnabled,
                        message = $"Booker '{booker.BookerName}' has been {(booker.IsEnabled ? "enabled" : "disabled")} successfully."
                    });
                }

                return Json(new { success = true, message = "Booker status updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling booker status for ID {BookerId}", id);
                return Json(new { success = false, message = "An error occurred while updating the booker status." });
            }
        }

        #endregion


        #region New Methods

        [HttpGet]
        public async Task<IActionResult> GetAllProjectsForCategories()
        {
            try
            {
                _logger.LogInformation("GetAllProjectsForCategories called");
                
                var userId = _currentUserService.GetCurrentUserId();
                _logger.LogInformation("Current user ID: {UserId}", userId);
                
                var projects = userId.HasValue
                    ? await _metaDataService.GetAllProjectsForCategoriesForUserAsync(userId.Value)
                    : new List<Project>();

                _logger.LogInformation("Found {ProjectCount} projects", projects.Count);
                
                var result = projects.Select(p => new {
                    projectId = p.ProjectId,
                    projectName = p.ProjectName
                }).ToList();

                _logger.LogInformation("Returning {ResultCount} project results", result.Count);
                
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading projects in GetAllProjectsForCategories");
                return BadRequest(new { error = $"Error loading projects: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCategoriesByProjectForView(int projectId)
        {
            try
            {
                _logger.LogInformation("GetCategoriesByProjectForView called with projectId: {ProjectId}", projectId);
                
                var categories = await _metaDataService.GetCategoriesByProjectAsync(projectId);
                _logger.LogInformation("Found {CategoryCount} categories for project {ProjectId}", categories.Count, projectId);
                
                // Get full category details with navigation properties
                var fullCategories = new List<object>();
                foreach (var category in categories)
                {
                    dynamic categoryData = category;
                    int categoryId = categoryData.categoryId;

                    var fullCategory = await _metaDataService.GetCategoryByIdAsync(categoryId);
                    if (fullCategory != null)
                    {
                        fullCategories.Add(new
                        {
                            categoryId = fullCategory.CategoryId,
                            categoryName = fullCategory.CategoryName,
                            projectId = fullCategory.ProjectId,
                            accountTypeId = fullCategory.AccountTypeId,
                            project = new
                            {
                                projectId = fullCategory.Project?.ProjectId,
                                projectName = fullCategory.Project?.ProjectName
                            },
                            accountType = new
                            {
                                accountTypeId = fullCategory.AccountType?.AccountTypeId,
                                accountTypeName = fullCategory.AccountType?.AccountTypeName
                            }
                        });
                    }
                }

                _logger.LogInformation("Returning {FullCategoryCount} full category results", fullCategories.Count);
                return Json(fullCategories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading categories for project {ProjectId}", projectId);
                return BadRequest(new { error = $"Error loading categories: {ex.Message}" });
            }
        }

        #endregion

        #region New Account Methods

        [HttpGet]
        public async Task<IActionResult> GetAllProjectsForAccounts()
        {
            try
            {
                _logger.LogInformation("GetAllProjectsForAccounts called");
                
                var userId = _currentUserService.GetCurrentUserId();
                _logger.LogInformation("Current user ID: {UserId}", userId);
                
                var projects = userId.HasValue
                    ? await _metaDataService.GetProjectsForAccountsForUserAsync(userId.Value)
                    : new List<Project>();

                _logger.LogInformation("Found {ProjectCount} projects", projects.Count);
                
                var result = projects.Select(p => new {
                    projectId = p.ProjectId,
                    projectName = p.ProjectName
                }).ToList();

                _logger.LogInformation("Returning {ResultCount} project results", result.Count);
                
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading projects in GetAllProjectsForAccounts");
                return BadRequest(new { error = $"Error loading projects: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAccountsByCriteria(int projectId, int? categoryId = null)
        {
            try
            {
                _logger.LogInformation("GetAccountsByCriteria called with projectId: {ProjectId}, categoryId: {CategoryId}", projectId, categoryId);
                
                // Get all accounts with details
                var allAccounts = await _metaDataService.GetAllAccountsAsync();
                
                // Filter by project (through category relationship)
                var filteredAccounts = allAccounts.Where(a => 
                    a.Category != null && 
                    a.Category.ProjectId == projectId &&
                    (!categoryId.HasValue || a.CategoryId == categoryId.Value)
                ).Select(a => new {
                    accountId = a.AccountId,
                    accountName = a.AccountName,
                    categoryId = a.CategoryId,
                    categoryName = a.Category?.CategoryName,
                    accountGroupId = a.AccountGroupId,
                    accountGroupName = a.AccountGroup?.AccountGroupName,
                    projectId = a.Category?.ProjectId,
                    projectName = a.Category?.Project?.ProjectName,
                    isEnabled = a.IsEnabled  // Add this property
                }).ToList();

                _logger.LogInformation("Returning {AccountCount} accounts for project {ProjectId}", filteredAccounts.Count, projectId);
                
                return Json(filteredAccounts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading accounts for project {ProjectId}, category {CategoryId}", projectId, categoryId);
                return BadRequest(new { error = $"Error loading accounts: {ex.Message}" });
            }
        }

        #endregion
    }
}