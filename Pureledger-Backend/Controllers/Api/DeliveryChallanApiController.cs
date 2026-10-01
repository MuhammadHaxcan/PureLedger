using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PureLedger.Attributes;
using PureLedger.Models;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.DeliveryChallan;

namespace PureLedger.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [ControllerAccess]
    public class DeliveryChallanApiController : ControllerBase
    {
        #region Fields and Constructor

        private readonly IDeliveryChallanService _dcService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<DeliveryChallanApiController> _logger;

        public DeliveryChallanApiController(
            IDeliveryChallanService deliveryChallanService,
            ICurrentUserService currentUserService,
            ILogger<DeliveryChallanApiController> logger,
            ISharedProcessingService sharedProcessingService)
        {
            _dcService = deliveryChallanService ?? throw new ArgumentNullException(nameof(deliveryChallanService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region Initialization and Filtered Data

        [HttpGet("process-dc-data")]
        public async Task<IActionResult> GetProcessDCData()
        {
            try
            {
                var projects = await GetUserProjectsAsync();
                return Ok(new
                {
                    success = true,
                    projects = projects.Select(p => new { id = p.ProjectId, name = p.ProjectName })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load delivery challan page data");
                return StatusCode(500, new { success = false, message = "Failed to load data" });
            }
        }

        [HttpGet("filtered-entries")]
        public async Task<IActionResult> GetFilteredEntries([FromQuery] DeliveryChallanFilterViewModel filter)
        {
            try
            {
                if (!IsValidProject(filter.ProjectId))
                    return Ok(new { success = true, vouchers = new List<object>() });

                ParseDateFilters(filter);
                var userId = _currentUserService.GetCurrentUserId();
                var result = await _dcService.GetFilteredEntriesAsync(filter.SinceDate, filter.ToDate, filter.ProjectId, filter.CategoryId, filter.AccountId, userId);

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve filtered entries");
                return StatusCode(500, new { success = false, message = "Failed to retrieve delivery challan entries" });
            }
        }

        #endregion

        #region Reference Data

        [HttpGet("locations")]
        public async Task<IActionResult> GetLocations()
        {
            try
            {
                var locations = await _dcService.GetLocationsAsync();
                return Ok(new
                {
                    success = true,
                    data = locations.Select(l => new { id = l.LocationId, name = l.LocationName })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve locations");
                return StatusCode(500, new { success = false, message = "Failed to retrieve locations" });
            }
        }

        [HttpGet("voucher-entries/{voucherId}")]
        public async Task<IActionResult> GetVoucherEntries(int voucherId)
        {
            try
            {
                var entries = await _dcService.GetVoucherEntriesAsync(voucherId);
                return Ok(new { success = true, data = entries });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve entries for voucher {VoucherId}", voucherId);
                return StatusCode(500, new { success = false, message = "Failed to retrieve voucher entries" });
            }
        }

        [HttpGet("check-existing-challan/{voucherId}")]
        public async Task<IActionResult> CheckExistingChallan(int voucherId)
        {
            try
            {
                var deliveryChallan = await _dcService.GetDCByJournalVoucherIdAsync(voucherId);
                if (deliveryChallan != null)
                {
                    var firstEntry = deliveryChallan.Entries.FirstOrDefault();
                    return Ok(new
                    {
                        success = true,
                        exists = true,
                        challanId = deliveryChallan.DeliveryChallanVoucherId,
                        challan = new
                        {
                            challanDate = deliveryChallan.DCDate,
                            locationId = firstEntry?.LocationId,
                            otherRef = firstEntry?.OtherReference
                        }
                    });
                }

                return Ok(new { success = true, exists = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check existing challan for voucher {VoucherId}", voucherId);
                return StatusCode(500, new { success = false, message = "Failed to check for existing delivery challan" });
            }
        }

        [HttpGet("categories-by-project/{projectId}")]
        public async Task<IActionResult> GetCategoriesByProject(int projectId)
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var categories = await _dcService.GetCategoriesForUserAsync(projectId, userId.Value);
                if (categories == null || !categories.Any())
                    return Ok(new { success = true, data = Array.Empty<object>() });

                var result = categories.Select(c => new { id = c.CategoryId, name = c.CategoryName });
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve categories for project {ProjectId}", projectId);
                return StatusCode(500, new { success = false, message = "Failed to retrieve categories" });
            }
        }

        [HttpGet("accounts-by-category/{categoryId}")]
        public async Task<IActionResult> GetAccountsByCategory(int categoryId)
        {
            try
            {
                var accounts = await _dcService.GetAccountsAsync(categoryId);
                var result = accounts.Select(a => new { id = a.AccountId, name = a.AccountName }).ToList();
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve accounts for category {CategoryId}", categoryId);
                return StatusCode(500, new { success = false, message = "Failed to retrieve accounts" });
            }
        }

        [HttpGet("transporter-category-id")]
        public async Task<IActionResult> GetTransporterCategoryId([FromQuery] int? projectId)
        {
            try
            {
                int? transporterCategoryId = null;
                if (projectId.HasValue)
                {
                    var settings = await _dcService.GetCartageSettingsAsync(projectId.Value);
                    transporterCategoryId = settings?.CartageTransporterCategoryId;
                }

                return Ok(new { success = true, value = transporterCategoryId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve transporter category ID for project {ProjectId}", projectId);
                return StatusCode(500, new { success = false, message = "Failed to retrieve transporter category" });
            }
        }

        [HttpGet("for-charges-category-id")]
        public async Task<IActionResult> GetForChargesCategoryId([FromQuery] int? projectId)
        {
            try
            {
                int? forCategoryId = null;
                if (projectId.HasValue)
                {
                    forCategoryId = await _dcService.GetForChargesCategoryIdAsync(projectId.Value);
                }

                return Ok(new { success = true, value = forCategoryId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve FOR charges category ID for project {ProjectId}", projectId);
                return StatusCode(500, new { success = false, message = "Failed to retrieve FOR charges category" });
            }
        }

        [HttpGet("check-for-charges-available")]
        public async Task<IActionResult> CheckForChargesAvailable([FromQuery] int? projectId)
        {
            try
            {
                if (!projectId.HasValue)
                    return Ok(new { success = true, available = false });

                var forCategoryId = await _dcService.GetForChargesCategoryIdAsync(projectId.Value);
                var isAvailable = forCategoryId.HasValue;

                return Ok(new { success = true, available = isAvailable, categoryId = forCategoryId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check FOR charges availability for project {ProjectId}", projectId);
                return StatusCode(500, new { success = false, message = "Failed to check FOR charges availability" });
            }
        }

        #endregion

        #region Delivery Challan Processing

        [HttpPost("single-entry")]
        public async Task<IActionResult> PostSingleEntry([FromBody] CreateDeliveryChallanEntryViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Invalid data submitted", errors = ModelState });

            try
            {
                var dcEntry = await _dcService.CreateSingleDCAsync(model.JournalEntryId, model.DCDate, model.OtherReference, model.LocationId, model.Remarks, model.IncludeForCharges, model.ForAmount, model.ForAccountId);

                return Ok(new
                {
                    success = true,
                    message = "Entry successfully added to delivery challan",
                    data = new
                    {
                        deliveryChallanId = dcEntry.DeliveryChallanVoucherId,
                        dcEntryId = dcEntry.DeliveryChallanEntryId,
                        journalVoucherId = dcEntry.DeliveryChallanVoucher.JournalVoucherId
                    }
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument for journal entry {EntryId}", model.JournalEntryId);
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (ApprovalConflictException ex)
            {
                return Conflict(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create delivery challan entry for journal entry {EntryId}", model.JournalEntryId);
                return StatusCode(500, new { success = false, message = "Failed to create delivery challan entry" });
            }
        }

        [HttpPost("process-all")]
        public async Task<IActionResult> ProcessAll([FromBody] DeliveryChallanBatchViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Invalid data submitted", errors = ModelState });

            try
            {
                var (deliveryChallanId, results) = await _dcService.ProcessBatchDCAsync(model.JournalVoucherId, model.DCDate, model.OtherReference, model.LocationId, model.Remarks, model.IncludeForCharges, model.ForAmount, model.ForAccountId);

                var successCount = results.Count(r => r.Success);

                return Ok(new
                {
                    success = true,
                    message = $"Processed {successCount} out of {results.Count} entries",
                    data = new
                    {
                        deliveryChallanId,
                        journalVoucherId = model.JournalVoucherId,
                        results = results.Select(r => new { r.EntryId, r.Success, r.Message })
                    }
                });
            }
            catch (ApprovalConflictException ex)
            {
                return Conflict(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation for voucher {VoucherId}", model.JournalVoucherId);
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process batch for voucher {VoucherId}", model.JournalVoucherId);
                return StatusCode(500, new { success = false, message = "Failed to process entries" });
            }
        }

        [HttpPost("add-cartage")]
        public async Task<IActionResult> AddCartage([FromBody] CartageViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Invalid data submitted", errors = ModelState });

            try
            {
                var deliveryChallan = await _dcService.ProcessCartageAsync(model);

                return Ok(new
                {
                    success = true,
                    message = "Cartage and delivery challan processed successfully",
                    data = new
                    {
                        deliveryChallanId = deliveryChallan.DeliveryChallanVoucherId,
                        journalVoucherId = deliveryChallan.JournalVoucherId
                    }
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument for cartage processing");
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (ApprovalConflictException ex)
            {
                return Conflict(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation for cartage processing");
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process cartage for voucher {VoucherId}, entry {EntryId}", model.JournalVoucherId, model.EntryId);
                return StatusCode(500, new { success = false, message = "Failed to process cartage" });
            }
        }

        #endregion

        #region FOR Charges Operations

        [HttpGet("pending-for-charges")]
        public async Task<IActionResult> GetPendingForCharges([FromQuery] DeliveryChallanFilterViewModel filter)
        {
            try
            {
                if (!IsValidProject(filter.ProjectId))
                    return Ok(new { success = true, vouchers = new List<object>() });

                ParseDateFilters(filter);
                var userId = _currentUserService.GetCurrentUserId();
                var result = await _dcService.GetPendingForChargesAsync(filter.SinceDate, filter.ToDate, filter.ProjectId, filter.CategoryId, filter.AccountId, userId);

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve pending FOR charges vouchers");
                return StatusCode(500, new { success = false, message = "Failed to retrieve pending FOR charges vouchers" });
            }
        }

        [HttpPost("process-pending-for-charges")]
        public async Task<IActionResult> ProcessPendingForCharges([FromBody] ProcessPendingForChargesViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Invalid data submitted", errors = ModelState });

            try
            {
                if (model.ForAmount == 0)
                {
                    await _dcService.ClearPendingForChargesAsync(model.JournalVoucherId, model.OtherRef);
                    return Ok(new
                    {
                        success = true,
                        message = "Pending FOR charges cleared successfully",
                        data = new
                        {
                            journalVoucherId = model.JournalVoucherId
                        }
                    });
                }

                var forJV = await _dcService.ProcessForChargesAsync(model.JournalVoucherId, null, model.ForAmount, model.DcDate, model.ForAccountId, model.OtherRef);

                return Ok(new
                {
                    success = true,
                    message = "FOR charges processed successfully",
                    data = new
                    {
                        forChargesVoucherId = forJV.JournalVoucherId,
                        journalVoucherId = model.JournalVoucherId
                    }
                });
            }
            catch (ApprovalConflictException ex)
            {
                return Conflict(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation for processing pending FOR charges for voucher {VoucherId}", model.JournalVoucherId);
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process pending FOR charges for voucher {VoucherId}", model.JournalVoucherId);
                return StatusCode(500, new { success = false, message = "Failed to process FOR charges" });
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task<List<Project>> GetUserProjectsAsync()
        {
            var userId = _currentUserService.GetCurrentUserId();
            return userId.HasValue
                ? await _dcService.GetUserProjectsAsync(userId.Value)
                : new List<Project>();
        }

        private static bool IsValidProject(int? projectId) => projectId.HasValue && projectId.Value > 0;

        private void ParseDateFilters(DeliveryChallanFilterViewModel filter)
        {
            if (filter.SinceDate == null && !string.IsNullOrEmpty(Request.Query["SinceDate"].ToString()))
            {
                if (DateOnly.TryParse(Request.Query["SinceDate"], out DateOnly sinceDate))
                    filter.SinceDate = sinceDate;
            }

            if (filter.ToDate == null && !string.IsNullOrEmpty(Request.Query["ToDate"].ToString()))
            {
                if (DateOnly.TryParse(Request.Query["ToDate"], out DateOnly toDate))
                    filter.ToDate = toDate;
            }
        }

        #endregion
    }
}
