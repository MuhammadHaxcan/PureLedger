using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PureLedger.Migrations
{
    /// <inheritdoc />
    public partial class addedprojectcartagefor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectCartageSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    ForExpenseAccountId = table.Column<int>(type: "int", nullable: false),
                    CartageTransporterCategoryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectCartageSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectCartageSettings_Accounts_ForExpenseAccountId",
                        column: x => x.ForExpenseAccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId");
                    table.ForeignKey(
                        name: "FK_ProjectCartageSettings_Categories_CartageTransporterCategoryId",
                        column: x => x.CartageTransporterCategoryId,
                        principalTable: "Categories",
                        principalColumn: "CategoryId");
                    table.ForeignKey(
                        name: "FK_ProjectCartageSettings_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCartageSettings_CartageTransporterCategoryId",
                table: "ProjectCartageSettings",
                column: "CartageTransporterCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCartageSettings_ForExpenseAccountId",
                table: "ProjectCartageSettings",
                column: "ForExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCartageSettings_ProjectId",
                table: "ProjectCartageSettings",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectCartageSettings");
        }
    }
}
