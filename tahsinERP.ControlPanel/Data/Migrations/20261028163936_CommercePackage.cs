using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tahsinERP.ControlPanel.Data.Migrations
{
    /// <inheritdoc />
    public partial class CommercePackage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ModuleWeights",
                keyColumn: "Module",
                keyValue: "Distribution",
                column: "Weight",
                value: 1);

            migrationBuilder.InsertData(
                table: "ModuleWeights",
                columns: new[] { "Module", "Weight" },
                values: new object[] { "Commerce", 2 });

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 3,
                column: "Modules",
                value: "Srm,Crm,Tasks,Mes,Engineering,Finance,Hr,Logistics,Docs,Commerce,Distribution");

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 4,
                column: "Modules",
                value: "Srm,Crm,Tasks,Mes,Engineering,Finance,Hr,Logistics,Docs,Commerce,Distribution");

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 6,
                column: "Modules",
                value: "Srm,Crm,Tasks,Finance,Commerce,Distribution");

            migrationBuilder.InsertData(
                table: "Plans",
                columns: new[] { "Id", "Code", "IsActive", "IsAddOn", "MaxUsers", "Modules", "Name", "PriceMonthTiyin", "PriceYearTiyin", "SortOrder", "TrialDays", "VatRatePercent" },
                values: new object[] { 7, "SAVDO", true, false, 20, "Srm,Crm,Tasks,Finance,Commerce", "Savdo", null, null, 6, null, 12m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ModuleWeights",
                keyColumn: "Module",
                keyValue: "Commerce");

            migrationBuilder.DeleteData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.UpdateData(
                table: "ModuleWeights",
                keyColumn: "Module",
                keyValue: "Distribution",
                column: "Weight",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 3,
                column: "Modules",
                value: "Srm,Crm,Tasks,Mes,Engineering,Finance,Hr,Logistics,Docs,Distribution");

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 4,
                column: "Modules",
                value: "Srm,Crm,Tasks,Mes,Engineering,Finance,Hr,Logistics,Docs,Distribution");

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 6,
                column: "Modules",
                value: "Srm,Crm,Tasks,Finance,Distribution");
        }
    }
}
