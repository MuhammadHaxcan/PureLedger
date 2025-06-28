namespace PureLedger.Models
{
    public class Account
    {
        public int AccountId { get; set; }
        public int CategoryId { get; set; }
        public string AccountGroup { get; set; } // "PRODUCT" or "ACCOUNT"

        // ACCOUNT DATA
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public string Area { get; set; }
        public string City { get; set; }
        public string Email { get; set; }

        // PRODUCT DATA
        public string PkgForm { get; set; }       // e.g., "Bottle"
        public int? Pkg { get; set; }             // quantity per package
        public string Type { get; set; }          // e.g., "Liquid"
        public long? SaleRate { get; set; }

        public Category Category { get; set; }
    }
}
