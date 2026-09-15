using System.Text.Json;
using System.Runtime.ExceptionServices;
using PureLedger.Services.Interface;

namespace PureLedger.Middleware
{
    public class GlobalExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public GlobalExceptionHandlerMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionHandlerMiddleware> logger,
            IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogError(exception, "Unhandled exception after response started: {Message}", exception.Message);
                ExceptionDispatchInfo.Capture(exception).Throw();
            }

            context.Response.ContentType = "application/json";
            var errorResponseFactory = context.RequestServices.GetRequiredService<IApiErrorResponseFactory>();
            var applicationLogService = context.RequestServices.GetRequiredService<IApplicationLogService>();

            var response = errorResponseFactory.CreateFromException(
                context,
                exception,
                _environment.IsDevelopment(),
                out var logLevel);

            context.Response.StatusCode = response.StatusCode;

            if (response.StatusCode >= StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(exception, "Unhandled exception {ErrorId}: {Message}", response.ErrorId, exception.Message);
            }
            else
            {
                _logger.LogWarning(exception, "Handled API exception {StatusCode}: {Message}", response.StatusCode, exception.Message);
            }

            await applicationLogService.LogExceptionAsync(
                context,
                exception,
                response.ErrorId ?? errorResponseFactory.CreateErrorId("WRN"),
                logLevel,
                context.Response.StatusCode,
                response.Message);

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            var jsonResponse = JsonSerializer.Serialize(response, jsonOptions);
            await context.Response.WriteAsync(jsonResponse);
        }
    }
}

