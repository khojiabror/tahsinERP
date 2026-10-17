using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Lot = PRODUCT_PRODUCTION. Sifat nazoratidan o'tgan production objectlar uchun qoldiq hisoblash.
    /// Lot taqsimoti transfer chiqimi qatori izohida saqlanadi: "ProdID:123:7|ProdID:124:3".
    /// Miqdorlar har doim InvariantCulture bilan yoziladi va o'qiladi (MES-B19).
    /// </summary>
    public static class ProductionLotService
    {
        private static readonly Regex LotTokenRegex = new Regex(
            @"ProdID:(\d+):([0-9]+(?:\.[0-9]+)?)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static string BuildLotAllocationsComment(List<LotAllocation> allocations)
        {
            if (allocations == null || !allocations.Any()) return null;
            return string.Join("|", allocations.Select(a =>
                "ProdID:" + a.ProductionID + ":" + a.Quantity.ToString(CultureInfo.InvariantCulture)));
        }

        /// <summary>
        /// Mahsulot bo'yicha barcha transfer izohlarini BIR so'rovda o'qib, ProductionID → o'tkazilgan miqdor
        /// lug'atini quradi (avval har lot uchun bir xil so'rov qayta yuborilardi, MES-P17).
        /// </summary>
        public static Dictionary<int, double> GetTransferredByProduction(int productID, DBTHSNEntities db)
        {
            var comments = TransferLines(db)
                .Where(ep => ep.ProductID == productID)
                .Select(ep => ep.Comment)
                .ToList();
            return SumTokens(comments);
        }

        /// <summary>
        /// Parses Comment to get transferred qty per ProductionID.
        /// </summary>
        public static double GetTransferredFromProduction(int productionID, int productID, DBTHSNEntities db)
        {
            double qty;
            return GetTransferredByProduction(productID, db).TryGetValue(productionID, out qty) ? qty : 0d;
        }

        /// <summary>
        /// Ushbu ishlab chiqarish (lot)idan sotuvga transfer qilinganmi (o'chirilmagan transfer chiqimida
        /// "ProdID:{id}:" tokeni bor). Ishlab chiqarishni tahrirlash/o'chirishni bloklash uchun.
        /// </summary>
        public static bool HasTransfers(int productionID, DBTHSNEntities db)
        {
            var token = "ProdID:" + productionID + ":";
            var comments = TransferLines(db)
                .Where(ep => ep.Comment.Contains(token))
                .Select(ep => ep.Comment)
                .Take(50)
                .ToList();
            // LIKE '%ProdID:12:%' "xProdID:12:" kabi qatorlarga ham mos kelishi mumkin — regex bilan aniqlaymiz
            double qty;
            return SumTokens(comments).TryGetValue(productionID, out qty) && qty > 0;
        }

        /// <summary>
        /// Barcha PRODUCT_PRODUCTION (production table) lotlarini yuklaydi.
        /// Remaining = Total - transfer qilingan. Sifat nazorati dropdown da emas, saqlashda tekshiriladi.
        /// Order by Date asc (FIFO).
        /// </summary>
        public static List<ProductionLotItem> GetAvailableProductionLots(int productID, DBTHSNEntities db)
        {
            var productions = db.PRODUCT_PRODUCTION.AsNoTracking()
                .Where(p => p.ProductID == productID
                    && (p.IsDeleted == false || p.IsDeleted == null)
                    && p.Total > 0)
                .OrderBy(p => p.Date)
                .Select(p => new { p.ID, p.Date, p.Total })
                .ToList();

            var transferredByProduction = GetTransferredByProduction(productID, db);

            var result = new List<ProductionLotItem>();
            foreach (var pp in productions)
            {
                double transferred;
                transferredByProduction.TryGetValue(pp.ID, out transferred);
                var remaining = Math.Max(0, pp.Total - transferred);
                if (remaining <= 0) continue;

                result.Add(new ProductionLotItem
                {
                    ProductionID = pp.ID,
                    Date = pp.Date,
                    Total = pp.Total,
                    AcceptedQty = pp.Total,
                    RemainingAmount = remaining,
                    DisplayText = $"Lot {pp.ID} ({pp.Date:dd.MM.yyyy}) — qoldiq: {remaining:N2}"
                });
            }
            return result;
        }

        /// <summary>
        /// Validate that all ProductionIDs in allocations have passed quality inspection.
        /// </summary>
        public static bool AllLotsPassedQuality(List<LotAllocation> allocations, int productID, DBTHSNEntities db, out string errorMessage)
        {
            errorMessage = null;
            if (allocations == null || !allocations.Any()) return true;

            var lotIds = allocations.Select(a => a.ProductionID).Distinct().ToList();
            var acceptedByLot = db.QUALITY_INSPECTION
                .Where(q => q.ProductionID.HasValue
                    && lotIds.Contains(q.ProductionID.Value)
                    && q.ProductID == productID
                    && !q.IsDeleted
                    && q.StatusCode == "APPROVED")
                .GroupBy(q => q.ProductionID.Value)
                .Select(g => new { ProductionID = g.Key, Qty = g.Sum(q => q.QtyAccepted) })
                .ToList()
                .ToDictionary(x => x.ProductionID, x => (double)x.Qty);
            var transferredByProduction = GetTransferredByProduction(productID, db);

            foreach (var a in allocations)
            {
                double totalAcceptedForLot;
                acceptedByLot.TryGetValue(a.ProductionID, out totalAcceptedForLot);
                double alreadyTransferredFromLot;
                transferredByProduction.TryGetValue(a.ProductionID, out alreadyTransferredFromLot);
                var wouldBeTransferred = alreadyTransferredFromLot + a.Quantity;

                if (totalAcceptedForLot <= 0 || wouldBeTransferred > (totalAcceptedForLot + 0.0001))
                {
                    var prodDate = db.PRODUCT_PRODUCTION.Where(p => p.ID == a.ProductionID).Select(p => (DateTime?)p.Date).FirstOrDefault();
                    errorMessage = $"Lot (Ishlab chiqarish #{a.ProductionID}, {(prodDate?.ToString("dd.MM.yyyy") ?? "")}) uchun qabul qilingan miqdor yetarli emas. " +
                        $"Qabul qilingan jami: {totalAcceptedForLot:N2}, avval o'tkazilgan: {alreadyTransferredFromLot:N2}, hozir so'ralgan: {a.Quantity:N2}.";
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Parse LotAllocations from form string "ProdID:123:7|ProdID:124:3"
        /// </summary>
        public static List<LotAllocation> ParseLotAllocations(string value)
        {
            var list = new List<LotAllocation>();
            if (string.IsNullOrWhiteSpace(value)) return list;
            foreach (var part in value.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var m = LotTokenRegex.Match(part.Trim());
                if (m.Success
                    && int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid)
                    && double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var qty)
                    && qty > 0)
                    list.Add(new LotAllocation { ProductionID = pid, Quantity = qty });
            }
            return list;
        }

        private static IQueryable<EXPENSE_PRODUCTS> TransferLines(DBTHSNEntities db)
        {
            return db.EXPENSE_PRODUCTS.AsNoTracking()
                .Where(ep => ep.Comment != null
                    && (ep.EXPENS.DocNo.StartsWith("TRNSF_") || ep.EXPENS.DocNo.Contains("TRANSFER-"))
                    && ep.EXPENS.IsDeleted == false);
        }

        private static Dictionary<int, double> SumTokens(IEnumerable<string> comments)
        {
            var result = new Dictionary<int, double>();
            foreach (var c in comments)
            {
                foreach (Match m in LotTokenRegex.Matches(c ?? ""))
                {
                    if (!int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                        continue;
                    if (!double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var q))
                        continue;
                    double current;
                    result.TryGetValue(pid, out current);
                    result[pid] = current + q;
                }
            }
            return result;
        }
    }

    /// <summary>
    /// Ishlab chiqarish yozuvi "qulflangan"mi: sifat nazorati yoki lot transferi bilan bog'langan bo'lsa,
    /// uni tahrirlash/o'chirish (va kunlik kiritish orqali qayta yozish) mumkin emas.
    /// Avval guard "mahsulot hech qachon sotilganmi" deb tekshirardi — sotilgan mahsulotning har qanday
    /// ishlab chiqarishi bloklanib qolardi (MES-B03).
    /// </summary>
    public static class ProductionLockGuard
    {
        public static bool IsLocked(DBTHSNEntities db, int productionId)
        {
            return db.QUALITY_INSPECTION.Any(q => q.ProductionID == productionId && q.IsDeleted == false)
                || ProductionLotService.HasTransfers(productionId, db);
        }
    }

    public class ProductionLotItem
    {
        public int ProductionID { get; set; }
        public DateTime Date { get; set; }
        public double Total { get; set; }
        public double AcceptedQty { get; set; }
        public double RemainingAmount { get; set; }
        public string DisplayText { get; set; }
    }

    public class LotAllocation
    {
        public int ProductionID { get; set; }
        public double Quantity { get; set; }
    }
}
