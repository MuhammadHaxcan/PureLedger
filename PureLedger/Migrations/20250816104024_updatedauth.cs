using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PureLedger.Migrations
{
    /// <inheritdoc />
    public partial class updatedauth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Action",
                table: "UserControllerAccess",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Action",
                table: "UserControllerAccess");
        }
    }
}
