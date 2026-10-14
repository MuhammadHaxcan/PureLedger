using System.Text.Json.Serialization;

namespace PureLedger.ViewModels.Api
{
    public class ApiErrorResponse
    {
        public bool Success { get; set; } = false;
        public string Message { get; set; } = string.Empty;
        public string[] Errors { get; set; } = Array.Empty<string>();
        public int StatusCode { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorId { get; set; }
        public string? CorrelationId { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object?> Extensions { get; set; } = new();
    }
}
