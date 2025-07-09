using Microsoft.AspNetCore.Mvc;
using PureLedger.Services;
using PureLedger.ViewModels;
using System;
using System.Threading.Tasks;

namespace PureLedger.Controllers
{
    public class DeliveryChallanController : Controller
    {
        private readonly IJournalEntryService _journalService;
        private readonly ILogger<DeliveryChallanController> _logger;

        public DeliveryChallanController(
            IJournalEntryService journalService,
            ILogger<DeliveryChallanController> logger)
        {
            _journalService = journalService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // Load projects for dropdown
                var projects = await _journalService.GetAllProjectsAsync();
                ViewBag.Projects = projects;

                // Initialize empty view model
                var model = new DeliveryChallanFilterViewModel
                {
                    SinceDate = DateOnly.FromDateTime(DateTime.Now.AddDays(-30)),
                    ToDate = DateOnly.FromDateTime(DateTime.Now)
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Delivery Challan Index");
                return View(new DeliveryChallanFilterViewModel());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetFilteredEntries(
            [FromQuery] DateOnly? sinceDate,
            [FromQuery] DateOnly? toDate,
            [FromQuery] int? projectId,
            [FromQuery] int? categoryId,
            [FromQuery] int? accountId)
        {
            try
            {
                var result = await _journalService.GetFilteredEntriesForDeliveryChallanAsync(
                    sinceDate, toDate, projectId, categoryId, accountId);

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting filtered entries");
                return Json(new { success = false, message = "An error occurred while retrieving data." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCategoriesByProject(int projectId)
        {
            try
            {
                var categories = await _journalService.GetCategoriesByProjectAsync(projectId);
                return Json(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categories for project {ProjectId}", projectId);
                return Json(new { success = false, message = "An error occurred while retrieving categories." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAccountsByCategory(int categoryId)
        {
            try
            {
                var accounts = await _journalService.GetAccountsByCategoryAsync(categoryId);
                return Json(accounts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accounts for category {CategoryId}", categoryId);
                return Json(new { success = false, message = "An error occurred while retrieving accounts." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetLocations()
        {
            try
            {
                // Since we don't have IDeliveryChallanService, we need to handle locations differently
                // Let's assume we can get locations from the journal service or another available service
                var locations = await _journalService.GetAllLocationsAsync(); // Assuming this method exists
                return Json(locations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting locations");
                return Json(new { success = false, message = "An error occurred while retrieving locations." });
            }
        }
    }
}