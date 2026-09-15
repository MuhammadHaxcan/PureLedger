using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using PureLedger.Data;

#nullable disable

namespace PureLedger.Migrations;

[DbContext(typeof(LedgerDbContext))]
[Migration("20260731010000_AddPaymentReceiptVouchers")]
public sealed class AddPaymentReceiptVouchers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "VoucherType", table: "ApprovalVouchers", type: "character varying(30)",
            maxLength: 30, nullable: true);
        migrationBuilder.Sql(
            "UPDATE \"ApprovalVouchers\" SET \"VoucherType\" = 'ManualExpense' WHERE \"VoucherType\" IS NULL;");
        migrationBuilder.AlterColumn<string>(
            name: "VoucherType", table: "ApprovalVouchers", type: "character varying(30)",
            maxLength: 30, nullable: false, defaultValue: "ManualExpense",
            oldClrType: typeof(string), oldType: "character varying(30)", oldMaxLength: 30, oldNullable: true);
        migrationBuilder.AddColumn<DateOnly>(
            name: "RequestedEntryDate", table: "ApprovalVouchers", type: "date", nullable: true);
        migrationBuilder.AddColumn<Guid>(
            name: "SnapshotVersion", table: "ApprovalVouchers", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<bool>(
            name: "HadDuplicateWarningsAtSubmission", table: "ApprovalVouchers", type: "boolean",
            nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>(
            name: "HadDuplicateWarningsAtDecision", table: "ApprovalVouchers", type: "boolean",
            nullable: false, defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "InstrumentType", table: "ApprovalVoucherEntries", type: "character varying(30)",
            maxLength: 30, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "ReferenceNumber", table: "ApprovalVoucherEntries", type: "character varying(100)",
            maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "NormalizedReferenceNumber", table: "ApprovalVoucherEntries", type: "character varying(100)",
            maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<DateOnly>(
            name: "InstrumentDate", table: "ApprovalVoucherEntries", type: "date", nullable: true);

        migrationBuilder.CreateTable(
            name: "JournalEntryInstrumentDetails",
            columns: table => new
            {
                JournalEntryInstrumentDetailId = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                JournalEntryId = table.Column<int>(type: "integer", nullable: false),
                LineNumber = table.Column<int>(type: "integer", nullable: false),
                InstrumentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                NormalizedReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                InstrumentDate = table.Column<DateOnly>(type: "date", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_JournalEntryInstrumentDetails", x => x.JournalEntryInstrumentDetailId);
                table.CheckConstraint("CK_JournalEntryInstrumentDetails_LineNumber", "\"LineNumber\" > 0");
                table.CheckConstraint(
                    "CK_JournalEntryInstrumentDetails_InstrumentFields",
                    "\"InstrumentType\" IN ('Cash','Cheque','BankTransfer','WireTransfer') AND ((\"InstrumentType\" = 'Cash' AND \"ReferenceNumber\" IS NULL AND \"NormalizedReferenceNumber\" IS NULL AND \"InstrumentDate\" IS NULL) OR (\"InstrumentType\" <> 'Cash' AND \"ReferenceNumber\" IS NOT NULL AND \"NormalizedReferenceNumber\" IS NOT NULL AND length(\"ReferenceNumber\") > 0 AND \"ReferenceNumber\" = btrim(\"ReferenceNumber\") AND \"NormalizedReferenceNumber\" = upper(\"ReferenceNumber\") AND \"InstrumentDate\" IS NOT NULL))");
                table.ForeignKey(
                    name: "FK_JournalEntryInstrumentDetails_JournalEntries_JournalEntryId",
                    column: x => x.JournalEntryId,
                    principalTable: "JournalEntries",
                    principalColumn: "JournalEntryId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.AddCheckConstraint(
            name: "CK_ApprovalVouchers_PaymentReceiptFields",
            table: "ApprovalVouchers",
            sql: "(\"VoucherType\" = 'ManualExpense' AND \"RequestedEntryDate\" IS NULL AND \"SnapshotVersion\" IS NULL) OR (\"VoucherType\" = 'PaymentReceipt' AND \"RequestedEntryDate\" IS NOT NULL AND \"SnapshotVersion\" IS NOT NULL)");
        migrationBuilder.AddCheckConstraint(
            name: "CK_ApprovalVoucherEntries_InstrumentFields",
            table: "ApprovalVoucherEntries",
            sql: "(\"InstrumentType\" IS NULL AND \"ReferenceNumber\" IS NULL AND \"NormalizedReferenceNumber\" IS NULL AND \"InstrumentDate\" IS NULL) OR (\"InstrumentType\" = 'Cash' AND \"ReferenceNumber\" IS NULL AND \"NormalizedReferenceNumber\" IS NULL AND \"InstrumentDate\" IS NULL) OR (\"InstrumentType\" IN ('Cheque','BankTransfer','WireTransfer') AND \"ReferenceNumber\" IS NOT NULL AND \"NormalizedReferenceNumber\" IS NOT NULL AND length(\"ReferenceNumber\") > 0 AND \"ReferenceNumber\" = btrim(\"ReferenceNumber\") AND \"NormalizedReferenceNumber\" = upper(\"ReferenceNumber\") AND \"InstrumentDate\" IS NOT NULL)");

        migrationBuilder.CreateIndex(
            name: "IX_JournalEntryInstrumentDetails_InstrumentType_NormalizedReferenceNumber",
            table: "JournalEntryInstrumentDetails",
            columns: new[] { "InstrumentType", "NormalizedReferenceNumber" });
        migrationBuilder.CreateIndex(
            name: "IX_JournalEntryInstrumentDetails_JournalEntryId",
            table: "JournalEntryInstrumentDetails",
            column: "JournalEntryId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_ApprovalVouchers_PaymentReceiptFields", table: "ApprovalVouchers");
        migrationBuilder.DropCheckConstraint(
            name: "CK_ApprovalVoucherEntries_InstrumentFields", table: "ApprovalVoucherEntries");
        migrationBuilder.DropIndex(
            name: "IX_JournalEntryInstrumentDetails_InstrumentType_NormalizedReferenceNumber",
            table: "JournalEntryInstrumentDetails");
        migrationBuilder.DropIndex(
            name: "IX_JournalEntryInstrumentDetails_JournalEntryId",
            table: "JournalEntryInstrumentDetails");
        migrationBuilder.DropTable(name: "JournalEntryInstrumentDetails");
        migrationBuilder.DropColumn(name: "InstrumentType", table: "ApprovalVoucherEntries");
        migrationBuilder.DropColumn(name: "ReferenceNumber", table: "ApprovalVoucherEntries");
        migrationBuilder.DropColumn(name: "NormalizedReferenceNumber", table: "ApprovalVoucherEntries");
        migrationBuilder.DropColumn(name: "InstrumentDate", table: "ApprovalVoucherEntries");
        migrationBuilder.DropColumn(name: "VoucherType", table: "ApprovalVouchers");
        migrationBuilder.DropColumn(name: "RequestedEntryDate", table: "ApprovalVouchers");
        migrationBuilder.DropColumn(name: "SnapshotVersion", table: "ApprovalVouchers");
        migrationBuilder.DropColumn(name: "HadDuplicateWarningsAtSubmission", table: "ApprovalVouchers");
        migrationBuilder.DropColumn(name: "HadDuplicateWarningsAtDecision", table: "ApprovalVouchers");
    }
}
