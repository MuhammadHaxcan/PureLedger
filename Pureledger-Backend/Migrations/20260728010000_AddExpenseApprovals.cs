using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PureLedger.Data;

#nullable disable

namespace PureLedger.Migrations;

[DbContext(typeof(LedgerDbContext))]
[Migration("20260728010000_AddExpenseApprovals")]
public sealed class AddExpenseApprovals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM "AccountGroups"
                    WHERE "AccountGroupId" = 3 AND "AccountGroupName" <> 'Expense'
                ) THEN
                    RAISE EXCEPTION 'AccountGroupId 3 is already assigned to a non-Expense group';
                END IF;
                IF NOT EXISTS (SELECT 1 FROM "AccountGroups" WHERE "AccountGroupId" = 3) THEN
                    INSERT INTO "AccountGroups" ("AccountGroupId", "AccountGroupName")
                    VALUES (3, 'Expense');
                END IF;
            END $$;
            """);

        migrationBuilder.CreateTable(
            name: "ApprovalVouchers",
            columns: table => new
            {
                ApprovalVoucherId = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                RequestKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                JournalVoucherId = table.Column<int>(type: "integer", nullable: true),
                ProjectId = table.Column<int>(type: "integer", nullable: false),
                BookerId = table.Column<int>(type: "integer", nullable: true),
                CreditDays = table.Column<int>(type: "integer", nullable: false),
                SubmittedDate = table.Column<DateOnly>(type: "date", nullable: false),
                PostedByUserId = table.Column<int>(type: "integer", nullable: false),
                DecisionDate = table.Column<DateOnly>(type: "date", nullable: true),
                DecisionByUserId = table.Column<int>(type: "integer", nullable: true),
                RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_ApprovalVouchers", x => x.ApprovalVoucherId));

        migrationBuilder.CreateTable(
            name: "ApprovalVoucherEntries",
            columns: table => new
            {
                ApprovalVoucherEntryId = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ApprovalVoucherId = table.Column<int>(type: "integer", nullable: false),
                OriginalJournalEntryId = table.Column<int>(type: "integer", nullable: true),
                LineNumber = table.Column<int>(type: "integer", nullable: false),
                AccountId = table.Column<int>(type: "integer", nullable: false),
                CategoryId = table.Column<int>(type: "integer", nullable: false),
                Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Debit = table.Column<decimal>(type: "decimal(14,4)", nullable: false),
                Credit = table.Column<decimal>(type: "decimal(14,4)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovalVoucherEntries", x => x.ApprovalVoucherEntryId);
                table.ForeignKey(
                    name: "FK_ApprovalVoucherEntries_ApprovalVouchers_ApprovalVoucherId",
                    column: x => x.ApprovalVoucherId,
                    principalTable: "ApprovalVouchers",
                    principalColumn: "ApprovalVoucherId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalVoucherEntries_ApprovalVoucherId_LineNumber",
            table: "ApprovalVoucherEntries",
            columns: new[] { "ApprovalVoucherId", "LineNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalVouchers_Status_ProjectId_SubmittedDate",
            table: "ApprovalVouchers",
            columns: new[] { "Status", "ProjectId", "SubmittedDate" });

        migrationBuilder.CreateIndex(
            name: "UX_ApprovalVouchers_OnePendingRevision",
            table: "ApprovalVouchers",
            column: "JournalVoucherId",
            unique: true,
            filter: "\"Status\" = 'Pending' AND \"JournalVoucherId\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ApprovalVoucherEntries");
        migrationBuilder.DropTable(name: "ApprovalVouchers");
        migrationBuilder.Sql("""
            DELETE FROM "AccountGroups"
            WHERE "AccountGroupId" = 3 AND "AccountGroupName" = 'Expense';
            """);
    }
}
