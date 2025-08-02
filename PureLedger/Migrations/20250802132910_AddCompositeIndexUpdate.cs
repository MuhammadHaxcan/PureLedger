using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PureLedger.Migrations
{
    /// <inheritdoc />
    public partial class AddCompositeIndexUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tags_TagName_TagId",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Locations_LocationName_LocationId",
                table: "Locations");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_TagName",
                table: "Tags",
                column: "TagName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Locations_LocationName",
                table: "Locations",
                column: "LocationName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tags_TagName",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Locations_LocationName",
                table: "Locations");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_TagName_TagId",
                table: "Tags",
                columns: new[] { "TagName", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Locations_LocationName_LocationId",
                table: "Locations",
                columns: new[] { "LocationName", "LocationId" },
                unique: true);
        }
    }
}
