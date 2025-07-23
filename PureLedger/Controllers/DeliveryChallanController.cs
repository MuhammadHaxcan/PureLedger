using Microsoft.AspNetCore.Mvc;
using PureLedger.Models;
using PureLedger.Services;
using PureLedger.ViewModels;

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
                filter.SinceDate ??= DateOnly.FromDateTime(DateTime.Now.AddDays(-30));
                filter.ToDate ??= DateOnly.FromDateTime(DateTime.Now);

                var result = await _deliveryChallanService.GetFilteredEntriesForDeliveryChallanAsync(
                    filter.SinceDate, filter.ToDate, filter.ProjectId, filter.CategoryId, filter.AccountId);

                _logger.LogInformation("GetFilteredEntries returning {VoucherCount} vouchers", result.Vouchers.Count);

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting filtered entries");
                return JsonError("An error occurred while retrieving data", ex);
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
                return JsonError("An error occurred while retrieving locations", ex);
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
                return JsonError("An error occurred while retrieving entries", ex);
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
                return JsonError("An error occurred while checking for existing challan", ex, new { exists = false });
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

                // Get or create delivery challan voucher with reference
                var deliveryChallanVoucher = await _deliveryChallanService.CreateDeliveryChallanVoucherAsync(
                    journalEntry.JournalVoucherId, model.DCDate, model.OtherReference);

                // Create delivery challan entry
                var deliveryChallanEntry = await _deliveryChallanService.CreateDeliveryChallanEntryAsync(
                    deliveryChallanVoucher.DeliveryChallanId,
                    model.JournalEntryId,
                    model.LocationId,  // Explicitly pass the LocationId from the model
                    model.Remarks,
                    model.OtherReference);

                // Update the voucher processed status
                await _deliveryChallanService.UpdateJournalVoucherProcessedStatusAsync(journalEntry.JournalVoucherId);

                return JsonSuccess("Entry successfully added to delivery challan.", new
                {
                    deliveryChallanId = deliveryChallanVoucher.DeliveryChallanId,
                    dcEntryId = deliveryChallanEntry.DCEntryId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating delivery challan entry");
                return JsonError($"An error occurred", ex);
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
                _logger.LogInformation("ProcessAll called with JournalVoucherId={JournalVoucherId}, DCDate={DCDate}, LocationId={LocationId}", 
                    model.JournalVoucherId, model.DCDate, model.LocationId);
                    
                int journalVoucherId = model.JournalVoucherId;

                // Get unprocessed entries
                var unprocessedEntries = await _deliveryChallanService.GetUnprocessedEntriesByVoucherIdAsync(journalVoucherId);

                if (!unprocessedEntries.Any())
                {
                    return JsonError("No unprocessed entries found for this voucher.");
                }

                // Get or create delivery challan voucher
                var deliveryChallanVoucher = await _deliveryChallanService.CreateDeliveryChallanVoucherAsync(
                    journalVoucherId, model.DCDate, model.OtherReference);

                // Process all entries and pass the date from the model
                var results = await ProcessEntriesBatchAsync(
                    deliveryChallanVoucher.DeliveryChallanId,
                    unprocessedEntries,
                    model.LocationId,
                    model.Remarks,
                    model.OtherReference,
                    model.DCDate);  // Pass the date from the model

                // Update the voucher processed status
                await _deliveryChallanService.UpdateJournalVoucherProcessedStatusAsync(journalVoucherId);

                return JsonSuccess($"Processed {results.Count(r => r.Success)} out of {unprocessedEntries.Count} entries.", new { results });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing multiple entries");
                return JsonError($"An error occurred", ex);
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
                var result = await _deliveryChallanService.ProcessCartageAndDeliveryAsync(model);

                return JsonSuccess("Cartage added and delivery challan processed successfully", new
                {
                    deliveryChallanId = result.DeliveryChallanId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing cartage");
                return JsonError($"An error occurred", ex);
            }
        }

        #endregion

        #region Helper Methods

        private async Task<List<EntryProcessResult>> ProcessEntriesBatchAsync(
            int deliveryChallanId, List<JournalEntry> entries, int locationId, string? remarks, string? otherReference, DateOnly entryDate)
        {
            var results = new List<EntryProcessResult>();

            // Add detailed logging to trace execution
            _logger.LogInformation("Starting to process {Count} entries for delivery challan ID {DeliveryChallanId} with date {EntryDate}",
                entries.Count, deliveryChallanId, entryDate);

            // Process entries sequentially to avoid EF Core concurrency issues
            foreach (var entry in entries)
            {
                try
                {
                    _logger.LogInformation("Processing entry ID {EntryId} for delivery challan ID {DeliveryChallanId}",
                        entry.EntryId, deliveryChallanId);

                    // Make sure this entry isn't already processed
                    if (entry.IsProcessed)
                    {
                        _logger.LogWarning("Entry ID {EntryId} is already marked as processed - skipping",
                            entry.EntryId);
                        
                        results.Add(new EntryProcessResult
                        {
                            EntryId = entry.EntryId,
                            Success = true,
                            Message = "Entry was already processed"
                        });
                        
                        continue;
                    }

                    // Create delivery challan entry with the specified date
                    var deliveryChallanEntry = await _deliveryChallanService.CreateDeliveryChallanEntryAsync(
                        deliveryChallanId,
                        entry.EntryId,
                        locationId,
                        remarks,
                        otherReference,
                        entryDate);  // Pass the entry date from the batch model

                    _logger.LogInformation("Successfully processed entry ID {EntryId}, created DC entry ID {DCEntryId} with date {EntryDate}",
                        entry.EntryId, deliveryChallanEntry.DCEntryId, entryDate);

                    results.Add(new EntryProcessResult
                    {
                        EntryId = entry.EntryId,
                        Success = true
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing entry {EntryId} for delivery challan {DeliveryChallanId}: {ErrorMessage}",
                        entry.EntryId, deliveryChallanId, ex.Message);

                    results.Add(new EntryProcessResult
                    {
                        EntryId = entry.EntryId,
                        Success = false,
                        Message = ex.Message
                    });

                    // Continue processing other entries even if one fails
                }
            }

            _logger.LogInformation("Completed processing entries for delivery challan ID {DeliveryChallanId}. " +
                "Success: {SuccessCount}/{TotalCount}",
                deliveryChallanId, results.Count(r => r.Success), entries.Count);
            
            return results;
        }

        private IActionResult JsonSuccess(string message, object? data = null)
        {
            var response = new
            {
                success = true,
                message
            };

            if (data != null)
            {
                // Combine response with data using reflection
                var dataProps = data.GetType().GetProperties();
                var combinedProps = response.GetType().GetProperties().ToList();
                combinedProps.AddRange(dataProps);

                // Create a dynamic object with all properties
                var expando = new System.Dynamic.ExpandoObject();
                var expandoDict = expando as IDictionary<string, object>;

                expandoDict["success"] = true;
                expandoDict["message"] = message;

                foreach (var prop in dataProps)
                {
                    expandoDict[prop.Name] = prop.GetValue(data);
                }

                return Json(expando);
            }

            return Json(response);
        }

        private IActionResult JsonError(string message, Exception? ex = null, object? additionalData = null)
        {
            var errorMessage = ex != null ? $"{message}: {ex.Message}" : message;

            if (additionalData != null)
            {
                // Combine error response with additional data
                var expando = new System.Dynamic.ExpandoObject();
                var expandoDict = expando as IDictionary<string, object>;

                expandoDict["success"] = false;
                expandoDict["message"] = errorMessage;

                // Add all properties from additionalData
                foreach (var prop in additionalData.GetType().GetProperties())
                {
                    expandoDict[prop.Name] = prop.GetValue(additionalData);
                }

                return Json(expando);
            }

            return Json(new { success = false, message = errorMessage });
        }

        #endregion

        #region Helper Classes

        private class EntryProcessResult
        {
            public int EntryId { get; set; }
            public bool Success { get; set; }
            public string? Message { get; set; }
        }

        #endregion
    }
}