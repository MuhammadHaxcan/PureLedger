using Microsoft.AspNetCore.Authorization;
using PureLedger.Exceptions;
using Microsoft.AspNetCore.Mvc;
using PureLedger.Attributes;
using PureLedger.Models;
using PureLedger.Services;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.Reporting;
using System.Linq;

namespace PureLedger.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [ControllerAccess]
    public class ReportingApiController : ControllerBase
    {
        private readonly IReportingService _reportingService;
        private readonly IOldReportingService _oldReportingService;
        private readonly ISettingsService _settingsService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<ReportingApiController> _logger;
        private readonly IPaymentReceiptClassifier _paymentReceiptClassifier;
        private readonly IPaymentReceiptService _paymentReceipts;

        private const string LEGACY_DATABASE_KEY = "legacyDatabase";

        public ReportingApiController(
            IReportingService reportingService,
            IOldReportingService oldReportingService,
            ISettingsService settingsService,
            ICurrentUserService currentUserService,
            ILogger<ReportingApiController> logger,
            IPaymentReceiptClassifier paymentReceiptClassifier,
            IPaymentReceiptService paymentReceipts)
        {
            _reportingService = reportingService;
            _oldReportingService = oldReportingService;
            _settingsService = settingsService;
            _currentUserService = currentUserService;
            _logger = logger;
            _paymentReceiptClassifier = paymentReceiptClassifier;
            _paymentReceipts = paymentReceipts;
        }

        private async Task<string> GetLegacyDatabaseAsync()
        {
            var db = await _settingsService.GetSettingAsync(LEGACY_DATABASE_KEY);
            var normalizedDb = db?.Trim().ToLowerInvariant();

            if (normalizedDb is "ghazi" or "ghazi2")
            {
                return normalizedDb;
            }

            if (!string.IsNullOrWhiteSpace(db))
            {
                _logger.LogWarning(
                    "Invalid legacy database setting value '{LegacyDatabase}'. Falling back to ghazi.",
                    db);
            }

            return "ghazi";
        }

        #region Voucher Reports

        [HttpGet("validate-voucher")]
        public async Task<IActionResult> ValidateVoucher([FromQuery] int journalVoucherId, [FromQuery] string type = "JV")
        {
            try
            {
                if (Enum.TryParse<VoucherType>(type, true, out var voucherType))
                {
                    var resolvedType = await _paymentReceiptClassifier.ResolvePrintableTypeAsync(journalVoucherId, voucherType);
                    if (resolvedType == VoucherType.PaymentReceipt)
                        await _paymentReceipts.EnsureVoucherAccessAsync(
                            journalVoucherId, RequireUser(), _currentUserService.IsCurrentUserAdmin());
                    var result = await _reportingService.ValidateVoucherAsync(journalVoucherId, resolvedType);
                    return Ok(new { success = true, exists = result.Exists, isBaseVoucher = result.IsBaseVoucher, resolvedVoucherType = resolvedType.ToString() });
                }
                return BadRequest(new { success = false, message = "Invalid voucher type" });
            }
            catch (Exception ex)
            {
                if (ex is ApprovalConflictException)
                    return Conflict(new { success = false, message = ExceptionClassifier.UserMessage(ex) });
                if (ex is PaymentReceiptIntegrityException)
                    return Conflict(new { success = false, message = ExceptionClassifier.UserMessage(ex) });
                if (ex is UnauthorizedAccessException)
                    return StatusCode(StatusCodes.Status403Forbidden,
                        new { success = false, message = "You do not have access to this Payment & Receipt voucher." });
                _logger.LogError(ex, "Error validating voucher");
                return StatusCode(500, new { success = false, message = "Error validating voucher" });
            }
        }

        [HttpGet("print-voucher/{journalVoucherId}")]
        public async Task<IActionResult> GetPrintVoucher(int journalVoucherId, [FromQuery] string type = "JV", [FromQuery] string filter = "all")
        {
            try
            {
                if (journalVoucherId <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid voucher ID" });
                }

                if (!Enum.TryParse<VoucherType>(type, true, out var voucherType))
                {
                    return BadRequest(new { success = false, message = "Invalid voucher type" });
                }

                voucherType = await _paymentReceiptClassifier.ResolvePrintableTypeAsync(journalVoucherId, voucherType);
                if (voucherType == VoucherType.PaymentReceipt)
                    await _paymentReceipts.EnsureVoucherAccessAsync(
                        journalVoucherId, RequireUser(), _currentUserService.IsCurrentUserAdmin());
                var normalizedFilter = filter?.ToLowerInvariant() ?? "all";
                var report = await _reportingService.GetSingleDocReportAsync(journalVoucherId, voucherType);
                if (report == null)
                {
                    string message = voucherType switch
                    {
                        VoucherType.DC => $"No Delivery Challan exists for Voucher ID {journalVoucherId}.",
                        VoucherType.INV => $"No Invoice exists for Voucher ID {journalVoucherId}.",
                        _ => $"No Journal Voucher exists for Voucher ID {journalVoucherId}."
                    };
                    return NotFound(new { success = false, message });
                }

                // Add customer balance for invoices
                decimal? customerBalance = null;
                decimal? previousBalance = null;
                if (voucherType == VoucherType.INV && !string.IsNullOrEmpty(report.CustomerAccountName))
                {
                    customerBalance = await _reportingService.GetAccountBalanceUpToVoucherAsync(journalVoucherId);
                    previousBalance = await _reportingService.GetAccountBalanceBeforeVoucherAsync(journalVoucherId);
                }

                if (voucherType == VoucherType.JV && report.Entries != null)
                {
                    if (normalizedFilter == "credit")
                    {
                        foreach (var entry in report.Entries.Where(e => e.Debit > 0))
                        {
                            entry.AccountName = string.Empty;
                        }
                    }
                    else if (normalizedFilter == "debit")
                    {
                        foreach (var entry in report.Entries.Where(e => e.Credit > 0))
                        {
                            entry.AccountName = string.Empty;
                        }
                    }
                }

                // Project image is now set in the service methods, but ensure it's included for all types
                // The service already handles ProjectImage for all voucher types

                return Ok(new
                {
                    success = true,
                    data = report,
                    type = voucherType.ToString(),
                    resolvedVoucherType = voucherType.ToString(),
                    filter = voucherType == VoucherType.JV ? normalizedFilter : null,
                    customerBalance,
                    previousBalance
                });
            }
            catch (Exception ex)
            {
                if (ex is ApprovalConflictException)
                    return Conflict(new { success = false, message = ExceptionClassifier.UserMessage(ex) });
                if (ex is PaymentReceiptIntegrityException)
                    return Conflict(new { success = false, message = ExceptionClassifier.UserMessage(ex) });
                if (ex is UnauthorizedAccessException)
                    return StatusCode(StatusCodes.Status403Forbidden,
                        new { success = false, message = "You do not have access to this Payment & Receipt voucher." });
                _logger.LogError(ex, "Error loading print voucher {VoucherId}", journalVoucherId);
                return StatusCode(500, new { success = false, message = "Error loading voucher" });
            }
        }

        [HttpGet("report-data")]
        public async Task<IActionResult> GetReportData()
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                var projects = userId.HasValue
                    ? await _reportingService.GetAllProjectsForUserAsync(userId.Value)
                    : new List<Project>();
                var categories = await _reportingService.GetAllCategoriesAsync();

                return Ok(new
                {
                    success = true,
                    projects = projects.Select(p => new { id = p.ProjectId.ToString(), name = p.ProjectName }),
                    categories = categories.Select(c => new { id = c.CategoryId.ToString(), name = c.CategoryName })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading report data");
                return StatusCode(500, new { success = false, message = "Error loading report data" });
            }
        }

        [HttpGet("account-report-init")]
        public async Task<IActionResult> GetAccountReportInit()
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                var projects = userId.HasValue
                    ? await _reportingService.GetAllProjectsForUserAsync(userId.Value)
                    : new List<Project>();
                var categories = await _reportingService.GetAllCategoriesAsync();

                return Ok(new
                {
                    success = true,
                    projects = projects.Select(p => new { id = p.ProjectId, name = p.ProjectName }),
                    categories = categories.Select(c => new { id = c.CategoryId, name = c.CategoryName })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading account report init data");
                return StatusCode(500, new { success = false, message = "Error loading data" });
            }
        }

        [HttpGet("master-report-init")]
        public async Task<IActionResult> GetMasterReportInit()
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                var projects = userId.HasValue
                    ? await _reportingService.GetAllProjectsForUserAsync(userId.Value)
                    : new List<Project>();
                var categories = await _reportingService.GetAllCategoriesAsync();
                var locations = await _reportingService.GetAllLocationsAsync();

                return Ok(new
                {
                    success = true,
                    projects = projects.Select(p => new { id = p.ProjectId, name = p.ProjectName }),
                    categories = categories.Select(c => new { id = c.CategoryId, name = c.CategoryName }),
                    locations = locations.Select(l => new { id = l.LocationId, name = l.LocationName })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading master report init data");
                return StatusCode(500, new { success = false, message = "Error loading data" });
            }
        }

        [HttpGet("old-master-report-init")]
        public async Task<IActionResult> GetOldMasterReportInit()
        {
            try
            {
                var legacyDatabase = await GetLegacyDatabaseAsync();
                var data = await _oldReportingService.GetOldMasterReportInitAsync(legacyDatabase);
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading old master report init data");
                return StatusCode(500, new { success = false, message = "Error loading data" });
            }
        }

        [HttpGet("old-master-report")]
        public async Task<IActionResult> GetOldMasterReport([FromQuery] OldMasterReportRequest request)
        {
            try
            {
                var legacyDatabase = await GetLegacyDatabaseAsync();
                var report = await _oldReportingService.GetOldMasterReportAsync(request, legacyDatabase);
                return Ok(new { success = true, data = report });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating old master report");
                return StatusCode(500, new { success = false, message = "Error generating report" });
            }
        }

        [HttpGet("old-master-report-categories-by-project/{projectId}")]
        public async Task<IActionResult> GetOldReportCategoriesByProject(int projectId)
        {
            try
            {
                var legacyDatabase = await GetLegacyDatabaseAsync();
                var categories = await _oldReportingService.GetCategoriesByProjectAsync(projectId, legacyDatabase);
                return Ok(new { success = true, data = categories });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading categories for project {ProjectId}", projectId);
                return StatusCode(500, new { success = false, message = "Error loading categories" });
            }
        }

        [HttpGet("old-master-report-accounts-by-category/{categoryName}")]
        public async Task<IActionResult> GetOldReportAccountsByCategory(string categoryName)
        {
            try
            {
                var legacyDatabase = await GetLegacyDatabaseAsync();
                var accounts = await _oldReportingService.GetAccountsByCategoryAsync(categoryName, legacyDatabase);
                return Ok(new { success = true, data = accounts });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading accounts for category {CategoryName}", categoryName);
                return StatusCode(500, new { success = false, message = "Error loading accounts" });
            }
        }

        [HttpGet("product-report-init")]
        public async Task<IActionResult> GetProductReportInit()
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                var projects = userId.HasValue
                    ? await _reportingService.GetAllProjectsForUserAsync(userId.Value)
                    : new List<Project>();
                var categories = await _reportingService.GetAllCategoriesAsync();
                var locations = await _reportingService.GetAllLocationsAsync();

                return Ok(new
                {
                    success = true,
                    projects = projects.Select(p => new { id = p.ProjectId, name = p.ProjectName }),
                    categories = categories.Select(c => new { id = c.CategoryId, name = c.CategoryName }),
                    locations = locations.Select(l => new { id = l.LocationId, name = l.LocationName })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading product report init data");
                return StatusCode(500, new { success = false, message = "Error loading data" });
            }
        }

        [HttpGet("missed-entries-init")]
        public async Task<IActionResult> GetMissedEntriesInit()
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                var projects = userId.HasValue
                    ? await _reportingService.GetAllProjectsForUserAsync(userId.Value)
                    : new List<Project>();

                return Ok(new
                {
                    success = true,
                    projects = projects.Select(p => new { id = p.ProjectId, name = p.ProjectName })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading missed entries init data");
                return StatusCode(500, new { success = false, message = "Error loading data" });
            }
        }

        #endregion

        #region Aging Report

        [HttpGet("aging-report-fifo")]
        public async Task<IActionResult> GetAgingFifo(
            [FromQuery] AgingFifoRequest request,
            [FromServices] PureLedger.Services.Auth.IAuthService authService,
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = RequireUser();
                if (!_currentUserService.IsCurrentUserAdmin() &&
                    (!await authService.HasProjectAccessAsync(userId, request.ProjectId) ||
                     (!await authService.HasControllerAccessAsync(userId, "Reporting") &&
                      !await authService.HasActionAccessAsync(userId, "Reporting", "AgingReport"))))
                    return Forbid();
                var report = await _reportingService.GetAgingFifoAsync(request, cancellationToken);
                return Ok(new { success = true, data = report });
            }
            catch (ArgumentException ex) when (ExceptionClassifier.IsApplicationRaised(ex)) { return BadRequest(new { success = false, message = ExceptionClassifier.UserMessage(ex) }); }
            catch (KeyNotFoundException ex) when (ExceptionClassifier.IsApplicationRaised(ex)) { return NotFound(new { success = false, message = ExceptionClassifier.UserMessage(ex) }); }
            catch (NotAuthenticatedException) { return Unauthorized(); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading aging FIFO for account {AccountId}", request.AccountId);
                return StatusCode(500, new { success = false, message = "Error loading FIFO breakdown" });
            }
        }

        [HttpGet("aging-report")]
        public async Task<IActionResult> GetAgingReport([FromQuery] AgingReportRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage);
                    return BadRequest(new { success = false, errors });
                }

                var report = await _reportingService.GetAgingReportAsync(request);
                return Ok(new { success = true, data = report });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating aging report");
                return StatusCode(500, new { success = false, message = "Error generating report" });
            }
        }

        #endregion

        #region Account Report

        [HttpGet("account-report")]
        public async Task<IActionResult> GetAccountReport([FromQuery] AccountReportRequestViewModel request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage);
                    return BadRequest(new { success = false, errors });
                }

                var report = await _reportingService.GetAccountReportAsync(request);
                return Ok(new { success = true, data = report });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating account report");
                return StatusCode(500, new { success = false, message = "Error generating report" });
            }
        }

        #endregion

        #region Master Report

        [HttpGet("master-report")]
        public async Task<IActionResult> GetMasterReport([FromQuery] MasterReportRequest request)
        {
            try
            {
                var report = await _reportingService.GetMasterReportAsync(request);
                return Ok(new { success = true, data = report });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating master report");
                return StatusCode(500, new { success = false, message = "Error generating report" });
            }
        }

        #endregion

        #region Product Report

        [HttpGet("product-report")]
        public async Task<IActionResult> GetProductReport([FromQuery] ProductReportRequest request)
        {
            try
            {
                // Parse dates from query string if needed
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
                return Ok(new { success = true, data = report });
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
            {
                return BadRequest(new { success = false, message = ExceptionClassifier.UserMessage(ex) });
            }
            catch (InventoryIntegrityException ex)
            {
                _logger.LogWarning(ex, "Product report blocked by stock-transfer voucher {VoucherId}", ex.JournalVoucherId);
                return UnprocessableEntity(new { success = false, message = ExceptionClassifier.UserMessage(ex) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating product report");
                return StatusCode(500, new { success = false, message = "Error generating report" });
            }
        }

        [HttpGet("product-report-stock-breakdown")]
        public async Task<IActionResult> GetProductStockBreakdown(
            [FromQuery] int projectId,
            [FromQuery] int productId,
            [FromQuery] DateOnly? endDate)
        {
            try
            {
                var breakdown = await _reportingService.GetProductStockBreakdownAsync(projectId, productId, endDate);
                return Ok(new { success = true, data = breakdown });
            }
            catch (ArgumentException ex) when (ExceptionClassifier.IsApplicationRaised(ex))
            {
                return BadRequest(new { success = false, message = ExceptionClassifier.UserMessage(ex) });
            }
            catch (KeyNotFoundException ex) when (ExceptionClassifier.IsApplicationRaised(ex))
            {
                return NotFound(new { success = false, message = ExceptionClassifier.UserMessage(ex) });
            }
            catch (InventoryIntegrityException ex)
            {
                _logger.LogWarning(ex, "Stock breakdown blocked by stock-transfer voucher {VoucherId}", ex.JournalVoucherId);
                return UnprocessableEntity(new { success = false, message = ExceptionClassifier.UserMessage(ex) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error loading stock breakdown for product {ProductId} in project {ProjectId}",
                    productId, projectId);
                return StatusCode(500, new { success = false, message = "Error loading stock breakdown" });
            }
        }

        [HttpGet("product-report-locations")]
        public async Task<IActionResult> GetProductReportLocations()
        {
            try
            {
                var locations = await _reportingService.GetAllLocationsAsync();
                return Ok(new
                {
                    success = true,
                    data = locations.Select(l => new { id = l.LocationId.ToString(), name = l.LocationName })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading locations");
                return StatusCode(500, new { success = false, message = "Error loading locations" });
            }
        }

        #endregion

        #region Missed Entries

        [HttpGet("missed-entries")]
        public async Task<IActionResult> GetMissedEntries(
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] int? projectId,
            [FromQuery] int page = 0,
            [FromQuery] int pageSize = 2500)
        {
            try
            {
                var report = await _reportingService.GetFilteredMissedEntriesPaginatedAsync(startDate, endDate, projectId, page, pageSize);
                return Ok(new { success = true, data = report });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading missed entries");
                return StatusCode(500, new { success = false, message = "Failed to load entries" });
            }
        }

        [HttpGet("missed-entries-summary")]
        public async Task<IActionResult> GetMissedEntriesSummary(
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] int? projectId)
        {
            try
            {
                var summary = await _reportingService.GetMissedEntriesSummaryAsync(startDate, endDate, projectId);
                return Ok(new { success = true, data = summary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading missed entries summary");
                return StatusCode(500, new { success = false, message = "Failed to load summary" });
            }
        }

        [HttpGet("missed-entries-by-customer")]
        public async Task<IActionResult> GetMissedEntriesByCustomer(
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] int? projectId)
        {
            try
            {
                var result = await _reportingService.GetMissedEntriesByCustomerAsync(startDate, endDate, projectId);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading missed entries by customer");
                return Ok(new { success = true, data = Array.Empty<object>() });
            }
        }

        [HttpGet("missed-entries-by-product")]
        public async Task<IActionResult> GetMissedEntriesByProduct(
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] int? projectId)
        {
            try
            {
                var result = await _reportingService.GetMissedEntriesByProductAsync(startDate, endDate, projectId);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading missed entries by product");
                return Ok(new { success = true, data = Array.Empty<object>() });
            }
        }

        [HttpGet("missed-entries-data")]
        public async Task<IActionResult> GetMissedEntriesData(
            [FromQuery] int customerAccountId,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] int? projectId)
        {
            try
            {
                var result = await _reportingService.GetMissedEntriesDataAsync(customerAccountId, startDate, endDate, projectId);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading missed entries data");
                return Ok(new { success = true, data = new { entries = Array.Empty<object>() } });
            }
        }

        #endregion

        #region User Report

        [HttpGet("user-report-init")]
        public async Task<IActionResult> GetUserReportInit()
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var result = await _reportingService.GetUserReportInitAsync(userId.Value);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user report init data");
                return StatusCode(500, new { success = false, message = "Error loading data" });
            }
        }

        [HttpGet("user-report-categories-by-project/{projectId}")]
        public async Task<IActionResult> GetUserReportCategoriesByProject(int projectId)
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var categories = await _reportingService.GetCategoriesByProjectForUserAsync(projectId, userId.Value);
                if (categories == null || !categories.Any())
                {
                    return Ok(new { success = true, data = Array.Empty<object>() });
                }

                var result = categories.Select(c => new { id = c.CategoryId, name = c.CategoryName });
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading categories for project {ProjectId}", projectId);
                return StatusCode(500, new { success = false, message = "Error loading categories" });
            }
        }

        [HttpGet("user-report")]
        public async Task<IActionResult> GetUserReport([FromQuery] UserReportRequest request)
        {
            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage);
                    return BadRequest(new { success = false, errors });
                }

                var report = await _reportingService.GetUserReportAsync(request, userId.Value);
                return Ok(new { success = true, data = report });
            }
            catch (ArgumentException ex) when (ExceptionClassifier.IsApplicationRaised(ex))
            {
                _logger.LogWarning(ex, "Invalid argument for user report");
                return BadRequest(new { success = false, message = ExceptionClassifier.UserMessage(ex) });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt for user report");
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating user report");
                return StatusCode(500, new { success = false, message = "Error generating report" });
            }
        }

        #endregion

        #region Lookup APIs

        [HttpGet("categories-by-project/{projectId}")]
        public async Task<IActionResult> GetCategoriesByProject(int projectId)
        {
            try
            {
                var categories = await _reportingService.GetCategoriesByProjectAsync(projectId);
                if (categories == null || !categories.Any())
                {
                    return Ok(new { success = true, data = Array.Empty<object>() });
                }

                var result = categories.Select(c => new { id = c.CategoryId, name = c.CategoryName });
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading categories for project {ProjectId}", projectId);
                return StatusCode(500, new { success = false, message = "Error loading categories" });
            }
        }

        [HttpGet("accounts-by-category/{categoryId}")]
        public async Task<IActionResult> GetAccountsByCategory(int categoryId)
        {
            try
            {
                var accounts = await _reportingService.GetAccountsByCategoryAsync(categoryId);
                var result = accounts.Select(a => new { id = a.AccountId, name = a.AccountName });
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading accounts for category {CategoryId}", categoryId);
                return StatusCode(500, new { success = false, message = "Error loading accounts" });
            }
        }

        [HttpGet("bookers")]
        public async Task<IActionResult> GetAllBookers()
        {
            try
            {
                var bookers = await _reportingService.GetAllBookersAsync();

                if (bookers == null || !bookers.Any())
                {
                    return Ok(new { success = true, data = Array.Empty<object>() });
                }

                var result = bookers.Select(b => new { id = b.BookerID, name = b.BookerName });
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading bookers");
                return StatusCode(500, new { success = false, message = "Error loading bookers" });
            }
        }

        #endregion

        private int RequireUser() => _currentUserService.GetCurrentUserId()
            ?? throw new NotAuthenticatedException();
    }
}

