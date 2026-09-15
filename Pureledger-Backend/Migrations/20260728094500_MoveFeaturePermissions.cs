using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PureLedger.Data;

#nullable disable

namespace PureLedger.Migrations;

[DbContext(typeof(LedgerDbContext))]
[Migration("20260728094500_MoveFeaturePermissions")]
public sealed class MoveFeaturePermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO "UserControllerAccess" ("UserId", "Controller", "Action", "HasAccess")
            SELECT legacy."UserId", 'Home', 'PendingApprovals', bool_or(legacy."HasAccess")
            FROM "UserControllerAccess" legacy
            WHERE legacy."Controller" = 'Approval'
              AND NOT EXISTS (
                  SELECT 1
                  FROM "UserControllerAccess" current
                  WHERE current."UserId" = legacy."UserId"
                    AND current."Controller" = 'Home'
                    AND current."Action" = 'PendingApprovals'
              )
            GROUP BY legacy."UserId";

            DELETE FROM "UserControllerAccess"
            WHERE "Controller" = 'Approval';

            INSERT INTO "UserControllerAccess" ("UserId", "Controller", "Action", "HasAccess")
            SELECT legacy."UserId", 'DeliveryChallan', 'StockTransfers', bool_or(legacy."HasAccess")
            FROM "UserControllerAccess" legacy
            WHERE legacy."Controller" = 'StockTransfer'
              AND NOT EXISTS (
                  SELECT 1
                  FROM "UserControllerAccess" current
                  WHERE current."UserId" = legacy."UserId"
                    AND current."Controller" = 'DeliveryChallan'
                    AND current."Action" = 'StockTransfers'
              )
            GROUP BY legacy."UserId";

            DELETE FROM "UserControllerAccess"
            WHERE "Controller" = 'StockTransfer';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "UserControllerAccess"
            SET "Controller" = 'Approval'
            WHERE "Controller" = 'Home' AND "Action" = 'PendingApprovals';

            UPDATE "UserControllerAccess"
            SET "Controller" = 'StockTransfer', "Action" = 'Transfers'
            WHERE "Controller" = 'DeliveryChallan' AND "Action" = 'StockTransfers';
            """);
    }
}
