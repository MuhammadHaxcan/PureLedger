using Microsoft.EntityFrameworkCore;
using PureLedger.Models.Master;

namespace PureLedger.Data
{
    public class MasterDbContext : DbContext
    {
        public MasterDbContext(DbContextOptions<MasterDbContext> options) : base(options) { }

        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<SystemAdmin> SystemAdmins { get; set; }
        public DbSet<TenantAuditLog> TenantAuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Set default schema for master database
            modelBuilder.HasDefaultSchema("master");

            // Tenant configuration
            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.HasIndex(t => t.Slug).IsUnique();
                entity.HasIndex(t => t.SchemaName).IsUnique();
                entity.HasIndex(t => t.IsActive);
            });

            // SystemAdmin configuration
            modelBuilder.Entity<SystemAdmin>(entity =>
            {
                entity.HasIndex(sa => sa.Username).IsUnique();
            });

            // TenantAuditLog configuration
            modelBuilder.Entity<TenantAuditLog>(entity =>
            {
                entity.HasIndex(tal => tal.TenantId);
                entity.HasIndex(tal => tal.Timestamp);
                entity.HasIndex(tal => tal.Operation);

                entity.HasOne(tal => tal.Tenant)
                    .WithMany(t => t.AuditLogs)
                    .HasForeignKey(tal => tal.TenantId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Seed default system admin (sysadmin / Admin@123)
            // Password hash generated using BCrypt.Net-Next
            modelBuilder.Entity<SystemAdmin>().HasData(
                new SystemAdmin
                {
                    Id = 1,
                    Username = "sysadmin",
                    PasswordHash = "$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/X4.xGhGCPrP6cPTDi", // BCrypt hash of "Admin@123"
                    FullName = "System Administrator",
                    Email = "sysadmin@pureledger.local",
                    IsActive = true,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        }
    }
}
