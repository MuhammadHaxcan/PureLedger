using global::PureLedger.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace PureLedger.Data
{
    public class LedgerDbContext : DbContext
    {
        public LedgerDbContext(DbContextOptions<LedgerDbContext> options) : base(options) { }

        public DbSet<Project> Projects { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<AccountType> AccountTypes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AccountType>().HasData(
                new AccountType { AccountTypeId = 1, AccountTypeName = "Assets" },
                new AccountType { AccountTypeId = 2, AccountTypeName = "Liabilities" },
                new AccountType { AccountTypeId = 3, AccountTypeName = "Equity" },
                new AccountType { AccountTypeId = 4, AccountTypeName = "Revenue" },
                new AccountType { AccountTypeId = 5, AccountTypeName = "Expense" }
            );
        }
    }
}
