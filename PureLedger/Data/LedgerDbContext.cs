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
        public DbSet<AccountGroup> AccountGroups { get; set; }
        public DbSet<AccountData> AccountDatas { get; set; }
        public DbSet<ProductData> ProductDatas { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<JournalVoucher> JournalVouchers { get; set; }
        public DbSet<JournalEntry> JournalEntries { get; set; }
        public DbSet<JournalEntryTag> JournalEntryTags { get; set; } // Add this line

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Many-to-many: JournalEntry <-> Tag via JournalEntryTag
            modelBuilder.Entity<JournalEntryTag>()
                .HasKey(jet => new { jet.JournalEntryId, jet.JournalEntryTagId });

            modelBuilder.Entity<JournalEntryTag>()
                .HasOne(jet => jet.JournalEntry)
                .WithMany(je => je.JournalEntryTags)
                .HasForeignKey(jet => jet.JournalEntryId);

            modelBuilder.Entity<JournalEntryTag>()
                .HasOne(jet => jet.Tag)
                .WithMany(t => t.JournalEntryTags)
                .HasForeignKey(jet => jet.JournalEntryTagId);

            // JournalEntries to Category relationship - use NO ACTION to break cascade path
            modelBuilder.Entity<JournalEntry>()
                .HasOne(je => je.Category)
                .WithMany()
                .HasForeignKey(je => je.CategoryId)
                .OnDelete(DeleteBehavior.NoAction);

            // JournalEntries to Account relationship
            modelBuilder.Entity<JournalEntry>()
                .HasOne(je => je.Account)
                .WithMany()
                .HasForeignKey(je => je.AccountId)
                .OnDelete(DeleteBehavior.NoAction);

            // JournalVoucher to Project relationship
            modelBuilder.Entity<JournalVoucher>()
                .HasOne(jv => jv.Project)
                .WithMany()
                .HasForeignKey(jv => jv.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // JournalEntries to JournalVoucher relationship
            modelBuilder.Entity<JournalEntry>()
                .HasOne(je => je.JournalVoucher)
                .WithMany(jv => jv.JournalEntries)
                .HasForeignKey(je => je.JournalVoucherId)
                .OnDelete(DeleteBehavior.Cascade);

            // Existing Account to AccountData and ProductData relationships
            modelBuilder.Entity<Account>()
                .HasOne(a => a.AccountData)
                .WithOne(ad => ad.Account)
                .HasForeignKey<AccountData>(ad => ad.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Account>()
                .HasOne(a => a.ProductData)
                .WithOne(pd => pd.Account)
                .HasForeignKey<ProductData>(pd => pd.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // Seed AccountType data
            modelBuilder.Entity<AccountType>().HasData(
                new AccountType { AccountTypeId = 1, AccountTypeName = "Assets" },
                new AccountType { AccountTypeId = 2, AccountTypeName = "Liabilities" },
                new AccountType { AccountTypeId = 3, AccountTypeName = "Equity" },
                new AccountType { AccountTypeId = 4, AccountTypeName = "Revenue" },
                new AccountType { AccountTypeId = 5, AccountTypeName = "Expense" }
            );

            // Seed AccountGroup data
            modelBuilder.Entity<AccountGroup>().HasData(
                new AccountGroup { AccountGroupId = 1, AccountGroupName = "Account" },
                new AccountGroup { AccountGroupId = 2, AccountGroupName = "Product" }
            );
        }
    }
}