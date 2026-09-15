using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PureLedger.Exceptions;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.Api;

namespace PureLedger.Services
{
    public class ApiErrorResponseFactory : IApiErrorResponseFactory
    {
        private const string PendingApprovalConstraint = "UX_ApprovalVouchers_OnePendingRevision";

        public string CreateErrorId(string prefix = "ERR")
        {
            return $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..29];
        }

        public ApiErrorResponse Create(
            HttpContext context,
            int statusCode,
            string message,
            IEnumerable<string>? errors = null,
            string? errorCode = null,
            string? errorId = null,
            IDictionary<string, object?>? extensions = null)
        {
            var cleanErrors = NormalizeErrors(errors).ToArray();
            if (cleanErrors.Length == 0 && !string.IsNullOrWhiteSpace(message))
            {
                cleanErrors = new[] { message };
            }

            var response = new ApiErrorResponse
            {
                Success = false,
                Message = message,
                Errors = cleanErrors,
                StatusCode = statusCode,
                ErrorCode = errorCode,
                ErrorId = errorId,
                CorrelationId = GetCorrelationId(context)
            };

            if (extensions != null)
            {
                foreach (var extension in extensions)
                {
                    response.Extensions[extension.Key] = extension.Value;
                }
            }

            return response;
        }

        public ApiErrorResponse CreateFromException(HttpContext context, Exception exception, bool includeDeveloperDetails, out string level)
        {
            level = "Error";

            if (exception is ApiException apiException)
            {
                level = apiException.StatusCode >= 500 ? "Error" : "Warning";
                return Create(
                    context,
                    apiException.StatusCode,
                    apiException.Message,
                    apiException.Errors.Length > 0 ? apiException.Errors : new[] { apiException.Message },
                    apiException.ErrorCode,
                    apiException.StatusCode >= 500 ? CreateErrorId() : null);
            }

            if (exception is UnauthorizedAccessException)
            {
                level = "Warning";
                return Create(context, StatusCodes.Status401Unauthorized, "You are not authorized to perform this action.", new[] { "You are not authorized to perform this action." }, "UNAUTHORIZED");
            }

            if (exception is ArgumentException argumentException)
            {
                level = "Warning";
                return Create(context, StatusCodes.Status400BadRequest, "Invalid request parameters.", new[] { argumentException.Message }, "INVALID_REQUEST");
            }

            if (exception is ApprovalConflictException approvalConflict)
            {
                level = "Warning";
                return Create(context, StatusCodes.Status409Conflict, "Approval conflict.", new[] { approvalConflict.Message }, "APPROVAL_CONFLICT");
            }

            if (exception is PaymentReceiptIntegrityException paymentReceiptIntegrity)
            {
                level = "Warning";
                return Create(context, StatusCodes.Status409Conflict, "Payment & Receipt integrity conflict.",
                    new[] { paymentReceiptIntegrity.Message }, "PAYMENT_RECEIPT_INTEGRITY");
            }

            if (exception is InvalidOperationException invalidOperationException)
            {
                level = "Warning";
                return Create(context, StatusCodes.Status400BadRequest, "Invalid operation.", new[] { invalidOperationException.Message }, "INVALID_OPERATION");
            }

            if (exception is KeyNotFoundException keyNotFoundException)
            {
                level = "Warning";
                return Create(context, StatusCodes.Status404NotFound, "The requested resource was not found.", new[] { keyNotFoundException.Message }, "NOT_FOUND");
            }

            if (exception is TimeoutException)
            {
                level = "Warning";
                return Create(context, StatusCodes.Status408RequestTimeout, "The request timed out. Please try again.", new[] { "The request timed out. Please try again." }, "REQUEST_TIMEOUT");
            }

            if (TryGetPostgresConflict(exception, out var postgresConflict))
            {
                level = "Warning";
                var message = postgresConflict.SqlState == PostgresErrorCodes.SerializationFailure
                    ? "This record changed while the request was being processed. Refresh and try again."
                    : "A conflicting record already exists or this action has already been completed.";
                return Create(
                    context,
                    StatusCodes.Status409Conflict,
                    "Database conflict.",
                    new[] { message },
                    "DATABASE_CONFLICT");
            }

            if (exception is DbUpdateConcurrencyException)
            {
                level = "Warning";
                return Create(
                    context,
                    StatusCodes.Status409Conflict,
                    "Concurrency conflict.",
                    new[] { "This record changed while the request was being processed. Refresh and try again." },
                    "CONCURRENCY_CONFLICT");
            }

            if (exception is DbUpdateException dbUpdateException)
            {
                var message = GetSafeDatabaseMessage(dbUpdateException);
                return Create(context, StatusCodes.Status400BadRequest, message, new[] { message }, "DATABASE_WRITE_FAILED", CreateErrorId());
            }

            var errorId = CreateErrorId();
            var safeMessage = includeDeveloperDetails
                ? exception.Message
                : "An internal server error occurred. Please try again later.";

            var extensions = includeDeveloperDetails
                ? new Dictionary<string, object?> { ["developerDetail"] = exception.ToString() }
                : null;

            return Create(
                context,
                StatusCodes.Status500InternalServerError,
                safeMessage,
                new[] { safeMessage },
                "INTERNAL_SERVER_ERROR",
                errorId,
                extensions);
        }

