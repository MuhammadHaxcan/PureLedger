using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PureLedger.Migrations
{
    /// <inheritdoc />
    public partial class NewFuncFinalChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsStockTransfer",
                table: "JournalVouchers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ApplicationErrorLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ErrorId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExceptionType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Message = table.Column<string>(type: "text", nullable: false),
                    StackTrace = table.Column<string>(type: "text", nullable: true),
                    Method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Username = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    RequestBodySummary = table.Column<string>(type: "text", nullable: true),
                    ExtraPropertiesJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationErrorLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalVouchers",
                columns: table => new
                {
                    ApprovalVoucherId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VoucherType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "ManualExpense"),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RequestKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    JournalVoucherId = table.Column<int>(type: "integer", nullable: true),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    BookerId = table.Column<int>(type: "integer", nullable: true),
                    CreditDays = table.Column<int>(type: "integer", nullable: false),
                    SubmittedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RequestedEntryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SnapshotVersion = table.Column<Guid>(type: "uuid", nullable: true),
                    HadDuplicateWarningsAtSubmission = table.Column<bool>(type: "boolean", nullable: false),
                    HadDuplicateWarningsAtDecision = table.Column<bool>(type: "boolean", nullable: false),
                    PostedByUserId = table.Column<int>(type: "integer", nullable: false),
                    DecisionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DecisionByUserId = table.Column<int>(type: "integer", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalVouchers", x => x.ApprovalVoucherId);
                    table.CheckConstraint("CK_ApprovalVouchers_PaymentReceiptFields", "(\"VoucherType\" = 'ManualExpense' AND \"RequestedEntryDate\" IS NULL AND \"SnapshotVersion\" IS NULL) OR (\"VoucherType\" = 'PaymentReceipt' AND \"RequestedEntryDate\" IS NOT NULL AND \"SnapshotVersion\" IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "JournalEntryInstrumentDetails",
                columns: table => new
                {
                    JournalEntryInstrumentDetailId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JournalEntryId = table.Column<int>(type: "integer", nullable: false),
                    InstrumentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NormalizedReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InstrumentDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntryInstrumentDetails", x => x.JournalEntryInstrumentDetailId);
                    table.CheckConstraint("CK_JournalEntryInstrumentDetails_InstrumentFields", "\"InstrumentType\" IN ('Cash','Cheque','BankTransfer','WireTransfer') AND ((\"InstrumentType\" = 'Cash' AND \"ReferenceNumber\" IS NULL AND \"NormalizedReferenceNumber\" IS NULL AND \"InstrumentDate\" IS NULL) OR (\"InstrumentType\" <> 'Cash' AND \"ReferenceNumber\" IS NOT NULL AND \"NormalizedReferenceNumber\" IS NOT NULL AND length(\"ReferenceNumber\") > 0 AND \"ReferenceNumber\" = btrim(\"ReferenceNumber\") AND \"NormalizedReferenceNumber\" = upper(\"ReferenceNumber\") AND \"InstrumentDate\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_JournalEntryInstrumentDetails_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "JournalEntryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectImg",
                columns: table => new
                {
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    ImageDataUrl = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectImg", x => x.ProjectId);
                    table.ForeignKey(
                        name: "FK_ProjectImg_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            // Move the existing image before removing its original column.
            migrationBuilder.Sql("""
                INSERT INTO "ProjectImg" ("ProjectId", "ImageDataUrl")
                SELECT DISTINCT ON ("ProjectId") "ProjectId", "ProjectImage"
                FROM "ProjectCartageSettings"
                WHERE "ProjectImage" IS NOT NULL AND btrim("ProjectImage") <> ''
                ORDER BY "ProjectId", "Id" DESC;
                """);

            migrationBuilder.DropColumn(
                name: "ProjectImage",
                table: "ProjectCartageSettings");

            migrationBuilder.CreateTable(
                name: "ApprovalVoucherEntries",
                columns: table => new
                {
                    ApprovalVoucherEntryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApprovalVoucherId = table.Column<int>(type: "integer", nullable: false),
                    OriginalJournalEntryId = table.Column<int>(type: "integer", nullable: true),
                    LineNumber = table.Column<int>(type: "integer", nullable: true),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Debit = table.Column<decimal>(type: "numeric(14,4)", nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(14,4)", nullable: false),
                    InstrumentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NormalizedReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InstrumentDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalVoucherEntries", x => x.ApprovalVoucherEntryId);
                    table.CheckConstraint("CK_ApprovalVoucherEntries_InstrumentFields", "(\"InstrumentType\" IS NULL AND \"ReferenceNumber\" IS NULL AND \"NormalizedReferenceNumber\" IS NULL AND \"InstrumentDate\" IS NULL) OR (\"InstrumentType\" = 'Cash' AND \"ReferenceNumber\" IS NULL AND \"NormalizedReferenceNumber\" IS NULL AND \"InstrumentDate\" IS NULL) OR (\"InstrumentType\" IN ('Cheque','BankTransfer','WireTransfer') AND \"ReferenceNumber\" IS NOT NULL AND \"NormalizedReferenceNumber\" IS NOT NULL AND length(\"ReferenceNumber\") > 0 AND \"ReferenceNumber\" = btrim(\"ReferenceNumber\") AND \"NormalizedReferenceNumber\" = upper(\"ReferenceNumber\") AND \"InstrumentDate\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ApprovalVoucherEntries_ApprovalVouchers_ApprovalVoucherId",
                        column: x => x.ApprovalVoucherId,
                        principalTable: "ApprovalVouchers",
                        principalColumn: "ApprovalVoucherId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AccountGroups",
                columns: new[] { "AccountGroupId", "AccountGroupName" },
                values: new object[] { 3, "Expense" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationErrorLogs_CorrelationId",
                table: "ApplicationErrorLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationErrorLogs_CreatedAtUtc",
                table: "ApplicationErrorLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationErrorLogs_ErrorId",
                table: "ApplicationErrorLogs",
                column: "ErrorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationErrorLogs_Level_CreatedAtUtc",
                table: "ApplicationErrorLogs",
                columns: new[] { "Level", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationErrorLogs_StatusCode_CreatedAtUtc",
                table: "ApplicationErrorLogs",
                columns: new[] { "StatusCode", "CreatedAtUtc" });

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

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryInstrumentDetails_InstrumentType_NormalizedRefe~",
                table: "JournalEntryInstrumentDetails",
                columns: new[] { "InstrumentType", "NormalizedReferenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryInstrumentDetails_JournalEntryId",
                table: "JournalEntryInstrumentDetails",
                column: "JournalEntryId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProjectImage",
                table: "ProjectCartageSettings",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "ProjectCartageSettings" AS settings
                SET "ProjectImage" = image."ImageDataUrl"
                FROM "ProjectImg" AS image
                WHERE settings."ProjectId" = image."ProjectId";
                """);

            migrationBuilder.DropTable(
                name: "ApplicationErrorLogs");

            migrationBuilder.DropTable(
                name: "ApprovalVoucherEntries");

            migrationBuilder.DropTable(
                name: "JournalEntryInstrumentDetails");

            migrationBuilder.DropTable(
                name: "ProjectImg");

            migrationBuilder.DropTable(
                name: "ApprovalVouchers");

            migrationBuilder.DeleteData(
                table: "AccountGroups",
                keyColumn: "AccountGroupId",
                keyValue: 3);

            migrationBuilder.DropColumn(
                name: "IsStockTransfer",
                table: "JournalVouchers");

        }
    }
}
