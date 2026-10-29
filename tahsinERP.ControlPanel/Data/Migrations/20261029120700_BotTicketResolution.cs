using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tahsinERP.ControlPanel.Data.Migrations
{
    /// <inheritdoc />
    public partial class BotTicketResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Resolution",
                table: "BotThreads",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResolvedByStaffId",
                table: "BotThreads",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BotThreads_ResolvedByStaffId",
                table: "BotThreads",
                column: "ResolvedByStaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_BotThreads_StaffUsers_ResolvedByStaffId",
                table: "BotThreads",
                column: "ResolvedByStaffId",
                principalTable: "StaffUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BotThreads_StaffUsers_ResolvedByStaffId",
                table: "BotThreads");

            migrationBuilder.DropIndex(
                name: "IX_BotThreads_ResolvedByStaffId",
                table: "BotThreads");

            migrationBuilder.DropColumn(
                name: "Resolution",
                table: "BotThreads");

            migrationBuilder.DropColumn(
                name: "ResolvedByStaffId",
                table: "BotThreads");
        }
    }
}
