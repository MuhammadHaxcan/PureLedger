using Microsoft.AspNetCore.Mvc;
using PureLedger.Attributes;
using PureLedger.Models;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.DeliveryChallan;

namespace PureLedger.Controllers
{
    /// <summary>
    /// Controller for managing delivery challan operations including entry processing,
    /// cartage handling, and FOR charges processing.
    /// </summary>
    [ControllerAccess]
    public class DeliveryChallanController : Controller
    {
        private readonly IDeliveryChallanService _deliveryChallanService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<DeliveryChallanController> _logger;

        public DeliveryChallanController(
            IDeliveryChallanService deliveryChallanService,
            ICurrentUserService currentUserService,
            ILogger<DeliveryChallanController> logger)
        {
            _deliveryChallanService = deliveryChallanService ?? throw new ArgumentNullException(nameof(deliveryChallanService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region View Pages

        /// <summary>
        /// Displays the delivery challan processing page.
        /// </summary>
        /// <returns>IActionResult representing the asynchronous operation.</returns>
        public async Task<IActionResult> ProcessDC()
        {
            try
            {
                var projects = await GetUserProjectsAsync();
                ViewBag.Projects = projects;

                var model = CreateDefaultFilterViewModel();
                return View(model); // Now automatically looks for ProcessDC.cshtml
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load delivery challan page");
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
                if (!IsValidProjectSelection(filter.ProjectId))
                {
                    return Json(new { vouchers = new List<object>() });
                }

                ApplyDefaultDatesIfNeeded(filter);

                _logger.LogInformation(
                    "Retrieving filtered entries - Project: {ProjectId}, Date Range: {StartDate} to {EndDate}, Category: {CategoryId}, Account: {AccountId}",
                    filter.ProjectId, filter.SinceDate, filter.ToDate, filter.CategoryId, filter.AccountId);

                var result = await _deliveryChallanService.GetFilteredEntriesForDeliveryChallanAsync(
                    filter.SinceDate, filter.ToDate, filter.ProjectId, filter.CategoryId, filter.AccountId);

                _logger.LogInformation("Retrieved {VoucherCount} vouchers with filtered entries", result.Vouchers.Count);

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve filtered entries");
                return JsonError("Failed to retrieve delivery challan entries", ex);
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
                _logger.LogError(ex, "Failed to retrieve locations");
                return JsonError("Failed to retrieve locations", ex);
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
                _logger.LogError(ex, "Failed to retrieve entries for voucher {VoucherId}", voucherId);
                return JsonError("Failed to retrieve voucher entries", ex);
            }
        }

        [HttpGet]
        public async Task<IActionResult> CheckExistingChallan(int voucherId)
        {
            try
            {
                var deliveryChallan = await _deliveryChallanService.GetDeliveryChallanVoucherByJournalVoucherIdAsync(voucherId);

                if (deliveryChallan != null)
                {
                    return Json(BuildExistingChallanResponse(deliveryChallan));
                }

                return Json(new { exists = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check existing challan for voucher {VoucherId}", voucherId);
                return JsonError("Failed to check for existing delivery challan", ex, new { exists = false });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCategoriesByProject(int projectId)
        {
            try
            {
                var categories = await _deliveryChallanService.GetCategoriesByProjectAsync(projectId);

                if (categories == null || !categories.Any())
                {
                    return Json(Array.Empty<object>());
                }

                var result = categories.Select(c => new { id = c.CategoryId, name = c.CategoryName });
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve categories for project {ProjectId}", projectId);
                return JsonError("Failed to retrieve categories", ex);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAccountsByCategory(int categoryId)
        {
            try
            {
                var accounts = await _deliveryChallanService.GetAccountsByCategoryAsync(categoryId);
                var result = accounts.Select(a => new { id = a.AccountId, name = a.AccountName });
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve accounts for category {CategoryId}", categoryId);
                return JsonError("Failed to retrieve accounts", ex);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTransporterCategoryId(int? projectId)
        {
            try
            {
                int? transporterCategoryId = null;

                if (projectId.HasValue)
                {
                    var settings = await _deliveryChallanService.GetProjectCartageSettingsAsync(projectId.Value);
                    transporterCategoryId = settings?.CartageTransporterCategoryId;
                }

                return Json(new { value = transporterCategoryId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve transporter category ID for project {ProjectId}", projectId);
                return JsonError("Failed to retrieve transporter category", ex);
            }
        }

        [HttpGet]
        public async Task<IActionResult> CheckForChargesAvailable(int? projectId)
        {
            try
            {
                if (!projectId.HasValue)
                {
                    _logger.LogWarning("CheckForChargesAvailable called without project ID");
                    return Json(new { available = false });
                }

                var isAvailable = await IsForChargesAvailableAsync(projectId.Value);

                _logger.LogInformation("FOR charges availability for project {ProjectId}: {IsAvailable}",
                    projectId.Value, isAvailable);

                return Json(new { available = isAvailable });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check FOR charges availability for project {ProjectId}", projectId);
                return JsonError("Failed to check FOR charges availability", ex, new { available = false });
            }
        }

        #endregion

        #region API Methods - POST

        [HttpPost]
        public async Task<IActionResult> PostSingleEntry([FromBody] CreateDeliveryChallanEntryViewModel model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for single entry: {@Errors}",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return BadRequest(new { success = false, message = "Invalid data submitted" });
            }

            try
            {
                _logger.LogInformation(
                    "Processing single delivery challan entry - Entry: {EntryId}, Date: {DCDate}, Location: {LocationId}",
                    model.JournalEntryId, model.DCDate, model.LocationId);

                var dcEntry = await _deliveryChallanService.CreateSingleDeliveryChallanAsync(
                    model.JournalEntryId,
                    model.DCDate,
                    model.OtherReference,
                    model.LocationId,
                    model.Remarks,
                    model.IncludeForCharges,
                    model.ForAmount);

                _logger.LogInformation("Created delivery challan entry {DCEntryId} for journal entry {EntryId}",
                    dcEntry.DeliveryChallanEntryId, model.JournalEntryId);

                return JsonSuccess("Entry successfully added to delivery challan", new
                {
                    deliveryChallanId = dcEntry.DeliveryChallanVoucherId,
                    dcEntryId = dcEntry.DeliveryChallanEntryId
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument for journal entry {EntryId}", model.JournalEntryId);
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create delivery challan entry for journal entry {EntryId}",
                    model.JournalEntryId);
                return JsonError("Failed to create delivery challan entry", ex);
            }
        }

        [HttpPost]
        public async Task<IActionResult> ProcessAll([FromBody] DeliveryChallanBatchViewModel model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for batch processing: {@Errors}",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return BadRequest(new { success = false, message = "Invalid data submitted" });
            }

            try
            {
                _logger.LogInformation(
                    "Processing batch for voucher {VoucherId} - Date: {DCDate}, Location: {LocationId}",
                    model.JournalVoucherId, model.DCDate, model.LocationId);

                var (deliveryChallanId, results) = await _deliveryChallanService.ProcessBatchDeliveryChallanAsync(
                    model.JournalVoucherId,
                    model.DCDate,
                    model.OtherReference,
                    model.LocationId,
                    model.Remarks,
                    model.IncludeForCharges,
                    model.ForAmount);

                var successCount = results.Count(r => r.Success);
                var failedEntries = results.Where(r => !r.Success).ToList();

                _logger.LogInformation(
                    "Batch processing completed - {SuccessCount}/{TotalCount} entries processed successfully",
                    successCount, results.Count);

                if (failedEntries.Any())
                {
                    _logger.LogWarning("Failed entries: {@FailedEntries}",
                        failedEntries.Select(f => new { f.EntryId, f.Message }));
                }

                return JsonSuccess($"Processed {successCount} out of {results.Count} entries", new
                {
                    deliveryChallanId,
                    results = results.Select(r => new
                    {
                        r.EntryId,
                        r.Success,
                        r.Message
                    })
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation for voucher {VoucherId}", model.JournalVoucherId);
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process batch for voucher {VoucherId}", model.JournalVoucherId);
                return JsonError("Failed to process entries", ex);
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddCartage([FromBody] CartageViewModel model)
            {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for cartage: {@Errors}",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return BadRequest(new { success = false, message = "Invalid data submitted" });
            }

            try
            {
                _logger.LogInformation(
                    "Processing cartage - Voucher: {VoucherId}, Entry: {EntryId}, Transporter: {TransporterId}, Amount: {Amount}",
                    model.JournalVoucherId, model.EntryId, model.TransporterId, model.Amount);

                var deliveryChallan = await _deliveryChallanService.ProcessCartageAndDeliveryAsync(model);

                _logger.LogInformation(
                    "Processed cartage for voucher {VoucherId}, entry {EntryId} - Delivery Challan: {DeliveryChallanId}",
                    model.JournalVoucherId, model.EntryId, deliveryChallan.DeliveryChallanVoucherId);

                return JsonSuccess("Cartage and delivery challan processed successfully", new
                {
                    deliveryChallanId = deliveryChallan.DeliveryChallanVoucherId
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument for cartage processing");
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation for cartage processing");
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process cartage for voucher {VoucherId}, entry {EntryId}",
                    model.JournalVoucherId, model.EntryId);
                return JsonError("Failed to process cartage", ex);
            }
        }

        #endregion

        #region Helper Methods - Business Logic

        private async Task<List<Project>> GetUserProjectsAsync()
        {
            var userId = _currentUserService.GetCurrentUserId();
            return userId.HasValue
                ? await _deliveryChallanService.GetAllProjectsForUserAsync(userId.Value)
                : new List<Project>();
        }

        private async Task<bool> IsForChargesAvailableAsync(int projectId)
        {
            var settings = await _deliveryChallanService.GetProjectCartageSettingsAsync(projectId);
            return settings?.ForExpenseAccountId > 0;
        }

        #endregion

        #region Helper Methods - View Models & Responses

        private static DeliveryChallanFilterViewModel CreateDefaultFilterViewModel()
        {
            return new DeliveryChallanFilterViewModel
            {
                SinceDate = DateOnly.FromDateTime(DateTime.Now.AddDays(-30)),
                ToDate = DateOnly.FromDateTime(DateTime.Now)
            };
        }

        private static bool IsValidProjectSelection(int? projectId)
        {
            return projectId.HasValue && projectId.Value > 0;
        }

        private static void ApplyDefaultDatesIfNeeded(DeliveryChallanFilterViewModel filter)
        {
            filter.SinceDate ??= DateOnly.FromDateTime(DateTime.Now.AddDays(-30));
            filter.ToDate ??= DateOnly.FromDateTime(DateTime.Now);
        }

        private static object BuildExistingChallanResponse(DeliveryChallanVoucher deliveryChallan)
        {
            var firstEntry = deliveryChallan.Entries.FirstOrDefault();

            return new
            {
                exists = true,
                challanId = deliveryChallan.DeliveryChallanVoucherId,
                challan = new
                {
                    challanDate = deliveryChallan.DCDate,
                    locationId = firstEntry?.LocationId,
                    otherRef = firstEntry?.OtherReference
                }
            };
        }

        private IActionResult JsonSuccess(string message, object? data = null)
        {
            if (data == null)
            {
                return Json(new { success = true, message });
            }

            var expando = new System.Dynamic.ExpandoObject();
            var expandoDict = expando as IDictionary<string, object>;

            expandoDict!["success"] = true;
            expandoDict["message"] = message;

            foreach (var prop in data.GetType().GetProperties())
            {
                expandoDict[prop.Name] = prop.GetValue(data)!;
            }

            return Json(expando);
        }

        private IActionResult JsonError(string message, Exception? ex = null, object? additionalData = null)
        {
            var errorMessage = ex != null ? $"{message}: {ex.Message}" : message;

            if (additionalData == null)
            {
                return Json(new { success = false, message = errorMessage });
            }

            var expando = new System.Dynamic.ExpandoObject();
            var expandoDict = expando as IDictionary<string, object>;

            expandoDict!["success"] = false;
            expandoDict["message"] = errorMessage;

            foreach (var prop in additionalData.GetType().GetProperties())
            {
                expandoDict[prop.Name] = prop.GetValue(additionalData)!;
            }

            return Json(expando);
        }

        #endregion
    }
}