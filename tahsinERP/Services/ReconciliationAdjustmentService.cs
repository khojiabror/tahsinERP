using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Akt-sverki tuzatishlari — DB jadvali (EDMX dan tashqari, SqlQuery).
    /// Har bir yozuv bitta shartnomaga (ContractID) bog'langan.
    /// </summary>
    public static class ReconciliationAdjustmentService
    {
        public const byte CounterpartyCustomer = 1;
        public const byte CounterpartySupplier = 2;
        public const byte CounterpartyForwarder = 4;

        public static byte TypeFromString(string reconciliationType)
        {
            if (string.Equals(reconciliationType, "Supplier", StringComparison.OrdinalIgnoreCase))
                return CounterpartySupplier;
            if (string.Equals(reconciliationType, "Customer", StringComparison.OrdinalIgnoreCase))
                return CounterpartyCustomer;
            if (string.Equals(reconciliationType, "Forwarder", StringComparison.OrdinalIgnoreCase))
                return CounterpartyForwarder;
            return 0;
        }

        public static string ModuleNameFromType(string reconciliationType)
        {
            if (string.Equals(reconciliationType, "Supplier", StringComparison.OrdinalIgnoreCase))
                return "Supplier";
            if (string.Equals(reconciliationType, "Customer", StringComparison.OrdinalIgnoreCase))
                return "Customer";
            if (string.Equals(reconciliationType, "Forwarder", StringComparison.OrdinalIgnoreCase))
                return "Forwarder";
            return null;
        }

        /// <summary>
        /// Hisobot sanasigacha (CutoffDate &lt;= asOfDate) bo'lgan tuzatishlar — shu shartnoma uchun.
        /// </summary>
        public static List<ReconciliationAdjustmentRow> LoadForReport(DBTHSNEntities db, byte counterpartyType, int counterpartyId, int contractId, DateTime asOfDate)
        {
            const string sql = @"
SELECT ID, CounterpartyType, CounterpartyID, EntryYear, CutoffDate, Party1Debit, Party1Credit, Note
FROM RECONCILIATION_ADJUSTMENTS
WHERE CounterpartyType = {0} AND CounterpartyID = {1} AND ContractID = {2} AND CutoffDate <= {3}
ORDER BY CutoffDate, EntryYear, ID";
            try
            {
                return db.Database.SqlQuery<ReconciliationAdjustmentRow>(sql, counterpartyType, counterpartyId, contractId, asOfDate.Date).ToList();
            }
            catch (SqlException)
            {
                return new List<ReconciliationAdjustmentRow>();
            }
        }

        public static ReconciliationAdjustmentRow LoadForYear(DBTHSNEntities db, byte counterpartyType, int counterpartyId, int contractId, int entryYear)
        {
            const string sql = @"
SELECT TOP 1 ID, CounterpartyType, CounterpartyID, EntryYear, CutoffDate, Party1Debit, Party1Credit, Note
FROM RECONCILIATION_ADJUSTMENTS
WHERE CounterpartyType = {0} AND CounterpartyID = {1} AND ContractID = {2} AND EntryYear = {3}";
            try
            {
                return db.Database.SqlQuery<ReconciliationAdjustmentRow>(sql, counterpartyType, counterpartyId, contractId, entryYear).FirstOrDefault();
            }
            catch (SqlException)
            {
                return null;
            }
        }

        public static void Save(DBTHSNEntities db, byte counterpartyType, int counterpartyId, int contractId, int entryYear, DateTime cutoffDate, decimal party1Debit, decimal party1Credit, string note, int userId)
        {
            if (userId <= 0)
                throw new InvalidOperationException("Foydalanuvchi aniqlanmadi.");

            var existing = LoadForYear(db, counterpartyType, counterpartyId, contractId, entryYear);
            var now = DateTime.UtcNow;
            note = string.IsNullOrWhiteSpace(note) ? null : note.Trim().Substring(0, Math.Min(note.Trim().Length, 500));

            if (existing != null)
            {
                const string upd = @"
UPDATE RECONCILIATION_ADJUSTMENTS
SET CutoffDate = {0}, Party1Debit = {1}, Party1Credit = {2}, Note = {3}, ModifiedDT = {4}
WHERE ID = {5}";
                db.Database.ExecuteSqlCommand(upd, cutoffDate.Date, party1Debit, party1Credit, (object)note ?? DBNull.Value, now, existing.ID);
            }
            else
            {
                const string ins = @"
INSERT INTO RECONCILIATION_ADJUSTMENTS (CounterpartyType, CounterpartyID, ContractID, EntryYear, CutoffDate, Party1Debit, Party1Credit, Note, CreatedDT, CreatedByUserID)
VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9})";
                db.Database.ExecuteSqlCommand(ins, counterpartyType, counterpartyId, contractId, entryYear, cutoffDate.Date, party1Debit, party1Credit, (object)note ?? DBNull.Value, now, userId);
            }
        }
    }

    public class ReconciliationAdjustmentRow
    {
        public int ID { get; set; }
        public byte CounterpartyType { get; set; }
        public int CounterpartyID { get; set; }
        public int EntryYear { get; set; }
        public DateTime CutoffDate { get; set; }
        public decimal Party1Debit { get; set; }
        public decimal Party1Credit { get; set; }
        public string Note { get; set; }
    }
}
