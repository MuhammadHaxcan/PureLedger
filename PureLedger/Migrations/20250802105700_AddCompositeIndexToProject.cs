using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PureLedger.Migrations
{
    /// <inheritdoc />
    public partial class AddCompositeIndexToProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tags_TagName",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Projects_ProjectName",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Locations_LocationName",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Categories_CategoryName",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_AccountName",
                table: "Accounts");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_TagName_TagId",
                table: "Tags",
                columns: new[] { "TagName", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectName_ProjectId",
                table: "Projects",
                columns: new[] { "ProjectName", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Locations_LocationName_LocationId",
                table: "Locations",
                columns: new[] { "LocationName", "LocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CategoryName_ProjectId",
                table: "Categories",
                columns: new[] { "CategoryName", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountName_CategoryId",
                table: "Accounts",
                columns: new[] { "AccountName", "CategoryId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tags_TagName_TagId",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Projects_ProjectName_ProjectId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Locations_LocationName_LocationId",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Categories_CategoryName_ProjectId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_AccountName_CategoryId",
                table: "Accounts");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_TagName",
                table: "Tags",
                column: "TagName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectName",
                table: "Projects",
                column: "ProjectName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Locations_LocationName",
                table: "Locations",
                column: "LocationName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CategoryName",
                table: "Categories",
                column: "CategoryName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountName",
                table: "Accounts",
                column: "AccountName",
                unique: true);
        }
    }
}
