namespace PureLedger.ViewModels.Logging
{
    public class ApplicationErrorLogSearchRequest
    {
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
        public string? Level { get; set; }
        public int? StatusCode { get; set; }
        public int? UserId { get; set; }
        public string? Username { get; set; }
        public string? Path { get; set; }
        public string? ErrorId { get; set; }
        public string? CorrelationId { get; set; }
        public string? Source { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class ClientErrorLogRequest
    {
        public string? Message { get; set; }
        public string? StackTrace { get; set; }
        public string? Path { get; set; }
        public string? Source { get; set; }
        public string? CorrelationId { get; set; }
        public string? ExtraPropertiesJson { get; set; }
    }
}
