using Serilog.Context;

namespace PureLedger.Middleware
{
    public class CorrelationIdMiddleware
    {
        public const string HeaderName = "X-Correlation-ID";
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = GetOrCreateCorrelationId(context);
            context.Items["CorrelationId"] = correlationId;
            context.TraceIdentifier = correlationId;
            context.Response.Headers[HeaderName] = correlationId;

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await _next(context);
            }
        }

        private static string GetOrCreateCorrelationId(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue(HeaderName, out var values))
            {
                var incoming = values.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(incoming) && incoming.Length <= 128)
                {
                    return incoming.Trim();
                }
            }

            return $"corr_{Guid.NewGuid():N}";
        }
    }
}
