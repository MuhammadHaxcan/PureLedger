using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PureLedger.Migrations
{
    /// <inheritdoc />
    public partial class updatedrelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryChallanEntries_JournalEntries_JournalEntryId",
                table: "DeliveryChallanEntries");

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryChallanEntries_JournalEntries_JournalEntryId",
                table: "DeliveryChallanEntries",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "JournalEntryId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryChallanEntries_JournalEntries_JournalEntryId",
                table: "DeliveryChallanEntries");

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryChallanEntries_JournalEntries_JournalEntryId",
                table: "DeliveryChallanEntries",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "JournalEntryId");
        }
    }
}
