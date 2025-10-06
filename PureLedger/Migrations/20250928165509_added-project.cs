using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PureLedger.Migrations
{
    /// <inheritdoc />
    public partial class addedproject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First, add the ProjectId column as nullable
            migrationBuilder.AddColumn<int>(
                name: "ProjectId",
                table: "MissedEntries",
                type: "int",
                nullable: true); // Make it nullable first

            // Update existing records to use a default project (assuming project ID 1 exists)
            migrationBuilder.Sql(@"
                UPDATE me
                SET ProjectId = (
                    SELECT TOP 1 jv.ProjectId 
                    FROM JournalVouchers jv 
                    INNER JOIN JournalEntries je ON je.JournalVoucherId = jv.JournalVoucherId
                    WHERE je.AccountId = me.CustomerAccountId
                    ORDER BY jv.EntryDate DESC
                )
                FROM MissedEntries me
                WHERE me.ProjectId IS NULL
            ");

            // Set any remaining null ProjectIds to the first project
            migrationBuilder.Sql(@"
                UPDATE me
                SET ProjectId = (SELECT TOP 1 ProjectId FROM Projects ORDER BY ProjectId)
                FROM MissedEntries me
                WHERE me.ProjectId IS NULL
            ");

            // Now make ProjectId non-nullable
            migrationBuilder.AlterColumn<int>(
                name: "ProjectId",
                table: "MissedEntries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Drop existing foreign keys
            migrationBuilder.DropForeignKey(
                name: "FK_MissedEntries_Accounts_CustomerAccountId",
                table: "MissedEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_MissedEntries_Accounts_ProductAccountId",
                table: "MissedEntries");

            // Create index for ProjectId
            migrationBuilder.CreateIndex(
                name: "IX_MissedEntries_ProjectId",
                table: "MissedEntries",
                column: "ProjectId");

            // Add foreign keys with proper delete behavior
            migrationBuilder.AddForeignKey(
                name: "FK_MissedEntries_Accounts_CustomerAccountId",
                table: "MissedEntries",
                column: "CustomerAccountId",
                principalTable: "Accounts",
                principalColumn: "AccountId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MissedEntries_Accounts_ProductAccountId",
                table: "MissedEntries",
                column: "ProductAccountId",
                principalTable: "Accounts",
                principalColumn: "AccountId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MissedEntries_Projects_ProjectId",
                table: "MissedEntries",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "ProjectId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MissedEntries_Accounts_CustomerAccountId",
                table: "MissedEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_MissedEntries_Accounts_ProductAccountId",
                table: "MissedEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_MissedEntries_Projects_ProjectId",
                table: "MissedEntries");

            migrationBuilder.DropIndex(
                name: "IX_MissedEntries_ProjectId",
                table: "MissedEntries");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "MissedEntries");

            migrationBuilder.AddForeignKey(
                name: "FK_MissedEntries_Accounts_CustomerAccountId",
                table: "MissedEntries",
                column: "CustomerAccountId",
                principalTable: "Accounts",
                principalColumn: "AccountId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MissedEntries_Accounts_ProductAccountId",
                table: "MissedEntries",
                column: "ProductAccountId",
                principalTable: "Accounts",
                principalColumn: "AccountId");
        }
    }
}
