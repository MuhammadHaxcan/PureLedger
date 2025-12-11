using Microsoft.AspNetCore.Authorization;
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
        private readonly ILogger<HomeApiController> _logger;
        private readonly JournalEntryService _journalService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMemoryCache _cache;

        public HomeApiController(
            JournalEntryService journalService,
            ICurrentUserService currentUserService,
            IMemoryCache cache,
            ILogger<HomeApiController> logger)
        {
            _journalService = journalService;
            _currentUserService = currentUserService;
            _cache = cache;
            _logger = logger;
        }

        #region Journal Entry Management

        [HttpGet("entry-data")]
        public async Task<IActionResult> GetEntryData(int? projectId = null)
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var projects = await _journalService.GetAllProjectsForUserAsync(userId.Value);
                var bookers = await _journalService.GetAllEnabledBookersAsync();

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
                JournalVoucher journalVoucher;

                if (editingVoucherId.HasValue)
                {
                    journalVoucher = await UpdateExistingVoucherAsync(editingVoucherId.Value, model);
                }
                else
                {
                    journalVoucher = await CreateNewVoucherAsync(model);
                }

                var response = new
                {
                    success = true,
                    voucherId = journalVoucher.JournalVoucherId,
                    projectId = journalVoucher.ProjectId,
                    message = editingVoucherId.HasValue ? $"Journal voucher updated. Jv-{journalVoucher.JournalVoucherId}" : $"Journal entries created. Jv-{journalVoucher.JournalVoucherId}",
                    shouldPrint = action == "submit_print",
                    printUrl = action == "submit_print" ? $"/api/reporting/print-voucher/{journalVoucher.JournalVoucherId}?type=JV" : null
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving journal entries");
                return StatusCode(500, new { success = false, message = "An error occurred while saving. Please try again." });
            }
        }

        [HttpGet("voucher/{id}")]
        public async Task<IActionResult> GetVoucherById(int id)
        {
            try
            {
                var voucher = await _journalService.GetVoucherWithEntriesAsync(id);
                if (voucher == null)
                {
                    return NotFound(new { success = false, message = "Voucher not found" });
                }

                var model = await _journalService.MapVoucherToViewModelAsync(voucher);

                // Get project name - try from navigation property first, then fetch if needed
                string projectName = "Unknown Project";
                if (voucher.Project != null)
                {
                    projectName = voucher.Project.ProjectName;
                }
                else if (voucher.ProjectId > 0)
                {
                    // Fallback: fetch project if not loaded
                    var project = await _journalService.GetProjectByIdAsync(voucher.ProjectId);
                    if (project != null)
                    {
                        projectName = project.ProjectName;
                    }
                }

                // Enhance the response with category and account names, and delivery challan location info
                var entriesList = new List<object>();
                foreach (var e in voucher.JournalEntries)
                {
                    // Check if this entry has a delivery challan entry
                    var dcEntry = await _journalService.GetDeliveryChallanEntryByJournalEntryIdAsync(e.JournalEntryId);
                    
                    var entryObj = new
                    {
                        entryId = e.JournalEntryId,
                        categoryId = e.CategoryId,
                        categoryName = e.Category?.CategoryName ?? "N/A",
                        accountId = e.AccountId,
                        accountName = e.Account?.AccountName ?? "N/A",
                        description = e.Description ?? "",
                        quantity = e.Quantity,
                        rate = e.Rate,
                        discount = e.Discount,
                        discountPercent = e.DiscountPercent,
                        debit = e.Debit,
                        credit = e.Credit,
                        hasDeliveryChallan = dcEntry != null,
                        locationId = dcEntry?.LocationId,
                        locationName = dcEntry?.Location?.LocationName,
                        deliveryChallanEntryId = dcEntry?.DeliveryChallanEntryId
                    };
                    entriesList.Add(entryObj);
                }

                var voucherResponse = new
                {
                    journalVoucherId = voucher.JournalVoucherId,
                    projectId = voucher.ProjectId,
                    projectName = projectName,
                    entryDate = voucher.EntryDate.ToString("yyyy-MM-dd"),
                    creditableDate = voucher.CreditableDate.ToString("yyyy-MM-dd"),
                    totalDebit = voucher.TotalDebit,
                    totalCredit = voucher.TotalCredit,
                    entries = entriesList
                };

                return Ok(new { success = true, voucher = voucherResponse });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving voucher {VoucherId}", id);
                return StatusCode(500, new { success = false, message = "An error occurred while loading the voucher.", detailedError = ex.Message });
            }
        }

        [HttpGet("edit-voucher/{id}")]
        public async Task<IActionResult> GetVoucherForEdit(int id)
        {
            try
            {
                var voucher = await _journalService.GetVoucherWithEntriesAsync(id);
                if (voucher == null)
                {
                    return NotFound(new { success = false, message = "Voucher not found" });
                }

                var model = await _journalService.MapVoucherToViewModelAsync(voucher);
                var userId = _currentUserService.GetCurrentUserId();
                
                var projects = userId.HasValue
                    ? await _journalService.GetAllProjectsForUserAsync(userId.Value)
                    : new List<Project>();
                var bookers = await _journalService.GetAllEnabledBookersAsync();

                return Ok(new
                {
                    success = true,
                    model,
                    projects = projects.Select(p => new { id = p.ProjectId, name = p.ProjectName }),
                    bookers = bookers.Select(b => new { id = b.BookerID, name = b.BookerName }),
                    editingVoucherId = id,
                    selectedProjectId = voucher.ProjectId
                });
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
            var model = await _journalService.GetJournalEntryForEditAsync(entryId);
            
            if (model != null)
            {
                // Normalize numeric values for the edit modal
                model.Rate = Math.Round(model.Rate, 4);
                model.Discount = Math.Round(model.Discount, 2);
                model.DiscountPercent = Math.Round(model.DiscountPercent, 2);
                model.Debit = Math.Round(model.Debit, 2);
                model.Credit = Math.Round(model.Credit, 2);
            }

            return model == null 
                ? NotFound(new { success = false, message = "Entry not found" })
                : Ok(new { success = true, data = model });
        }

        [HttpPut("journal-entry")]
        public async Task<IActionResult> UpdateJournalEntry([FromBody] JournalEntryUpdateViewModel model)
        {
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

            try
            {
                var success = await _journalService.UpdateJournalEntryAsync(model);
                return success
                    ? Ok(new { success = true })
                    : BadRequest(new { success = false, message = "Error updating entry" });
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
            var success = await _journalService.DeleteJournalEntryAsync(entryId);
            return success
                ? Ok(new { success = true })
                : BadRequest(new { success = false, message = "Invalid entry." });
        }

        #endregion

        #region Voucher Management

        [HttpGet("voucher-entry-data")]
        public async Task<IActionResult> GetVoucherEntryData(int? jvId = null)
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                var projects = userId.HasValue
                    ? await _journalService.GetAllProjectsForUserAsync(userId.Value)
                    : new List<Project>();

                object? voucherData = null;
                if (jvId.HasValue)
                {
                    var voucher = await _journalService.GetVoucherWithEntriesAsync(jvId.Value);
                    if (voucher != null)
                    {
                        voucherData = new
                        {
                            voucherId = voucher.JournalVoucherId,
                            projectId = voucher.ProjectId,
                            projectName = voucher.Project?.ProjectName ?? "Unknown Project",
                            entryDate = voucher.EntryDate,
                            creditableDate = voucher.CreditableDate,
                            totalDebit = voucher.TotalDebit,
                            totalCredit = voucher.TotalCredit,
                            entries = voucher.JournalEntries.Select(e => new
                            {
                                e.JournalEntryId,
                                e.Description,
                                e.Quantity,
                                e.Rate,
                                e.Debit,
                                e.Credit
                            })
                        };
                    }
                }

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
                // summary already includes projectName from GetVoucherSummaryAsync
                return Ok(new { success = true, data = summary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading voucher summary for {VoucherId}", voucherId);
                return StatusCode(500, new { success = false, message = "Error loading voucher summary" });
            }
        }

        [HttpGet("modified-entries-init")]
        public async Task<IActionResult> GetModifiedEntriesInit()
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                var projects = userId.HasValue
                    ? await _journalService.GetAllProjectsForUserAsync(userId.Value)
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
                {
                    return Ok(new 
                    { 
                        success = true, 
                        data = new List<ModifiedEntriesSummaryViewModel>(),
                        totalRecords = 0,
                        page = 0,
                        pageSize = pageSize,
                        hasMoreData = false
                    });
                }

                page = Math.Max(0, page);
                pageSize = Math.Clamp(pageSize, 1, 500);

                var (vouchers, totalCount) = await _journalService.GetVouchersWithModifiedEntriesPaginatedAsync(projectId, page, pageSize);
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
                    page = page,
                    pageSize = pageSize,
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
                {
                    return NotFound(new { success = false, message = "Voucher not found" });
                }

                var model = new VoucherModifiedEntriesViewModel
                {
                    JvId = voucher.JournalVoucherId,
                    Entries = voucher.JournalEntries.Select(e => new VoucherEntryDetailViewModel
                    {
                        EntryId = e.JournalEntryId,
                        ModifyTime = e.ModifiedDateTime,
                        Category = e.Category?.CategoryName ?? "",
                        Account = e.Account?.AccountName ?? "",
                        Quantity = e.Quantity,
                        Rate = e.Rate,
                        Debit = e.Debit,
                        Credit = e.Credit,
                        IsModified = e.IsEdited
                    }).ToList()
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

        #region Base Voucher Processing

        [HttpGet("base-vouchers-init")]
        public async Task<IActionResult> GetBaseVouchersInit()
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                var projects = userId.HasValue
                    ? await _journalService.GetAllProjectsForUserAsync(userId.Value)
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
        public async Task<IActionResult> GetBaseVouchers(int? projectId = null, int page = 0, int pageSize = 250)
        {
            try
            {
                if (projectId == null)
                {
                    return Ok(new
                    {
                        success = true,
                        vouchers = new List<object>(),
                        totalRecords = 0,
                        page = 0,
                        pageSize = pageSize,
                        hasMoreData = false
                    });
                }

                page = Math.Max(0, page);
                pageSize = Math.Clamp(pageSize, 1, 500);

                var (baseVouchers, totalCount) = await _journalService.GetAllBaseVouchersPaginatedAsync(projectId, page, pageSize);
                
                return Ok(new
                {
                    success = true,
                    vouchers = baseVouchers.Select(v => new
                    {
                        journalVoucherId = v.JournalVoucherId,
                        projectId = v.ProjectId,
                        projectName = v.Project?.ProjectName ?? "Unknown Project",
                        customerName = v.JournalEntries?
                            .FirstOrDefault(e => e.Account?.AccountGroupId == 1)?
                            .Account?.AccountName ?? "N/A",
                        entryDate = v.EntryDate,
                        unprocessedEntries = v.JournalEntries?.Count(e => !e.IsProcessed && e.Quantity > 0) ?? 0,
                        totalEntries = v.JournalEntries?.Count ?? 0
                    }).ToList(),
                    totalRecords = totalCount,
                    page = page,
                    pageSize = pageSize,
                    hasMoreData = (page + 1) * pageSize < totalCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving base vouchers");
                return StatusCode(500, new { success = false, message = "Error loading base vouchers" });
            }
        }

        [HttpGet("process-base-voucher/{id}")]
        public async Task<IActionResult> GetBaseVoucherForProcessing(int id)
        {
            try
            {
                var voucher = await _journalService.GetBaseVoucherWithUnprocessedEntriesAsync(id);
                if (voucher == null)
                {
                    return NotFound(new { success = false, message = "Base voucher not found" });
                }

                var model = new BaseVoucherProcessingViewModel
                {
                    BaseVoucherId = id,
                    ProjectId = voucher.ProjectId,
                    ProjectName = voucher.Project?.ProjectName ?? "Unknown Project",
                    EntryDate = voucher.EntryDate,
                    TotalDebit = voucher.TotalDebit,
                    TotalCredit = voucher.TotalCredit,
                    CreditableDate = voucher.CreditableDate,
                    Entries = voucher.JournalEntries
                        .OrderBy(e => e.IsProcessed)
                        .ThenBy(e => e.JournalEntryId)
                        .Select(e => new BaseVoucherEntryViewModel
                        {
                            EntryId = e.JournalEntryId,
                            Description = e.Description,
                            AccountName = e.Account?.AccountName ?? "N/A",
                            CategoryName = e.Category?.CategoryName ?? "N/A",
                            AccountId = e.AccountId,
                            CategoryId = e.CategoryId,
                            AvailableQuantity = e.IsProcessed ? 0 : e.Quantity,
                            ProcessQuantity = 0,
                            Pkg = e.Account?.ProductData?.Pkg ?? 0,
                            Rate = e.Rate,
                            Debit = e.Debit,
                            Credit = e.Credit,
                            Percent = e.DiscountPercent ?? 0,
                            Disc = e.Discount,
                            Rbp = e.Rbp,
                            AccountGroupId = e.Account?.AccountGroupId ?? 0,
                            IsProcessed = e.IsProcessed
                        })
                        .ToList()
                };

                // Format dates for JSON serialization
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        baseVoucherId = model.BaseVoucherId,
                        projectId = model.ProjectId,
                        projectName = model.ProjectName,
                        entryDate = model.EntryDate.ToString("yyyy-MM-dd"),
                        creditableDate = model.CreditableDate.ToString("yyyy-MM-dd"),
                        totalDebit = model.TotalDebit,
                        totalCredit = model.TotalCredit,
                        entries = model.Entries.Select(e => new
                        {
                            entryId = e.EntryId,
                            description = e.Description,
                            accountName = e.AccountName,
                            categoryName = e.CategoryName,
                            accountId = e.AccountId,
                            categoryId = e.CategoryId,
                            availableQuantity = e.AvailableQuantity,
                            processQuantity = e.ProcessQuantity,
                            rate = e.Rate,
                            debit = e.Debit,
                            credit = e.Credit,
                            percent = e.Percent,
                            disc = e.Disc,
                            rbp = e.Rbp,
                            pkg = e.Pkg,
                            accountGroupId = e.AccountGroupId,
                            isProcessed = e.IsProcessed
                        }).ToList()
                    }
                });
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

            try
            {
                var entriesToProcess = model.Entries
                    .Where(e => e.ProcessQuantity > 0 || e.AccountGroupId == 1)
                    .ToList();

                if (!entriesToProcess.Any())
                {
                    return BadRequest(new { success = false, message = "Please select at least one entry to process." });
                }

                var derivedVoucherId = await _journalService.CreateDerivedVoucherAsync(
                    model.BaseVoucherId,
                    entriesToProcess,
                    model.AccountEntryAmount,
                    model.AccountEntryIsDebit,
                    model.CreditableDate,
                    model.BookerId);

                return Ok(new
                {
                    success = true,
                    message = $"Successfully created derived voucher JV-{derivedVoucherId}",
                    voucherId = derivedVoucherId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating derived voucher from base voucher {Id}", model.BaseVoucherId);
                return StatusCode(500, new { success = false, message = ex.Message });
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

        #region Recipe Management

        [HttpGet("recipes")]
        public async Task<IActionResult> GetRecipes(int? projectId = null)
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                var projects = userId.HasValue
                    ? await _journalService.GetAllProjectsForUserAsync(userId.Value)
                    : new List<Project>();

                var viewModel = new RecipesViewModel
                {
                    ProjectId = projectId ?? 0
                };

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
                {
                    return NotFound(new { success = false, message = "Recipe not found" });
                }

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
                    ? await _journalService.GetAllProjectsForUserAsync(userId.Value)
                    : new List<Project>();
                var bookers = await _journalService.GetAllEnabledBookersAsync();

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
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid recipe or project: {Message}", ex.Message);
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating entry from recipe {RecipeId}", recipeId);
                return StatusCode(500, new { success = false, message = "An error occurred while preparing the entry form. Please try again." });
            }
        }

        #endregion

        #region Lookup Endpoints

        [HttpGet("categories-by-project/{projectId}")]
        public async Task<IActionResult> GetCategoriesByProject(int projectId)
        {
            try
            {
                var cacheKey = CacheKeys.CategoriesByProject(projectId);
                
                if (!_cache.TryGetValue(cacheKey, out object? cachedResult))
                {
                    var categories = await _journalService.GetCategoriesByProjectAsync(projectId);
                    if (categories == null || !categories.Any())
                    {
                        return Ok(new { success = true, data = Array.Empty<object>() });
                    }

                    cachedResult = categories.Select(c => new { id = c.CategoryId, name = c.CategoryName }).ToList();
                    
                    // Cache indefinitely - will be invalidated manually on CRUD operations
                    _cache.Set(cacheKey, cachedResult);
                    CacheInvalidationService.RegisterCategoryKey(cacheKey);
                    
                    _logger.LogDebug("Cached categories for project {ProjectId}", projectId);
                }

                return Ok(new { success = true, data = cachedResult });
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
                    var accounts = await _journalService.GetEnabledAccountsByCategoryAsync(categoryId);
                    cachedResult = accounts.Select(a => new { id = a.AccountId, name = a.AccountName }).ToList();
                    
                    // Cache indefinitely - will be invalidated manually on CRUD operations
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
                    data = locations.Select(l => new
                    {
                        locationId = l.LocationId,
                        locationName = l.LocationName
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading locations");
                return StatusCode(500, new { success = false, message = "Error loading locations" });
            }
        }

        #endregion

        #region Helper Methods

        private async Task<JournalVoucher> CreateNewVoucherAsync(JournalEntryViewModel model)
        {
            var journalVoucher = await _journalService.CreateJournalVoucherAsync(model);

            if (model.SaveAsRecipe && !string.IsNullOrWhiteSpace(model.RecipeName))
            {
                try
                {
                    int recipeId = await _journalService.CreateRecipeFromVoucherAsync(model, journalVoucher.JournalVoucherId);
                    _logger.LogInformation("Recipe '{RecipeName}' saved with ID {RecipeId}", model.RecipeName, recipeId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving recipe from voucher {VoucherId}", journalVoucher.JournalVoucherId);
                }
            }

            return journalVoucher;
        }

        private async Task<JournalVoucher> UpdateExistingVoucherAsync(int voucherId, JournalEntryViewModel model)
        {
            return await _journalService.UpdateJournalVoucherAsync(voucherId, model);
        }

        #endregion
    }
}

