using System.Text;
using PureLedger.Services.Logging;

namespace PureLedger.Middleware
{
    public class RequestBodyCaptureMiddleware
    {
        private const int MaxBodyBytes = 16 * 1024;
        private readonly RequestDelegate _next;

        public RequestBodyCaptureMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (ShouldCapture(context.Request))
            {
                context.Request.EnableBuffering();

                using var reader = new StreamReader(
                    context.Request.Body,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: 4096,
                    leaveOpen: true);

                var buffer = new char[MaxBodyBytes];
                var read = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
                var body = new string(buffer, 0, read);
                context.Request.Body.Position = 0;

                context.Items["RequestBodySummary"] = SensitiveDataRedactor.SanitizeRequestBody(body);
            }

            await _next(context);
        }

        private static bool ShouldCapture(HttpRequest request)
        {
            if (!HttpMethods.IsPost(request.Method) &&
                !HttpMethods.IsPut(request.Method) &&
                !HttpMethods.IsPatch(request.Method) &&
                !HttpMethods.IsDelete(request.Method))
            {
                return false;
            }

            if (request.ContentLength is null or 0)
            {
                return false;
            }

            var contentType = request.ContentType ?? string.Empty;
            return contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase);
        }
    }
}
