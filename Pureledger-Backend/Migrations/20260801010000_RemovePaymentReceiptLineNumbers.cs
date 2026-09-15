using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PureLedger.Data;

#nullable disable

namespace PureLedger.Migrations;

[DbContext(typeof(LedgerDbContext))]
[Migration("20260801010000_RemovePaymentReceiptLineNumbers")]
public sealed class RemovePaymentReceiptLineNumbers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_JournalEntryInstrumentDetails_LineNumber",
            table: "JournalEntryInstrumentDetails");

        migrationBuilder.DropColumn(
            name: "LineNumber",
            table: "JournalEntryInstrumentDetails");

        migrationBuilder.AlterColumn<int>(
            name: "LineNumber",
            table: "ApprovalVoucherEntries",
            type: "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.Sql(
            "UPDATE \"ApprovalVoucherEntries\" AS e SET \"LineNumber\" = NULL " +
            "FROM \"ApprovalVouchers\" AS a " +
            "WHERE a.\"ApprovalVoucherId\" = e.\"ApprovalVoucherId\" " +
            "AND a.\"VoucherType\" = 'PaymentReceipt';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "LineNumber",
            table: "JournalEntryInstrumentDetails",
            type: "integer",
            nullable: true);

        migrationBuilder.Sql(@"
            WITH numbered AS (
                SELECT d.""JournalEntryInstrumentDetailId"",
                       row_number() OVER (
                           PARTITION BY e.""JournalVoucherId""
                           ORDER BY e.""JournalEntryId"")::integer AS line_number
                FROM ""JournalEntryInstrumentDetails"" d
                JOIN ""JournalEntries"" e ON e.""JournalEntryId"" = d.""JournalEntryId""
            )
            UPDATE ""JournalEntryInstrumentDetails"" d
            SET ""LineNumber"" = numbered.line_number
            FROM numbered
            WHERE numbered.""JournalEntryInstrumentDetailId"" = d.""JournalEntryInstrumentDetailId"";");

        migrationBuilder.AlterColumn<int>(
            name: "LineNumber",
            table: "JournalEntryInstrumentDetails",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_JournalEntryInstrumentDetails_LineNumber",
            table: "JournalEntryInstrumentDetails",
            sql: "\"LineNumber\" > 0");

        migrationBuilder.Sql(@"
            WITH numbered AS (
                SELECT e.""ApprovalVoucherEntryId"",
                       row_number() OVER (
                           PARTITION BY e.""ApprovalVoucherId""
                           ORDER BY e.""ApprovalVoucherEntryId"")::integer AS line_number
                FROM ""ApprovalVoucherEntries"" e
                JOIN ""ApprovalVouchers"" a ON a.""ApprovalVoucherId"" = e.""ApprovalVoucherId""
                WHERE a.""VoucherType"" = 'PaymentReceipt'
            )
            UPDATE ""ApprovalVoucherEntries"" e
            SET ""LineNumber"" = numbered.line_number
            FROM numbered
            WHERE numbered.""ApprovalVoucherEntryId"" = e.""ApprovalVoucherEntryId"";");

        migrationBuilder.AlterColumn<int>(
            name: "LineNumber",
            table: "ApprovalVoucherEntries",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);
    }
}
