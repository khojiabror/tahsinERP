using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tahsinERP.ControlPanel.Data.Migrations
{
    /// <inheritdoc />
    public partial class SiteCustomDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SiteDomains",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Host = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RedirectToDomain = table.Column<bool>(type: "bit", nullable: false),
                    PendingRemoval = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DnsCheckedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DnsOk = table.Column<bool>(type: "bit", nullable: false),
                    DnsResult = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastJobId = table.Column<long>(type: "bigint", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteDomains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteDomains_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SiteDomains_Host",
                table: "SiteDomains",
                column: "Host",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteDomains_TenantId",
                table: "SiteDomains",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteDomains");
        }
    }
}
