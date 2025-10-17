using Microsoft.AspNetCore.Mvc;
using PureLedger.Attributes;
using PureLedger.Models;
using PureLedger.Services;
using PureLedger.ViewModels;

namespace PureLedger.Controllers
{
    [ControllerAccess]
    public class ReportingController : Controller
    {
        private readonly IReportingService _reportingService;
        private readonly ICurrentUserService _currentUserService;

        public ReportingController(
            IReportingService reportingService, 
            ICurrentUserService currentUserService)
        {
            _reportingService = reportingService;
            _currentUserService = currentUserService;
        }

        #region Voucher Reports

        [HttpGet]
        public IActionResult SingleDoc()
        {
            return View("SingleDoc");
        }

        [HttpGet]
        public async Task<IActionResult> PrintVoucher(int journalVoucherId, VoucherType type, string filter = "all")
        {
            if (!IsValidInput(journalVoucherId, type, out IActionResult errorResult))
                return errorResult;

            var report = await _reportingService.GetSingleDocReportAsync(journalVoucherId, type);
            if (report == null)
                return HandleVoucherNotFound(journalVoucherId, type);

            if (type == VoucherType.INV && !string.IsNullOrEmpty(report.CustomerAccountName))
            {
                await AddCustomerBalanceToViewBag(journalVoucherId);
            }

            if (type == VoucherType.JV)
            {
                ViewBag.Filter = filter;
            }

            ViewBag.Type = type.ToString();
            return View("PrintVoucher", report);
        }

        [HttpGet]
        public async Task<IActionResult> ValidateVoucher(int journalVoucherId, string type)
        {
            if (Enum.TryParse<VoucherType>(type, out var voucherType))
            {
                var result = await _reportingService.ValidateVoucherAsync(journalVoucherId, voucherType);
                return Json(new { exists = result.Exists, isBaseVoucher = result.IsBaseVoucher });
            }
            return BadRequest(new { error = "Invalid voucher type" });
        }

        #endregion

        #region Aging Report

