using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
            context.Response.ContentType = "application/json";
            
            var response = new ErrorResponse
            {
                Success = false,
                Message = "An error occurred while processing your request."
            };

            switch (exception)
            {
                case UnauthorizedAccessException:
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    response.Message = "You are not authorized to perform this action.";
                    response.Errors = new[] { exception.Message };
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    _logger.LogWarning(exception, "Unauthorized access attempt");
                    break;

                case ArgumentException argEx:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response.Message = "Invalid request parameters.";
                    response.Errors = new[] { argEx.Message };
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    _logger.LogWarning(exception, "Invalid argument: {Message}", argEx.Message);
                    break;

                case InvalidOperationException invalidOpEx:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response.Message = "Invalid operation.";
                    response.Errors = new[] { invalidOpEx.Message };
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    _logger.LogWarning(exception, "Invalid operation: {Message}", invalidOpEx.Message);
                    break;

                case KeyNotFoundException keyNotFoundEx:
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.Message = "The requested resource was not found.";
                    response.Errors = new[] { keyNotFoundEx.Message };
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    _logger.LogWarning(exception, "Resource not found: {Message}", keyNotFoundEx.Message);
                    break;

                case DbUpdateException dbEx:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response.Message = "A database error occurred.";
                    
                    // Handle specific database errors
                    if (dbEx.InnerException != null)
                    {
                        var innerMessage = dbEx.InnerException.Message;
                        if (innerMessage.Contains("duplicate key") || innerMessage.Contains("UNIQUE constraint"))
                        {
                            response.Message = "A record with this information already exists.";
                        }
                        else if (innerMessage.Contains("foreign key") || innerMessage.Contains("FOREIGN KEY constraint"))
                        {
                            response.Message = "Cannot delete or modify this record because it is referenced by other records.";
                        }
                        else if (innerMessage.Contains("violates not-null constraint"))
                        {
                            response.Message = "Required fields are missing.";
                        }
                    }
                    
                    response.Errors = new[] { response.Message };
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    _logger.LogError(exception, "Database error: {Message}", dbEx.Message);
                    break;

                case TimeoutException:
                    response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                    response.Message = "The request timed out. Please try again.";
                    response.Errors = new[] { exception.Message };
                    context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                    _logger.LogWarning(exception, "Request timeout");
                    break;

                default:
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    response.Message = _environment.IsDevelopment() 
                        ? exception.Message 
                        : "An internal server error occurred. Please try again later.";
                    
                    if (_environment.IsDevelopment())
                    {
                        response.Errors = new[] { exception.ToString() };
                        response.StackTrace = exception.StackTrace;
                    }
                    else
                    {
                        response.Errors = new[] { response.Message };
                    }
                    
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
                    break;
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            var jsonResponse = JsonSerializer.Serialize(response, jsonOptions);
            await context.Response.WriteAsync(jsonResponse);
        }
    }

    public class ErrorResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public string[]? Errors { get; set; }
        public string? StackTrace { get; set; }
    }
}

