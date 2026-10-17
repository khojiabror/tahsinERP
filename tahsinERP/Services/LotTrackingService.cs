using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;
using tahsinERP.Services.Stock;

namespace tahsinERP.Services
{
    /// <summary>
    /// FIFO/Lot tracking service. DB o'zgarishsiz Income hujjatini "lot" sifatida ishlatadi.
    /// Keyinchalik DLOC (direct location) qo'shilganda bu service kengaytiriladi.
    /// </summary>
    public static class LotTrackingService
    {
        /// <summary>
        /// Berilgan ombordagi mahsulot uchun mavjud "lot"lar (QABUL QILINGAN kirim hujjatlari) - FIFO tartibida (eng eski birinchi).
        /// Avval har kirim uchun alohida <c>LIKE '%IncomeID:x%'</c> so'rovi ketardi (N+1, prefiks to'qnashuvi:
        /// IncomeID:12 → 120, 123 ham hisoblanardi — SRM-B06/P03). Endi 2 ta so'rov va aniq ID moslik.
        /// </summary>
        public static async Task<List<IncomeLotItem>> GetAvailableIncomeLotsAsync(
            int warehouseId, int productId, DBTHSNEntities db)
        {
            var receipts = await db.INCOME_PRODUCTS
                .AsNoTracking()
                .Where(ip => ip.ProductID == productId
                    && ip.INCOME.ReceivingWHID == warehouseId
                    && ip.INCOME.RecieveStatus == true
                    && (ip.INCOME.IsDeleted == null || ip.INCOME.IsDeleted == false))
                .GroupBy(ip => new { ip.IncomeID, ip.INCOME.DocNo, ip.INCOME.IssueDateTime })
                .Select(g => new
                {
                    g.Key.IncomeID,
                    g.Key.DocNo,
                    g.Key.IssueDateTime,
                    TotalAmount = g.Sum(x => x.Amount),
                    UnitID = g.Min(x => x.UnitID)
                })
                .ToListAsync();

            if (receipts.Count == 0)
                return new List<IncomeLotItem>();

            var consumedByIncome = await GetConsumedByIncomeAsync(productId, db);

            var result = new List<IncomeLotItem>();
            foreach (var g in receipts.OrderBy(x => x.IssueDateTime).ThenBy(x => x.IncomeID))
            {
                double consumed;
                consumedByIncome.TryGetValue(g.IncomeID, out consumed);
                var remaining = Math.Max(0, g.TotalAmount - consumed);
                if (remaining <= 0) continue;

                result.Add(new IncomeLotItem
                {
                    IncomeID = g.IncomeID,
                    DocNo = g.DocNo ?? "",
                    IssueDateTime = g.IssueDateTime,
                    OriginalAmount = g.TotalAmount,
                    RemainingAmount = remaining,
                    UnitID = g.UnitID
                });
            }
            return result;
        }

        /// <summary>
        /// Mahsulotning "IncomeID:X" izohli (o'chirilmagan) chiqim qatorlari — kirim ID bo'yicha jami chiqim.
        /// </summary>
        private static async Task<Dictionary<int, double>> GetConsumedByIncomeAsync(int productId, DBTHSNEntities db)
        {
            var rows = await db.EXPENSE_PRODUCTS
                .AsNoTracking()
                .Where(ep => ep.ProductID == productId
                    && ep.Comment != null
                    && ep.Comment.Contains(DocumentMarkers.IncomeId)
                    && ep.EXPENS.IsDeleted == false)
                .Select(ep => new { ep.Comment, ep.Amount })
                .ToListAsync();

            return rows
                .Select(r => new { IncomeID = ParseIncomeIdFromComment(r.Comment), r.Amount })
                .Where(r => r.IncomeID.HasValue)
                .GroupBy(r => r.IncomeID.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
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
