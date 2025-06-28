using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PureLedger.Migrations
{
    /// <inheritdoc />
    public partial class addedaccounttype : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountTypes",
                columns: table => new
                {
                    AccountTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTypes", x => x.AccountTypeId);
                });

            migrationBuilder.InsertData(
                table: "AccountTypes",
                columns: new[] { "AccountTypeId", "Name" },
                values: new object[,]
                {
                    { 1, "Assets" },
                    { 2, "Liabilities" },
                    { 3, "Equity" },
                    { 4, "Revenue" },
                    { 5, "Expense" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_AccountTypeId",
                table: "Categories",
                column: "AccountTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_AccountTypes_AccountTypeId",
                table: "Categories",
                column: "AccountTypeId",
                principalTable: "AccountTypes",
                principalColumn: "AccountTypeId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_AccountTypes_AccountTypeId",
                table: "Categories");

            migrationBuilder.DropTable(
                name: "AccountTypes");

            migrationBuilder.DropIndex(
                name: "IX_Categories_AccountTypeId",
                table: "Categories");
        }
    }
}
