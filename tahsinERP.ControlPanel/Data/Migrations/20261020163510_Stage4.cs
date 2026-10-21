using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tahsinERP.ControlPanel.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OfflineGraceDays",
                table: "Tenants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OnlineCheck",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "OtpChannel",
                table: "PortalUsers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DidoxCheckedAtUtc",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DidoxError",
                table: "Invoices",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DidoxSentAtUtc",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DidoxStatus",
                table: "Invoices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BankImports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Format = table.Column<int>(type: "int", nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Account = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DateFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    DateTo = table.Column<DateOnly>(type: "date", nullable: true),
                    UploadedByStaffId = table.Column<int>(type: "int", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankImports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankImportLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportId = table.Column<int>(type: "int", nullable: false),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    DocNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DocDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AmountTiyin = table.Column<long>(type: "bigint", nullable: false),
                    PayerName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PayerInn = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: true),
                    PayerAccount = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DedupKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AppliedKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    MatchStatus = table.Column<int>(type: "int", nullable: false),
                    MatchMethod = table.Column<int>(type: "int", nullable: false),
                    InvoiceId = table.Column<int>(type: "int", nullable: true),
                    MatchNote = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    PaymentId = table.Column<int>(type: "int", nullable: true),
                    AppliedByStaffId = table.Column<int>(type: "int", nullable: true),
                    AppliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankImportLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankImportLines_BankImports_ImportId",
                        column: x => x.ImportId,
                        principalTable: "BankImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankImportLines_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankImportLines_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.InsertData(
                table: "Plans",
                columns: new[] { "Id", "Code", "IsActive", "IsAddOn", "MaxUsers", "Modules", "Name", "PriceMonthTiyin", "PriceYearTiyin", "SortOrder", "TrialDays", "VatRatePercent" },
                values: new object[] { 6, "DIST", true, false, 20, "Srm,Crm,Tasks,Finance,Distribution", "Distributsiya", null, null, 5, null, 12m });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DidoxId",
                table: "Invoices",
                column: "DidoxId",
                filter: "[DidoxId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BankImportLines_AppliedKey",
                table: "BankImportLines",
                column: "AppliedKey",
                unique: true,
                filter: "[AppliedKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BankImportLines_DedupKey",
                table: "BankImportLines",
                column: "DedupKey");

            migrationBuilder.CreateIndex(
                name: "IX_BankImportLines_ImportId_LineNo",
                table: "BankImportLines",
                columns: new[] { "ImportId", "LineNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankImportLines_InvoiceId",
                table: "BankImportLines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_BankImportLines_PaymentId",
                table: "BankImportLines",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_BankImports_UploadedAtUtc",
                table: "BankImports",
                column: "UploadedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankImportLines");

            migrationBuilder.DropTable(
                name: "BankImports");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_DidoxId",
                table: "Invoices");

            migrationBuilder.DeleteData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DropColumn(
                name: "OfflineGraceDays",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "OnlineCheck",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "OtpChannel",
                table: "PortalUsers");

            migrationBuilder.DropColumn(
                name: "DidoxCheckedAtUtc",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DidoxError",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DidoxSentAtUtc",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DidoxStatus",
                table: "Invoices");

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 3,
                column: "Modules",
                value: "Srm,Crm,Tasks,Mes,Engineering,Finance,Hr,Logistics,Docs");

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 4,
                column: "Modules",
                value: "Srm,Crm,Tasks,Mes,Engineering,Finance,Hr,Logistics,Docs");
        }
    }
}
