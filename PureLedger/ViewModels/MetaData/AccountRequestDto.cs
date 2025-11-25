using System.Text.Json.Serialization;

namespace PureLedger.ViewModels.MetaData
{
    public class AccountRequestDto
    {
        [JsonPropertyName("accountId")]
        public int AccountId { get; set; }
        
        [JsonPropertyName("accountName")]
        public string AccountName { get; set; } = string.Empty;
        
        [JsonPropertyName("categoryId")]
        public int CategoryId { get; set; }
        
        [JsonPropertyName("accountGroupId")]
        public int AccountGroupId { get; set; }
        
        [JsonPropertyName("accountData")]
        public AccountDataDto? AccountData { get; set; }
        
        [JsonPropertyName("productData")]
        public ProductDataDto? ProductData { get; set; }
    }

    public class AccountDataDto
    {
        [JsonPropertyName("accountDataId")]
        public int? AccountDataId { get; set; }
        
        [JsonPropertyName("phoneNumber")]
        public string? PhoneNumber { get; set; }
        
        [JsonPropertyName("email")]
        public string? Email { get; set; }
        
        [JsonPropertyName("address")]
        public string? Address { get; set; }
        
        [JsonPropertyName("area")]
        public string? Area { get; set; }
        
        [JsonPropertyName("city")]
        public string? City { get; set; }
    }

    public class ProductDataDto
    {
        [JsonPropertyName("productDataId")]
        public int? ProductDataId { get; set; }
        
        [JsonPropertyName("pkgForm")]
        public string? PkgForm { get; set; }
        
        [JsonPropertyName("pkg")]
        public int? Pkg { get; set; }
        
        [JsonPropertyName("type")]
        public string? Type { get; set; }
        
        [JsonPropertyName("saleRate")]
        public int? SaleRate { get; set; }
    }
}

