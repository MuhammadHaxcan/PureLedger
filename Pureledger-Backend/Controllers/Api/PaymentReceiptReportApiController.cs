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
public sealed class PaymentReceiptReportApiController : ControllerBase
{
    private readonly IPaymentReceiptService _service;
    private readonly ICurrentUserService _currentUser;

    public PaymentReceiptReportApiController(IPaymentReceiptService service, ICurrentUserService currentUser)
        => (_service, _currentUser) = (service, currentUser);

    [HttpGet("init")]
    public async Task<IActionResult> Init() =>
        Ok(await _service.GetReportInitAsync(UserId(), _currentUser.IsCurrentUserAdmin()));

    [HttpGet]
    public async Task<IActionResult> Report([FromQuery] PaymentReceiptReportRequest request) =>
        Ok(await _service.GetReportAsync(request, UserId(), _currentUser.IsCurrentUserAdmin()));

    private int UserId() => _currentUser.GetCurrentUserId()
        ?? throw new UnauthorizedAccessException("User is not authenticated.");
}
