using System.Text.Json.Serialization;

namespace PureLedger.ViewModels
{
    public class ProjectCartageSettingsViewModel
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        
        [JsonPropertyName("projectId")]
        public int ProjectId { get; set; }
        
        [JsonPropertyName("forExpenseCategoryId")]
        public int? ForExpenseCategoryId { get; set; }
        
        [JsonPropertyName("cartageTransporterCategoryId")]
        public int? CartageTransporterCategoryId { get; set; }
        
    }
}