        [HttpGet]
        public async Task<IActionResult> AgingReport()
        {
            var viewModel = await CreateReportBaseViewModel<AgingReportPageViewModel>();
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetAgingReportData([FromQuery] AgingReportRequest request)
        {
            var report = await _reportingService.GetAgingReportAsync(request);
            return Json(report);
        }

        #endregion

        #region Account Report

        [HttpGet]
        public async Task<IActionResult> AccountReport()
        {
            var viewModel = await CreateReportBaseViewModel<AgingReportPageViewModel>();
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetAccountReportData([FromQuery] AccountReportRequest request)
        {
            var report = await _reportingService.GetAccountReportAsync(request);
            return Json(report);
        }

        #endregion

        #region Master Report

        [HttpGet]
        public async Task<IActionResult> MasterReport()
        {
            var viewModel = await CreateReportBaseViewModel<MasterReportPageViewModel>();
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetMasterReportData([FromQuery] MasterReportRequest request)
        {
            var report = await _reportingService.GetMasterReportAsync(request);
            return Json(report);
        }

        #endregion

        #region Product Report

        [HttpGet]
        public async Task<IActionResult> ProductReport()
        {
            var viewModel = await CreateReportBaseViewModel<ProductReportPageViewModel>();
            
            // Get locations for the dropdown
            var locations = await _reportingService.GetAllLocationsAsync();
            viewModel.Locations = locations.Select(l => new LocationViewModel
            {
                Id = l.LocationId.ToString(),
                Name = l.LocationName
            }).ToList();
            
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetProductReportData([FromQuery] ProductReportRequest request)
        {
            var report = await _reportingService.GetProductReportAsync(request);
            return Json(report);
        }

        #endregion

        #region Missed Entries Report

        [HttpGet]
        public async Task<IActionResult> MissedEntriesReport()
        {
            var viewModel = await CreateReportBaseViewModel<MissedEntriesReportViewModel>();
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetMissedEntriesData([FromQuery] MissedEntriesReportRequest request)
        {
            var report = await _reportingService.GetMissedEntriesDetailAsync(request);
            return Json(report);
        }

        [HttpGet]
        public async Task<IActionResult> GetMissedEntriesSummary([FromQuery] MissedEntriesSummaryRequest request)
        {
            var summary = await _reportingService.GetMissedEntriesSummaryAsync(request);
            return Json(summary);
        }

        [HttpGet]
        public async Task<IActionResult> GetMissedEntriesByCustomer([FromQuery] MissedEntriesReportRequest request)
        {
            var report = await _reportingService.GetMissedEntriesByCustomerAsync(request);
            return Json(report);
        }

        [HttpGet]
        public async Task<IActionResult> GetMissedEntriesByProduct([FromQuery] MissedEntriesReportRequest request)
        {
            var report = await _reportingService.GetMissedEntriesByProductAsync(request);
            return Json(report);
        }

        #endregion

        #region Lookup APIs

        [HttpGet]
        public async Task<IActionResult> GetCategoriesByProject(int projectId)
        {
            try
            {
                var categories = await _reportingService.GetCategoriesByProjectAsync(projectId);

                if (categories == null || !categories.Any())
                {
                    return Json(new object[] { });
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

        #endregion

        #region Helper Methods

        private bool IsValidInput(int journalVoucherId, VoucherType type, out IActionResult errorResult)
        {
            errorResult = null;
            if (journalVoucherId <= 0)
            {
                TempData["Error"] = "Invalid input.";
                errorResult = RedirectToAction("SingleDoc");
                return false;
            }
            return true;
        }

        private IActionResult HandleVoucherNotFound(int journalVoucherId, VoucherType type)
        {
            string message = type switch
            {
                VoucherType.DC => $"No Delivery Challan exists for Voucher ID {journalVoucherId}.",
                VoucherType.INV => $"No Invoice exists for Voucher ID {journalVoucherId}.",
                _ => $"No Journal Voucher exists for Voucher ID {journalVoucherId}."
            };

            TempData["Error"] = message;
            return RedirectToAction("SingleDoc");
        }

        private async Task<T> CreateReportBaseViewModel<T>() where T : class, new()
        {
            var userId = _currentUserService.GetCurrentUserId();
            var projects = userId.HasValue
                ? await _reportingService.GetAllProjectsForUserAsync(userId.Value)
                : new List<Project>();
            var categories = await _reportingService.GetAllCategoriesAsync();

            var viewModel = new T();

            var projectsProperty = typeof(T).GetProperty("Projects");
            if (projectsProperty != null && projectsProperty.PropertyType == typeof(List<ProjectViewModel>))
            {
                var projectViewModels = projects.Select(p => new ProjectViewModel
                {
                    Id = p.ProjectId.ToString(),
                    Name = p.ProjectName
                }).ToList();

                projectsProperty.SetValue(viewModel, projectViewModels);
            }

            var categoriesProperty = typeof(T).GetProperty("Categories");
            if (categoriesProperty != null && categoriesProperty.PropertyType == typeof(List<CategoryViewModel>))
            {
                var categoryViewModels = categories.Select(c => new CategoryViewModel
                {
                    Id = c.CategoryId.ToString(),
                    Name = c.CategoryName
                }).ToList();

                categoriesProperty.SetValue(viewModel, categoryViewModels);
            }

            return viewModel;
        }

        private async Task AddCustomerBalanceToViewBag(int journalVoucherId)
        {
            try
            {
                // Get current running balance
                var balance = await _reportingService.GetAccountBalanceUpToVoucherAsync(journalVoucherId);
                ViewBag.CustomerRunningBalance = balance;
                
                // Get previous balance (before this voucher)
                var previousBalance = await _reportingService.GetAccountBalanceBeforeVoucherAsync(journalVoucherId);
                ViewBag.PreviousBalance = previousBalance;

                Console.WriteLine($"Customer balance for voucher {journalVoucherId}: {balance}, Previous: {previousBalance}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculating balance for voucher {journalVoucherId}: {ex.Message}");
                ViewBag.CustomerRunningBalance = 0m;
                ViewBag.PreviousBalance = 0m;
            }
        }

        #endregion
    }
}