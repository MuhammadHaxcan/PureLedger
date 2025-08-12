using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PureLedger.Migrations
{
    /// <inheritdoc />
    public partial class removecartagevoucherid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First, create the new JournalVoucherReference table
            migrationBuilder.CreateTable(
                name: "JournalVoucherReferences",
                columns: table => new
                {
                    JournalVoucherReferenceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MainVoucherId = table.Column<int>(type: "int", nullable: false),
                    ReferencedVoucherId = table.Column<int>(type: "int", nullable: false),
                    ReferenceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalVoucherReferences", x => x.JournalVoucherReferenceId);
                    table.ForeignKey(
                        name: "FK_JournalVoucherReferences_JournalVouchers_MainVoucherId",
                        column: x => x.MainVoucherId,
                        principalTable: "JournalVouchers",
                        principalColumn: "JournalVoucherId");
                    table.ForeignKey(
                        name: "FK_JournalVoucherReferences_JournalVouchers_ReferencedVoucherId",
                        column: x => x.ReferencedVoucherId,
                        principalTable: "JournalVouchers",
                        principalColumn: "JournalVoucherId");
                });

            // Create indexes for the foreign keys
            migrationBuilder.CreateIndex(
                name: "IX_JournalVoucherReferences_MainVoucherId",
                table: "JournalVoucherReferences",
                column: "MainVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalVoucherReferences_ReferencedVoucherId",
                table: "JournalVoucherReferences",
                column: "ReferencedVoucherId");

            // Then, add SQL to migrate existing CartageVoucherId references
            migrationBuilder.Sql(@"
                INSERT INTO JournalVoucherReferences (MainVoucherId, ReferencedVoucherId, ReferenceType)
                SELECT JournalVoucherId, CartageVoucherId, 'Cartage'
                FROM JournalVouchers
                WHERE CartageVoucherId IS NOT NULL");

            // Finally, drop the CartageVoucherId column
            migrationBuilder.DropForeignKey(    
                name: "FK_JournalVouchers_JournalVouchers_CartageVoucherId",
                table: "JournalVouchers");

            migrationBuilder.DropIndex(
                name: "IX_JournalVouchers_CartageVoucherId",
                table: "JournalVouchers");

            migrationBuilder.DropColumn(
                name: "CartageVoucherId",
                table: "JournalVouchers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalVoucherReferences");

            migrationBuilder.AddColumn<int>(
                name: "CartageVoucherId",
                table: "JournalVouchers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalVouchers_CartageVoucherId",
                table: "JournalVouchers",
                column: "CartageVoucherId");

            migrationBuilder.AddForeignKey(
                name: "FK_JournalVouchers_JournalVouchers_CartageVoucherId",
                table: "JournalVouchers",
                column: "CartageVoucherId",
                principalTable: "JournalVouchers",
                principalColumn: "JournalVoucherId");
        }
    }
}
