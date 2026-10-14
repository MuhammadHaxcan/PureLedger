using Microsoft.AspNetCore.Authorization;
using PureLedger.Exceptions;
using Microsoft.AspNetCore.Mvc;
using PureLedger.Attributes;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.PaymentReceipt;

namespace PureLedger.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[ControllerAccess]
public sealed class PaymentReceiptVoucherApiController : ControllerBase
{
    private readonly IPaymentReceiptService _service;
    private readonly ICurrentUserService _currentUser;

    public PaymentReceiptVoucherApiController(IPaymentReceiptService service, ICurrentUserService currentUser)
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

    [HttpGet("{journalVoucherId:int}")]
    public async Task<IActionResult> Posted(int journalVoucherId)
    {
        var result = await _service.GetPostedAsync(journalVoucherId, UserId(), IsAdmin());
        return result == null ? NotFound(new { message = "Payment & Receipt voucher was not found." }) : Ok(result);
    }

    [HttpPost("{journalVoucherId:int}/revision")]
    public async Task<IActionResult> SubmitRevision(int journalVoucherId, [FromBody] PaymentReceiptSubmissionRequest request) =>
        Ok(await _service.SubmitRevisionAsync(journalVoucherId, request, UserId(), IsAdmin()));

    private int UserId() => _currentUser.GetCurrentUserId()
        ?? throw new NotAuthenticatedException();
    private bool IsAdmin() => _currentUser.IsCurrentUserAdmin();
}
