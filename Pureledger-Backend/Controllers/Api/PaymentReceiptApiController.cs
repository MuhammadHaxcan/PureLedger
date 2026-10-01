using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PureLedger.Attributes;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.PaymentReceipt;

namespace PureLedger.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[ControllerAccess]
public sealed class PaymentReceiptApiController : ControllerBase
{
    private readonly IPaymentReceiptService _service;
    private readonly ICurrentUserService _currentUser;

    public PaymentReceiptApiController(IPaymentReceiptService service, ICurrentUserService currentUser)
        => (_service, _currentUser) = (service, currentUser);

    [HttpGet("init")]
    public async Task<IActionResult> Init() => Ok(await _service.GetInitAsync(UserId(), IsAdmin()));

    [HttpGet("categories/{projectId:int}")]
    public async Task<IActionResult> Categories(int projectId) =>
        Ok(await _service.GetCategoriesAsync(projectId, UserId(), IsAdmin()));

    [HttpGet("accounts/{categoryId:int}")]
    public async Task<IActionResult> Accounts(int categoryId) =>
        Ok(await _service.GetAccountsAsync(categoryId, UserId(), IsAdmin()));

    [HttpPost("duplicate-warnings")]
    public async Task<IActionResult> DuplicateWarnings([FromBody] PaymentReceiptDuplicateLookupRequest request) =>
        Ok(new { warnings = await _service.FindDuplicateWarningsAsync(
            request.Entries, request.ApprovalVoucherId, request.JournalVoucherId) });

    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] PaymentReceiptSubmissionRequest request) =>
        Ok(await _service.SubmitAsync(request, UserId(), IsAdmin()));

    private int UserId() => _currentUser.GetCurrentUserId()
        ?? throw new UnauthorizedAccessException("User is not authenticated.");
    private bool IsAdmin() => _currentUser.IsCurrentUserAdmin();
}
