namespace PureLedger.Models
{
    public class Account
    {
        public int AccountId { get; set; }
        public int CategoryId { get; set; }
        public int AccountGroupId { get; set; }
        public string? AccountName { get; set; }

        public AccountData? AccountData { get; set; }
        public ProductData? ProductData { get; set; }
        public Category? Category { get; set; }
        public AccountGroup? AccountGroup { get; set; }
        // ... navigation properties
    }

    public class AccountData
    {
        public int AccountDataId { get; set; }
        public int AccountId { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? Area { get; set; }
        public string? City { get; set; }
        public string? Email { get; set; }
        public Account Account { get; set; }
    }

    public class ProductData
    {
        public int ProductDataId { get; set; }
        public int AccountId { get; set; }
        public string? PkgForm { get; set; }
        public int? Pkg { get; set; }
        public string? Type { get; set; }
        public long? SaleRate { get; set; }
        public Account Account { get; set; }
    }
}
