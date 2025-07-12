using Microsoft.AspNetCore.Mvc;
using PureLedger.Models;
using PureLedger.Services;
using PureLedger.ViewModels;
using System;
using System.Threading.Tasks;

namespace PureLedger.Controllers
{
    public class DeliveryChallanController : Controller
    {
        private readonly IDeliveryChallanService _deliveryChallanService;
        private readonly ILogger<DeliveryChallanController> _logger;

        public DeliveryChallanController(
            IDeliveryChallanService deliveryChallanService,
            ILogger<DeliveryChallanController> logger)
        {
            _deliveryChallanService = deliveryChallanService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // Load projects for dropdown
                var projects = await _deliveryChallanService.GetAllProjectsAsync();
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
        public async Task<IActionResult> GetFilteredEntries([FromQuery] DeliveryChallanFilterViewModel filter)
        {
            try
            {
                var result = await _deliveryChallanService.GetFilteredEntriesForDeliveryChallanAsync(
                    filter.SinceDate, filter.ToDate, filter.ProjectId, filter.CategoryId, filter.AccountId);

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
                var categories = await _deliveryChallanService.GetCategoriesByProjectAsync(projectId);
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
                var accounts = await _deliveryChallanService.GetAccountsByCategoryAsync(categoryId);
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
                var locations = await _deliveryChallanService.GetAllLocationsAsync();
                return Json(locations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting locations");
                return Json(new { success = false, message = "An error occurred while retrieving locations." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetVoucherEntries(int voucherId)
        {
            try
            {
                var entries = await _deliveryChallanService.GetEntriesByVoucherIdAsync(voucherId);
                return Json(entries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting entries for voucher {VoucherId}", voucherId);
                return Json(new { success = false, message = "An error occurred while retrieving entries." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> CheckExistingChallan(int voucherId)
        {
            try
            {
                var deliveryChallanVoucher = await _deliveryChallanService.GetDeliveryChallanVoucherByJournalVoucherIdAsync(voucherId);

                if (deliveryChallanVoucher != null)
                {
                    return Json(new
                    {
                        exists = true,
                        challanId = deliveryChallanVoucher.DeliveryChallanId,
                        challan = new
                        {
                            challanDate = deliveryChallanVoucher.DCDate,
                            locationId = deliveryChallanVoucher.DeliveryChallanEntries.FirstOrDefault()?.LocationId,
                            remarks = deliveryChallanVoucher.ReferenceNumber
                        }
                    });
                }

                return Json(new { exists = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existing challan for voucher {VoucherId}", voucherId);
                return Json(new { exists = false, success = false, message = "An error occurred while checking for existing challan." });
            }
        }
        [HttpPost]
        public async Task<IActionResult> PostSingleEntry([FromBody] CreateDeliveryChallanEntryViewModel model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state: {@ModelState}", ModelState);
                return BadRequest(new { success = false, message = "Invalid data submitted." });
            }

            try
            {
                var journalEntry = await _deliveryChallanService.GetJournalEntryByIdAsync(model.JournalEntryId);
                if (journalEntry == null)
                {
                    return NotFound(new { success = false, message = $"Journal entry with ID {model.JournalEntryId} not found." });
                }

                var deliveryChallanVoucher = await _deliveryChallanService.GetDeliveryChallanVoucherByJournalVoucherIdAsync(journalEntry.JournalVoucherId);

                if (deliveryChallanVoucher == null)
                {
                    var dcDate = string.IsNullOrEmpty(model.DCDate)
                        ? DateOnly.FromDateTime(DateTime.Now)
                        : DateOnly.Parse(model.DCDate);

                    deliveryChallanVoucher = await _deliveryChallanService.CreateDeliveryChallanVoucherAsync(
                        journalEntry.JournalVoucherId, dcDate);

                    if (!string.IsNullOrEmpty(model.OtherReference))
                    {
                        deliveryChallanVoucher.ReferenceNumber = model.OtherReference;
                        await _deliveryChallanService.UpdateDeliveryChallanVoucherAsync(deliveryChallanVoucher);
                    }
                }

                var deliveryChallanEntry = await _deliveryChallanService.CreateDeliveryChallanEntryAsync(
                    deliveryChallanVoucher.DeliveryChallanId,
                    model.JournalEntryId,
                    model.LocationId,
                    model.Remarks,
                    model.OtherReference);

                return Json(new
                {
                    success = true,
                    message = "Entry successfully added to delivery challan.",
                    deliveryChallanId = deliveryChallanVoucher.DeliveryChallanId,
                    dcEntryId = deliveryChallanEntry.DCEntryId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating delivery challan entry");
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }



        [HttpPost]
        public async Task<IActionResult> ProcessAll([FromBody] DeliveryChallanBatchViewModel model)
        {
            if (!ModelState.IsValid || model.SelectedEntries == null || !model.SelectedEntries.Any())
            {
                return BadRequest(new { success = false, message = "Invalid data submitted or no entries selected." });
            }

            try
            {
                var results = new List<object>();
                int journalVoucherId = model.JournalVoucherId;

                // Create or get the delivery challan voucher
                var deliveryChallanVoucher = await _deliveryChallanService.GetDeliveryChallanVoucherByJournalVoucherIdAsync(journalVoucherId);

                // If no delivery challan voucher exists, create one
                if (deliveryChallanVoucher == null)
                {
                    var dcDate = string.IsNullOrEmpty(model.DCDate)
                        ? DateOnly.FromDateTime(DateTime.Now)
                        : DateOnly.Parse(model.DCDate);

                    deliveryChallanVoucher = await _deliveryChallanService.CreateDeliveryChallanVoucherAsync(
                        journalVoucherId, dcDate);

                    // Set reference if provided
                    if (!string.IsNullOrEmpty(model.OtherReference))
                    {
                        deliveryChallanVoucher.ReferenceNumber = model.OtherReference;
                        await _deliveryChallanService.UpdateDeliveryChallanVoucherAsync(deliveryChallanVoucher);
                    }
                }

                // Process each selected entry
                foreach (var entryId in model.SelectedEntries)
                {
                    try
                    {
                        // Create delivery challan entry
                        var deliveryChallanEntry = await _deliveryChallanService.CreateDeliveryChallanEntryAsync(
                            deliveryChallanVoucher.DeliveryChallanId,
                            entryId,
                            model.LocationId,
                            model.Remarks,
                            model.OtherReference);

                        results.Add(new
                        {
                            entryId,
                            success = true
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing entry {EntryId}", entryId);
                        results.Add(new
                        {
                            entryId,
                            success = false,
                            message = ex.Message
                        });
                    }
                }

                return Json(new
                {
                    success = true,
                    message = $"Processed {results.Count(r => ((dynamic)r).success == true)} out of {model.SelectedEntries.Count} entries.",
                    results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing multiple entries");
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }
    }
}