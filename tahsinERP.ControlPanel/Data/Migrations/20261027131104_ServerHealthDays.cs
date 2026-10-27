using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tahsinERP.ControlPanel.Data.Migrations
{
    /// <inheritdoc />
    public partial class ServerHealthDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServerHealthDays",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServerId = table.Column<int>(type: "int", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Samples = table.Column<int>(type: "int", nullable: false),
                    CpuSamples = table.Column<int>(type: "int", nullable: false),
                    CpuAvgPercent = table.Column<double>(type: "float", nullable: true),
                    CpuMaxPercent = table.Column<double>(type: "float", nullable: true),
                    RamTotalBytes = table.Column<long>(type: "bigint", nullable: false),
                    RamUsedAvgBytes = table.Column<long>(type: "bigint", nullable: false),
                    RamUsedMaxBytes = table.Column<long>(type: "bigint", nullable: false),
                    DisksJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    UptimeSeconds = table.Column<long>(type: "bigint", nullable: false),
                    Reboots = table.Column<int>(type: "int", nullable: false),
                    FirstAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerHealthDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServerHealthDays_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServerHealthDays_ServerId_Day",
                table: "ServerHealthDays",
                columns: new[] { "ServerId", "Day" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServerHealthDays");
        }
    }
}
