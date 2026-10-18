using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Akt-sverki tuzatishlari (RECONCILIATION_ADJUSTMENTS, EDMX).
    /// FIN-B20: avval xom SQL + SqlException yutilardi — jadval/ustun xatosi tuzatishlarni jimgina yo'q qilardi.
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
            var day = asOfDate.Date;
            return Query(db, counterpartyType, counterpartyId, contractId)
                .Where(a => a.CutoffDate <= day)
                .OrderBy(a => a.CutoffDate).ThenBy(a => a.EntryYear).ThenBy(a => a.ID)
                .Select(ToRow)
                .ToList();
        }

        /// <summary>Kontragentning barcha shartnomalari bo'yicha tuzatishlar (akt-sverki "Barcha shartnomalar" rejimi).</summary>
        public static List<ReconciliationAdjustmentRow> LoadForCounterparty(DBTHSNEntities db, byte counterpartyType, int counterpartyId, DateTime asOfDate)
        {
            var day = asOfDate.Date;
            return db.RECONCILIATION_ADJUSTMENTS.AsNoTracking()
                .Where(a => a.CounterpartyType == counterpartyType && a.CounterpartyID == counterpartyId && a.CutoffDate <= day)
                .OrderBy(a => a.CutoffDate).ThenBy(a => a.EntryYear).ThenBy(a => a.ID)
                .Select(ToRow)
                .ToList();
        }

        public static ReconciliationAdjustmentRow LoadForYear(DBTHSNEntities db, byte counterpartyType, int counterpartyId, int contractId, int entryYear)
        {
            return Query(db, counterpartyType, counterpartyId, contractId)
                .Where(a => a.EntryYear == entryYear)
                .OrderBy(a => a.ID)
                .Select(ToRow)
                .FirstOrDefault();
        }

        private static IQueryable<RECONCILIATION_ADJUSTMENTS> Query(DBTHSNEntities db, byte counterpartyType, int counterpartyId, int contractId)
        {
            return db.RECONCILIATION_ADJUSTMENTS.AsNoTracking()
                .Where(a => a.CounterpartyType == counterpartyType && a.CounterpartyID == counterpartyId && a.ContractID == contractId);
        }

        private static readonly System.Linq.Expressions.Expression<Func<RECONCILIATION_ADJUSTMENTS, ReconciliationAdjustmentRow>> ToRow =
            a => new ReconciliationAdjustmentRow
            {
                ID = a.ID,
                CounterpartyType = a.CounterpartyType,
                CounterpartyID = a.CounterpartyID,
                EntryYear = a.EntryYear,
                CutoffDate = a.CutoffDate,
                Party1Debit = a.Party1Debit,
                Party1Credit = a.Party1Credit,
                Note = a.Note
            };

        public static void Save(DBTHSNEntities db, byte counterpartyType, int counterpartyId, int contractId, int entryYear, DateTime cutoffDate, decimal party1Debit, decimal party1Credit, string note, int userId)
        {
            if (userId <= 0)
                throw new InvalidOperationException("Foydalanuvchi aniqlanmadi.");

            var existing = LoadForYear(db, counterpartyType, counterpartyId, contractId, entryYear);
            var now = DateTime.UtcNow;
            note = string.IsNullOrWhiteSpace(note) ? null : note.Trim().Substring(0, Math.Min(note.Trim().Length, 500));

            var row = existing != null ? db.RECONCILIATION_ADJUSTMENTS.Find(existing.ID) : null;
            if (row == null)
            {
                row = new RECONCILIATION_ADJUSTMENTS
                {
                    CounterpartyType = counterpartyType,
                    CounterpartyID = counterpartyId,
                    ContractID = contractId,
                    EntryYear = entryYear,
                    CreatedDT = now,
                    CreatedByUserID = userId
                };
                db.RECONCILIATION_ADJUSTMENTS.Add(row);
            }
            else
            {
                row.ModifiedDT = now;
            }
            row.CutoffDate = cutoffDate.Date;
            row.Party1Debit = party1Debit;
            row.Party1Credit = party1Credit;
            row.Note = note;
            db.SaveChanges();
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
