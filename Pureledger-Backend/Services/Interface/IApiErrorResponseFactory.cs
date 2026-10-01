using PureLedger.ViewModels.Api;

namespace PureLedger.Services.Interface
{
    public interface IApiErrorResponseFactory
    {
        string CreateErrorId(string prefix = "ERR");

        ApiErrorResponse Create(
            HttpContext context,
            int statusCode,
            string message,
            IEnumerable<string>? errors = null,
            string? errorCode = null,
            string? errorId = null,
            IDictionary<string, object?>? extensions = null);

        ApiErrorResponse CreateFromException(HttpContext context, Exception exception, bool includeDeveloperDetails, out string level);

        ApiErrorResponse CreateFromObjectResult(HttpContext context, int statusCode, object? value, string? fallbackMessage = null);
    }
}
