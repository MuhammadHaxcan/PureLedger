using Microsoft.AspNetCore.Mvc;
using PureLedger.Attributes;
using PureLedger.Models;
using PureLedger.Services;
using PureLedger.Services.Interface;
using PureLedger.ViewModels;
using PureLedger.ViewModels.PureLedger.ViewModels;

namespace PureLedger.Controllers
{
    [ControllerAccess]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly JournalEntryService _journalService;
        private readonly ICurrentUserService _currentUserService;

        public HomeController(
            JournalEntryService journalService,
            ICurrentUserService currentUserService,
            ILogger<HomeController> logger)
        {
            _journalService = journalService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        #region Journal Entry Management

        [HttpGet]
        [ActionName("Entry")]
        public async Task<IActionResult> Entry(int? projectId = null)
        {
            await LoadProjectsDataAsync(projectId);
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Entry(JournalEntryViewModel model, string action, int? editingVoucherId = null)
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

                if (action == "submit_print")
                {
                    return RedirectToAction("PrintVoucher", "Reporting",
                        new
                        {
                            journalVoucherId = journalVoucher.JournalVoucherId,
                            type = "JV"
                        });
                }

                return RedirectToAction(nameof(Entry), new { projectId = model.ProjectId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving journal entries");
                await LoadProjectsDataAsync(model.ProjectId);
                ViewBag.Error = "An error occurred while saving. Please try again.";
                return View(model);
            }
        }

        [HttpGet]
        [Route("Home/EditVoucher/{id}")]
        public async Task<IActionResult> EditVoucher(int id)
        {
            try
            {
                _logger.LogInformation("Attempting to edit voucher with ID {VoucherId}", id);

                var voucher = await _journalService.GetVoucherWithEntriesAsync(id);
                if (voucher == null)
                {
                    _logger.LogWarning("Voucher with ID {VoucherId} not found", id);
                    return NotFound("Voucher not found");
                }

                _logger.LogInformation("Found voucher with {EntriesCount} entries", voucher.JournalEntries.Count);

                var model = await _journalService.MapVoucherToViewModelAsync(voucher);
                _logger.LogInformation("Mapped to view model with {EntriesCount} entries", model.Entries.Count);

                await LoadProjectsDataAsync(voucher.ProjectId);

                ViewBag.EditingVoucherId = id;
                ViewBag.Title = $"Edit Journal Voucher #{id}";
                ViewBag.SelectedProjectId = voucher.ProjectId;

                return View("Entry", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing voucher {VoucherId} for editing", id);
                TempData["Error"] = "An error occurred while loading the voucher. Please try again.";
                return RedirectToAction("VoucherEntry");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetJournalEntry(int entryId)
        {
            var model = await _journalService.GetJournalEntryForEditAsync(entryId);
            return model == null ? NotFound() : Json(model);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateJournalEntry([FromForm] JournalEntryUpdateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return JsonError(ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList());
            }

            try
            {
                var success = await _journalService.UpdateJournalEntryAsync(model);
                return success ? JsonSuccess() : JsonError("Error updating entry");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateJournalEntry: {Message}", ex.Message);
                return JsonError("An unexpected error occurred");
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteJournalEntry(int entryId)
        {
            var success = await _journalService.DeleteJournalEntryAsync(entryId);
            return success ? JsonSuccess() : JsonError("Invalid entry.");
        }

        #endregion

        #region Voucher Management

        [HttpGet]
        public async Task<IActionResult> VoucherEntry(int? jvId = null)
        {
            await LoadProjectsDataAsync();

            if (jvId.HasValue)
            {
                var voucher = await _journalService.GetVoucherWithEntriesAsync(jvId.Value);
                if (voucher != null)
                {
                    ViewBag.Voucher = voucher;
                    ViewBag.Entries = voucher.JournalEntries.ToList();
                }
            }

            return View(new JournalEntryViewModel());
        }

        [HttpGet]
        public async Task<IActionResult> VoucherSummary()
        {
            var vouchers = await _journalService.GetAllVouchersAsync();
            return View(vouchers);
        }

        [HttpGet]
        public async Task<IActionResult> GetVoucherSummary(int voucherId)
        {
            var summary = await _journalService.GetVoucherSummaryAsync(voucherId);
            return Json(summary);
        }

        [HttpGet]
        public async Task<IActionResult> ModifiedEntries()
        {
            var vouchersWithModifiedEntries = await _journalService.GetVouchersWithModifiedEntriesAsync();
            var modifiedEntriesSummary = vouchersWithModifiedEntries.Select(v => new ModifiedEntriesSummaryViewModel
            {
                JvId = v.JournalVoucherId,
                TotalDebit = v.TotalDebit,
                TotalCredit = v.TotalCredit,
                HasModifiedEntries = v.JournalEntries.Any(e => e.IsEdited)
            }).ToList();

            return View(modifiedEntriesSummary);
        }

        [HttpGet]
        public async Task<IActionResult> VoucherModifiedEntries(int jvId)
        {
            var voucher = await _journalService.GetVoucherWithEntriesAsync(jvId);
            if (voucher == null)
                return NotFound();

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

            return View(model);
        }

        #endregion

        #region Base Voucher Processing

        [HttpGet]
        public async Task<IActionResult> BaseVouchers()
        {
            try
            {
                var baseVouchers = await _journalService.GetAllBaseVouchersAsync();

                foreach (var voucher in baseVouchers)
                {
                    int unprocessedEntries = voucher.JournalEntries?.Count(e => !e.IsProcessed && e.Quantity > 0) ?? 0;
                    int totalEntries = voucher.JournalEntries?.Count ?? 0;

                    _logger.LogInformation("Base voucher {VoucherId}: {UnprocessedEntries}/{TotalEntries} entries available",
                        voucher.JournalVoucherId, unprocessedEntries, totalEntries);
                }

                return View(baseVouchers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving base vouchers");
                return View(new List<JournalVoucher>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> ProcessBaseVoucher(int id)
        {
            try
            {
                var voucher = await _journalService.GetBaseVoucherWithUnprocessedEntriesAsync(id);
                if (voucher == null)
                {
                    _logger.LogWarning("Base voucher with ID {Id} not found", id);
                    return NotFound();
                }

                var model = new BaseVoucherProcessingViewModel
                {
                    BaseVoucherId = id,
                    ProjectId = voucher.ProjectId,
                    Entries = voucher.JournalEntries
                        .OrderBy(e => e.IsProcessed)
                        .ThenBy(e => e.JournalEntryId)
                        .Select(e => new BaseVoucherEntryViewModel
                        {
                            EntryId = e.JournalEntryId,
                            Description = e.Description,
                            AccountName = e.Account?.AccountName,
                            AccountId = e.AccountId,
                            CategoryId = e.CategoryId,
                            AvailableQuantity = e.IsProcessed ? 0 : e.Quantity,
                            ProcessQuantity = 0,
                            Rate = e.Rate,
                            Debit = e.Debit,
                            Credit = e.Credit,
                            Percent = e.DiscountPercent ?? 0,
                            Disc = e.Discount,
                            Rbp = e.Rbp,
                            AccountGroupId = e.Account?.AccountGroupId ?? 0,
                        })
                        .ToList()
                };

                _logger.LogInformation("Prepared processing model for base voucher {Id} with {EntryCount} entries",
                    id, model.Entries.Count);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing base voucher {Id} for processing", id);
                return RedirectToAction(nameof(BaseVouchers));
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateDerivedVoucher(BaseVoucherProcessingViewModel model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state when creating derived voucher");
                return View("ProcessBaseVoucher", model);
            }

            try
            {
                var entriesToProcess = model.Entries
                    .Where(e => e.ProcessQuantity > 0 || e.AccountGroupId == 1)
                    .ToList();

                if (!entriesToProcess.Any())
                {
                    ModelState.AddModelError("", "Please select at least one entry to process.");
                    return View("ProcessBaseVoucher", model);
                }

                _logger.LogInformation("Creating derived voucher from base voucher {Id} with {EntryCount} entries",
                    model.BaseVoucherId, entriesToProcess.Count);

                var derivedVoucherId = await _journalService.CreateDerivedVoucherAsync(
                    model.BaseVoucherId,
                    entriesToProcess,
                    model.AccountEntryAmount,
                    model.AccountEntryIsDebit,
                    model.CreditableDate);

                _logger.LogInformation("Created derived voucher {DerivedId} from base voucher {BaseId}",
                    derivedVoucherId, model.BaseVoucherId);

                TempData["Success"] = $"Successfully created derived voucher JV-{derivedVoucherId}";
                return RedirectToAction("VoucherEntry", new { jvId = derivedVoucherId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating derived voucher from base voucher {Id}", model.BaseVoucherId);
                ModelState.AddModelError("", ex.Message);
                return View("ProcessBaseVoucher", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> MarkVoucherAsMissed(int voucherId)
        {
            try
            {
                await _journalService.MarkEntriesAsMissedAsync(voucherId);
                return JsonSuccess();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking voucher {VoucherId} as missed", voucherId);
                return JsonError("Error marking entries as missed");
            }
        }

        #endregion

        #region Missed Entries

        [HttpGet]
        public async Task<IActionResult> MissedEntries()
        {
            await LoadProjectsDataAsync();
            return View(new MissedEntriesListViewModel());
        }

        [HttpGet]
        public async Task<IActionResult> GetMissedEntries(DateOnly? startDate, DateOnly? endDate, int? projectId)
        {
            try
            {
                var entries = await _journalService.GetFilteredMissedEntriesAsync(startDate, endDate, projectId);
                return Json(entries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting missed entries");
                return Json(new { error = "Failed to load entries" });
            }
        }

        #endregion

        #region Recipe Management

        [HttpGet]
        public async Task<IActionResult> Recipes(int? projectId = null)
        {
            await LoadProjectsDataAsync(projectId);

            var viewModel = new RecipesViewModel
            {
                ProjectId = projectId ?? 0
            };

            if (projectId.HasValue)
            {
                try
                {
                    var recipes = await _journalService.GetRecipesByProjectAsync(projectId.Value);
                    viewModel.Recipes = recipes.Select(r => new RecipeListItemViewModel
                    {
                        RecipeId = r.RecipeId,
                        RecipeName = r.RecipeName,
                        ProjectName = r.Project?.ProjectName ?? "Unknown Project"
                    }).ToList();

                    _logger.LogInformation("Loaded {Count} recipes for project {ProjectId}",
                        viewModel.Recipes.Count, projectId.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading recipes for project {ProjectId}", projectId);
                    ViewBag.Error = "Failed to load recipes. Please try again.";
                }
            }

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetRecipeItems(int recipeId)
        {
            try
            {
                var recipe = await _journalService.GetRecipeWithItemsAsync(recipeId);
                if (recipe == null)
                    return NotFound();

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

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recipe items for recipe {RecipeId}", recipeId);
                return Json(new { error = "Failed to load recipe items" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> CreateFromRecipe(int recipeId, int projectId)
        {
            try
            {
                _logger.LogInformation("Creating new journal entry from recipe {RecipeId} for project {ProjectId}",
                    recipeId, projectId);

                await LoadProjectsDataAsync(projectId);

                var model = await _journalService.CreateViewModelFromRecipeAsync(recipeId, projectId);

                ViewBag.Title = "New Journal Entry from Recipe";
                ViewBag.SelectedProjectId = projectId;

                return View("Entry", model);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid recipe or project: {Message}", ex.Message);
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Recipes), new { projectId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating entry from recipe {RecipeId}", recipeId);
                TempData["Error"] = "An error occurred while preparing the entry form. Please try again.";
                return RedirectToAction(nameof(Recipes), new { projectId });
            }
        }

        #endregion

        #region Lookup Endpoints

        [HttpGet]
        public async Task<IActionResult> GetCategoriesByProject(int projectId)
        {
            try
            {
                var categories = await _journalService.GetCategoriesByProjectAsync(projectId);

                if (categories == null || !categories.Any())
                {
                    return Json(Array.Empty<object>());
                }

                var result = categories.Select(c => new { id = c.CategoryId, name = c.CategoryName });
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categories for project {ProjectId}", projectId);
                return Json(new { error = "Error loading categories" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAccountsByCategory(int categoryId)
        {
            try
            {
                var accounts = await _journalService.GetAccountsByCategoryAsync(categoryId);
                var result = accounts.Select(a => new { id = a.AccountId, name = a.AccountName });
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accounts for category {CategoryId}", categoryId);
                return Json(new { error = "Error loading accounts" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetProductDetails(int accountId)
        {
            var productData = await _journalService.GetProductDetailsAsync(accountId);
            return Json(productData);
        }

        #endregion

        #region Helper Methods

        private async Task LoadProjectsDataAsync(int? selectedProjectId = null)
        {
            var userId = _currentUserService.GetCurrentUserId();

            if (userId.HasValue)
            {
                var projects = await _journalService.GetAllProjectsForUserAsync(userId.Value);
                var bookers = await _journalService.GetAllBookersAsync();

                ViewBag.Projects = projects;
                ViewBag.Bookers = bookers;
                ViewBag.SelectedProjectId = selectedProjectId;
            }
            else
            {
                ViewBag.Projects = new List<Project>();
                ViewBag.Bookers = new List<Booker>();
                ViewBag.SelectedProjectId = selectedProjectId;
            }
        }

        private async Task<JournalVoucher> CreateNewVoucherAsync(JournalEntryViewModel model)
        {
            var journalVoucher = await _journalService.CreateJournalVoucherAsync(model);
            TempData["Success"] = $"Journal entries created. Jv-{journalVoucher.JournalVoucherId}";

            if (model.SaveAsRecipe && !string.IsNullOrWhiteSpace(model.RecipeName))
            {
                try
                {
                    int recipeId = await _journalService.CreateRecipeFromVoucherAsync(model, journalVoucher.JournalVoucherId);
                    if (recipeId > 0)
                    {
                        TempData["Success"] = $"{TempData["Success"]} Recipe '{model.RecipeName}' was saved.";
                    }
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
            var journalVoucher = await _journalService.UpdateJournalVoucherAsync(voucherId, model);
            TempData["Success"] = $"Journal voucher updated. Jv-{journalVoucher.JournalVoucherId}";
            return journalVoucher;
        }

        private IActionResult JsonSuccess(object? data = null)
        {
            return data != null
                ? Json(new { success = true, data })
                : Json(new { success = true });
        }

        private IActionResult JsonError(string message)
        {
            return Json(new { success = false, message });
        }

        private IActionResult JsonError(List<string> errors)
        {
            return Json(new { success = false, errors });
        }

        #endregion
    }
}