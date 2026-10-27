using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace tahsinERP.ControlPanel.Data.Migrations
{
    /// <inheritdoc />
    public partial class Economics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Competitors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Company = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Tariff = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PriceMonthTiyin = table.Column<long>(type: "bigint", nullable: true),
                    PriceMonthUsd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Users = table.Column<int>(type: "int", nullable: true),
                    MatchPlanCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Include = table.Column<bool>(type: "bit", nullable: false),
                    Features = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Competitors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EconomicsSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    UsdRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PayrollTaxPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ServerPriceTiyin = table.Column<long>(type: "bigint", nullable: false),
                    FirmsPerServer = table.Column<int>(type: "int", nullable: false),
                    MinServers = table.Column<int>(type: "int", nullable: false),
                    PerFirmTiyin = table.Column<long>(type: "bigint", nullable: false),
                    PlatformTiyin = table.Column<long>(type: "bigint", nullable: false),
                    SupportSalaryTiyin = table.Column<long>(type: "bigint", nullable: false),
                    FirmsPerSupport = table.Column<int>(type: "int", nullable: false),
                    MinSupport = table.Column<int>(type: "int", nullable: false),
                    TurnoverTaxPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    BankFeePercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ProfitTaxPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EconomicsSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    AmountOriginal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AmountTiyin = table.Column<long>(type: "bigint", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RecordedByStaffId = table.Column<int>(type: "int", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModuleWeights",
                columns: table => new
                {
                    Module = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Weight = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleWeights", x => x.Module);
                });

            migrationBuilder.CreateTable(
                name: "PlanEconomics",
                columns: table => new
                {
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    PlannedTenants = table.Column<int>(type: "int", nullable: false),
                    MarkupPercent = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    TargetGrossMarginPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanEconomics", x => x.PlanId);
                    table.ForeignKey(
                        name: "FK_PlanEconomics_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlannedCosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    AmountTiyin = table.Column<long>(type: "bigint", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannedCosts", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Competitors",
                columns: new[] { "Id", "Company", "Features", "Include", "MatchPlanCode", "Note", "PriceMonthTiyin", "PriceMonthUsd", "SortOrder", "Tariff", "Users" },
                values: new object[,]
                {
                    { 1, "Bito ERP", "Warehouse,Sales,~Finance,~Bot,Pos", true, "START", "bito.uz/tarif (2026-10): +1 user 90 000 so'm, 3 filial, 14 kun bepul", 29000000L, null, 1, "Start", 3 },
                    { 2, "Bito ERP", "Warehouse,Sales,Finance,~Production,Distribution,Bot,Pos", true, "DIST", "bito.uz/tarif: distributsiya, mijoz boti, cash-flow, oddiy ishlab chiqarish", 49000000L, null, 2, "Business", 5 },
                    { 3, "Bito ERP", "Warehouse,Sales,Finance,Production,Distribution,Bot,Pos", true, "BIZNES", "bito.uz/tarif: pro ishlab chiqarish, shaxsiy menejer", 99000000L, null, 3, "Pro", 10 },
                    { 4, "Linko", "Warehouse,Sales,~Finance,Pos", true, "START", "linko.uz/pos: do'kon/POS, foydalanuvchi soni ko'rsatilmagan", 15000000L, null, 4, "Start", null },
                    { 5, "Linko", "Warehouse,Sales,Finance,~Hr,Pos", true, "START", "linko.uz/pos: 58/73 funksiya, aksiya 150 000", 25000000L, null, 5, "Standard", null },
                    { 6, "Linko", "Warehouse,Sales,Finance,~Hr,Pos", true, "START", "linko.uz/pos: barcha 73 funksiya", 50000000L, null, 6, "Pro", null },
                    { 7, "Hippo", "Warehouse,Sales,Pos", true, "START", "hippo.uz/pos: 1 ombor, CRM + kassa", 24720000L, null, 7, "Ombor Base", null },
                    { 8, "Hippo", "Warehouse,Sales,Pos", true, "START", "hippo.uz/pos: 3 ombor", 41200000L, null, 8, "Ombor Medium", null },
                    { 9, "Hippo", "Warehouse,Sales,Pos", true, "START", "hippo.uz/pos: 7 ombor. Hippo ERP narxi — so'rov bo'yicha", 61800000L, null, 9, "Ombor Pro", null },
                    { 10, "MoySklad", "Warehouse,Sales,~Finance,Pos", true, "START", "moysklad.uz (2026-05): 2 yur. shaxs, 0.5 GB", 15680000L, null, 10, "Bazaviy", null },
                    { 11, "MoySklad", "Warehouse,Sales,Finance,~Production,Pos", true, "BIZNES", "moysklad.uz: 10 yur. shaxs, oddiy ishlab chiqarish", 45920000L, null, 11, "Prof", null },
                    { 12, "Smartup", "Warehouse,Sales,Finance,Distribution,Bot", false, "DIST", "smartup24.com: narx ochiq emas", null, null, 12, "So'rov bo'yicha", null },
                    { 13, "Sales Doctor", "Warehouse,Sales,~Finance,Distribution,Bot", true, "DIST", "CEO LinkedIn: $15/oy (1 agent uchunligini aniqlashtiring)", null, 15m, 13, "Obuna", 1 },
                    { 14, "1C", "Warehouse,Sales,Finance,~Production,~Hr,~Pos", true, "START", "4 814 000 so'm bir martalik ÷ 36 oy (1cba.uz); joriy qilish alohida", 13372200L, null, 14, "UNF, 1 o'rin", 1 },
                    { 15, "1C", "Warehouse,Sales,Finance,~Production,~Hr,~Pos", true, "BIZNES", "8 855 000 so'm ÷ 36 oy (1cba.uz)", 24597200L, null, 15, "UNF, 5 user", 5 },
                    { 16, "1C", "Warehouse,Sales,~Finance,~Distribution,~Pos", true, "DIST", "6 018 000 so'm ÷ 36 oy (1cba.uz)", 16716700L, null, 16, "Savdoni boshqarish, 1 o'rin", 1 },
                    { 17, "Odoo", "Warehouse,Sales,Finance,Production,Hr,~Distribution,Pos", true, "KORP", "1-yil aksiya $7.25; hosting kiritilgan, joriy qilish hamkor orqali", null, 8.95m, 17, "Standard (Online)", 1 },
                    { 18, "Odoo", "Warehouse,Sales,Finance,Production,Hr,~Distribution,Pos", true, "KORP", "1-yil aksiya $10.90; Studio, API, o'z serveri", null, 13.60m, 18, "Custom", 1 },
                    { 19, "SAP Business One", "Warehouse,Sales,Finance", false, "START", "Hamkorlar: ~$38/user (≤5 user); xalqaro mo'ljal", null, 38m, 19, "Starter", 1 },
                    { 20, "SAP Business One", "Warehouse,Sales,Finance,Production,~Hr", false, "KORP", "Hamkorlar: $95–185/user, joriy qilish $15 000+", null, 95m, 20, "Professional", 1 }
                });

            migrationBuilder.InsertData(
                table: "EconomicsSettings",
                columns: new[] { "Id", "BankFeePercent", "FirmsPerServer", "FirmsPerSupport", "MinServers", "MinSupport", "PayrollTaxPercent", "PerFirmTiyin", "PlatformTiyin", "ProfitTaxPercent", "ServerPriceTiyin", "SupportSalaryTiyin", "TurnoverTaxPercent", "UpdatedAtUtc", "UsdRate" },
                values: new object[] { 1, 1m, 8, 15, 1, 1, 12m, 1270000L, 2540000L, 15m, 50800000L, 635000000L, 0m, null, 12700m });

            migrationBuilder.InsertData(
                table: "ModuleWeights",
                columns: new[] { "Module", "Weight" },
                values: new object[,]
                {
                    { "Core", 1 },
                    { "Crm", 2 },
                    { "Distribution", 3 },
                    { "Docs", 1 },
                    { "Engineering", 2 },
                    { "Finance", 2 },
                    { "Hr", 2 },
                    { "Logistics", 2 },
                    { "Mes", 3 },
                    { "Srm", 2 },
                    { "Tasks", 1 }
                });

            migrationBuilder.InsertData(
                table: "PlanEconomics",
                columns: new[] { "PlanId", "MarkupPercent", "PlannedTenants", "TargetGrossMarginPercent" },
                values: new object[,]
                {
                    { 1, 40m, 10, 45m },
                    { 2, 40m, 3, 60m },
                    { 3, 40m, 2, 60m },
                    { 6, 40m, 5, 60m }
                });

            migrationBuilder.InsertData(
                table: "PlannedCosts",
                columns: new[] { "Id", "AmountTiyin", "Category", "Count", "Kind", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, 0L, 5, 1, 1, "Asoschi / rahbar", 1 },
                    { 2, 0L, 3, 1, 1, "Dasturchi (senior)", 2 },
                    { 3, 0L, 3, 1, 1, "Dasturchi (junior)", 3 },
                    { 4, 635000000L, 4, 1, 1, "Sotuv menejeri", 4 },
                    { 5, 127000000L, 3, 1, 2, "AI vositalar (Claude va b.)", 10 },
                    { 6, 381000000L, 4, 1, 2, "Marketing (reklama, ko'rgazma)", 11 },
                    { 7, 38100000L, 5, 1, 2, "Internet, telefon, aloqa", 12 },
                    { 8, 127000000L, 5, 1, 2, "Buxgalter / yuridik xizmat", 13 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_Date",
                table: "Expenses",
                column: "Date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Competitors");

            migrationBuilder.DropTable(
                name: "EconomicsSettings");

            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "ModuleWeights");

            migrationBuilder.DropTable(
                name: "PlanEconomics");

            migrationBuilder.DropTable(
                name: "PlannedCosts");
        }
    }
}
