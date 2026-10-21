using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;

namespace tahsinERP.Areas.Distribution.Services.Orders
{
    /// <summary>
    /// Aksiyalar (TZ 6-bo'lim: 10+1, hajmga qarab, muddatli). Buyurtma saqlanganda avtomatik:
    ///   - DISCOUNT: tovar / kategoriya / hamma tovar, qatordagi miqdor (asosiy birlikda) MinQtyBase dan kam bo'lmasa —
    ///     qatorga aksiya foizi; bir nechtasi mos kelsa eng kattasi. MinQtyBase = 0 — oddiy muddatli chegirma;
    ///     bosqichli (hajmga qarab) chegirma — bir nechta aksiya turli MinQtyBase bilan.
    ///   - FREE: tovar uchun har BuyQtyBase ga FreeQtyBase sovg'a (Repeatable — necha marta bo'lsa shuncha), sovg'a o'sha yoki
    ///     boshqa tovar; narxsiz alohida qator.
    /// PriceTypeID berilgan aksiya faqat shu narx turidagi mijozlarga. Aksiya chegirmasi rol chegirma limiti va minimal narx
    /// tekshiruvidan ozod (rahbar belgilagan) — qo'lda berilgan chegirma esa avvalgidek tekshiriladi.
    /// </summary>
    public static class PromotionService
    {
        public sealed class LineInfo
        {
            public int Index { get; set; }
            public int ProductID { get; set; }
            public int? CategoryID { get; set; }
            public decimal QtyBase { get; set; }
        }

        public sealed class LineDiscount
        {
            public int PromotionID { get; set; }
            public decimal Pct { get; set; }
        }

        public sealed class FreeGoods
        {
            public int PromotionID { get; set; }
            public int ProductID { get; set; }
            public decimal QtyBase { get; set; }
            public string Name { get; set; }
        }

        public sealed class Result
        {
            public Dictionary<int, LineDiscount> Discounts { get; } = new Dictionary<int, LineDiscount>();
            public List<FreeGoods> Free { get; } = new List<FreeGoods>();
        }

        /// <summary>Bugun amal qiladigan aksiyalar (mijoz narx turi bo'yicha).</summary>
        public static List<Promotion> Active(DistributionDbContext db, int? priceTypeId, DateTime now)
        {
            var day = now.Date;
            return db.Promotions.AsNoTracking()
                .Where(p => p.IsActive && p.DateFrom <= day && (p.DateTo == null || p.DateTo >= day)
                            && (p.PriceTypeID == null || p.PriceTypeID == priceTypeId))
                .ToList();
        }

        /// <summary>Qatorlar uchun aksiyalar (sof funksiya — test qilinadi).</summary>
        public static Result Apply(IReadOnlyCollection<Promotion> promotions, IReadOnlyCollection<LineInfo> lines)
        {
            var result = new Result();
            foreach (var line in lines)
            {
                var best = promotions
                    .Where(p => p.Kind == PromotionKinds.Discount && (p.DiscountPct ?? 0) > 0 && line.QtyBase >= p.MinQtyBase
                                && (p.ProductID == line.ProductID || (p.ProductID == null && (p.CategoryID == null || p.CategoryID == line.CategoryID))))
                    .OrderByDescending(p => p.DiscountPct).ThenBy(p => p.ID)
                    .FirstOrDefault();
                if (best != null) result.Discounts[line.Index] = new LineDiscount { PromotionID = best.ID, Pct = best.DiscountPct.Value };
            }

            // sovg'a: tovar bo'yicha jami miqdor (bir tovar ikki qatorda bo'lsa ham)
            var qtyByProduct = lines.GroupBy(l => l.ProductID).ToDictionary(g => g.Key, g => g.Sum(l => l.QtyBase));
            foreach (var p in promotions.Where(p => p.Kind == PromotionKinds.Free && p.ProductID.HasValue && (p.BuyQtyBase ?? 0) > 0 && (p.FreeQtyBase ?? 0) > 0)
                         .OrderBy(p => p.ID))
            {
                if (!qtyByProduct.TryGetValue(p.ProductID.Value, out var qty) || qty < p.BuyQtyBase.Value) continue;
                decimal times = p.Repeatable ? Math.Floor(qty / p.BuyQtyBase.Value) : 1;
                result.Free.Add(new FreeGoods
                {
                    PromotionID = p.ID, ProductID = p.FreeProductID ?? p.ProductID.Value, QtyBase = times * p.FreeQtyBase.Value, Name = p.Name
                });
            }
            return result;
        }

        /// <summary>Katalog uchun qisqa yozuv: tovar → aksiya matnlari.</summary>
        public static Dictionary<int, List<string>> Labels(IReadOnlyCollection<Promotion> promotions, IReadOnlyCollection<(int ProductID, int? CategoryID)> products)
        {
            var result = new Dictionary<int, List<string>>();
            foreach (var (productId, categoryId) in products)
            {
                var texts = promotions
                    .Where(p => p.ProductID == productId || (p.Kind == PromotionKinds.Discount && p.ProductID == null && (p.CategoryID == null || p.CategoryID == categoryId)))
                    .OrderBy(p => p.ID)
                    .Select(Describe).ToList();
                if (texts.Count > 0) result[productId] = texts;
            }
            return result;
        }

        public static string Describe(Promotion p)
        {
            if (p.Kind == PromotionKinds.Free)
                return p.BuyQtyBase.Value.ToString("0.##") + "+" + p.FreeQtyBase.Value.ToString("0.##") + (p.FreeProductID.HasValue && p.FreeProductID != p.ProductID ? " 🎁" : "");
            return "−" + p.DiscountPct.Value.ToString("0.##") + "%" + (p.MinQtyBase > 0 ? " ≥" + p.MinQtyBase.ToString("0.##") : "");
        }

        /// <summary>Saqlashdan oldin tekshiruv (web forma).</summary>
        public static void Validate(DistributionDbContext db, Promotion p)
        {
            if (string.IsNullOrWhiteSpace(p.Name)) throw new DstValidationException(DstLocalization.T("Error_Required"));
            if (!PromotionKinds.All.Contains(p.Kind)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (p.DateTo.HasValue && p.DateTo < p.DateFrom) throw new DstValidationException(DstLocalization.T("Promo_BadDates"));
            if (p.MinQtyBase < 0) throw new DstValidationException(DstLocalization.T("Error_NumberFormat"));
            if (p.Kind == PromotionKinds.Discount)
            {
                if (!(p.DiscountPct > 0 && p.DiscountPct <= 100)) throw new DstValidationException(DstLocalization.T("Promo_BadPercent"));
                p.BuyQtyBase = null; p.FreeQtyBase = null; p.FreeProductID = null;
                if (p.ProductID.HasValue) p.CategoryID = null;
            }
            else
            {
                if (!p.ProductID.HasValue) throw new DstValidationException(DstLocalization.T("Promo_ProductRequired"));
                if (!(p.BuyQtyBase > 0 && p.FreeQtyBase > 0)) throw new DstValidationException(DstLocalization.T("Promo_BadFree"));
                p.DiscountPct = null; p.CategoryID = null; p.MinQtyBase = 0;
            }
            var ids = new[] { p.ProductID, p.FreeProductID }.Where(x => x.HasValue).Select(x => x.Value).Distinct().ToList();
            if (db.Products.Count(x => ids.Contains(x.ID) && !x.IsDeleted) != ids.Count) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
        }
    }
}
