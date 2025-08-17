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
        private readonly JournalEntryValidationService _validator;
        private readonly ICurrentUserService _currentUserService;

        public HomeController(
            JournalEntryService journalService,
            JournalEntryValidationService validator,
            ICurrentUserService currentUserService,
            ILogger<HomeController> logger)
        {
            _journalService = journalService;
            _validator = validator;
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
            var errors = await _validator.ValidateAsync(model);

            if (errors.Any())
            {
                await LoadProjectsData(model.ProjectId);
                ViewBag.Error = string.Join("<br/>", errors);
                return View(model);
            }

            try
            {
                var journalVoucher = await _journalService.CreateJournalVoucherAsync(model);

                if (action == "submit_print")
                {
                    // Redirect to the print page with the new JV id
                    return Redirect($"https://localhost:7062/Reporting/PrintVoucher?journalVoucherId={journalVoucher.JournalVoucherId}&type=JV");
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
                    ModifyTime = e.ModifiedDateTime, // <-- Use the DB value
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