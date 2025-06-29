using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PureLedger.Migrations
{
    /// <inheritdoc />
    public partial class accountgroupaddedandaccountname : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AccountGroup",
                table: "Accounts",
                newName: "AccountName");

            migrationBuilder.AddColumn<int>(
                name: "AccountGroupId",
                table: "Accounts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AccountGroups",
                columns: table => new
                {
                    AccountGroupId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountGroupName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountGroups", x => x.AccountGroupId);
                });

            migrationBuilder.InsertData(
                table: "AccountGroups",
                columns: new[] { "AccountGroupId", "AccountGroupName" },
                values: new object[,]
                {
                    { 1, "Account" },
                    { 2, "Product" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountGroupId",
                table: "Accounts",
                column: "AccountGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_AccountGroups_AccountGroupId",
                table: "Accounts",
                column: "AccountGroupId",
                principalTable: "AccountGroups",
                principalColumn: "AccountGroupId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_AccountGroups_AccountGroupId",
                table: "Accounts");

            migrationBuilder.DropTable(
                name: "AccountGroups");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_AccountGroupId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "AccountGroupId",
                table: "Accounts");

            migrationBuilder.RenameColumn(
                name: "AccountName",
                table: "Accounts",
                newName: "AccountGroup");
        }
    }
}
