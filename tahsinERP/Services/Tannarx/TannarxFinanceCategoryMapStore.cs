using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services.Tannarx
{
    /// <summary>
    /// Finance xarajat kategoriyasi → tannarx guruhi (qo'lda belgilash). dbo.TANNARX_FIN_CATEGORY_MAP jadvalida saqlanadi.
    /// Jadval EDMX ga kiritilmagan (umumiy model fayllariga tegmaslik uchun) — xom SQL bilan ishlanadi.
    /// Jadval yo'q bo'lsa, o'qishda bo'sh natija qaytadi (nom qoidalari ishlaydi), saqlashda jadval yaratiladi.
    /// </summary>
    internal class TannarxFinanceCategoryMapStore
    {
        private const string EnsureTableSql = @"
IF OBJECT_ID(N'dbo.TANNARX_FIN_CATEGORY_MAP', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TANNARX_FIN_CATEGORY_MAP (
        CategoryID      INT      NOT NULL CONSTRAINT PK_TANNARX_FIN_CATEGORY_MAP PRIMARY KEY,
        Bucket          TINYINT  NOT NULL,   -- 0 = chiqarilsin, 1 = Material, 2 = Soliq va bank, 3 = Boshqa
        UpdatedAt       DATETIME NOT NULL CONSTRAINT DF_TANNARX_FIN_CATEGORY_MAP_UpdatedAt DEFAULT (GETDATE()),
        UpdatedByUserID INT      NULL,
        CONSTRAINT FK_TANNARX_FIN_CATEGORY_MAP_CATEGORY FOREIGN KEY (CategoryID) REFERENCES dbo.FIN_CATEGORIES (ID),
        CONSTRAINT CK_TANNARX_FIN_CATEGORY_MAP_Bucket CHECK (Bucket IN (0, 1, 2, 3))
    );
END";

        private readonly DBTHSNEntities _db;

        internal TannarxFinanceCategoryMapStore(DBTHSNEntities db)
        {
            _db = db;
        }

        internal async Task<bool> TableExistsAsync()
        {
            var id = await _db.Database
                .SqlQuery<int?>("SELECT OBJECT_ID(N'dbo.TANNARX_FIN_CATEGORY_MAP', N'U')")
                .FirstOrDefaultAsync();
            return id.HasValue;
        }

        /// <summary>CategoryID → guruh (faqat qo'lda belgilanganlar).</summary>
        internal async Task<Dictionary<int, TannarxFinanceBucket>> LoadAsync()
        {
            if (!await TableExistsAsync())
                return new Dictionary<int, TannarxFinanceBucket>();

            var rows = await _db.Database
                .SqlQuery<MapRow>("SELECT CategoryID, CAST(Bucket AS INT) AS Bucket FROM dbo.TANNARX_FIN_CATEGORY_MAP")
                .ToListAsync();
            return rows
                .Where(r => Enum.IsDefined(typeof(TannarxFinanceBucket), r.Bucket) && r.Bucket != (int)TannarxFinanceBucket.OtherAuto)
                .ToDictionary(r => r.CategoryID, r => (TannarxFinanceBucket)r.Bucket);
        }

        /// <summary>
        /// To'liq almashtiradi: map da bo'lmagan kategoriya "Avtomatik" (nom qoidasi) ga qaytadi.
        /// </summary>
        internal async Task SaveAsync(IDictionary<int, TannarxFinanceBucket> map, int? userId)
        {
            using (var tx = _db.Database.BeginTransaction())
            {
                await _db.Database.ExecuteSqlCommandAsync(EnsureTableSql);
                await _db.Database.ExecuteSqlCommandAsync("DELETE FROM dbo.TANNARX_FIN_CATEGORY_MAP");
                foreach (var kv in map)
                {
                    await _db.Database.ExecuteSqlCommandAsync(
                        "INSERT INTO dbo.TANNARX_FIN_CATEGORY_MAP (CategoryID, Bucket, UpdatedAt, UpdatedByUserID) VALUES (@CategoryID, @Bucket, GETDATE(), @UserID)",
                        new SqlParameter("@CategoryID", kv.Key),
                        new SqlParameter("@Bucket", (byte)kv.Value),
                        new SqlParameter("@UserID", (object)userId ?? DBNull.Value));
                }
                tx.Commit();
            }
        }

        private class MapRow
        {
            public int CategoryID { get; set; }
            public int Bucket { get; set; }
        }
    }
}
