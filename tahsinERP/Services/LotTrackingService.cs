using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// FIFO/Lot tracking service. DB o'zgarishsiz Income hujjatini "lot" sifatida ishlatadi.
    /// Keyinchalik DLOC (direct location) qo'shilganda bu service kengaytiriladi.
    /// </summary>
    public static class LotTrackingService
    {
        /// <summary>
        /// Berilgan ombordagi mahsulot uchun mavjud "lot"lar (Income hujjatlar) - FIFO tartibida (eng eski birinchi).
        /// </summary>
        public static async Task<List<IncomeLotItem>> GetAvailableIncomeLotsAsync(
            int warehouseId, int productId, DBTHSNEntities db)
        {
            var incomeProducts = await db.INCOME_PRODUCTS
                .Include("INCOME")
                .Where(ip => ip.ProductID == productId
                    && ip.INCOME.ReceivingWHID == warehouseId
                    && (ip.INCOME.IsDeleted == null || ip.INCOME.IsDeleted == false))
                .ToListAsync();

            var grouped = incomeProducts
                .GroupBy(ip => ip.IncomeID)
                .Select(g => new
                {
                    IncomeID = g.Key,
                    Income = g.First().INCOME,
                    TotalAmount = g.Sum(x => x.Amount),
                    UnitID = g.First().UnitID
                })
                .OrderBy(x => x.Income.IssueDateTime)
                .ToList();

            var result = new List<IncomeLotItem>();
            foreach (var g in grouped)
            {
                var consumed = await GetConsumedFromIncomeAsync(g.IncomeID, productId, db);
                var remaining = Math.Max(0, g.TotalAmount - consumed);
                if (remaining <= 0) continue;

                result.Add(new IncomeLotItem
                {
                    IncomeID = g.IncomeID,
                    DocNo = g.Income.DocNo ?? "",
                    IssueDateTime = g.Income.IssueDateTime,
                    OriginalAmount = g.TotalAmount,
                    RemainingAmount = remaining,
                    UnitID = g.UnitID
                });
            }
            return result;
        }

        /// <summary>
        /// EXPENSE_PRODUCTS.Comment dan "IncomeID:X" formatida saqlangan bo'lsa, shu Income dan chiqim qilingan miqdorni hisoblaydi.
        /// </summary>
        private static async Task<double> GetConsumedFromIncomeAsync(int incomeId, int productId, DBTHSNEntities db)
        {
            var prefix = "IncomeID:" + incomeId;
            var consumed = await db.EXPENSE_PRODUCTS
                .Where(ep => ep.ProductID == productId
                    && ep.Comment != null
                    && ep.Comment.Contains(prefix)
                    && (ep.EXPENS.IsDeleted == false))
                .SumAsync(ep => (double?)ep.Amount) ?? 0;
            return consumed;
        }

        /// <summary>
        /// Comment da "IncomeID:X" saqlash uchun format.
        /// </summary>
        public static string FormatIncomeIdComment(int incomeId)
        {
            return $"IncomeID:{incomeId}";
        }

        /// <summary>
        /// Comment dan IncomeID ni ajratib oladi. Topilmasa null.
        /// </summary>
        public static int? ParseIncomeIdFromComment(string comment)
        {
            if (string.IsNullOrWhiteSpace(comment)) return null;
            var prefix = "IncomeID:";
            var idx = comment.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var start = idx + prefix.Length;
            var end = start;
            while (end < comment.Length && char.IsDigit(comment[end])) end++;
            var numStr = comment.Substring(start, end - start);
            return int.TryParse(numStr, out int id) ? id : (int?)null;
        }
    }

    public class IncomeLotItem
    {
        public int IncomeID { get; set; }
        public string DocNo { get; set; }
        public DateTime IssueDateTime { get; set; }
        public double OriginalAmount { get; set; }
        public double RemainingAmount { get; set; }
        public int UnitID { get; set; }
        public string DisplayText => $"{DocNo} ({IssueDateTime:dd.MM.yyyy}) — qoldiq: {RemainingAmount:N2}";
    }
}
