using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tahsinERP.ControlPanel.Data.Migrations
{
    /// <inheritdoc />
    public partial class TrialActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SampleData",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialEndedAtUtc",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UsageDailyDocs",
                columns: table => new
                {
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    DocType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageDailyDocs", x => new { x.TenantId, x.Date, x.DocType });
                    table.ForeignKey(
                        name: "FK_UsageDailyDocs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsageDailyModules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Module = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Controller = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ActionType = table.Column<byte>(type: "tinyint", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageDailyModules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsageDailyModules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsageDailyUsers",
                columns: table => new
                {
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Logins = table.Column<int>(type: "int", nullable: false),
                    Actions = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageDailyUsers", x => new { x.TenantId, x.Date, x.UserId });
                    table.ForeignKey(
                        name: "FK_UsageDailyUsers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsageEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Module = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Controller = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ActionType = table.Column<byte>(type: "tinyint", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsageEvents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsageStates",
                columns: table => new
                {
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    EntryCursor = table.Column<long>(type: "bigint", nullable: false),
                    LogCursor = table.Column<long>(type: "bigint", nullable: false),
                    SyncedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastActionUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    More = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageStates", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_UsageStates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsageUsers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Login = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastLoginUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastActionUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsageUsers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UsageDailyDocs_Date",
                table: "UsageDailyDocs",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_UsageDailyModules_Date_Module",
                table: "UsageDailyModules",
                columns: new[] { "Date", "Module" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageDailyModules_TenantId_Date_Module_Controller_ActionType",
                table: "UsageDailyModules",
                columns: new[] { "TenantId", "Date", "Module", "Controller", "ActionType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageDailyUsers_Date",
                table: "UsageDailyUsers",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_AtUtc",
                table: "UsageEvents",
                column: "AtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_TenantId_AtUtc",
                table: "UsageEvents",
                columns: new[] { "TenantId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_TenantId_Kind_SourceId_AtUtc",
                table: "UsageEvents",
                columns: new[] { "TenantId", "Kind", "SourceId", "AtUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageUsers_TenantId_UserId",
                table: "UsageUsers",
                columns: new[] { "TenantId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UsageDailyDocs");

            migrationBuilder.DropTable(
                name: "UsageDailyModules");

            migrationBuilder.DropTable(
                name: "UsageDailyUsers");

            migrationBuilder.DropTable(
                name: "UsageEvents");

            migrationBuilder.DropTable(
                name: "UsageStates");

            migrationBuilder.DropTable(
                name: "UsageUsers");

            migrationBuilder.DropColumn(
                name: "SampleData",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TrialEndedAtUtc",
                table: "Tenants");
        }
    }
}