        public ApiErrorResponse CreateFromObjectResult(HttpContext context, int statusCode, object? value, string? fallbackMessage = null)
        {
            if (value is ApiErrorResponse apiErrorResponse)
            {
                apiErrorResponse.CorrelationId ??= GetCorrelationId(context);
                apiErrorResponse.StatusCode = apiErrorResponse.StatusCode == 0 ? statusCode : apiErrorResponse.StatusCode;
                return apiErrorResponse;
            }

            var extracted = ExtractErrorData(value);
            var isServerError = statusCode >= StatusCodes.Status500InternalServerError;
            var message = isServerError
                ? DefaultMessageForStatus(statusCode)
                : extracted.Message
                    ?? fallbackMessage
                    ?? DefaultMessageForStatus(statusCode);
            var errorId = statusCode >= 500 ? extracted.ErrorId ?? CreateErrorId() : extracted.ErrorId;

            return Create(
                context,
                statusCode,
                message,
                !isServerError && extracted.Errors.Length > 0 ? extracted.Errors : new[] { message },
                extracted.ErrorCode ?? DefaultErrorCodeForStatus(statusCode),
                errorId,
                extracted.Extensions);
        }

        private static string GetCorrelationId(HttpContext context)
        {
            return context.Items.TryGetValue("CorrelationId", out var item) && item is string value
                ? value
                : context.TraceIdentifier;
        }

        private static IEnumerable<string> NormalizeErrors(IEnumerable<string>? errors)
        {
            return errors?
                .Where(error => !string.IsNullOrWhiteSpace(error))
                .Select(error => error.Trim())
                .Distinct()
                ?? Enumerable.Empty<string>();
        }

        private static string GetSafeDatabaseMessage(DbUpdateException exception)
        {
            var innerMessage = exception.InnerException?.Message ?? string.Empty;
            if (innerMessage.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                innerMessage.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase))
            {
                return "A record with this information already exists.";
            }

            if (innerMessage.Contains("foreign key", StringComparison.OrdinalIgnoreCase) ||
                innerMessage.Contains("FOREIGN KEY constraint", StringComparison.OrdinalIgnoreCase))
            {
                return "Cannot delete or modify this record because it is referenced by other records.";
            }

            if (innerMessage.Contains("not-null constraint", StringComparison.OrdinalIgnoreCase))
            {
                return "Required fields are missing.";
            }

