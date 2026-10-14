using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PureLedger.Attributes;
using PureLedger.Services;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.StockTransfer;

namespace PureLedger.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[ControllerAccess]
public sealed class StockTransferApiController : ControllerBase
{
    private readonly IStockTransferService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<StockTransferApiController> _logger;

    public StockTransferApiController(IStockTransferService service, ICurrentUserService currentUser, ILogger<StockTransferApiController> logger)
        => (_service, _currentUser, _logger) = (service, currentUser, logger);

    [HttpGet("init")]
    public Task<IActionResult> Init() => ExecuteAsync((u, a) => _service.GetInitAsync(u, a));

    [HttpGet("categories")]
    public Task<IActionResult> Categories([FromQuery] int projectId, [FromQuery] int? accountGroupId) =>
        ExecuteAsync((u, a) => _service.GetCategoriesAsync(projectId, accountGroupId, u, a));

    [HttpGet("accounts")]
    public Task<IActionResult> Accounts([FromQuery] int categoryId, [FromQuery] int? accountGroupId) =>
        ExecuteAsync((u, a) => _service.GetAccountsAsync(categoryId, accountGroupId, u, a));

    [HttpGet("project-accounts")]
    public Task<IActionResult> ProjectAccounts([FromQuery] int projectId, [FromQuery] int accountGroupId = 1) =>
        ExecuteAsync((u, a) => _service.GetProjectAccountsAsync(projectId, accountGroupId, u, a));

    [HttpGet("availability")]
    public Task<IActionResult> Availability([FromQuery] int projectId, [FromQuery] int sourceLocationId,
        [FromQuery] int productAccountId, [FromQuery] DateOnly? asOfDate,
        [FromQuery] int? excludeDispatchJvId) =>
        ExecuteAsync((u, a) => _service.GetAvailabilityAsync(
            projectId, sourceLocationId, productAccountId, asOfDate, excludeDispatchJvId, u, a));

    [HttpGet("settings")]
    public Task<IActionResult> Settings([FromQuery] int projectId) => ExecuteAsync((u, a) => _service.GetSettingsAsync(projectId, u, a));

    [HttpPut("settings")]
    public Task<IActionResult> SaveSettings([FromBody] StockTransferSettingsRequest request) =>
        ExecuteAsync((u, a) => _service.SaveSettingsAsync(request, u, a));

    [HttpGet]
    public Task<IActionResult> List([FromQuery] StockTransferListRequest request) => ExecuteAsync((u, a) => _service.GetListAsync(request, u, a));

    [HttpGet("report")]
    public Task<IActionResult> Report([FromQuery] StockTransferListRequest request) => ExecuteAsync((u, a) => _service.GetListAsync(request, u, a));

    [HttpGet("{dispatchJvId:int}")]
    public Task<IActionResult> Details(int dispatchJvId) => ExecuteAsync(async (u, a) =>
        await _service.GetDetailsAsync(dispatchJvId, u, a) ?? throw new StockTransferValidationException("Stock transfer not found."));

    [HttpPost("dispatch")]
    public Task<IActionResult> Dispatch([FromBody] CreateStockTransferRequest request) => ExecuteAsync((u, a) => _service.CreateAsync(request, u, a));

    [HttpPut("{dispatchJvId:int}")]
    public Task<IActionResult> Edit(int dispatchJvId, [FromBody] EditStockTransferRequest request) =>
        ExecuteAsync((u, a) => _service.EditAsync(dispatchJvId, request, u, a));

    [HttpPost("{dispatchJvId:int}/receive")]
    public Task<IActionResult> Receive(int dispatchJvId, [FromBody] ReceiveStockTransferRequest request) =>
        ExecuteAsync((u, a) => _service.ReceiveAsync(dispatchJvId, request, u, a));

    [HttpPost("{dispatchJvId:int}/cancel")]
    public Task<IActionResult> Cancel(int dispatchJvId, [FromBody] StockTransferVersionRequest request) =>
        ExecuteAsync((u, a) => _service.CancelRemainingAsync(dispatchJvId, request, u, a));

    [HttpPost("{anyLinkedJvId:int}/delete")]
    public Task<IActionResult> Delete(int anyLinkedJvId, [FromBody] DeleteStockTransferRequest request) =>
        ExecuteAsync((u, a) => _service.DeleteAsync(anyLinkedJvId, request, u, a));

    private async Task<IActionResult> ExecuteAsync(Func<int, bool, Task<object>> action)
    {
        var userId = _currentUser.GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(new { success = false, message = "User is not authenticated." });
        try
        {
            var result = await action(userId.Value, _currentUser.IsCurrentUserAdmin());
            return Ok(new { success = true, data = result });
        }
        catch (StockTransferConflictException ex)
        {
            return Conflict(new { success = false, message = ex.Message });
        }
        catch (StockTransferValidationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = ex.Message });
        }
        catch (InventoryIntegrityException ex)
        {
            _logger.LogWarning(ex, "Stock transfer blocked by voucher {VoucherId}", ex.JournalVoucherId);
            return UnprocessableEntity(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stock transfer operation failed");
            return StatusCode(500, new { success = false, message = "Stock transfer operation failed." });
        }
    }
}
