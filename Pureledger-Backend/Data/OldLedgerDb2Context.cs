using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PureLedger.Models;

namespace PureLedger.Data
{
    public class OldLedgerDb2Context : DbContext, IOldLedgerDbContext
    {
        public OldLedgerDb2Context(DbContextOptions<OldLedgerDb2Context> options) : base(options) { }

        public DbSet<LegacyJournalEntry> JournalEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Value converter for DateOnly? to handle MySQL 0000-00-00 dates
            var dateOnlyToDateTimeConverter = new ValueConverter<DateOnly?, DateTime?>(
                v => v.HasValue ? v.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                v => v.HasValue ? DateOnly.FromDateTime(v.Value) : null
            );

            modelBuilder.Entity<LegacyJournalEntry>(entity =>
            {
                entity.ToTable("journalentries");
                entity.HasKey(e => e.Id);

                // Map columns to match the legacy database schema
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.MasterId).HasColumnName("master_id");
                entity.Property(e => e.ProjectId).HasColumnName("project_id");
                entity.Property(e => e.ProjectName).HasColumnName("project_name").HasMaxLength(250);
                entity.Property(e => e.CategoryId).HasColumnName("category_id").HasMaxLength(250);
                entity.Property(e => e.CategoryName).HasColumnName("category_name").HasMaxLength(250);
                entity.Property(e => e.AccountId).HasColumnName("account_id").HasMaxLength(250);
                entity.Property(e => e.AccountName).HasColumnName("account_name").HasMaxLength(250);
                entity.Property(e => e.Rbp).HasColumnName("rbp").HasMaxLength(250);
                entity.Property(e => e.PkgType).HasColumnName("pkg_type").HasMaxLength(250);
                entity.Property(e => e.EntryDate).HasColumnName("entry_date").HasConversion(dateOnlyToDateTimeConverter);
                entity.Property(e => e.DefaultTags).HasColumnName("default_tags").HasMaxLength(250);
                entity.Property(e => e.Tags).HasColumnName("tags").HasMaxLength(250);
                entity.Property(e => e.Des).HasColumnName("des").HasMaxLength(250);
                entity.Property(e => e.Pp2).HasColumnName("pp2").HasMaxLength(250);
                entity.Property(e => e.Nic).HasColumnName("nic").HasMaxLength(250);
                entity.Property(e => e.Contact).HasColumnName("contact").HasMaxLength(250);
                entity.Property(e => e.Enom).HasColumnName("enom").HasMaxLength(250);
                entity.Property(e => e.Qty).HasColumnName("qty");
                entity.Property(e => e.Rate).HasColumnName("rate").HasPrecision(10, 5);
                entity.Property(e => e.Debit).HasColumnName("debit").HasPrecision(10, 2);
                entity.Property(e => e.Credit).HasColumnName("credit").HasPrecision(10, 2);
                entity.Property(e => e.Dcn).HasColumnName("dcn").HasMaxLength(250);
                entity.Property(e => e.Location).HasColumnName("location").HasMaxLength(250);
                entity.Property(e => e.DcRemarks).HasColumnName("dc_remarks").HasMaxLength(250);
                entity.Property(e => e.Dc).HasColumnName("dc").HasMaxLength(250);
                entity.Property(e => e.Station).HasColumnName("station").HasMaxLength(250);
                entity.Property(e => e.Otherref).HasColumnName("otherref").HasMaxLength(250);
                entity.Property(e => e.DcDate).HasColumnName("dc_date").HasConversion(dateOnlyToDateTimeConverter);
                entity.Property(e => e.CartageRef).HasColumnName("cartage_ref").HasMaxLength(250);
                entity.Property(e => e.EntryStatus).HasColumnName("entry_status").HasMaxLength(50);
                entity.Property(e => e.EntryType).HasColumnName("entry_type").HasMaxLength(250);
                entity.Property(e => e.Fcrate).HasColumnName("fcrate").HasPrecision(20, 2);
                entity.Property(e => e.Roe).HasColumnName("roe").HasPrecision(20, 2);
                entity.Property(e => e.FcAmount).HasColumnName("fc_amount").HasPrecision(20, 2);
                entity.Property(e => e.Discount).HasColumnName("discount");
                entity.Property(e => e.Disper).HasColumnName("disper").HasPrecision(6, 0);
                entity.Property(e => e.Cas).HasColumnName("cas").HasPrecision(20, 2);
                entity.Property(e => e.Nm).HasColumnName("nm").HasPrecision(20, 2);
                entity.Property(e => e.Tpc).HasColumnName("tpc").HasPrecision(20, 2);
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.UserName).HasColumnName("user_name").HasMaxLength(250);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.EModify).HasColumnName("e_modify");
                entity.Property(e => e.Modifytime).HasColumnName("modifytime");
                entity.Property(e => e.DueDate).HasColumnName("due_date").HasConversion(dateOnlyToDateTimeConverter);
                entity.Property(e => e.BookerId).HasColumnName("booker_id");
                entity.Property(e => e.BookerName).HasColumnName("booker_name").HasMaxLength(50);

                // Add indexes for common query patterns
                entity.HasIndex(e => e.EntryDate).HasDatabaseName("idx_entry_date");
                entity.HasIndex(e => e.ProjectName).HasDatabaseName("idx_project_name");
                entity.HasIndex(e => e.CategoryName).HasDatabaseName("idx_category_name");
                entity.HasIndex(e => e.AccountId).HasDatabaseName("idx_account_id");
                entity.HasIndex(e => e.BookerName).HasDatabaseName("idx_booker_name");
                entity.HasIndex(e => e.Location).HasDatabaseName("idx_location");
            });
        }
    }
}
