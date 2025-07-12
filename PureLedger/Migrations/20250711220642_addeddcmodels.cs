using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PureLedger.Migrations
{
    /// <inheritdoc />
    public partial class addeddcmodels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeliveryChallanVouchers",
                columns: table => new
                {
                    DeliveryChallanId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JournalVoucherId = table.Column<int>(type: "int", nullable: false),
                    DCDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FinalDeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryChallanVouchers", x => x.DeliveryChallanId);
                    table.ForeignKey(
                        name: "FK_DeliveryChallanVouchers_JournalVouchers_JournalVoucherId",
                        column: x => x.JournalVoucherId,
                        principalTable: "JournalVouchers",
                        principalColumn: "JvId");
                });

            migrationBuilder.CreateTable(
                name: "DeliveryChallanEntries",
                columns: table => new
                {
                    DCEntryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeliveryChallanId = table.Column<int>(type: "int", nullable: false),
                    JournalEntryId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OtherReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryChallanEntries", x => x.DCEntryId);
                    table.ForeignKey(
                        name: "FK_DeliveryChallanEntries_DeliveryChallanVouchers_DeliveryChallanId",
                        column: x => x.DeliveryChallanId,
                        principalTable: "DeliveryChallanVouchers",
                        principalColumn: "DeliveryChallanId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeliveryChallanEntries_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "EntryId");
                    table.ForeignKey(
                        name: "FK_DeliveryChallanEntries_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "LocationId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanEntries_DeliveryChallanId",
                table: "DeliveryChallanEntries",
                column: "DeliveryChallanId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanEntries_JournalEntryId",
                table: "DeliveryChallanEntries",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanEntries_LocationId",
                table: "DeliveryChallanEntries",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanVouchers_JournalVoucherId",
                table: "DeliveryChallanVouchers",
                column: "JournalVoucherId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliveryChallanEntries");

            migrationBuilder.DropTable(
                name: "DeliveryChallanVouchers");
        }
    }
}
