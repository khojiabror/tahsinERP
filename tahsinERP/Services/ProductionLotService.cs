using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text.RegularExpressions;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Lot = PRODUCT_PRODUCTION. Sifat nazoratidan o'tgan production objectlar uchun qoldiq hisoblash.
    /// </summary>
    public static class ProductionLotService
    {
        /// <summary>
        /// ProdID:123:7|ProdID:124:3 format
        /// </summary>
        public static string FormatLotAllocationComment(int productionID, double qty)
        {
            return $"ProdID:{productionID}:{qty}";
        }

        public static string BuildLotAllocationsComment(List<LotAllocation> allocations)
        {
            if (allocations == null || !allocations.Any()) return null;
            return string.Join("|", allocations.Select(a => $"ProdID:{a.ProductionID}:{a.Quantity}"));
        }

        /// <summary>
        /// Parses Comment to get transferred qty per ProductionID.
        /// </summary>
        public static double GetTransferredFromProduction(int productionID, int productID, DBTHSNEntities db)
        {
            var pattern = new Regex(@"ProdID:" + productionID + @":([0-9.]+)", RegexOptions.IgnoreCase);
            var sum = 0.0;
            var comments = db.EXPENSE_PRODUCTS
                .Include(ep => ep.EXPENS)
                .Where(ep => ep.ProductID == productID
                    && ep.Comment != null
                    && (ep.EXPENS.DocNo.StartsWith("TRNSF_") || ep.EXPENS.DocNo.Contains("TRANSFER-"))
                    && (ep.EXPENS.IsDeleted == false))
                .Select(ep => ep.Comment)
                .ToList();

            foreach (var c in comments)
            {
                var m = pattern.Match(c ?? "");
                if (m.Success && double.TryParse(m.Groups[1].Value, out var q))
                    sum += q;
            }
            return sum;
        }

        /// <summary>
        /// Barcha PRODUCT_PRODUCTION (production table) lotlarini yuklaydi.
        /// Remaining = Total - transfer qilingan. Sifat nazorati dropdown da emas, saqlashda tekshiriladi.
        /// Order by Date asc (FIFO).
        /// </summary>
        public static List<ProductionLotItem> GetAvailableProductionLots(int productID, DBTHSNEntities db)
        {
            var productions = db.PRODUCT_PRODUCTION
                .Where(p => p.ProductID == productID
                    && (p.IsDeleted == false || p.IsDeleted == null)
                    && p.Total > 0)
                .OrderBy(p => p.Date)
                .ToList();

            var result = new List<ProductionLotItem>();
            foreach (var pp in productions)
            {
                var transferred = GetTransferredFromProduction(pp.ID, productID, db);
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

            foreach (var a in allocations)
            {
                var qi = db.QUALITY_INSPECTION
                    .FirstOrDefault(q => q.ProductionID == a.ProductionID
                        && q.ProductID == productID
                        && q.IsDeleted == false
                        && q.StatusCode == "APPROVED"
                        && q.QtyAccepted >= (decimal)a.Quantity);
                if (qi == null)
                {
                    var prod = db.PRODUCT_PRODUCTION.Find(a.ProductionID);
                    errorMessage = $"Lot (Ishlab chiqarish #{a.ProductionID}, {(prod?.Date.ToString("dd.MM.yyyy") ?? "")}) sifat nazoratidan o'tmagan yoki qabul qilingan miqdor yetarli emas.";
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Parse LotAllocations from form string "123:7|124:3"
        /// </summary>
        public static List<LotAllocation> ParseLotAllocations(string value)
        {
            var list = new List<LotAllocation>();
            if (string.IsNullOrWhiteSpace(value)) return list;
            foreach (var part in value.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var m = Regex.Match(part.Trim(), @"ProdID:(\d+):([0-9.]+)", RegexOptions.IgnoreCase);
                if (m.Success && int.TryParse(m.Groups[1].Value, out var pid) && double.TryParse(m.Groups[2].Value, out var qty) && qty > 0)
                    list.Add(new LotAllocation { ProductionID = pid, Quantity = qty });
            }
            return list;
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
