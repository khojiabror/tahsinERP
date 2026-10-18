using System;
using System.Data.Entity;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services.Common
{
    /// <summary>
    /// Hujjat raqamini (PO-2026-10-0001, INV_0012, ...) takrorlanmaydigan qilib beradi.
    ///
    /// Avvalgi 8+ ta GenerateXNo nusxasidagi muammolar:
    ///  - bir vaqtda ikki foydalanuvchi bir xil raqam olardi (qulf yo'q);
    ///  - o'chirilgan/bekor qilingan hujjat raqami qayta berilardi (IsDeleted filtri);
    ///  - matn bo'yicha saralash ("M_9" &gt; "M_10") noto'g'ri "oxirgi"ni topardi.
    ///
    /// Bu servis: sp_getapplock bilan (tranzaksiya oxirigacha) qulflaydi, prefiksdan keyingi
    /// sonni SQL'da raqam sifatida MAX qiladi va o'chirilganlarni ham hisobga oladi.
    ///
    /// Foydalanish (yozuv qo'shiladigan tranzaksiya ichida):
    /// <code>
    /// using (var tx = db.Database.BeginTransaction())
    /// {
    ///     order.OrderNo = await DocumentNumberService.NextAsync(db, "P_ORDERS", "OrderNo", $"PO-{y}-{m:D2}-", 4);
    ///     db.P_ORDERS.Add(order);
    ///     await db.SaveChangesAsync();
    ///     tx.Commit();
    /// }
    /// </code>
    /// </summary>
    public static class DocumentNumberService
    {
        private static readonly Regex Identifier = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        public static string Next(DBTHSNEntities db, string table, string column, string prefix, int pad = 0)
        {
            AcquireLock(db, table + "." + column + ":" + prefix);
            long? max = db.Database.SqlQuery<long?>(BuildMaxSql(table, column), prefix.Length + 1, EscapeLike(prefix) + "%")
                .FirstOrDefault();
            return Format(prefix, max, pad);
        }

        public static async Task<string> NextAsync(DBTHSNEntities db, string table, string column, string prefix, int pad = 0)
        {
            await AcquireLockAsync(db, table + "." + column + ":" + prefix);
            long? max = await db.Database.SqlQuery<long?>(BuildMaxSql(table, column), prefix.Length + 1, EscapeLike(prefix) + "%")
                .FirstOrDefaultAsync();
            return Format(prefix, max, pad);
        }

        /// <summary>
        /// Tranzaksiya tugaguncha amal qiladigan eksklyuziv qulf (raqam hisoblash + INSERT atomik bo'lishi uchun).
        /// </summary>
        public static void AcquireLock(DBTHSNEntities db, string resource)
        {
            EnsureTransaction(db);
            db.Database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction, LockSql, "docno:" + resource);
        }

        public static Task AcquireLockAsync(DBTHSNEntities db, string resource)
        {
            EnsureTransaction(db);
            return db.Database.ExecuteSqlCommandAsync(TransactionalBehavior.DoNotEnsureTransaction, LockSql, "docno:" + resource);
        }

        // --- O'chirilgan hujjat raqamini arxivlash (SRM-D4) ---
        // Soft-delete qilingan qator raqamni UNIQUE indeksda band qilib turadi. Foydalanuvchi shu raqamni
        // qo'lda qayta kiritsa, eski qatorning raqamiga "_deleted" (sig'masa "_dl") qo'shiladi.

        private const string ArchivedSuffix = "_deleted";
        private const string ArchivedSuffixShort = "_dl";

        /// <summary>Raqamga arxiv qo'shimchasini qo'shadi (maxLength'dan oshmaydi; allaqachon arxivlangan bo'lsa o'zgarmaydi).</summary>
        public static string ArchiveNumber(string number, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(number))
                return number;

            var trimmed = number.Trim();
            if (trimmed.EndsWith(ArchivedSuffix, StringComparison.OrdinalIgnoreCase) ||
                trimmed.EndsWith(ArchivedSuffixShort, StringComparison.OrdinalIgnoreCase))
                return trimmed;

            if (trimmed.Length + ArchivedSuffix.Length <= maxLength)
                return trimmed + ArchivedSuffix;
            if (trimmed.Length + ArchivedSuffixShort.Length <= maxLength)
                return trimmed + ArchivedSuffixShort;
            return trimmed.Substring(0, maxLength - ArchivedSuffixShort.Length) + ArchivedSuffixShort;
        }

        /// <summary>Faol (o'chirilmagan) qatorda shu raqam bormi. Jadvalda ID va IsDeleted ustunlari bo'lishi kerak.</summary>
        public static Task<bool> ActiveNumberExistsAsync(DBTHSNEntities db, string table, string column, string number, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(number))
                return Task.FromResult(false);
            CheckIdentifiers(table, column);
            var sql = "SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM [" + table + "] WHERE [" + column + "] = @p0 " +
                      "AND (IsDeleted IS NULL OR IsDeleted = 0) AND (@p1 IS NULL OR ID <> @p1)) THEN 1 ELSE 0 END AS bit)";
            return db.Database.SqlQuery<bool>(sql, number.Trim(), (object)excludeId ?? DBNull.Value).FirstAsync();
        }

        /// <summary>O'chirilgan qatorlardagi shu raqamni arxivlaydi (bitta UPDATE) — yangi yozuv uchun UNIQUE bo'shaydi.</summary>
        public static Task<int> ReleaseNumberFromDeletedAsync(DBTHSNEntities db, string table, string column, string number, int maxLength, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(number))
                return Task.FromResult(0);
            CheckIdentifiers(table, column);
            var normalized = number.Trim();
            var sql = "UPDATE [" + table + "] SET [" + column + "] = @p1 WHERE [" + column + "] = @p0 " +
                      "AND IsDeleted = 1 AND (@p2 IS NULL OR ID <> @p2)";
            return db.Database.ExecuteSqlCommandAsync(sql, normalized, ArchiveNumber(normalized, maxLength), (object)excludeId ?? DBNull.Value);
        }

        private static void CheckIdentifiers(string table, string column)
        {
            if (!Identifier.IsMatch(table) || !Identifier.IsMatch(column))
                throw new ArgumentException("Jadval/ustun nomi noto'g'ri.");
        }

        private const string LockSql =
            "DECLARE @r int; " +
            "EXEC @r = sp_getapplock @Resource = @p0, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; " +
            "IF @r < 0 THROW 50001, N'Hujjat raqami uchun qulf olinmadi, qayta urinib ko''ring.', 1;";

        private static void EnsureTransaction(DBTHSNEntities db)
        {
            if (db.Database.CurrentTransaction == null)
                throw new InvalidOperationException("DocumentNumberService tranzaksiya ichida chaqirilishi kerak (db.Database.BeginTransaction()).");
        }

        private static string BuildMaxSql(string table, string column)
        {
            if (!Identifier.IsMatch(table) || !Identifier.IsMatch(column))
                throw new ArgumentException("Jadval/ustun nomi noto'g'ri.");

            // Prefiksdan keyingi qism faqat raqam bo'lsa hisoblanadi; o'chirilganlar ham (raqam qayta berilmaydi)
            return "SELECT MAX(TRY_CAST(SUBSTRING([" + column + "], @p0, 50) AS BIGINT)) " +
                   "FROM [" + table + "] WHERE [" + column + "] LIKE @p1";
        }

        private static string Format(string prefix, long? max, int pad)
        {
            long next = (max ?? 0) + 1;
            return prefix + (pad > 0 ? next.ToString("D" + pad) : next.ToString());
        }

        private static string EscapeLike(string value)
        {
            return value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
        }
    }
}
