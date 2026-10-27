using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tahsinERP.ControlPanel.Data.Migrations
{
    /// <inheritdoc />
    public partial class DemoTenants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FullSampleData",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDemo",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FullSampleData",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IsDemo",
                table: "Tenants");
        }
    }
}
