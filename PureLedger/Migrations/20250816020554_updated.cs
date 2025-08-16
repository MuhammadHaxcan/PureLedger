using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PureLedger.Migrations
{
    /// <inheritdoc />
    public partial class updated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 1,
                columns: new[] { "CreatedDate", "PasswordHash" },
                values: new object[] { new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "$2a$11$C2k4DHRpLpAB/vpdrTV.DuMIeTbqj0hiQi8zDVH4WoQ76jrgp1Q32" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 1,
                columns: new[] { "CreatedDate", "PasswordHash" },
                values: new object[] { new DateTime(2025, 8, 16, 2, 1, 49, 16, DateTimeKind.Utc).AddTicks(4127), "$2a$11$f.Fn/tltNBg/nciexH/FDuBSYYCrqR8i/Fb2AwJYvD1PZUxpLIzZC" });
        }
    }
}
