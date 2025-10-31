using Microsoft.AspNetCore.Mvc;
using PureLedger.Attributes;
using PureLedger.Models;
using PureLedger.Services;
using PureLedger.Services.Interface;
using PureLedger.ViewModels;
using PureLedger.ViewModels.Reporting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
            if (journalVoucherId <= 0)
            {
                TempData["Error"] = "Invalid input.";
                return RedirectToAction("SingleDoc");
            }

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

            if (type != VoucherType.JV)
            {
                var voucher = await _reportingService.GetJournalVoucherAsync(journalVoucherId);
                if (voucher != null)
                {
                    var projectCartageSettings = await _reportingService.GetProjectCartageSettingsAsync(voucher.ProjectId);
                    if (!string.IsNullOrEmpty(projectCartageSettings?.ProjectImage))
                    {
                        report.ProjectImage = projectCartageSettings.ProjectImage;
                    }
                }
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
            // Validate the request
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(new { errors });
            }

            try
            {
                var report = await _reportingService.GetAgingReportAsync(request);
                return Json(report);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = $"Error generating report: {ex.Message}" });
            }
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
        public async Task<IActionResult> GetAccountReportData([FromQuery] AccountReportRequestViewModel request)
        {
            // Validate the request
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(new { errors });
            }

            try
            {
                var report = await _reportingService.GetAccountReportAsync(request);
                return Json(report);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = $"Error generating report: {ex.Message}" });
            }
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
            // Parse dates from query string - allow null values
            if (request.StartDate == null && !string.IsNullOrEmpty(Request.Query["startDate"].ToString()))
            {
                if (DateOnly.TryParse(Request.Query["startDate"], out DateOnly startDate))
                    request.StartDate = startDate;
            }
            
            if (request.EndDate == null && !string.IsNullOrEmpty(Request.Query["endDate"].ToString()))
            {
                if (DateOnly.TryParse(Request.Query["endDate"], out DateOnly endDate))
                    request.EndDate = endDate;
            }
            
            var report = await _reportingService.GetProductReportAsync(request);
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

        [HttpGet]
        public async Task<IActionResult> GetAllBookers()
        {
            try
            {
                var bookers = await _reportingService.GetAllBookersAsync();
                
                if (bookers == null || !bookers.Any())
                {
                    return Json(new object[] { });
                }

                var result = bookers.Select(b => new { id = b.BookerID, name = b.BookerName });
                return Json(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = $"Error loading bookers: {ex.Message}" });
            }
        }

        #endregion

        #region Missed Entries

        [HttpGet]
        public async Task<IActionResult> MissedEntriesReport()
        {
            var userId = _currentUserService.GetCurrentUserId();
            var projects = userId.HasValue
                ? await _reportingService.GetAllProjectsForUserAsync(userId.Value)
                : new List<Project>();

            ViewBag.Projects = projects;
            return View(new MissedEntriesListViewModel());
        }

        [HttpGet]
        public async Task<IActionResult> GetMissedEntries(DateOnly? startDate, DateOnly? endDate, int? projectId, int page = 0, int pageSize = 25)
        {
            try
            {
                var report = await _reportingService.GetFilteredMissedEntriesPaginatedAsync(startDate, endDate, projectId, page, pageSize);
                return Json(report);
            }
            catch (Exception ex)
            {
                return Json(new { error = "Failed to load entries" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMissedEntriesSummary(DateOnly? startDate, DateOnly? endDate, int? projectId)
        {
            try
            {
                var summary = await _reportingService.GetMissedEntriesSummaryAsync(startDate, endDate, projectId);
                return Json(summary);
            }
            catch (Exception ex)
            {
                // log if you have logger, but return safe shape expected by client
                return Json(new { error = "Failed to load summary" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMissedEntriesByCustomer(DateOnly? startDate, DateOnly? endDate, int? projectId)
        {
            try
            {
                var result = await _reportingService.GetMissedEntriesByCustomerAsync(startDate, endDate, projectId);
                return Json(result);
            }
            catch (Exception)
            {
                return Json(Array.Empty<object>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMissedEntriesByProduct(DateOnly? startDate, DateOnly? endDate, int? projectId)
        {
            try
            {
                var result = await _reportingService.GetMissedEntriesByProductAsync(startDate, endDate, projectId);
                return Json(result);
            }
            catch (Exception)
            {
                return Json(Array.Empty<object>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMissedEntriesData(int customerAccountId, DateOnly? startDate, DateOnly? endDate, int? projectId)
        {
            try
            {
                var result = await _reportingService.GetMissedEntriesDataAsync(customerAccountId, startDate, endDate, projectId);
                return Json(result);
            }
            catch (Exception)
            {
                return Json(new { entries = new object[] { } });
            }
        }

        #endregion

        #region Helper Methods
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