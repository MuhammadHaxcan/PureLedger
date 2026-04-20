using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PureLedger.Attributes;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.Reporting;

namespace PureLedger.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [ControllerAccess]
    public class OldReportingApiController : ControllerBase
    {
        private readonly IOldReportingService _oldReportingService;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<OldReportingApiController> _logger;

        private const string LEGACY_DATABASE_KEY = "legacyDatabase";

        public OldReportingApiController(
            IOldReportingService oldReportingService,
            ISettingsService settingsService,
            ILogger<OldReportingApiController> logger)
        {
            _oldReportingService = oldReportingService;
            _settingsService = settingsService;
            _logger = logger;
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

        [HttpGet("categories-by-project/{projectId}")]
        public async Task<IActionResult> GetCategoriesByProject(int projectId)
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

        [HttpGet("accounts-by-category/{categoryName}")]
        public async Task<IActionResult> GetAccountsByCategory(string categoryName)
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
    }
}
