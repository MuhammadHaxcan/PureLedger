using Microsoft.AspNetCore.Mvc;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Services;
using PureLedger.ViewModels;
using PureLedger.ViewModels.PureLedger.ViewModels;
using System.Diagnostics;

namespace PureLedger.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IJournalEntryService _journalService;
        private readonly JournalEntryValidationService _validator;

        public HomeController(
            IJournalEntryService journalService,
            JournalEntryValidationService validator,
            ILogger<HomeController> logger)
        {
            _journalService = journalService;
            _validator = validator;
            _logger = logger;
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpGet]
        [ActionName("Entry")]
        public async Task<IActionResult> Entry(int? projectId = null)
        {
            var projects = await _journalService.GetAllProjectsAsync();
            ViewBag.Projects = projects;
            ViewBag.SelectedProjectId = projectId;
            return View();
        }


        // AJAX: Get product details for an account
        [HttpGet]
        public async Task<IActionResult> GetProductDetails(int accountId)
        {
            var productData = await _journalService.GetProductDetailsAsync(accountId);
            return Json(productData);
        }

        [HttpPost]
        public async Task<IActionResult> Entry(JournalEntryViewModel model)
        {
            var errors = await _validator.ValidateAsync(model);

            if (errors.Any())
            {
                var projects = await _journalService.GetAllProjectsAsync();
                ViewBag.Projects = projects;
                ViewBag.Error = string.Join("<br/>", errors);
                return View(model);
            }

            try
            {
                var journalVoucher = await _journalService.CreateJournalVoucherAsync(model);
                TempData["Success"] = $"Journal entries created. Jv-{journalVoucher.JvId}";
                return RedirectToAction(nameof(Entry), new { projectId = model.ProjectId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving journal entries");
                var projects = await _journalService.GetAllProjectsAsync();
                ViewBag.Projects = projects;
                ViewBag.Error = "An error occurred while saving. Please try again.";
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> VoucherEntry(int? jvId = null)
        {
            var projects = await _journalService.GetAllProjectsAsync();
            ViewBag.Projects = projects;

            if (jvId.HasValue)
            {
                var voucher = await _journalService.GetVoucherWithEntriesAsync(jvId.Value);
                if (voucher != null)
                {
                    ViewBag.Voucher = voucher;
                    ViewBag.Entries = voucher.JournalEntries.ToList();
                }
            }

            var model = new JournalEntryViewModel();
            return View(model);
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

            var projects = await _journalService.GetAllProjectsAsync();
            ViewBag.Projects = projects;
            ViewBag.SelectedProjectId = voucher.ProjectId;
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
                return Json(new
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
                if (success)
                {
                    return Json(new { success = true });
                }
                else
                {
                    return Json(new { success = false, message = "Error updating entry" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateJournalEntry: {Message}", ex.Message);
                return Json(new { success = false, message = "An unexpected error occurred" });
            }
        }


        [HttpGet]
        public async Task<IActionResult> ModifiedEntries()
        {
            var vouchersWithModifiedEntries = await _journalService.GetVouchersWithModifiedEntriesAsync();
            var modifiedEntriesSummary = vouchersWithModifiedEntries.Select(v => new ModifiedEntriesSummaryViewModel
            {
                JvId = v.JvId,
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
            if (success)
                return Json(new { success = true });
            else
                return Json(new { success = false, message = "Invalid entry." });
        }

        [HttpGet]
        public async Task<IActionResult> VoucherModifiedEntries(int jvId)
        {
            var voucher = await _journalService.GetVoucherWithEntriesAsync(jvId);
            if (voucher == null)
                return NotFound();

            var model = new VoucherModifiedEntriesViewModel
            {
                JvId = voucher.JvId,
                Entries = voucher.JournalEntries.Select(e => new VoucherEntryDetailViewModel
                {
                    EntryId = e.EntryId,
                    ModifyTime = e.IsEdited ? DateTime.Now : null,
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
    }
}