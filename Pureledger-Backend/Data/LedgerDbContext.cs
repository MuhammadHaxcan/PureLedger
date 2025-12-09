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
        public DbSet<JournalVoucher> JournalVouchers { get; set; }
        public DbSet<JournalEntry> JournalEntries { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<DeliveryChallanVoucher> DeliveryChallanVouchers { get; set; }
        public DbSet<DeliveryChallanEntry> DeliveryChallanEntries { get; set; }
        public DbSet<JournalVoucherReference> JournalVoucherReferences { get; set; }
        
        // Authentication tables
        public DbSet<User> Users { get; set; }
        public DbSet<UserProject> UserProjects { get; set; }
        public DbSet<UserControllerAccess> UserControllerAccess { get; set; }
        public DbSet<ProjectCartageSettings> ProjectCartageSettings { get; set; }
        public DbSet<MissedEntry> MissedEntries { get; set; }

        // Recipe related tables
        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<RecipeItem> RecipeItems { get; set; }
        public DbSet<Booker> Bookers { get; set; }
        
        // Signature table
        public DbSet<Signature> Signatures { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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

            modelBuilder.Entity<Account>()
                .Navigation(a => a.ProductData)
                .AutoInclude();

            // New relationships for Delivery Challan
            // DeliveryChallanVoucher to JournalVoucher relationship
            modelBuilder.Entity<DeliveryChallanVoucher>()
                .HasOne(dc => dc.JournalVoucher)
                .WithMany()
                .HasForeignKey(dc => dc.JournalVoucherId)
                .OnDelete(DeleteBehavior.NoAction);

            // DeliveryChallanEntry to DeliveryChallanVoucher relationship
            modelBuilder.Entity<DeliveryChallanEntry>()
                .HasOne(dce => dce.DeliveryChallanVoucher)
                .WithMany(dcv => dcv.Entries)
                .HasForeignKey(dce => dce.DeliveryChallanVoucherId)
                .OnDelete(DeleteBehavior.Cascade);

            // DeliveryChallanEntry to JournalEntry relationship
            modelBuilder.Entity<DeliveryChallanEntry>()
                .HasOne(dce => dce.JournalEntry)
                .WithMany()
                .HasForeignKey(dce => dce.JournalEntryId)
                .OnDelete(DeleteBehavior.Cascade);

            // DeliveryChallanEntry to Location relationship
            modelBuilder.Entity<DeliveryChallanEntry>()
                .HasOne(dce => dce.Location)
                .WithMany()
                .HasForeignKey(dce => dce.LocationId)
                .OnDelete(DeleteBehavior.NoAction);

            // User relationships
            modelBuilder.Entity<UserProject>()
                .HasOne(up => up.User)
                .WithMany(u => u.UserProjects)
                .HasForeignKey(up => up.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserProject>()
                .HasOne(up => up.Project)
                .WithMany()
                .HasForeignKey(up => up.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<UserControllerAccess>()
                .HasOne(uca => uca.User)
                .WithMany(u => u.ControllerAccess)
                .HasForeignKey(uca => uca.UserId)
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

            // Configure JournalVoucherReference relationships
            modelBuilder.Entity<JournalVoucherReference>()
                .HasOne(r => r.MainVoucher)
                .WithMany(v => v.OutgoingReferences)
                .HasForeignKey(r => r.MainVoucherId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<JournalVoucherReference>()
                .HasOne(r => r.ReferencedVoucher)
                .WithMany(v => v.IncomingReferences)
                .HasForeignKey(r => r.ReferencedVoucherId)
                .OnDelete(DeleteBehavior.NoAction);
                
            // Seed default admin user (password: admin123)
            modelBuilder.Entity<User>().HasData(
                new User { 
                    UserId = 1, 
                    Username = "admin",
                    PasswordHash = "$2a$11$s7AG4CSDFAkbkR9NAogKTeGdBsW8/6E0DQuGe8/V.ePYHuXyHxhoa", // Pre-generated hash for "admin123"
                    FullName = "System Administrator",
                    Email = "admin@example.com",
                    IsAdmin = true, 
                    Subroute = "Default",
                    IsActive = true,
                    CreatedDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc) // Fixed date
                }
            );

            // ProjectCartageSettings relationships - break cascade to avoid multiple cascade paths
            modelBuilder.Entity<ProjectCartageSettings>()
                .HasOne(pcs => pcs.Project)
                .WithMany()
                .HasForeignKey(pcs => pcs.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProjectCartageSettings>()
                .HasOne(pcs => pcs.ForExpenseAccount)
                .WithMany()
                .HasForeignKey(pcs => pcs.ForExpenseAccountId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ProjectCartageSettings>()
                .HasOne(pcs => pcs.CartageTransporterCategory)
                .WithMany()
                .HasForeignKey(pcs => pcs.CartageTransporterCategoryId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<MissedEntry>()
                .HasOne(m => m.Project)
                .WithMany()
                .HasForeignKey(m => m.ProjectId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<MissedEntry>()
                .HasOne(m => m.CustomerAccount)
                .WithMany()
                .HasForeignKey(m => m.CustomerAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MissedEntry>()
                .HasOne(m => m.ProductAccount)
                .WithMany()
                .HasForeignKey(m => m.ProductAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            // Recipe to Project relationship
            modelBuilder.Entity<Recipe>()
                .HasOne(r => r.Project)
                .WithMany()
                .HasForeignKey(r => r.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // RecipeItem to Recipe relationship
            modelBuilder.Entity<RecipeItem>()
                .HasOne(ri => ri.Recipe)
                .WithMany(r => r.RecipeItems)
                .HasForeignKey(ri => ri.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            // RecipeItem to Category relationship
            modelBuilder.Entity<RecipeItem>()
                .HasOne(ri => ri.Category)
                .WithMany()
                .HasForeignKey(ri => ri.CategoryId)
                .OnDelete(DeleteBehavior.NoAction);
                
            // RecipeItem to Account relationship
            modelBuilder.Entity<RecipeItem>()
                .HasOne(ri => ri.Account)
                .WithMany()
                .HasForeignKey(ri => ri.AccountId)
                .OnDelete(DeleteBehavior.NoAction);

            // ========================================
            // Performance Indexes
            // ========================================

            // Journal Entries indexes for common query patterns
            modelBuilder.Entity<JournalEntry>()
                .HasIndex(je => new { je.AccountId, je.JournalVoucherId })
                .HasDatabaseName("IX_JournalEntries_AccountId_JournalVoucherId");

            modelBuilder.Entity<JournalEntry>()
                .HasIndex(je => je.CategoryId)
                .HasDatabaseName("IX_JournalEntries_CategoryId");

            modelBuilder.Entity<JournalEntry>()
                .HasIndex(je => new { je.IsProcessed, je.Quantity })
                .HasDatabaseName("IX_JournalEntries_IsProcessed_Quantity");

            // Journal Vouchers indexes for filtering and reporting
            modelBuilder.Entity<JournalVoucher>()
                .HasIndex(jv => new { jv.EntryDate, jv.IsCompleted })
                .HasDatabaseName("IX_JournalVouchers_EntryDate_IsCompleted");

            modelBuilder.Entity<JournalVoucher>()
                .HasIndex(jv => new { jv.ProjectId, jv.IsCompleted })
                .HasDatabaseName("IX_JournalVouchers_ProjectId_IsCompleted");

            modelBuilder.Entity<JournalVoucher>()
                .HasIndex(jv => jv.IsBaseVoucher)
                .HasDatabaseName("IX_JournalVouchers_IsBaseVoucher");

            // Delivery Challan indexes
            modelBuilder.Entity<DeliveryChallanEntry>()
                .HasIndex(dce => dce.JournalEntryId)
                .HasDatabaseName("IX_DeliveryChallanEntries_JournalEntryId");

            modelBuilder.Entity<DeliveryChallanVoucher>()
                .HasIndex(dcv => dcv.JournalVoucherId)
                .HasDatabaseName("IX_DeliveryChallanVouchers_JournalVoucherId");

            // Account indexes
            modelBuilder.Entity<Account>()
                .HasIndex(a => new { a.CategoryId, a.IsEnabled })
                .HasDatabaseName("IX_Accounts_CategoryId_IsEnabled");

            // Category indexes
            modelBuilder.Entity<Category>()
                .HasIndex(c => c.ProjectId)
                .HasDatabaseName("IX_Categories_ProjectId");

            // User indexes
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique()
                .HasDatabaseName("IX_Users_Username");

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Subroute)
                .HasDatabaseName("IX_Users_Subroute");

            // JournalVoucherReference indexes
            modelBuilder.Entity<JournalVoucherReference>()
                .HasIndex(r => new { r.MainVoucherId, r.ReferenceType })
                .HasDatabaseName("IX_JournalVoucherReferences_MainVoucherId_ReferenceType");
        }
    }
}