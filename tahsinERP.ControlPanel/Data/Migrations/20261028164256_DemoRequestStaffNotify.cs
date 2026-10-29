using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tahsinERP.ControlPanel.Data.Migrations
{
    /// <inheritdoc />
    public partial class DemoRequestStaffNotify : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StaffNotifiedAtUtc",
                table: "DemoRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StaffNotifyAttempts",
                table: "DemoRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StaffNotifyError",
                table: "DemoRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StaffNotifyLastAtUtc",
                table: "DemoRequests",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StaffNotifiedAtUtc",
                table: "DemoRequests");

            migrationBuilder.DropColumn(
                name: "StaffNotifyAttempts",
                table: "DemoRequests");

            migrationBuilder.DropColumn(
                name: "StaffNotifyError",
                table: "DemoRequests");

            migrationBuilder.DropColumn(
                name: "StaffNotifyLastAtUtc",
                table: "DemoRequests");
        }
    }
}
