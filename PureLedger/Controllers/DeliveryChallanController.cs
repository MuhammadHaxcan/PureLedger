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

        #region View Pages

        public async Task<IActionResult> Index()
        {
            try
            {
                var projects = await _deliveryChallanService.GetAllProjectsAsync();
                ViewBag.Projects = projects;

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

        #endregion

        #region API Methods - GET

        [HttpGet]
        public async Task<IActionResult> GetFilteredEntries([FromQuery] DeliveryChallanFilterViewModel filter)
        {
            try
            {
                _logger.LogInformation("GetFilteredEntries called with parameters: SinceDate={SinceDate}, ToDate={ToDate}, ProjectId={ProjectId}, CategoryId={CategoryId}, AccountId={AccountId}",
                    filter.SinceDate, filter.ToDate, filter.ProjectId, filter.CategoryId, filter.AccountId);

                // Set default dates if not provided
                if (!filter.SinceDate.HasValue)
                {
                    filter.SinceDate = DateOnly.FromDateTime(DateTime.Now.AddDays(-30));
                }

                if (!filter.ToDate.HasValue)
                {
                    filter.ToDate = DateOnly.FromDateTime(DateTime.Now);
                }

                var result = await _deliveryChallanService.GetFilteredEntriesForDeliveryChallanAsync(
                    filter.SinceDate, filter.ToDate, filter.ProjectId, filter.CategoryId, filter.AccountId);

                _logger.LogInformation("GetFilteredEntries returning {VoucherCount} vouchers",
                    result.Vouchers.Count);

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting filtered entries");
                return Json(new { success = false, message = "An error occurred while retrieving data: " + ex.Message });
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
                    var firstEntry = deliveryChallanVoucher.DeliveryChallanEntries.FirstOrDefault();

                    return Json(new
                    {
                        exists = true,
                        challanId = deliveryChallanVoucher.DeliveryChallanId,
                        challan = new
                        {
                            challanDate = deliveryChallanVoucher.DCDate,
                            locationId = firstEntry?.LocationId,
                            remarks = deliveryChallanVoucher.ReferenceNumber,
                            otherRef = firstEntry?.OtherReference
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

        #endregion

        #region API Methods - POST

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

                // Get or create delivery challan voucher
                var deliveryChallanVoucher = await GetOrCreateDeliveryChallanVoucherAsync(
                    journalEntry.JournalVoucherId, model.DCDate, model.OtherReference);

                // Create delivery challan entry
                var deliveryChallanEntry = await _deliveryChallanService.CreateDeliveryChallanEntryAsync(
                    deliveryChallanVoucher.DeliveryChallanId,
                    model.JournalEntryId,
                    model.LocationId,
                    model.Remarks,
                    model.OtherReference);

                // Update the voucher processed status
                await _deliveryChallanService.UpdateJournalVoucherProcessedStatusAsync(journalEntry.JournalVoucherId);

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
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid data submitted." });
            }

            try
            {
                int journalVoucherId = model.JournalVoucherId;

                // Get unprocessed entries
                var unprocessedEntries = await _deliveryChallanService.GetUnprocessedEntriesByVoucherIdAsync(journalVoucherId);

                if (!unprocessedEntries.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "No unprocessed entries found for this voucher."
                    });
                }

                // Get or create delivery challan voucher
                var deliveryChallanVoucher = await GetOrCreateDeliveryChallanVoucherAsync(
                    journalVoucherId, model.DCDate, model.OtherReference);

                // Process all entries
                var results = await ProcessEntriesBatchAsync(
                    deliveryChallanVoucher.DeliveryChallanId,
                    unprocessedEntries,
                    model.LocationId,
                    model.Remarks,
                    model.OtherReference);

                // Update the voucher processed status
                await _deliveryChallanService.UpdateJournalVoucherProcessedStatusAsync(journalVoucherId);

                return Json(new
                {
                    success = true,
                    message = $"Processed {results.Count(r => r.Success)} out of {unprocessedEntries.Count} entries.",
                    results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing multiple entries");
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddCartage([FromBody] CartageViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid data submitted." });
            }

            try
            {
                var result = await _deliveryChallanService.ProcessCartageAndDeliveryAsync(
                    model.JournalVoucherId,
                    model.EntryId,
                    model.TransporterId,
                    model.Amount,
                    model.PostToCustomer,
                    model.Referece,
                    model.DcDate,
                    model.DcRemarks,
                    model.DcStation,
                    model.DcOtherRef,
                    model.DcLocation
                );

                return Json(new
                {
                    success = true,
                    message = "Cartage added and delivery challan processed successfully",
                    deliveryChallanId = result.DeliveryChallanId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing cartage");
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        #endregion

        #region Helper Methods

        private async Task<DeliveryChallanVoucher> GetOrCreateDeliveryChallanVoucherAsync(
            int journalVoucherId, DateOnly dcDate, string otherReference)
        {
            var deliveryChallanVoucher = await _deliveryChallanService.GetDeliveryChallanVoucherByJournalVoucherIdAsync(journalVoucherId);

            if (deliveryChallanVoucher == null)
            {
                // Convert DateTimeOnly to DateOnly
                var parsedDcDate = DateOnly.FromDateTime(dcDate.ToDateTime(TimeOnly.MinValue));

                deliveryChallanVoucher = await _deliveryChallanService.CreateDeliveryChallanVoucherAsync(
                    journalVoucherId, parsedDcDate);

                if (!string.IsNullOrEmpty(otherReference))
                {
                    deliveryChallanVoucher.ReferenceNumber = otherReference;
                    await _deliveryChallanService.UpdateDeliveryChallanVoucherAsync(deliveryChallanVoucher);
                }
            }

            return deliveryChallanVoucher;
        }

        private async Task<List<EntryProcessResult>> ProcessEntriesBatchAsync(
            int deliveryChallanId, List<JournalEntry> entries, int locationId, string remarks, string otherReference)
        {
            var processResults = new List<EntryProcessResult>();

            // Process entries in parallel for better performance
            var tasks = entries.Select(async entry =>
            {
                try
                {
                    var deliveryChallanEntry = await _deliveryChallanService.CreateDeliveryChallanEntryAsync(
                        deliveryChallanId,
                        entry.EntryId,
                        locationId,
                        remarks,
                        otherReference);

                    return new EntryProcessResult
                    {
                        EntryId = entry.EntryId,
                        Success = true
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing entry {EntryId}", entry.EntryId);

                    return new EntryProcessResult
                    {
                        EntryId = entry.EntryId,
                        Success = false,
                        Message = ex.Message
                    };
                }
            });

            // Wait for all tasks to complete
            var taskResults = await Task.WhenAll(tasks);

            return taskResults.ToList();
        }

        #endregion

        #region Helper Classes

        private class EntryProcessResult
        {
            public int EntryId { get; set; }
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        #endregion
    }
}