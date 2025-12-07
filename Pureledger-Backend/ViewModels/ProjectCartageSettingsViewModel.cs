using System.Text.Json.Serialization;

namespace PureLedger.ViewModels
{
    public class ProjectCartageSettingsViewModel
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        
        [JsonPropertyName("projectId")]
        public int ProjectId { get; set; }
        
        [JsonPropertyName("forExpenseAccountId")]
        public int? ForExpenseAccountId { get; set; }
        
        [JsonPropertyName("cartageTransporterCategoryId")]
        public int? CartageTransporterCategoryId { get; set; }
        
        [JsonPropertyName("projectImageBase64")]
        public string? ProjectImageBase64 { get; set; } // Base64 string (can include data URL prefix)
    }
}


