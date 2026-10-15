using Microsoft.AspNetCore.Authorization;
using PureLedger.Exceptions;
using Microsoft.AspNetCore.Mvc;
using PureLedger.Attributes;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.Approval;

namespace PureLedger.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[ControllerAccess]
public sealed class ApprovalApiController : ControllerBase
{
    // Matches [MaxLength] on ApprovalVoucher.RejectionReason.
    private const int MaxRejectionReasonLength = 1000;

    private readonly IApprovalService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IPaymentReceiptService _paymentReceipts;

    public ApprovalApiController(IApprovalService service, ICurrentUserService currentUser, IPaymentReceiptService paymentReceipts)
        => (_service, _currentUser, _paymentReceipts) = (service, currentUser, paymentReceipts);

    [HttpGet("pending")]
    public async Task<IActionResult> Pending() =>
        Ok(await _service.GetPendingAsync(RequireUser(), _currentUser.IsCurrentUserAdmin()));

    [HttpGet("history")]
    public async Task<IActionResult> History() =>
        Ok(await _service.GetHistoryAsync(RequireUser(), _currentUser.IsCurrentUserAdmin()));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var result = await _service.GetAsync(id, RequireUser(), _currentUser.IsCurrentUserAdmin());
        return result == null ? NotFound(new { message = "Approval request was not found." }) : Ok(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int id, [FromBody] ApprovalEditRequest request) =>
        Ok(await _service.EditAsync(id, request, RequireUser(), _currentUser.IsCurrentUserAdmin()));

    [HttpPut("{id:int}/payment-receipt")]
    public async Task<IActionResult> EditPaymentReceipt(int id, [FromBody] PureLedger.ViewModels.PaymentReceipt.PaymentReceiptApprovalEditRequest request) =>
        Ok(await _paymentReceipts.EditPendingAsync(id, request, RequireUser(), _currentUser.IsCurrentUserAdmin()));

    [HttpGet("payment-receipt/init")]
    public async Task<IActionResult> PaymentReceiptInit() =>
        Ok(await _paymentReceipts.GetInitAsync(RequireUser(), _currentUser.IsCurrentUserAdmin()));

    [HttpGet("payment-receipt/categories/{projectId:int}")]
    public async Task<IActionResult> PaymentReceiptCategories(int projectId) =>
        Ok(await _paymentReceipts.GetCategoriesAsync(
            projectId, RequireUser(), _currentUser.IsCurrentUserAdmin()));

    [HttpGet("payment-receipt/accounts/{categoryId:int}")]
    public async Task<IActionResult> PaymentReceiptAccounts(int categoryId) =>
        Ok(await _paymentReceipts.GetAccountsAsync(
            categoryId, RequireUser(), _currentUser.IsCurrentUserAdmin()));

    [HttpPost("payment-receipt/duplicate-warnings")]
    public async Task<IActionResult> PaymentReceiptDuplicateWarnings(
        [FromBody] PureLedger.ViewModels.PaymentReceipt.PaymentReceiptDuplicateLookupRequest request) =>
        Ok(new { warnings = await _paymentReceipts.FindDuplicateWarningsAsync(
            request.Entries, request.ApprovalVoucherId, request.JournalVoucherId) });

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, [FromBody] ApprovalDecisionRequest? request) =>
        Ok(await _service.ApproveAsync(id, request?.SnapshotVersion, RequireUser(), _currentUser.IsCurrentUserAdmin()));

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] ApprovalDecisionRequest request)
    {
        // Validated here so a missing reason is a clear 400 instead of surfacing as a server error.
        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
            throw new BusinessRuleException("A rejection reason is required.", "REJECTION_REASON_REQUIRED");
        if (reason.Length > MaxRejectionReasonLength)
            throw new BusinessRuleException(
                $"The rejection reason must not exceed {MaxRejectionReasonLength} characters.", "REJECTION_REASON_TOO_LONG");
        return Ok(await _service.RejectAsync(id, reason, request.SnapshotVersion, RequireUser(), _currentUser.IsCurrentUserAdmin()));
    }

    private int RequireUser() =>
        _currentUser.GetCurrentUserId() ?? throw new NotAuthenticatedException();
}
