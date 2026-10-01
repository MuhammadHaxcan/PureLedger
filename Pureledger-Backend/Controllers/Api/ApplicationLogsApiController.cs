using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.Logging;

namespace PureLedger.Controllers.Api
{
    [ApiController]
    [Route("api/AdminApi/logs")]
    public class ApplicationLogsApiController : ControllerBase
    {
        private readonly IApplicationLogService _applicationLogService;

        public ApplicationLogsApiController(IApplicationLogService applicationLogService)
        {
            _applicationLogService = applicationLogService;
        }

        [HttpGet("errors")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SearchErrors([FromQuery] ApplicationErrorLogSearchRequest request)
        {
            var (items, totalCount) = await _applicationLogService.SearchAsync(request);

            return Ok(new
            {
                success = true,
                totalCount,
                page = Math.Max(1, request.Page),
                pageSize = Math.Clamp(request.PageSize, 1, 100),
                data = items.Select(ToSummary)
            });
        }

        [HttpGet("errors/{errorId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetError(string errorId)
        {
            var item = await _applicationLogService.GetByErrorIdAsync(errorId);
            if (item == null)
            {
                return NotFound(new { success = false, message = "Error log not found" });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    item.Id,
                    item.ErrorId,
                    item.CorrelationId,
                    item.CreatedAtUtc,
                    item.Level,
                    item.Source,
                    item.ExceptionType,
                    item.Message,
                    item.StackTrace,
                    item.Method,
                    item.Path,
                    item.StatusCode,
                    item.UserId,
                    item.Username,
                    item.IpAddress,
                    item.UserAgent,
                    item.RequestBodySummary,
                    item.ExtraPropertiesJson
                }
            });
        }

        [HttpPost("client-error")]
        [Authorize]
        public async Task<IActionResult> LogClientError([FromBody] ClientErrorLogRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { success = false, message = "Client error message is required" });
            }

            var errorId = $"CLI-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..29];
            await _applicationLogService.LogClientErrorAsync(HttpContext, request, errorId);

            return Ok(new
            {
                success = true,
                errorId,
                correlationId = request.CorrelationId ?? HttpContext.TraceIdentifier
            });
        }

        private static object ToSummary(Models.ApplicationErrorLog item)
        {
            return new
            {
                item.Id,
                item.ErrorId,
                item.CorrelationId,
                item.CreatedAtUtc,
                item.Level,
                item.Source,
                item.ExceptionType,
                item.Message,
                item.Method,
                item.Path,
                item.StatusCode,
                item.UserId,
                item.Username
            };
        }
    }
}
