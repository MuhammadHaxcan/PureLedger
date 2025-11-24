using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PureLedger.Attributes;
using PureLedger.Models;
using PureLedger.Services;
using PureLedger.Services.Interface;

namespace PureLedger.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [ControllerAccess]
    public class MetaDataApiController : ControllerBase
    {
        private readonly MetaDataValidationService _validationService;
        private readonly IMetaDataService _metaDataService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<MetaDataApiController> _logger;

        public MetaDataApiController(
            MetaDataValidationService validationService,
            IMetaDataService metaDataService,
            ICurrentUserService currentUserService,
            ILogger<MetaDataApiController> logger)
        {
            _validationService = validationService;
            _metaDataService = metaDataService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        #region Projects

        [HttpGet("projects")]
        public async Task<IActionResult> GetProjects()
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var projects = await _metaDataService.GetAllProjectsForUserAsync(userId.Value);
                return Ok(new
                {
                    success = true,
                    data = projects.Select(p => new
                    {
                        p.ProjectId,
                        p.ProjectName
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading projects");
                return StatusCode(500, new { success = false, message = "Error loading projects" });
            }
        }

        [HttpGet("projects/{id}")]
        public async Task<IActionResult> GetProject(int id)
        {
            try
            {
                var project = await _metaDataService.GetProjectByIdAsync(id);
                if (project == null)
                {
                    return NotFound(new { success = false, message = "Project not found" });
                }

                return Ok(new { success = true, data = project });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading project {ProjectId}", id);
                return StatusCode(500, new { success = false, message = "Error loading project" });
            }
        }

        [HttpPost("projects")]
        public async Task<IActionResult> CreateProject([FromBody] Project project)
        {
            try
            {
                var errors = await _validationService.ValidateProjectAsync(project);
                if (errors.Count > 0)
                {
                    return BadRequest(new { success = false, errors });
                }

                var creationErrors = await _metaDataService.CreateProjectAsync(project);
                if (creationErrors.Count > 0)
                {
                    return BadRequest(new { success = false, errors = creationErrors });
                }

                return Ok(new { success = true, message = "Project created successfully", data = project });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project");
                return StatusCode(500, new { success = false, message = "Error creating project" });
            }
        }

        [HttpPut("projects/{id}")]
        public async Task<IActionResult> UpdateProject(int id, [FromBody] Project project)
        {
            try
            {
                if (id != project.ProjectId)
                {
                    return BadRequest(new { success = false, message = "Project ID mismatch" });
                }

                var errors = await _validationService.ValidateProjectAsync(project);
                if (errors.Count > 0)
                {
                    return BadRequest(new { success = false, errors });
                }

                var updateErrors = await _metaDataService.UpdateProjectAsync(project);
                if (updateErrors.Count > 0)
                {
                    return BadRequest(new { success = false, errors = updateErrors });
                }

                return Ok(new { success = true, message = "Project updated successfully", data = project });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project {ProjectId}", id);
                return StatusCode(500, new { success = false, message = "Error updating project" });
            }
        }

        #endregion

        #region Categories

        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories([FromQuery] int? projectId = null)
        {
            try
            {
                var categories = await _metaDataService.GetAllCategoriesAsync();
                
                // Filter by project if provided
                if (projectId.HasValue)
                {
                    categories = categories.Where(c => c.ProjectId == projectId.Value).ToList();
                }
                
                return Ok(new
                {
                    success = true,
                    data = categories.Select(c => new
                    {
                        c.CategoryId,
                        c.CategoryName,
                        c.ProjectId,
                        c.AccountTypeId
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading categories");
                return StatusCode(500, new { success = false, message = "Error loading categories" });
            }
        }

        [HttpGet("categories/{id}")]
        public async Task<IActionResult> GetCategory(int id)
        {
            try
            {
                var category = await _metaDataService.GetCategoryByIdAsync(id);
                if (category == null)
                {
                    return NotFound(new { success = false, message = "Category not found" });
                }

                // Check for associations
                var associatedAccountIds = await _metaDataService.GetAssociatedAccountIdsAsync(id);
                var hasAssociations = associatedAccountIds.Any();

                return Ok(new
                {
                    success = true,
                    data = category,
                    hasAccountAssociations = hasAssociations,
                    associatedAccountCount = associatedAccountIds.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading category {CategoryId}", id);
                return StatusCode(500, new { success = false, message = "Error loading category" });
            }
        }

        [HttpGet("categories/by-project/{projectId}")]
        public async Task<IActionResult> GetCategoriesByProject(int projectId)
        {
            try
            {
                var categories = await _metaDataService.GetCategoriesByProjectAsync(projectId);
                // GetCategoriesByProjectAsync returns List<object>, so we need to handle it differently
                var allCategories = await _metaDataService.GetAllCategoriesAsync();
                var filteredCategories = allCategories.Where(c => c.ProjectId == projectId).ToList();
                return Ok(new
                {
                    success = true,
                    data = filteredCategories.Select(c => new { id = c.CategoryId, name = c.CategoryName })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading categories for project {ProjectId}", projectId);
                return StatusCode(500, new { success = false, message = "Error loading categories" });
            }
        }

        [HttpPost("categories")]
        public async Task<IActionResult> CreateCategory([FromBody] Category category)
        {
            try
            {
                var errors = await _validationService.ValidateCategoryAsync(category);
                if (errors.Count > 0)
                {
                    return BadRequest(new { success = false, errors });
                }

                var creationErrors = await _metaDataService.CreateCategoryAsync(category);
                if (creationErrors.Count > 0)
                {
                    return BadRequest(new { success = false, errors = creationErrors });
                }

                return Ok(new { success = true, message = "Category created successfully", data = category });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating category");
                return StatusCode(500, new { success = false, message = "Error creating category" });
            }
        }

        [HttpPut("categories/{id}")]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] Category category)
        {
            try
            {
                if (id != category.CategoryId)
                {
                    return BadRequest(new { success = false, message = "Category ID mismatch" });
                }

                // Check for associations
                var associatedAccountIds = await _metaDataService.GetAssociatedAccountIdsAsync(id);
                if (associatedAccountIds.Any())
                {
                    // Preserve original ProjectId and AccountTypeId
                    var originalCategory = await _metaDataService.GetCategoryByIdAsync(id);
                    if (originalCategory != null)
                    {
                        category.ProjectId = originalCategory.ProjectId;
                        category.AccountTypeId = originalCategory.AccountTypeId;
                    }
                }

                var errors = await _validationService.ValidateCategoryAsync(category);
                if (errors.Count > 0)
                {
                    return BadRequest(new { success = false, errors });
                }

                var updateErrors = await _metaDataService.UpdateCategoryAsync(category);
                if (updateErrors.Count > 0)
                {
                    return BadRequest(new { success = false, errors = updateErrors });
                }

                return Ok(new { success = true, message = "Category updated successfully", data = category });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category {CategoryId}", id);
                return StatusCode(500, new { success = false, message = "Error updating category" });
            }
        }

        [HttpDelete("categories/{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            try
            {
                var (success, associatedAccountIds) = await _metaDataService.DeleteCategoryAsync(id);
                if (!success)
                {
                    var message = associatedAccountIds.Any()
                        ? "This category cannot be deleted because it is associated with accounts."
                        : "Failed to delete the category.";
                    return BadRequest(new { success = false, message });
                }

                return Ok(new { success = true, message = "Category deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category {CategoryId}", id);
                return StatusCode(500, new { success = false, message = "Error deleting category" });
            }
        }

        [HttpGet("categories/{id}/can-delete")]
        public async Task<IActionResult> CanDeleteCategory(int id)
        {
            try
            {
                var category = await _metaDataService.GetCategoryByIdAsync(id);
                if (category == null)
                {
                    return Ok(new { canDelete = false, message = "Category not found." });
                }

                var associatedAccountIds = await _metaDataService.GetAssociatedAccountIdsAsync(id);
                if (associatedAccountIds.Any())
                {
                    return Ok(new { canDelete = false, message = "This category cannot be deleted because it is associated with accounts." });
                }

                return Ok(new { canDelete = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking category deletion {CategoryId}", id);
                return StatusCode(500, new { success = false, message = "Error checking category" });
            }
        }

        #endregion

        #region Accounts

        [HttpGet("accounts")]
        public async Task<IActionResult> GetAccounts([FromQuery] int? projectId = null, [FromQuery] int? categoryId = null)
        {
            try
            {
                var accounts = await _metaDataService.GetAllAccountsAsync();
                
                // Filter by project if provided
                if (projectId.HasValue)
                {
                    var categories = await _metaDataService.GetAllCategoriesAsync();
                    var projectCategoryIds = categories.Where(c => c.ProjectId == projectId.Value).Select(c => c.CategoryId).ToList();
                    accounts = accounts.Where(a => projectCategoryIds.Contains(a.CategoryId)).ToList();
                }
                
                // Filter by category if provided
                if (categoryId.HasValue)
                {
                    accounts = accounts.Where(a => a.CategoryId == categoryId.Value).ToList();
                }
                
                return Ok(new
                {
                    success = true,
                    data = accounts.Select(a => new
                    {
                        a.AccountId,
                        a.AccountName,
                        a.CategoryId,
                        a.AccountGroupId,
                        a.IsEnabled
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading accounts");
                return StatusCode(500, new { success = false, message = "Error loading accounts" });
            }
        }

        [HttpGet("accounts/{id}")]
        public async Task<IActionResult> GetAccount(int id)
        {
            try
            {
                var account = await _metaDataService.GetAccountByIdAsync(id);
                if (account == null)
                {
                    return NotFound(new { success = false, message = "Account not found" });
                }

                var associatedVoucherIds = await _metaDataService.GetAssociatedVoucherIdsAsync(id);
                var hasAssociations = associatedVoucherIds.Any();

                return Ok(new
                {
                    success = true,
                    data = account,
                    hasJournalAssociations = hasAssociations,
                    associatedVoucherCount = associatedVoucherIds.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading account {AccountId}", id);
                return StatusCode(500, new { success = false, message = "Error loading account" });
            }
        }

        [HttpGet("accounts/by-category/{categoryId}")]
        public async Task<IActionResult> GetAccountsByCategory(int categoryId)
        {
            try
            {
                // GetAccountsByCategoryAsync doesn't exist in IMetaDataService, so filter from all accounts
                var allAccounts = await _metaDataService.GetAllAccountsAsync();
                var filteredAccounts = allAccounts.Where(a => a.CategoryId == categoryId && a.IsEnabled).ToList();
                return Ok(new
                {
                    success = true,
                    data = filteredAccounts.Select(a => new { id = a.AccountId, name = a.AccountName })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading accounts for category {CategoryId}", categoryId);
                return StatusCode(500, new { success = false, message = "Error loading accounts" });
            }
        }

        [HttpPost("accounts")]
        public async Task<IActionResult> CreateAccount([FromBody] Account account)
        {
            try
            {
                var errors = await _validationService.ValidateAccountAsync(account);
                if (errors.Count > 0)
                {
                    return BadRequest(new { success = false, errors });
                }

                var creationErrors = await _metaDataService.CreateAccountAsync(account);
                if (creationErrors.Count > 0)
                {
                    return BadRequest(new { success = false, errors = creationErrors });
                }

                return Ok(new { success = true, message = "Account created successfully", data = account });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating account");
                return StatusCode(500, new { success = false, message = "Error creating account" });
            }
        }

        [HttpPut("accounts/{id}")]
        public async Task<IActionResult> UpdateAccount(int id, [FromBody] Account account)
        {
            try
            {
                if (id != account.AccountId)
                {
                    return BadRequest(new { success = false, message = "Account ID mismatch" });
                }

                // Check for associations
                var associatedVoucherIds = await _metaDataService.GetAssociatedVoucherIdsAsync(id);
                if (associatedVoucherIds.Any())
                {
                    // Preserve original CategoryId and AccountGroupId
                    var originalAccount = await _metaDataService.GetAccountByIdAsync(id);
                    if (originalAccount != null)
                    {
                        account.CategoryId = originalAccount.CategoryId;
                        account.AccountGroupId = originalAccount.AccountGroupId;
                    }
                }

                var errors = await _validationService.ValidateAccountAsync(account);
                if (errors.Count > 0)
                {
                    return BadRequest(new { success = false, errors });
                }

                var updateErrors = await _metaDataService.UpdateAccountAsync(account);
                if (updateErrors.Count > 0)
                {
                    return BadRequest(new { success = false, errors = updateErrors });
                }

                return Ok(new { success = true, message = "Account updated successfully", data = account });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating account {AccountId}", id);
                return StatusCode(500, new { success = false, message = "Error updating account" });
            }
        }

        [HttpDelete("accounts/{id}")]
        public async Task<IActionResult> DeleteAccount(int id)
        {
            try
            {
                var (success, associatedVoucherIds) = await _metaDataService.DeleteAccountAsync(id);
                if (!success)
                {
                    var message = associatedVoucherIds.Any()
                        ? "This account cannot be deleted because it is associated with journal vouchers."
                        : "Failed to delete the account.";
                    return BadRequest(new { success = false, message });
                }

                return Ok(new { success = true, message = "Account deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting account {AccountId}", id);
                return StatusCode(500, new { success = false, message = "Error deleting account" });
            }
        }

        [HttpPost("accounts/{id}/toggle-status")]
        public async Task<IActionResult> ToggleAccountStatus(int id)
        {
            try
            {
                var errors = await _metaDataService.ToggleAccountStatusAsync(id);
                if (errors.Any())
                {
                    return BadRequest(new { success = false, message = string.Join("; ", errors) });
                }

                var account = await _metaDataService.GetAccountByIdAsync(id);
                var isEnabled = account?.IsEnabled ?? false;
                return Ok(new
                {
                    success = true,
                    isEnabled = isEnabled,
                    message = $"Account '{account?.AccountName}' has been {(isEnabled ? "enabled" : "disabled")} successfully."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling account status for ID {AccountId}", id);
                return StatusCode(500, new { success = false, message = "An error occurred while updating the account status." });
            }
        }

        [HttpGet("accounts/{id}/can-delete")]
        public async Task<IActionResult> CanDeleteAccount(int id)
        {
            try
            {
                var account = await _metaDataService.GetAccountByIdAsync(id);
                if (account == null)
                {
                    return Ok(new { canDelete = false, message = "Account not found." });
                }

                var associatedVoucherIds = await _metaDataService.GetAssociatedVoucherIdsAsync(id);
                if (associatedVoucherIds.Any())
                {
                    return Ok(new { canDelete = false, message = "This account cannot be deleted because it is associated with journal vouchers." });
                }

                return Ok(new { canDelete = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking account deletion {AccountId}", id);
                return StatusCode(500, new { success = false, message = "Error checking account" });
            }
        }

        #endregion

        #region Locations

        [HttpGet("locations")]
        public async Task<IActionResult> GetLocations()
        {
            try
            {
                var locations = await _metaDataService.GetAllLocationsAsync();
                return Ok(new
                {
                    success = true,
                    data = locations.Select(l => new
                    {
                        l.LocationId,
                        l.LocationName
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading locations");
                return StatusCode(500, new { success = false, message = "Error loading locations" });
            }
        }

        [HttpGet("locations/{id}")]
        public async Task<IActionResult> GetLocation(int id)
        {
            try
            {
                var location = await _metaDataService.GetLocationByIdAsync(id);
                if (location == null)
                {
                    return NotFound(new { success = false, message = "Location not found" });
                }

                return Ok(new { success = true, data = location });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading location {LocationId}", id);
                return StatusCode(500, new { success = false, message = "Error loading location" });
            }
        }

        [HttpPost("locations")]
        public async Task<IActionResult> CreateLocation([FromBody] Location location)
        {
            try
            {
                var errors = await _validationService.ValidateLocationAsync(location);
                if (errors.Count > 0)
                {
                    return BadRequest(new { success = false, errors });
                }

                var creationErrors = await _metaDataService.CreateLocationAsync(location);
                if (creationErrors.Count > 0)
                {
                    return BadRequest(new { success = false, errors = creationErrors });
                }

                return Ok(new { success = true, message = "Location created successfully", data = location });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating location");
                return StatusCode(500, new { success = false, message = "Error creating location" });
            }
        }

        [HttpPut("locations/{id}")]
        public async Task<IActionResult> UpdateLocation(int id, [FromBody] Location location)
        {
            try
            {
                if (id != location.LocationId)
                {
                    return BadRequest(new { success = false, message = "Location ID mismatch" });
                }

                var errors = await _validationService.ValidateLocationAsync(location);
                if (errors.Count > 0)
                {
                    return BadRequest(new { success = false, errors });
                }

                var updateErrors = await _metaDataService.UpdateLocationAsync(location);
                if (updateErrors.Count > 0)
                {
                    return BadRequest(new { success = false, errors = updateErrors });
                }

                return Ok(new { success = true, message = "Location updated successfully", data = location });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating location {LocationId}", id);
                return StatusCode(500, new { success = false, message = "Error updating location" });
            }
        }

        #endregion

        #region Bookers

        [HttpGet("bookers")]
        public async Task<IActionResult> GetBookers()
        {
            try
            {
                var bookers = await _metaDataService.GetAllBookersAsync();
                return Ok(new
                {
                    success = true,
                    data = bookers.Select(b => new
                    {
                        b.BookerID,
                        b.BookerName,
                        b.IsEnabled
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading bookers");
                return StatusCode(500, new { success = false, message = "Error loading bookers" });
            }
        }

        [HttpGet("bookers/{id}")]
        public async Task<IActionResult> GetBooker(int id)
        {
            try
            {
                var booker = await _metaDataService.GetBookerByIdAsync(id);
                if (booker == null)
                {
                    return NotFound(new { success = false, message = "Booker not found" });
                }

                return Ok(new { success = true, data = booker });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading booker {BookerId}", id);
                return StatusCode(500, new { success = false, message = "Error loading booker" });
            }
        }

        [HttpPost("bookers")]
        public async Task<IActionResult> CreateBooker([FromBody] Booker booker)
        {
            try
            {
                var errors = await _metaDataService.CreateBookerAsync(booker);
                if (errors.Count > 0)
                {
                    return BadRequest(new { success = false, errors });
                }

                return Ok(new { success = true, message = "Booker created successfully", data = booker });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating booker");
                return StatusCode(500, new { success = false, message = "Error creating booker" });
            }
        }

        [HttpPut("bookers/{id}")]
        public async Task<IActionResult> UpdateBooker(int id, [FromBody] Booker booker)
        {
            try
            {
                if (id != booker.BookerID)
                {
                    return BadRequest(new { success = false, message = "Booker ID mismatch" });
                }

                var errors = await _metaDataService.UpdateBookerAsync(booker);
                if (errors.Count > 0)
                {
                    return BadRequest(new { success = false, errors });
                }

                return Ok(new { success = true, message = "Booker updated successfully", data = booker });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating booker {BookerId}", id);
                return StatusCode(500, new { success = false, message = "Error updating booker" });
            }
        }

        [HttpPost("bookers/{id}/toggle-status")]
        public async Task<IActionResult> ToggleBookerStatus(int id)
        {
            try
            {
                var errors = await _metaDataService.ToggleBookerStatusAsync(id);
                if (errors.Any())
                {
                    return BadRequest(new { success = false, message = string.Join("; ", errors) });
                }

                var booker = await _metaDataService.GetBookerByIdAsync(id);
                var isEnabled = booker?.IsEnabled ?? false;
                return Ok(new
                {
                    success = true,
                    isEnabled = isEnabled,
                    message = $"Booker '{booker?.BookerName}' has been {(isEnabled ? "enabled" : "disabled")} successfully."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling booker status for ID {BookerId}", id);
                return StatusCode(500, new { success = false, message = "An error occurred while updating the booker status." });
            }
        }

        #endregion

        #region Lookup Endpoints

        [HttpGet("projects-for-categories")]
        public async Task<IActionResult> GetAllProjectsForCategories()
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                var projects = userId.HasValue
                    ? await _metaDataService.GetAllProjectsForCategoriesForUserAsync(userId.Value)
                    : new List<Project>();

                return Ok(new
                {
                    success = true,
                    data = projects.Select(p => new { projectId = p.ProjectId, projectName = p.ProjectName })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading projects");
                return StatusCode(500, new { success = false, message = "Error loading projects" });
            }
        }

        [HttpGet("projects-for-accounts")]
        public async Task<IActionResult> GetAllProjectsForAccounts()
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                var projects = userId.HasValue
                    ? await _metaDataService.GetProjectsForAccountsForUserAsync(userId.Value)
                    : new List<Project>();

                return Ok(new
                {
                    success = true,
                    data = projects.Select(p => new { projectId = p.ProjectId, projectName = p.ProjectName })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading projects");
                return StatusCode(500, new { success = false, message = "Error loading projects" });
            }
        }

        [HttpGet("account-types")]
        public async Task<IActionResult> GetAccountTypes()
        {
            try
            {
                var accountTypes = await _metaDataService.GetAllAccountTypesAsync();
                return Ok(new
                {
                    success = true,
                    data = accountTypes.Select(at => new { id = at.AccountTypeId, name = at.AccountTypeName })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading account types");
                return StatusCode(500, new { success = false, message = "Error loading account types" });
            }
        }

        [HttpGet("account-groups")]
        public async Task<IActionResult> GetAccountGroups()
        {
            try
            {
                var accountGroups = await _metaDataService.GetAccountGroupsAsync();
                return Ok(new
                {
                    success = true,
                    data = accountGroups.Select(ag => new { id = ag.AccountGroupId, name = ag.AccountGroupName })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading account groups");
                return StatusCode(500, new { success = false, message = "Error loading account groups" });
            }
        }

        #endregion
    }
}

