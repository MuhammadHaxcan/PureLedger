using Microsoft.AspNetCore.Mvc;
using PureLedger.Attributes;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Services;
using PureLedger.ViewModels;
using PureLedger.ViewModels.PureLedger.ViewModels;
using System.Diagnostics;

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

        [HttpGet]
        [ActionName("Entry")]
        public async Task<IActionResult> Entry(int? projectId = null)
        {
            await LoadProjectsData(projectId);
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetProductDetails(int accountId)
        {
            var productData = await _journalService.GetProductDetailsAsync(accountId);
            return Json(productData);
        }

        [HttpPost]
        public async Task<IActionResult> Entry(JournalEntryViewModel model, string action)
        {
            //var errors = await _validator.ValidateAsync(model);
            //if (errors.Any())
            //{
            //    await LoadProjectsData(model.ProjectId);
            //    ViewBag.Error = string.Join("<br/>", errors);
            //    return View(model);
            //}

            try
            {
                var journalVoucher = await _journalService.CreateJournalVoucherAsync(model);

                if (action == "submit_print")
                {
                    // Redirect to the print page with the new JV id using URL helper
                    // This creates a proper, dynamic URL rather than hardcoding it
                    return RedirectToAction("PrintVoucher", "Reporting", 
                        new { 
                            journalVoucherId = journalVoucher.JournalVoucherId, 
                            type = "JV" 
                        });
                }
                else
                {
                    TempData["Success"] = $"Journal entries created. Jv-{journalVoucher.JournalVoucherId}";
                    return RedirectToAction(nameof(Entry), new { projectId = model.ProjectId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving journal entries");
                await LoadProjectsData(model.ProjectId);
                ViewBag.Error = "An error occurred while saving. Please try again.";
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> VoucherEntry(int? jvId = null)
        {
            await LoadProjectsData();

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
        public async Task<IActionResult> EditVoucher(int jvId)
        {
            var voucher = await _journalService.GetVoucherWithEntriesAsync(jvId);
            if (voucher == null)
                return NotFound();

            var model = await _journalService.MapVoucherToViewModelAsync(voucher);
            await LoadProjectsData(voucher.ProjectId);

            return View("Entry", model);
        }

        [HttpGet]
        public async Task<IActionResult> GetVoucherSummary(int voucherId)
        {
            var summary = await _journalService.GetVoucherSummaryAsync(voucherId);
            return Json(summary);
        }

        [HttpGet]
        public async Task<IActionResult> GetJournalEntry(int entryId)
        {
            var model = await _journalService.GetJournalEntryForEditAsync(entryId);
            if (model == null)
                return NotFound();

            return Json(model);
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
                return success
                    ? JsonSuccess()
                    : JsonError("Error updating entry");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateJournalEntry: {Message}", ex.Message);
                return JsonError("An unexpected error occurred");
            }
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

        [HttpPost]
        public async Task<IActionResult> DeleteJournalEntry(int entryId)
        {
            var success = await _journalService.DeleteJournalEntryAsync(entryId);
            return success
                ? JsonSuccess()
                : JsonError("Invalid entry.");
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

        [HttpGet]
        public async Task<IActionResult> GetCategoriesByProject(int projectId)
        {
            try
            {
                // Get categories directly from repository through the service
                // We need to add this method to JournalEntryService if it doesn't exist
                var categories = await _journalService.GetCategoriesByProjectAsync(projectId);
                
                if (categories == null || !categories.Any())
                {
                    // Return empty array instead of 404 for better frontend handling
                    return Json(new object[] { });
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
                // This uses the JournalEntryService to get accounts by category
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
        public async Task<IActionResult> BaseVouchers()
        {
            try
            {
                var baseVouchers = await _journalService.GetAllBaseVouchersAsync();
                
                // Log some diagnostic info
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
                        .OrderBy(e => e.IsProcessed) // Unprocessed entries first
                        .ThenBy(e => e.JournalEntryId)
                        .Select(e => new PureLedger.ViewModels.BaseVoucherEntryViewModel 
                        {
                            EntryId = e.JournalEntryId,
                            Description = e.Description,
                            AccountName = e.Account?.AccountName,
                            AccountId = e.AccountId,
                            CategoryId = e.CategoryId, // Include CategoryId
                            AvailableQuantity = e.IsProcessed ? 0 : e.Quantity,
                            ProcessQuantity = 0,  // Default to 0, user will set this
                            Rate = e.Rate,
                            Debit = e.Debit,
                            Credit = e.Credit,
                            Percent = e.DiscountPercent ?? 0, // Include discount percent
                            Disc = e.Discount, // Include discount amount
                            Rbp = e.Rbp, // Include Rbp
                            AccountGroupId = e.Account?.AccountGroupId ?? 0,
                            // Include tags if present
                            Tags = e.JournalEntryTags?.Select(t => t.TagId).ToList()
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
                    .Where(e => e.ProcessQuantity > 0)
                    .ToList();

                // Add entries where AccountGroupId == 1
                entriesToProcess.AddRange(
                    model.Entries.Where(e => e.AccountGroupId == 1)
                );
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
                    model.CreditableDate // <-- Pass the creditable date
                );

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

        #region Helper Methods
        private async Task LoadProjectsData(int? selectedProjectId = null)
        {
            var userId = _currentUserService.GetCurrentUserId();
            if (userId.HasValue)
            {
                var projects = await _journalService.GetAllProjectsForUserAsync(userId.Value);
                ViewBag.Projects = projects;
                ViewBag.SelectedProjectId = selectedProjectId;
            }
            else
            {
                ViewBag.Projects = new List<Project>();
                ViewBag.SelectedProjectId = selectedProjectId;
            }
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