using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tahsinERP.ControlPanel.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOnServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAddOn",
                table: "Plans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsAddOn",
                value: false);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 2,
                column: "IsAddOn",
                value: false);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 3,
                column: "IsAddOn",
                value: false);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 4,
                column: "IsAddOn",
                value: false);

            migrationBuilder.InsertData(
                table: "Plans",
                columns: new[] { "Id", "Code", "IsActive", "IsAddOn", "MaxUsers", "Modules", "Name", "PriceMonthTiyin", "PriceYearTiyin", "SortOrder", "TrialDays", "VatRatePercent" },
                values: new object[] { 5, "SITE", true, true, 0, "Website", "Sayt-katalog", null, null, 10, null, 12m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DropColumn(
                name: "IsAddOn",
                table: "Plans");
        }
    }
}
