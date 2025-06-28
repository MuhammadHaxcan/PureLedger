using System.Security.Principal;

namespace PureLedger.Models
{
    public class Category
    {
        public int CategoryId { get; set; }
        public int ProjectId { get; set; }
        public int AccountTypeId { get; set; } // FK to predefined AccountType table
        public string CategoryName { get; set; }

        public Project Project { get; set; }
        public ICollection<Account> Accounts { get; set; }
        public AccountType AccountType { get; set; }
    }

}
