using System.ComponentModel.DataAnnotations;

namespace PureLedger.Models
{
    public class ApplicationErrorLog
    {
        public long Id { get; set; }

        [MaxLength(32)]
        public string ErrorId { get; set; } = string.Empty;

        [MaxLength(128)]
        public string CorrelationId { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [MaxLength(32)]
        public string Level { get; set; } = "Error";

        [MaxLength(64)]
        public string Source { get; set; } = "Backend";

        [MaxLength(256)]
        public string? ExceptionType { get; set; }

        public string Message { get; set; } = string.Empty;

        public string? StackTrace { get; set; }

        [MaxLength(16)]
        public string? Method { get; set; }

        [MaxLength(512)]
        public string? Path { get; set; }

        public int? StatusCode { get; set; }

        public int? UserId { get; set; }

        [MaxLength(256)]
        public string? Username { get; set; }

        [MaxLength(128)]
        public string? IpAddress { get; set; }

        [MaxLength(512)]
        public string? UserAgent { get; set; }

        public string? RequestBodySummary { get; set; }

        public string? ExtraPropertiesJson { get; set; }
    }
}
