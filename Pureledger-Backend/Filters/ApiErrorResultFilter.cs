using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.Json;
using PureLedger.Services.Interface;
using PureLedger.Services.Logging;
using PureLedger.ViewModels.Api;

namespace PureLedger.Filters
{
    public class ApiErrorResultFilter : IAsyncResultFilter
    {
        private readonly IApiErrorResponseFactory _errorResponseFactory;
        private readonly IApplicationLogService _applicationLogService;
        private readonly ILogger<ApiErrorResultFilter> _logger;

        public ApiErrorResultFilter(
            IApiErrorResponseFactory errorResponseFactory,
            IApplicationLogService applicationLogService,
            ILogger<ApiErrorResultFilter> logger)
        {
            _errorResponseFactory = errorResponseFactory;
            _applicationLogService = applicationLogService;
            _logger = logger;
        }

        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            var statusCode = GetStatusCode(context.Result);
            if (statusCode >= StatusCodes.Status400BadRequest)
            {
                var originalValue = GetValue(context.Result);
                var standardized = _errorResponseFactory.CreateFromObjectResult(context.HttpContext, statusCode, originalValue);

                if (statusCode >= StatusCodes.Status500InternalServerError && !string.IsNullOrWhiteSpace(standardized.ErrorId))
                {
                    await LogManualServerErrorAsync(context.HttpContext, standardized, originalValue);
                }

                context.Result = new ObjectResult(standardized)
                {
                    StatusCode = statusCode
                };
            }

            await next();
        }

        private async Task LogManualServerErrorAsync(HttpContext context, ApiErrorResponse response, object? originalValue)
        {
            try
            {
                var originalSummary = originalValue == null
                    ? "No response body."
                    : JsonSerializer.Serialize(originalValue);
                if (originalSummary.Length > 4000)
                {
                    originalSummary = originalSummary[..4000];
                }

                originalSummary = SensitiveDataRedactor.SanitizeRequestBody(originalSummary)
                    ?? SensitiveDataRedactor.RedactText(originalSummary);
                var exception = new InvalidOperationException($"API returned HTTP {response.StatusCode}. Original response: {originalSummary}");
                await _applicationLogService.LogExceptionAsync(
                    context,
                    exception,
                    response.ErrorId!,
                    "Error",
                    response.StatusCode,
                    response.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist standardized API error result {ErrorId}", response.ErrorId);
            }
        }

        private static int GetStatusCode(IActionResult result)
        {
            return result switch
            {
                ForbidResult => StatusCodes.Status403Forbidden,
                ChallengeResult => StatusCodes.Status401Unauthorized,
                ObjectResult objectResult => objectResult.StatusCode ?? StatusCodes.Status200OK,
                JsonResult jsonResult => jsonResult.StatusCode ?? StatusCodes.Status200OK,
                StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
                _ => StatusCodes.Status200OK
            };
        }

        private static object? GetValue(IActionResult result)
        {
            return result switch
            {
                ObjectResult objectResult => objectResult.Value,
                JsonResult jsonResult => jsonResult.Value,
                _ => null
            };
        }
    }
}
