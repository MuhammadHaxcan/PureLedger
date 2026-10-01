using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PureLedger.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectImg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProjectImage",
                table: "ProjectCartageSettings");

            migrationBuilder.CreateTable(
                name: "ProjectImg",
                columns: table => new
                {
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    ImageDataUrl = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectImg", x => x.ProjectId);
                    table.ForeignKey(
                        name: "FK_ProjectImg_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectImg");

            migrationBuilder.AddColumn<string>(
                name: "ProjectImage",
                table: "ProjectCartageSettings",
                type: "text",
                nullable: true);
        }
    }
}