            return "A database error occurred while saving your changes.";
        }

        private static bool TryGetPostgresConflict(Exception exception, out PostgresException postgresException)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is PostgresException postgres &&
                    (postgres.SqlState == PostgresErrorCodes.SerializationFailure ||
                     postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
                     string.Equals(
                         postgres.ConstraintName,
                         PendingApprovalConstraint,
                         StringComparison.Ordinal)))
                {
                    postgresException = postgres;
                    return true;
                }
            }

            postgresException = null!;
            return false;
        }

        private static string DefaultMessageForStatus(int statusCode)
        {
            return statusCode switch
            {
                StatusCodes.Status400BadRequest => "Invalid request. Please check your input and try again.",
                StatusCodes.Status401Unauthorized => "Your session has expired. Please log in again.",
                StatusCodes.Status403Forbidden => "You do not have permission to access this resource.",
                StatusCodes.Status404NotFound => "The requested resource was not found.",
                StatusCodes.Status409Conflict => "The request could not be completed because of a conflict.",
                >= 500 => "An internal server error occurred. Please try again later.",
                _ => "The request could not be completed."
            };
        }

        private static string DefaultErrorCodeForStatus(int statusCode)
        {
            return statusCode switch
            {
                StatusCodes.Status400BadRequest => "INVALID_REQUEST",
                StatusCodes.Status401Unauthorized => "UNAUTHORIZED",
                StatusCodes.Status403Forbidden => "FORBIDDEN",
                StatusCodes.Status404NotFound => "NOT_FOUND",
                StatusCodes.Status409Conflict => "CONFLICT",
                >= 500 => "INTERNAL_SERVER_ERROR",
                _ => "REQUEST_FAILED"
            };
        }

        private static ExtractedErrorData ExtractErrorData(object? value)
        {
            if (value == null)
            {
                return new ExtractedErrorData(null, Array.Empty<string>(), null, null, new Dictionary<string, object?>());
            }

            try
            {
                using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
                var root = document.RootElement;
                var message = TryGetString(root, "message") ?? TryGetString(root, "title") ?? TryGetString(root, "error");
                var errorCode = TryGetString(root, "errorCode") ?? TryGetString(root, "code");
                var errorId = TryGetString(root, "errorId");
                var errors = ExtractErrors(root);
                var extensions = ExtractExtensions(root);
                return new ExtractedErrorData(message, errors, errorCode, errorId, extensions);
            }
            catch
            {
                return new ExtractedErrorData(value.ToString(), Array.Empty<string>(), null, null, new Dictionary<string, object?>());
            }
        }

        private static string? TryGetString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
        }

        private static string[] ExtractErrors(JsonElement root)
        {
            if (!root.TryGetProperty("errors", out var errorsElement))
            {
                return Array.Empty<string>();
            }

            if (errorsElement.ValueKind == JsonValueKind.Array)
            {
                return errorsElement.EnumerateArray()
                    .Select(error => error.ValueKind == JsonValueKind.String ? error.GetString() : error.ToString())
                    .Where(error => !string.IsNullOrWhiteSpace(error))
                    .Select(error => error!)
                    .ToArray();
            }

            if (errorsElement.ValueKind == JsonValueKind.Object)
            {
                return errorsElement.EnumerateObject()
                    .SelectMany(property =>
                        property.Value.ValueKind == JsonValueKind.Array
                            ? property.Value.EnumerateArray().Select(error => $"{property.Name}: {(error.ValueKind == JsonValueKind.String ? error.GetString() : error.ToString())}")
                            : new[] { $"{property.Name}: {property.Value}" })
                    .Where(error => !string.IsNullOrWhiteSpace(error))
                    .ToArray();
            }

            return new[] { errorsElement.ToString() };
        }

        private static Dictionary<string, object?> ExtractExtensions(JsonElement root)
        {
            var extensions = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "success", "message", "errors", "statusCode", "errorCode", "errorId", "correlationId", "stackTrace"
            };

            foreach (var property in root.EnumerateObject())
            {
                if (reserved.Contains(property.Name))
                {
                    continue;
                }

                extensions[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number when property.Value.TryGetInt64(out var longValue) => longValue,
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Null => null,
                    _ => property.Value.ToString()
                };
            }

            return extensions;
        }

        private sealed record ExtractedErrorData(
            string? Message,
            string[] Errors,
            string? ErrorCode,
            string? ErrorId,
            Dictionary<string, object?> Extensions);
    }
}
