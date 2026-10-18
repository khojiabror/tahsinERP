using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tahsinERP.ControlPanel.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApiNonces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiNonces",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstallationId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Nonce = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiNonces", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiNonces_ExpiresUtc",
                table: "ApiNonces",
                column: "ExpiresUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ApiNonces_InstallationId_Nonce",
                table: "ApiNonces",
                columns: new[] { "InstallationId", "Nonce" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiNonces");
        }
    }
}
