using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PureLedger.Migrations
{
    /// <inheritdoc />
    public partial class init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountGroups",
                columns: table => new
                {
                    AccountGroupId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountGroupName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountGroups", x => x.AccountGroupId);
                });

            migrationBuilder.CreateTable(
                name: "AccountTypes",
                columns: table => new
                {
                    AccountTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountTypeName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTypes", x => x.AccountTypeId);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    LocationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LocationName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Region = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.LocationId);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    ProjectId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.ProjectId);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    TagId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TagName = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.TagId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsAdmin = table.Column<bool>(type: "bit", nullable: false),
                    Subroute = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    CategoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    AccountTypeId = table.Column<int>(type: "int", nullable: false),
                    CategoryName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.CategoryId);
                    table.ForeignKey(
                        name: "FK_Categories_AccountTypes_AccountTypeId",
                        column: x => x.AccountTypeId,
                        principalTable: "AccountTypes",
                        principalColumn: "AccountTypeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Categories_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JournalVouchers",
                columns: table => new
                {
                    JournalVoucherId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreditableDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalVouchers", x => x.JournalVoucherId);
                    table.ForeignKey(
                        name: "FK_JournalVouchers_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserControllerAccess",
                columns: table => new
                {
                    UserControllerAccessId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Controller = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HasAccess = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserControllerAccess", x => x.UserControllerAccessId);
                    table.ForeignKey(
                        name: "FK_UserControllerAccess_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserProjects",
                columns: table => new
                {
                    UserProjectId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProjects", x => x.UserProjectId);
                    table.ForeignKey(
                        name: "FK_UserProjects_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserProjects_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    AccountId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    AccountGroupId = table.Column<int>(type: "int", nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.AccountId);
                    table.ForeignKey(
                        name: "FK_Accounts_AccountGroups_AccountGroupId",
                        column: x => x.AccountGroupId,
                        principalTable: "AccountGroups",
                        principalColumn: "AccountGroupId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Accounts_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryChallanVouchers",
                columns: table => new
                {
                    DeliveryChallanVoucherId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JournalVoucherId = table.Column<int>(type: "int", nullable: false),
                    DCDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FinalDeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryChallanVouchers", x => x.DeliveryChallanVoucherId);
                    table.ForeignKey(
                        name: "FK_DeliveryChallanVouchers_JournalVouchers_JournalVoucherId",
                        column: x => x.JournalVoucherId,
                        principalTable: "JournalVouchers",
                        principalColumn: "JournalVoucherId");
                });

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

            migrationBuilder.CreateTable(
                name: "AccountDatas",
                columns: table => new
                {
                    AccountDataId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Area = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    City = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountDatas", x => x.AccountDataId);
                    table.ForeignKey(
                        name: "FK_AccountDatas_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntries",
                columns: table => new
                {
                    JournalEntryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JournalVoucherId = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    IsProcessed = table.Column<bool>(type: "bit", nullable: false),
                    IsEdited = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Rate = table.Column<int>(type: "int", nullable: false),
                    DiscountPercent = table.Column<int>(type: "int", nullable: true),
                    Discount = table.Column<int>(type: "int", nullable: false),
                    Debit = table.Column<int>(type: "int", nullable: false),
                    Credit = table.Column<int>(type: "int", nullable: false),
                    Rbp = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    ModifiedDateTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntries", x => x.JournalEntryId);
                    table.ForeignKey(
                        name: "FK_JournalEntries_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId");
                    table.ForeignKey(
                        name: "FK_JournalEntries_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "CategoryId");
                    table.ForeignKey(
                        name: "FK_JournalEntries_JournalVouchers_JournalVoucherId",
                        column: x => x.JournalVoucherId,
                        principalTable: "JournalVouchers",
                        principalColumn: "JournalVoucherId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductDatas",
                columns: table => new
                {
                    ProductDataId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    PkgForm = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Pkg = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SaleRate = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductDatas", x => x.ProductDataId);
                    table.ForeignKey(
                        name: "FK_ProductDatas_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryChallanEntries",
                columns: table => new
                {
                    DeliveryChallanEntryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeliveryChallanVoucherId = table.Column<int>(type: "int", nullable: false),
                    JournalEntryId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OtherReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryChallanEntries", x => x.DeliveryChallanEntryId);
                    table.ForeignKey(
                        name: "FK_DeliveryChallanEntries_DeliveryChallanVouchers_DeliveryChallanVoucherId",
                        column: x => x.DeliveryChallanVoucherId,
                        principalTable: "DeliveryChallanVouchers",
                        principalColumn: "DeliveryChallanVoucherId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeliveryChallanEntries_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "JournalEntryId");
                    table.ForeignKey(
                        name: "FK_DeliveryChallanEntries_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "LocationId");
                });

            migrationBuilder.CreateTable(
                name: "JournalEntryTags",
                columns: table => new
                {
                    JournalEntryTagId = table.Column<int>(type: "int", nullable: false),
                    JournalEntryId = table.Column<int>(type: "int", nullable: false),
                    TagId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntryTags", x => new { x.JournalEntryId, x.JournalEntryTagId });
                    table.ForeignKey(
                        name: "FK_JournalEntryTags_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "JournalEntryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JournalEntryTags_Tags_JournalEntryTagId",
                        column: x => x.JournalEntryTagId,
                        principalTable: "Tags",
                        principalColumn: "TagId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AccountGroups",
                columns: new[] { "AccountGroupId", "AccountGroupName" },
                values: new object[,]
                {
                    { 1, "Account" },
                    { 2, "Product" }
                });

            migrationBuilder.InsertData(
                table: "AccountTypes",
                columns: new[] { "AccountTypeId", "AccountTypeName" },
                values: new object[,]
                {
                    { 1, "Assets" },
                    { 2, "Liabilities" },
                    { 3, "Equity" },
                    { 4, "Revenue" },
                    { 5, "Expense" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "CreatedDate", "Email", "FullName", "IsActive", "IsAdmin", "LastLoginDate", "PasswordHash", "Subroute", "Username" },
                values: new object[] { 1, new DateTime(2025, 8, 16, 2, 1, 49, 16, DateTimeKind.Utc).AddTicks(4127), "admin@example.com", "System Administrator", true, true, null, "$2a$11$f.Fn/tltNBg/nciexH/FDuBSYYCrqR8i/Fb2AwJYvD1PZUxpLIzZC", "Default", "admin" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountDatas_AccountId",
                table: "AccountDatas",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountGroupId",
                table: "Accounts",
                column: "AccountGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountName_CategoryId",
                table: "Accounts",
                columns: new[] { "AccountName", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_CategoryId",
                table: "Accounts",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_AccountTypeId",
                table: "Categories",
                column: "AccountTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CategoryName_ProjectId",
                table: "Categories",
                columns: new[] { "CategoryName", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ProjectId",
                table: "Categories",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanEntries_DeliveryChallanVoucherId",
                table: "DeliveryChallanEntries",
                column: "DeliveryChallanVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanEntries_JournalEntryId",
                table: "DeliveryChallanEntries",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanEntries_LocationId",
                table: "DeliveryChallanEntries",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanVouchers_JournalVoucherId",
                table: "DeliveryChallanVouchers",
                column: "JournalVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_AccountId",
                table: "JournalEntries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_CategoryId",
                table: "JournalEntries",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_JournalVoucherId",
                table: "JournalEntries",
                column: "JournalVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryTags_JournalEntryTagId",
                table: "JournalEntryTags",
                column: "JournalEntryTagId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalVoucherReferences_MainVoucherId",
                table: "JournalVoucherReferences",
                column: "MainVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalVoucherReferences_ReferencedVoucherId",
                table: "JournalVoucherReferences",
                column: "ReferencedVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalVouchers_ProjectId",
                table: "JournalVouchers",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_LocationName",
                table: "Locations",
                column: "LocationName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductDatas_AccountId",
                table: "ProductDatas",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectName",
                table: "Projects",
                column: "ProjectName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_TagName",
                table: "Tags",
                column: "TagName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserControllerAccess_UserId",
                table: "UserControllerAccess",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProjects_ProjectId",
                table: "UserProjects",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProjects_UserId",
                table: "UserProjects",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountDatas");

            migrationBuilder.DropTable(
                name: "DeliveryChallanEntries");

            migrationBuilder.DropTable(
                name: "JournalEntryTags");

            migrationBuilder.DropTable(
                name: "JournalVoucherReferences");

            migrationBuilder.DropTable(
                name: "ProductDatas");

            migrationBuilder.DropTable(
                name: "UserControllerAccess");

            migrationBuilder.DropTable(
                name: "UserProjects");

            migrationBuilder.DropTable(
                name: "DeliveryChallanVouchers");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "JournalEntries");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "JournalVouchers");

            migrationBuilder.DropTable(
                name: "AccountGroups");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "AccountTypes");

            migrationBuilder.DropTable(
                name: "Projects");
        }
    }
}
