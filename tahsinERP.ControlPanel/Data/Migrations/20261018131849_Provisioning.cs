using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tahsinERP.ControlPanel.Data.Migrations
{
    /// <inheritdoc />
    public partial class Provisioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProvisionedAtUtc",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AgentLastSeenUtc",
                table: "Servers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentTokenHash",
                table: "Servers",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AgentTokenIssuedUtc",
                table: "Servers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentVersion",
                table: "Servers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProvisioningJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServerId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LeaseUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExitCode = table.Column<int>(type: "int", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Result = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Log = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecretProtected = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProvisioningJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProvisioningJobs_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProvisioningJobs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Servers_AgentTokenHash",
                table: "Servers",
                column: "AgentTokenHash",
                unique: true,
                filter: "[AgentTokenHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningJobs_ServerId_Status",
                table: "ProvisioningJobs",
                columns: new[] { "ServerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningJobs_TenantId",
                table: "ProvisioningJobs",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProvisioningJobs");

            migrationBuilder.DropIndex(
                name: "IX_Servers_AgentTokenHash",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "ProvisionedAtUtc",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AgentLastSeenUtc",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "AgentTokenHash",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "AgentTokenIssuedUtc",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "AgentVersion",
                table: "Servers");
        }
    }
}
