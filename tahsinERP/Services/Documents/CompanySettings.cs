using System;
using System.Data.Entity;
using System.Linq;
using System.Runtime.Caching;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services.Documents
{
    /// <summary>
    /// Hujjat shablonlari uchun kompaniya sozlamalari. Direktor ismi COMPANIES.DirectorName ustunidan
    /// (Database_Migrations/Alter_COMPANIES_AddDirectorName.sql) — avval kodda qattiq yozilgan edi (DOC-B11).
    /// Ustun EDMX'da yo'q, shuning uchun raw SQL; o'zgartirilsa 10 daqiqada yangilanadi yoki <see cref="Invalidate"/>.
    /// </summary>
    public static class CompanySettings
    {
        /// <summary>DirectorName ustuni hali qo'shilmagan bazalar uchun zaxira qiymat.</summary>
        private const string LegacyDirectorName = "X.M. Maxmudov";
        private const string DirectorKey = "DirectorName";

        private static readonly MemoryCache Cache = new MemoryCache("tahsinERP.CompanySettings");
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

        // Ustun bo'lmasa xato bermasligi uchun dinamik SQL
        private const string DirectorSql =
            "IF COL_LENGTH('dbo.COMPANIES', 'DirectorName') IS NULL " +
            "    SELECT CAST(NULL AS NVARCHAR(200)) AS DirectorName, CAST(0 AS BIT) AS HasColumn " +
            "ELSE " +
            "    EXEC(N'SELECT TOP 1 DirectorName, CAST(1 AS BIT) AS HasColumn FROM dbo.COMPANIES " +
            "          WHERE IsActive = 1 ORDER BY CASE WHEN DirectorName IS NULL THEN 1 ELSE 0 END, ID')";

        private sealed class DirectorRow
        {
            public string DirectorName { get; set; }
            public bool HasColumn { get; set; }
        }

        public static async Task<string> DirectorNameAsync(DBTHSNEntities db)
        {
            var cached = Cache.Get(DirectorKey) as string;
            if (cached != null)
                return cached;

            var row = await db.Database.SqlQuery<DirectorRow>(DirectorSql).FirstOrDefaultAsync();
            string name = row == null || !row.HasColumn
                ? LegacyDirectorName
                : (row.DirectorName ?? string.Empty);

            Cache.Set(DirectorKey, name, DateTimeOffset.Now.Add(Ttl));
            return name;
        }

        public static void Invalidate()
        {
            Cache.Remove(DirectorKey);
        }
    }
}
