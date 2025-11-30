using System.Text.Json.Serialization;

namespace PureLedger.ViewModels
{
    public class SignatureViewModel
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("signatureImageBase64")]
        public string? SignatureImageBase64 { get; set; } // Base64 string (can include data URL prefix)
        
        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }
    }
}


