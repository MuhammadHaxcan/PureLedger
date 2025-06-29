using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PureLedger.Migrations
{
    /// <inheritdoc />
    public partial class SplitAccountData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_AccountGroups_AccountGroupId",
                table: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_AccountGroupId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Area",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Pkg",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PkgForm",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "SaleRate",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Accounts");

            migrationBuilder.AlterColumn<string>(
                name: "AccountName",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "AccountDatas",
                columns: table => new
                {
                    AccountDataId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Area = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountDatas", x => x.AccountDataId);
                    table.ForeignKey(
                        name: "FK_AccountDatas_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductDatas",
                columns: table => new
                {
                    ProductDataId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    PkgForm = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Pkg = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SaleRate = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductDatas", x => x.ProductDataId);
                    table.ForeignKey(
                        name: "FK_ProductDatas_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountDatas_AccountId",
                table: "AccountDatas",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductDatas_AccountId",
                table: "ProductDatas",
                column: "AccountId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountDatas");

            migrationBuilder.DropTable(
                name: "ProductDatas");

            migrationBuilder.AlterColumn<string>(
                name: "AccountName",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Area",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Pkg",
                table: "Accounts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PkgForm",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "SaleRate",
                table: "Accounts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

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
    }
}
