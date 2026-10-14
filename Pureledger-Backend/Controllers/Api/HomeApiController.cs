using Microsoft.AspNetCore.Authorization;
using PureLedger.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PureLedger.Attributes;
using PureLedger.Helpers;
using PureLedger.Models;
using PureLedger.Services;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.Home;

namespace PureLedger.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [ControllerAccess]
    public class HomeApiController : ControllerBase
    {
        #region Fields and Constructor

        private readonly ILogger<HomeApiController> _logger;
        private readonly JournalEntryService _journalService;
        private readonly JournalEntryValidationService _journalEntryValidationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMemoryCache _cache;
        private readonly IApprovalService _approvalService;

        public HomeApiController(
            JournalEntryService journalService,
            JournalEntryValidationService journalEntryValidationService,
            ICurrentUserService currentUserService,
            IMemoryCache cache,
            ILogger<HomeApiController> logger,
            ISharedProcessingService sharedProcessingService,
            IApprovalService approvalService)
        {
            _journalService = journalService;
            _journalEntryValidationService = journalEntryValidationService;
            _currentUserService = currentUserService;
            _cache = cache;
            _logger = logger;
            _approvalService = approvalService;
        }

        #endregion

        #region Entry Operations

        [HttpGet("entry-data")]
        public async Task<IActionResult> GetEntryData(int? projectId = null)
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var projects = await _journalService.GetUserProjectsAsync(userId.Value);
                var bookers = await _journalService.GetActiveBookersAsync();

                return Ok(new
                {
                    success = true,
                    projects = projects.Select(p => new { id = p.ProjectId, name = p.ProjectName }),
                    bookers = bookers.Select(b => new { id = b.BookerID, name = b.BookerName }),
                    selectedProjectId = projectId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading entry data");
                return StatusCode(500, new { success = false, message = "Error loading data" });
            }
        }

        [HttpPost("entry")]
        public async Task<IActionResult> CreateOrUpdateEntry([FromBody] JournalEntryViewModel model, [FromQuery] string? action = null, [FromQuery] int? editingVoucherId = null)
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                if (!userId.HasValue) return Unauthorized(new { success = false, message = "User not authenticated" });

                var approvalResult = await _approvalService.SubmitManualExpenseAsync(
                    model, editingVoucherId, userId.Value);
                if (approvalResult != null)
                {
                    return Ok(new
                    {
                        success = true,
                        status = approvalResult.IsPending ? "PendingApproval" : ApprovalStatuses.Approved,
                        isPending = approvalResult.IsPending,
                        autoApproved = approvalResult.AutoApproved,
                        approvalVoucherId = approvalResult.ApprovalVoucherId,
                        voucherId = approvalResult.JournalVoucherId,
                        projectId = approvalResult.ProjectId,
                        message = approvalResult.IsPending
                            ? "Voucher submitted for approval."
                            : $"Expense voucher posted and automatically approved. JV-{approvalResult.JournalVoucherId}",
                        shouldPrint = !approvalResult.IsPending && action == "submit_print",
                        printUrl = !approvalResult.IsPending && action == "submit_print"
                            ? $"/api/reporting/print-voucher/{approvalResult.JournalVoucherId}?type=JV" : null
                    });
                }

                if (!editingVoucherId.HasValue)
                {
                    var validationErrors = await _journalEntryValidationService.ValidateAsync(model);
                    if (validationErrors.Any())
                        return BadRequest(new { success = false, errors = validationErrors });
                }

                var journalVoucher = editingVoucherId.HasValue
                    ? await _journalService.UpdateVoucherAsync(editingVoucherId.Value, model)
                    : await _journalService.CreateVoucherAsync(model);

                return Ok(new
                {
                    success = true,
                    voucherId = journalVoucher.JournalVoucherId,
                    projectId = journalVoucher.ProjectId,
                    message = editingVoucherId.HasValue
                        ? $"Journal voucher updated. Jv-{journalVoucher.JournalVoucherId}"
                        : $"Journal entries created. Jv-{journalVoucher.JournalVoucherId}",
                    shouldPrint = action == "submit_print",
                    printUrl = action == "submit_print" ? $"/api/reporting/print-voucher/{journalVoucher.JournalVoucherId}?type=JV" : null
                });
            }
            catch (NotAuthenticatedException ex)
            {
                return Unauthorized(new { success = false, message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                // 403, not 401: the client treats 401 as an expired session and would refresh and replay the POST.
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = ExceptionClassifier.UserMessage(ex) });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                return Conflict(new { success = false, message = "The voucher changed while it was being saved. Reload and try again." });
            }
            catch (Exception ex) when (
                ex is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.SerializationFailure } ||
                ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.SerializationFailure })
            {
                return Conflict(new { success = false, message = "The voucher changed while it was being saved. Reload and try again." });
            }
            catch (InvalidOperationException ex) when (ExceptionClassifier.IsApplicationRaised(ex))
            {
                if (ex is ApprovalConflictException)
                    return Conflict(new { success = false, message = ExceptionClassifier.UserMessage(ex) });
                return BadRequest(new { success = false, message = ExceptionClassifier.UserMessage(ex) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving journal entries");
                return StatusCode(500, new { success = false, message = "An error occurred while saving. Please try again." });
            }
        }

        #endregion

        #region Voucher Operations

        [HttpGet("voucher/{id}")]
        public async Task<IActionResult> GetVoucherById(int id)
        {
            try
            {
                var result = await _journalService.GetVoucherDetailsAsync(id);
                if (result == null)
                    return NotFound(new { success = false, message = "Voucher not found" });

                return Ok(new { success = true, voucher = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving voucher {VoucherId}", id);
                return StatusCode(500, new { success = false, message = "An error occurred while loading the voucher." });
            }
        }

        [HttpGet("edit-voucher/{id}")]
        public async Task<IActionResult> GetVoucherForEdit(int id)
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                var result = await _journalService.GetVoucherForEditAsync(id, userId);
                if (result == null)
                    return NotFound(new { success = false, message = "Voucher not found" });

                return Ok(result);
            }
            catch (InvalidOperationException ex) when (ExceptionClassifier.IsApplicationRaised(ex))
            {
                return BadRequest(new { success = false, message = ExceptionClassifier.UserMessage(ex) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing voucher {VoucherId} for editing", id);
                return StatusCode(500, new { success = false, message = "An error occurred while loading the voucher. Please try again." });
            }
        }

        [HttpGet("journal-entry/{entryId}")]
        public async Task<IActionResult> GetJournalEntry(int entryId)
        {
            var model = await _journalService.GetEntryForEditAsync(entryId);
            return model == null
                ? NotFound(new { success = false, message = "Entry not found" })
                : Ok(new { success = true, data = model });
        }

        [HttpPut("journal-entry")]
        public async Task<IActionResult> UpdateJournalEntry([FromBody] JournalEntryUpdateViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList() });

            try
            {
                var (success, errorMessage) = await _journalService.UpdateEntryAsync(model);
                return success
                    ? Ok(new { success = true })
                    : BadRequest(new { success = false, message = errorMessage ?? "Error updating entry" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateJournalEntry: {Message}", ex.Message);
                return StatusCode(500, new { success = false, message = "An unexpected error occurred" });
            }
        }

        [HttpDelete("journal-entry/{entryId}")]
        public async Task<IActionResult> DeleteJournalEntry(int entryId)
        {
            var (success, errorMessage) = await _journalService.DeleteEntryAsync(entryId);
            return success
                ? Ok(new { success = true })
                : BadRequest(new { success = false, message = errorMessage ?? "Failed to delete entry." });
        }

        #endregion

        #region Voucher Summary

        [HttpGet("voucher-entry-data")]
        public async Task<IActionResult> GetVoucherEntryData(int? jvId = null)
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                var projects = userId.HasValue
                    ? await _journalService.GetUserProjectsAsync(userId.Value)
                    : new List<Project>();

                object? voucherData = jvId.HasValue
                    ? await _journalService.GetVoucherSummaryDataAsync(jvId.Value)
                    : null;

                return Ok(new
                {
                    success = true,
                    projects = projects.Select(p => new { id = p.ProjectId, name = p.ProjectName }),
                    voucher = voucherData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading voucher entry data");
                return StatusCode(500, new { success = false, message = "Error loading data" });
            }
        }

        [HttpGet("voucher-summary")]
        public async Task<IActionResult> GetVoucherSummary()
        {
            try
            {
                var vouchers = await _journalService.GetAllVouchersAsync();
                return Ok(new
                {
                    success = true,
                    vouchers = vouchers.Select(v => new
                    {
                        v.JournalVoucherId,
                        v.ProjectId,
                        projectName = v.Project?.ProjectName ?? "Unknown Project",
                        v.EntryDate,
                        v.TotalDebit,
                        v.TotalCredit
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading voucher summary");
                return StatusCode(500, new { success = false, message = "Error loading vouchers" });
            }
        }

        [HttpGet("voucher-summary/{voucherId}")]
        public async Task<IActionResult> GetVoucherSummaryById(int voucherId)
        {
            try
            {
                var summary = await _journalService.GetVoucherSummaryAsync(voucherId);
                return Ok(new { success = true, data = summary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading voucher summary for {VoucherId}", voucherId);
                return StatusCode(500, new { success = false, message = "Error loading voucher summary" });
            }
        }

        #endregion

        #region Modified Entries

        [HttpGet("modified-entries-init")]
        public async Task<IActionResult> GetModifiedEntriesInit()
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                var projects = userId.HasValue
                    ? await _journalService.GetUserProjectsAsync(userId.Value)
                    : new List<Project>();

                return Ok(new
                {
                    success = true,
                    projects = projects.Select(p => new { id = p.ProjectId, name = p.ProjectName }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading modified entries init data");
                return StatusCode(500, new { success = false, message = "Error loading data" });
            }
        }

        [HttpGet("modified-entries")]
        public async Task<IActionResult> GetModifiedEntries(int? projectId = null, int page = 0, int pageSize = 250)
        {
            try
            {
                if (projectId == null)
                    return Ok(new { success = true, data = new List<ModifiedEntriesSummaryViewModel>(), totalRecords = 0, page = 0, pageSize, hasMoreData = false });

                page = Math.Max(0, page);
                pageSize = Math.Clamp(pageSize, 1, 2500);

                var (vouchers, totalCount) = await _journalService.GetModifiedEntriesPaginatedAsync(projectId, page, pageSize);
                var modifiedEntriesSummary = vouchers.Select(v => new ModifiedEntriesSummaryViewModel
                {
                    JvId = v.JournalVoucherId,
                    TotalDebit = v.TotalDebit,
                    TotalCredit = v.TotalCredit,
                    HasModifiedEntries = v.JournalEntries.Any(e => e.IsEdited)
                }).ToList();

                return Ok(new
                {
                    success = true,
                    data = modifiedEntriesSummary,
                    totalRecords = totalCount,
                    page,
                    pageSize,
                    hasMoreData = (page + 1) * pageSize < totalCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading modified entries");
                return StatusCode(500, new { success = false, message = "Error loading modified entries" });
            }
        }

        [HttpGet("voucher-modified-entries/{jvId}")]
        public async Task<IActionResult> GetVoucherModifiedEntries(int jvId)
        {
            try
            {
                var voucher = await _journalService.GetVoucherWithEntriesAsync(jvId);
                if (voucher == null)
                    return NotFound(new { success = false, message = "Voucher not found" });

                // Calculate running balance for each entry
                decimal runningBalance = 0;
                var entries = voucher.JournalEntries.Select(e =>
                {
                    runningBalance += e.Debit - e.Credit;
                    return new VoucherEntryDetailViewModel
                    {
                        EntryId = e.JournalEntryId,
                        ModifyTime = e.ModifiedDateTime,
                        Category = e.Category?.CategoryName ?? "",
                        Account = e.Account?.AccountName ?? "",
                        Quantity = e.Quantity,
                        Rate = e.Rate,
                        Debit = e.Debit,
                        Credit = e.Credit,
                        Balance = runningBalance,
                        IsModified = e.IsEdited
                    };
                }).ToList();

                var model = new VoucherModifiedEntriesViewModel
                {
                    JvId = voucher.JournalVoucherId,
                    Entries = entries
                };

                return Ok(new { success = true, data = model });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading voucher modified entries for {JvId}", jvId);
                return StatusCode(500, new { success = false, message = "Error loading voucher modified entries" });
            }
        }

        #endregion

        #region Base Vouchers and Derived Vouchers

        [HttpGet("base-vouchers-init")]
        public async Task<IActionResult> GetBaseVouchersInit()
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                var projects = userId.HasValue
                    ? await _journalService.GetUserProjectsAsync(userId.Value)
                    : new List<Project>();

                return Ok(new
                {
                    success = true,
                    projects = projects.Select(p => new { id = p.ProjectId, name = p.ProjectName }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading base vouchers init data");
                return StatusCode(500, new { success = false, message = "Error loading data" });
            }
        }

        [HttpGet("base-vouchers")]
        public async Task<IActionResult> GetBaseVouchers(
            int? projectId = null,
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            bool hideFullyProcessed = true,
            int page = 0,
            int pageSize = 250)
        {
            try
            {
                if (projectId == null)
                    return Ok(new { success = true, vouchers = new List<object>(), totalRecords = 0, page = 0, pageSize, hasMoreData = false });

                page = Math.Max(0, page);
                pageSize = Math.Clamp(pageSize, 1, 2500);

                var (baseVouchers, totalCount) = await _journalService.GetBaseVouchersPaginatedAsync(
                    projectId, startDate, endDate, hideFullyProcessed, page, pageSize);

                return Ok(new
                {
                    success = true,
                    vouchers = baseVouchers.Select(v => new
                    {
                        journalVoucherId = v.JournalVoucherId,
                        projectId = v.ProjectId,
                        projectName = v.Project?.ProjectName ?? "Unknown Project",
                        customerName = v.JournalEntries?.FirstOrDefault(e => e.Account?.AccountGroupId == 1)?.Account?.AccountName ?? "N/A",
                        entryDate = v.EntryDate,
                        unprocessedEntries = v.JournalEntries?.Count(e => !e.IsProcessed && e.Quantity > 0) ?? 0,
                        totalEntries = v.JournalEntries?.Count(e => e.Account?.AccountGroupId != 1) ?? 0
                    }).ToList(),
                    totalRecords = totalCount,
                    page,
                    pageSize,
                    hasMoreData = (page + 1) * pageSize < totalCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving base vouchers");
                return StatusCode(500, new { success = false, message = "Error loading base vouchers" });
            }
        }

        [HttpGet("base-vouchers-summary")]
        public async Task<IActionResult> GetBaseVouchersSummary(
            int? projectId = null,
            DateOnly? startDate = null,
            DateOnly? endDate = null)
        {
            try
            {
                var summary = await _journalService.GetBaseVouchersSummaryAsync(projectId, startDate, endDate);
                return Ok(new { success = true, data = summary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving base vouchers summary for project {ProjectId}", projectId);
                return StatusCode(500, new { success = false, message = "Error loading base vouchers summary" });
            }
        }

        [HttpGet("process-base-voucher/{id}")]
        public async Task<IActionResult> GetBaseVoucherForProcessing(int id)
        {
            try
            {
                var result = await _journalService.GetBaseVoucherForProcessingAsync(id);
                if (result == null)
                    return NotFound(new { success = false, message = "Base voucher not found" });

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing base voucher {Id} for processing", id);
                return StatusCode(500, new { success = false, message = "Error loading base voucher" });
            }
        }

        [HttpPost("create-derived-voucher")]
        public async Task<IActionResult> CreateDerivedVoucher([FromBody] BaseVoucherProcessingViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList() });

            try
            {
                var entriesToProcess = model.Entries.Where(e => e.ProcessQuantity > 0 || e.AccountGroupId == 1).ToList();
                if (!entriesToProcess.Any())
                    return BadRequest(new { success = false, message = "Please select at least one entry to process." });

                var derivedVoucherId = await _journalService.CreateDerivedVoucherAsync(
                    model.BaseVoucherId, entriesToProcess, model.AccountEntryAmount, model.AccountEntryIsDebit,
                    model.DerivedEntryDate, model.DerivedCreditableDate);

                return Ok(new { success = true, message = $"Successfully created derived voucher JV-{derivedVoucherId}", voucherId = derivedVoucherId });
            }
            catch (InvalidOperationException ex) when (ExceptionClassifier.IsApplicationRaised(ex))
            {
                return BadRequest(new { success = false, message = ExceptionClassifier.UserMessage(ex) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating derived voucher from base voucher {Id}", model.BaseVoucherId);
                return StatusCode(500, new { success = false, message = "An error occurred while creating the derived voucher." });
            }
        }

        [HttpPost("mark-voucher-as-missed/{voucherId}")]
        public async Task<IActionResult> MarkVoucherAsMissed(int voucherId)
        {
            try
            {
                await _journalService.MarkEntriesAsMissedAsync(voucherId);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking voucher {VoucherId} as missed", voucherId);
                return StatusCode(500, new { success = false, message = "Error marking entries as missed" });
            }
        }

        #endregion

        #region Recipe Operations

        [HttpGet("recipes")]
        public async Task<IActionResult> GetRecipes(int? projectId = null)
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                var projects = userId.HasValue
                    ? await _journalService.GetUserProjectsAsync(userId.Value)
                    : new List<Project>();

                var viewModel = new RecipesViewModel { ProjectId = projectId ?? 0 };

                if (projectId.HasValue)
                {
                    var recipes = await _journalService.GetRecipesByProjectAsync(projectId.Value);
                    viewModel.Recipes = recipes.Select(r => new RecipeListItemViewModel
                    {
                        RecipeId = r.RecipeId,
                        RecipeName = r.RecipeName,
                        ProjectName = r.Project?.ProjectName ?? "Unknown Project"
                    }).ToList();
                }

                return Ok(new
                {
                    success = true,
                    data = viewModel,
                    projects = projects.Select(p => new { id = p.ProjectId, name = p.ProjectName })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading recipes for project {ProjectId}", projectId);
                return StatusCode(500, new { success = false, message = "Failed to load recipes. Please try again." });
            }
        }

        [HttpGet("recipe-items/{recipeId}")]
        public async Task<IActionResult> GetRecipeItems(int recipeId)
        {
            try
            {
                var recipe = await _journalService.GetRecipeWithItemsAsync(recipeId);
                if (recipe == null)
                    return NotFound(new { success = false, message = "Recipe not found" });

                var result = new
                {
                    recipeName = recipe.RecipeName,
                    projectName = recipe.Project?.ProjectName ?? "Unknown Project",
                    items = recipe.RecipeItems.Select(item => new
                    {
                        recipeItemId = item.RecipeItemId,
                        categoryName = item.Category?.CategoryName ?? "Unknown Category",
                        accountName = item.Account?.AccountName ?? "Unknown Account"
                    }).ToList()
                };

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recipe items for recipe {RecipeId}", recipeId);
                return StatusCode(500, new { success = false, message = "Failed to load recipe items" });
            }
        }

        [HttpGet("create-from-recipe/{recipeId}")]
        public async Task<IActionResult> GetCreateFromRecipe(int recipeId, int projectId)
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                var projects = userId.HasValue
                    ? await _journalService.GetUserProjectsAsync(userId.Value)
                    : new List<Project>();
                var bookers = await _journalService.GetActiveBookersAsync();
                var model = await _journalService.CreateViewModelFromRecipeAsync(recipeId, projectId);

                return Ok(new
                {
                    success = true,
                    data = model,
                    projects = projects.Select(p => new { id = p.ProjectId, name = p.ProjectName }),
                    bookers = bookers.Select(b => new { id = b.BookerID, name = b.BookerName }),
                    selectedProjectId = projectId
                });
            }
            catch (ArgumentException ex) when (ExceptionClassifier.IsApplicationRaised(ex))
            {
                _logger.LogWarning(ex, "Invalid recipe or project: {Message}", ex.Message);
                return BadRequest(new { success = false, message = ExceptionClassifier.UserMessage(ex) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating entry from recipe {RecipeId}", recipeId);
                return StatusCode(500, new { success = false, message = "An error occurred while preparing the entry form. Please try again." });
            }
        }

        #endregion

        #region Reference Data

        [HttpGet("categories-by-project/{projectId}")]
        public async Task<IActionResult> GetCategoriesByProject(int projectId)
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var categories = await _journalService.GetCategoriesForUserAsync(projectId, userId.Value);
                if (categories == null || !categories.Any())
                    return Ok(new { success = true, data = Array.Empty<object>() });

                var result = categories.Select(c => new { id = c.CategoryId, name = c.CategoryName }).ToList();
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categories for project {ProjectId}", projectId);
                return StatusCode(500, new { success = false, message = "Error loading categories" });
            }
        }

        [HttpGet("accounts-by-category/{categoryId}")]
        public async Task<IActionResult> GetAccountsByCategory(int categoryId)
        {
            try
            {
                var cacheKey = CacheKeys.AccountsByCategory(categoryId);

                if (!_cache.TryGetValue(cacheKey, out object? cachedResult))
                {
                    var accounts = await _journalService.GetActiveAccountsByCategoryAsync(categoryId);
                    cachedResult = accounts.Select(a => new { id = a.AccountId, name = a.AccountName }).ToList();
                    _cache.Set(cacheKey, cachedResult);
                    CacheInvalidationService.RegisterAccountKey(cacheKey);
                    _logger.LogDebug("Cached accounts for category {CategoryId}", categoryId);
                }

                return Ok(new { success = true, data = cachedResult });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accounts for category {CategoryId}", categoryId);
                return StatusCode(500, new { success = false, message = "Error loading accounts" });
            }
        }

        [HttpGet("product-details/{accountId}")]
        public async Task<IActionResult> GetProductDetails(int accountId)
        {
            try
            {
                var productData = await _journalService.GetProductDetailsAsync(accountId);
                return Ok(new { success = true, data = productData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product details for account {AccountId}", accountId);
                return StatusCode(500, new { success = false, message = "Error loading product details" });
            }
        }

        [HttpGet("locations")]
        public async Task<IActionResult> GetLocations()
        {
            try
            {
                var locations = await _journalService.GetAllLocationsAsync();
                return Ok(new
                {
                    success = true,
                    data = locations.Select(l => new { locationId = l.LocationId, locationName = l.LocationName })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading locations");
                return StatusCode(500, new { success = false, message = "Error loading locations" });
            }
        }

        #endregion
    }
}
