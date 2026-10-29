using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tahsinERP.ControlPanel.Data.Migrations
{
    /// <summary>
    /// Raqobatchi bir nechta paketga mos kelishi mumkin: MatchPlanCode → MatchPlanCodes ("DIST,SAVDO"). Ustun qayta nomlanadi
    /// (qo'lda kiritilgan qiymatlar saqlanadi); SAVDO faqat boshlang'ich qiymati o'zgarmagan seed qatorlariga qo'shiladi.
    /// </summary>
    public partial class CompetitorMultiPackage : Migration
    {
        // Seed Id → boshlang'ich paket: savdo/ombor/moliya tariflari SAVDO paketiga ham mos.
        private static readonly (int Id, string Was)[] Savdo =
            [(2, "DIST"), (6, "START"), (8, "START"), (11, "BIZNES"), (12, "DIST"), (16, "DIST")];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(name: "MatchPlanCode", table: "Competitors", newName: "MatchPlanCodes");
            migrationBuilder.AlterColumn<string>(
                name: "MatchPlanCodes", table: "Competitors", type: "nvarchar(200)", maxLength: 200, nullable: true,
                oldClrType: typeof(string), oldType: "nvarchar(20)", oldMaxLength: 20, oldNullable: true);
            foreach (var (id, was) in Savdo)
                migrationBuilder.Sql($"UPDATE Competitors SET MatchPlanCodes = MatchPlanCodes + N',SAVDO' WHERE Id = {id} AND MatchPlanCodes = N'{was}';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Competitors SET MatchPlanCodes = LEFT(MatchPlanCodes, CHARINDEX(N',', MatchPlanCodes) - 1) WHERE CHARINDEX(N',', MatchPlanCodes) > 0;");
            migrationBuilder.AlterColumn<string>(
                name: "MatchPlanCodes", table: "Competitors", type: "nvarchar(20)", maxLength: 20, nullable: true,
                oldClrType: typeof(string), oldType: "nvarchar(200)", oldMaxLength: 200, oldNullable: true);
            migrationBuilder.RenameColumn(name: "MatchPlanCodes", table: "Competitors", newName: "MatchPlanCode");
        }
    }
}
