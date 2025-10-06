using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PureLedger.Migrations
{
    /// <inheritdoc />
    public partial class addedmissedtable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MissedEntries",
                columns: table => new
                {
                    MissedEntryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerAccountId = table.Column<int>(type: "int", nullable: false),
                    ProductAccountId = table.Column<int>(type: "int", nullable: false),
                    MissedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Rate = table.Column<int>(type: "int", nullable: false),
                    Discount = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissedEntries", x => x.MissedEntryId);
                    table.ForeignKey(
                        name: "FK_MissedEntries_Accounts_CustomerAccountId",
                        column: x => x.CustomerAccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MissedEntries_Accounts_ProductAccountId",
                        column: x => x.ProductAccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MissedEntries_CustomerAccountId",
                table: "MissedEntries",
                column: "CustomerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MissedEntries_ProductAccountId",
                table: "MissedEntries",
                column: "ProductAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MissedEntries");
        }
    }
}
