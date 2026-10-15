using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PureLedger.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountLinkedBooker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LinkedBookerId",
                table: "Accounts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_LinkedBookerId",
                table: "Accounts",
                column: "LinkedBookerId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_LinkedBooker_AccountGroup",
                table: "Accounts",
                sql: "\"LinkedBookerId\" IS NULL OR \"AccountGroupId\" = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_Bookers_LinkedBookerId",
                table: "Accounts",
                column: "LinkedBookerId",
                principalTable: "Bookers",
                principalColumn: "BookerID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_Bookers_LinkedBookerId",
                table: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_LinkedBookerId",
                table: "Accounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_LinkedBooker_AccountGroup",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "LinkedBookerId",
                table: "Accounts");
        }
    }
}
