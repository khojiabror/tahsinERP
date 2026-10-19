using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;

namespace tahsinERP.Areas.Distribution.Services
{
    /// <summary>Tovarning sotish birligi: asosiy (Factor = 1, ProductUnitID = null) yoki dst.PRODUCT_UNITS qatori.</summary>
    public sealed class SaleUnit
    {
        public int? ProductUnitID { get; set; }
        public int UnitID { get; set; }
        public string UnitName { get; set; }
        public decimal Factor { get; set; }
        public decimal? WeightKg { get; set; }
        public decimal? VolumeM3 { get; set; }
        public bool IsBase => ProductUnitID == null;
        public bool IsDefaultSale { get; set; }
    }

    /// <summary>
    /// O'lchov birliklari: agent qulay birlikda (quti, m², qop) kiritadi, ombor va narx ASOSIY birlikda
    /// (PRODUCTS.UnitID). 1 birlik = Factor asosiy birlik.
    /// </summary>
    public static class UnitMath
    {
        public const int QuantityScale = 4;

        public static decimal ToBase(decimal qty, decimal factor)
        {
            if (factor <= 0) throw new ArgumentOutOfRangeException(nameof(factor), "Koeffitsient musbat bo'lishi kerak.");
            return Math.Round(qty * factor, QuantityScale, MidpointRounding.AwayFromZero);
        }

        public static decimal FromBase(decimal baseQty, decimal factor)
        {
            if (factor <= 0) throw new ArgumentOutOfRangeException(nameof(factor), "Koeffitsient musbat bo'lishi kerak.");
            return Math.Round(baseQty / factor, QuantityScale, MidpointRounding.AwayFromZero);
        }

        /// <summary>Birlik narxi = asosiy birlik narxi × Factor (so'mgacha yaxlitlanadi).</summary>
        public static decimal UnitPrice(decimal basePrice, decimal factor)
        {
            return Math.Round(basePrice * factor, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>Birlik og'irligi: o'zida berilgan bo'lsa — u, aks holda asosiy og'irlik × Factor.</summary>
        public static decimal? Weight(decimal? ownWeight, decimal? baseWeight, decimal factor)
        {
            if (ownWeight.HasValue) return ownWeight;
            return baseWeight.HasValue ? baseWeight * factor : null;
        }
    }

    public class UnitService
    {
        private readonly DistributionDbContext _db;

        public UnitService(DistributionDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>Tovarning barcha faol birliklari: avval asosiy, keyin qo'shimchalar (Factor bo'yicha).</summary>
        public List<SaleUnit> UnitsOf(int productId)
        {
            var product = _db.Products.Where(p => p.ID == productId)
                .Select(p => new { p.UnitID, Unit = _db.Units.Where(u => u.ID == p.UnitID).Select(u => u.ShortName).FirstOrDefault() })
                .FirstOrDefault();
            if (product == null) return new List<SaleUnit>();

            var profile = _db.ProductProfiles.Where(p => p.ProductID == productId)
                .Select(p => new { p.BaseWeightKg, p.BaseVolumeM3 }).FirstOrDefault();

            var extra = (from pu in _db.ProductUnits
                         join u in _db.Units on pu.UnitID equals u.ID
                         where pu.ProductID == productId && pu.IsActive
                         orderby pu.Factor
                         select new { pu.ID, pu.UnitID, u.ShortName, pu.Factor, pu.WeightKg, pu.VolumeM3, pu.IsDefaultSale }).ToList();

            var result = new List<SaleUnit>
            {
                new SaleUnit
                {
                    UnitID = product.UnitID, UnitName = product.Unit, Factor = 1m,
                    WeightKg = profile?.BaseWeightKg, VolumeM3 = profile?.BaseVolumeM3,
                    IsDefaultSale = !extra.Any(e => e.IsDefaultSale)
                }
            };
            result.AddRange(extra.Select(e => new SaleUnit
            {
                ProductUnitID = e.ID, UnitID = e.UnitID, UnitName = e.ShortName, Factor = e.Factor,
                WeightKg = UnitMath.Weight(e.WeightKg, profile?.BaseWeightKg, e.Factor),
                VolumeM3 = UnitMath.Weight(e.VolumeM3, profile?.BaseVolumeM3, e.Factor),
                IsDefaultSale = e.IsDefaultSale
            }));
            return result;
        }

        /// <summary>Yangi qo'shimcha birlik qoidalari (saqlashdan oldin).</summary>
        public static void Validate(int baseUnitId, int unitId, decimal factor, IEnumerable<int> existingUnitIds)
        {
            if (unitId == baseUnitId)
                throw new DstValidationException(DstLocalization.T("Error_UnitIsBase"));
            if (factor <= 0)
                throw new DstValidationException(DstLocalization.T("Error_FactorPositive"));
            if (existingUnitIds != null && existingUnitIds.Contains(unitId))
                throw new DstValidationException(DstLocalization.T("Error_UnitDuplicate"));
        }
    }
}
