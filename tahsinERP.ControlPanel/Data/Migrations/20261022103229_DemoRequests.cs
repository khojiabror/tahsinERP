using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tahsinERP.ControlPanel.Data.Migrations
{
    /// <inheritdoc />
    public partial class DemoRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DemoRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Company = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Industry = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CompanySize = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Modules = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Plan = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Lang = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Ip = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StaffNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    HandledByStaffId = table.Column<int>(type: "int", nullable: true),
                    HandledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemoRequests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DemoRequests_Phone",
                table: "DemoRequests",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_DemoRequests_Status_Id",
                table: "DemoRequests",
                columns: new[] { "Status", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_DemoRequests_TenantId",
                table: "DemoRequests",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemoRequests");
        }
    }
}
