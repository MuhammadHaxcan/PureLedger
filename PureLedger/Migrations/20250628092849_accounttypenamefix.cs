using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PureLedger.Migrations
{
    /// <inheritdoc />
    public partial class accounttypenamefix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Name",
                table: "AccountTypes",
                newName: "AccountTypeName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AccountTypeName",
                table: "AccountTypes",
                newName: "Name");
        }
    }
}
