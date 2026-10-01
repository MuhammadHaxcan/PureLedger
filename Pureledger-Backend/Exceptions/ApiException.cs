namespace PureLedger.Exceptions
{
    public class ApiException : Exception
    {
        public ApiException(string message, int statusCode, string? errorCode = null, IEnumerable<string>? errors = null)
            : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
            Errors = errors?.Where(e => !string.IsNullOrWhiteSpace(e)).ToArray() ?? Array.Empty<string>();
        }

        public int StatusCode { get; }
        public string? ErrorCode { get; }
        public string[] Errors { get; }
    }

    public sealed class BusinessRuleException : ApiException
    {
        public BusinessRuleException(string message, string? errorCode = "BUSINESS_RULE_FAILED")
            : base(message, StatusCodes.Status400BadRequest, errorCode, new[] { message })
        {
        }
    }

    public sealed class ResourceNotFoundException : ApiException
    {
        public ResourceNotFoundException(string message = "The requested resource was not found.", string? errorCode = "NOT_FOUND")
            : base(message, StatusCodes.Status404NotFound, errorCode, new[] { message })
        {
        }
    }

    public sealed class ConflictApiException : ApiException
    {
        public ConflictApiException(string message, string? errorCode = "CONFLICT")
            : base(message, StatusCodes.Status409Conflict, errorCode, new[] { message })
        {
        }
    }
}
