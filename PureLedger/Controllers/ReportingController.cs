// Controllers/ReportingController.cs
using Microsoft.AspNetCore.Mvc;
using PureLedger.Services;
using PureLedger.ViewModels;
using System.Threading.Tasks;

namespace PureLedger.Controllers
{
    public class ReportingController : Controller
    {
        private readonly IReportingService _reportingService;

        public ReportingController(IReportingService reportingService)
        {
            _reportingService = reportingService;
        }

        [HttpGet]
        public IActionResult SingleDoc()
        {
            return View("SingleDoc");
        }

        [HttpGet]
        public async Task<IActionResult> PrintVoucher(int journalVoucherId, string type)
        {
            if (!IsValidInput(journalVoucherId, type, out IActionResult errorResult))
                return errorResult;

            var report = await _reportingService.GetSingleDocReportAsync(journalVoucherId, type);
            if (report == null)
                return HandleVoucherNotFound(journalVoucherId, type);

            // For Invoice, calculate running balance for the customer account
            if (type == "INV" && !string.IsNullOrEmpty(report.CustomerAccountName))
            {
                // You may need to use AccountId instead of AccountName for accuracy
                var balance = await _reportingService.GetAccountBalanceUpToVoucherAsync(
                    journalVoucherId
                );
                ViewBag.CustomerRunningBalance = balance;
            }

            ViewBag.Type = type;
            return View("PrintVoucher", report);
        }

        [HttpGet]
        public async Task<IActionResult> ValidateVoucher(int journalVoucherId, string type)
        {
            bool exists = await _reportingService.GetSingleDocReportAsync(journalVoucherId, type) != null;
            return Json(new { exists });
        }

        [HttpGet]
        public async Task<IActionResult> AgingReport()
        {
            // Fetch projects and categories from your services/repositories
            var projects = await _reportingService.GetAllProjectsAsync();
            var categories = await _reportingService.GetAllCategoriesAsync();

            var viewModel = new AgingReportPageViewModel
            {
                Projects = projects.Select(p => new ProjectViewModel { Id = p.ProjectId.ToString(), Name = p.ProjectName }).ToList(),
                Categories = categories.Select(c => new CategoryViewModel { Id = c.CategoryId.ToString(), Name = c.CategoryName }).ToList()
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetAgingReportData([FromQuery] AgingReportRequest request)
        {
            var report = await _reportingService.GetAgingReportAsync(request);
            return Json(report);
        }

        [HttpGet]
        public async Task<IActionResult> AccountReport()
        {
            var projects = await _reportingService.GetAllProjectsAsync();
            var categories = await _reportingService.GetAllCategoriesAsync();
            return View(new AgingReportPageViewModel
            {
                Projects = projects.Select(p => new ProjectViewModel { Id = p.ProjectId.ToString(), Name = p.ProjectName }).ToList(),
                Categories = categories.Select(c => new CategoryViewModel { Id = c.CategoryId.ToString(), Name = c.CategoryName }).ToList()
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetAccountReportData([FromQuery] AccountReportRequest request)
        {
            var report = await _reportingService.GetAccountReportAsync(request);
            return Json(report);
        }

        [HttpGet]
        public async Task<IActionResult> MasterReport()
        {
            var projects = await _reportingService.GetAllProjectsAsync();
            var categories = await _reportingService.GetAllCategoriesAsync();

            var viewModel = new MasterReportPageViewModel
            {
                Projects = projects.Select(p => new ProjectViewModel { Id = p.ProjectId.ToString(), Name = p.ProjectName }).ToList(),
                Categories = categories.Select(c => new CategoryViewModel { Id = c.CategoryId.ToString(), Name = c.CategoryName }).ToList()
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetMasterReportData([FromQuery] MasterReportRequest request)
        {
            var report = await _reportingService.GetMasterReportAsync(request);
            return Json(report);
        }

        // New API endpoints to replace the ones from CommonApiController

        [HttpGet]
        public async Task<IActionResult> GetCategoriesByProject(int projectId)
        {
            try
            {
                // Get categories directly instead of trying to access project.Categories
                var categories = await _reportingService.GetCategoriesByProjectAsync(projectId);
                
                if (categories == null || !categories.Any())
                {
                    return Json(new object[] { }); // Return empty array instead of 404
                }
                
                var result = categories.Select(c => new { id = c.CategoryId, name = c.CategoryName });
                return Json(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = $"Error loading categories: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAccountsByCategory(int categoryId)
        {
            try
            {
                var accounts = await _reportingService.GetAccountsByCategoryAsync(categoryId);
                var result = accounts.Select(a => new { id = a.AccountId, name = a.AccountName });
                return Json(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = $"Error loading accounts: {ex.Message}" });
            }
        }

        // --- Helpers ---

        private bool IsValidInput(int journalVoucherId, string type, out IActionResult errorResult)
        {
            errorResult = null;
            if (string.IsNullOrWhiteSpace(type) || journalVoucherId <= 0)
            {
                TempData["Error"] = "Invalid input.";
                errorResult = RedirectToAction("SingleDoc");
                return false;
            }
            return true;
        }

        private IActionResult HandleVoucherNotFound(int journalVoucherId, string type)
        {
            string message = type switch
            {
                "DC" => $"No Delivery Challan exists for Voucher ID {journalVoucherId}.",
                "INV" => $"No Invoice exists for Voucher ID {journalVoucherId}.",
                _ => $"No Journal Voucher exists for Voucher ID {journalVoucherId}."
            };

            TempData["Error"] = message;
            return RedirectToAction("SingleDoc");
        }
    }
}
