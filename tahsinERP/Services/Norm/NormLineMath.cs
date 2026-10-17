using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Services.Norm
{
    /// <summary>
    /// NORM_INPUT_LINES qatorining sarf/chiqindi/narx hisob-kitobi — yagona manba.
    /// Avval bir xil formulalar NormController (4 joy), TannarxService va BomMaterialSummaryService'da
    /// nusxalangan edi (audit MES dublikatlar #1–#3).
    ///
    /// Birliklar (DB):
    ///  - Consumption: kesiladigan material va CutLengthMm &gt; 0 bo'lsa — qator bo'yicha jami mm; aks holda
    ///    UI'dagi sarf qiymati (dona/m/kg) o'zicha.
    ///  - WasteAmount: har doim qator bo'yicha jami mm.
    /// Ko'rsatish (VM): metr.
    /// </summary>
    public static class NormLineMath
    {
        /// <summary>Materialning amaldagi standart uzunligi (mm): StandardLengthMm &gt; 0, aks holda Length &gt; 0, aks holda 0.</summary>
        public static double EffectiveStandardLengthMm(double? standardLengthMm, double? length)
        {
            if (standardLengthMm.HasValue && standardLengthMm.Value > 0)
                return standardLengthMm.Value;
            if (length.HasValue && length.Value > 0)
                return length.Value;
            return 0;
        }

        public static double EffectiveStandardLengthMm(PRODUCT material)
        {
            return material == null ? 0 : EffectiveStandardLengthMm(material.StandardLengthMm, material.Length);
        }

        /// <summary>"Yog'och" kategoriyasi (RawMaterialGroup yoki nomida "yog'och"/"yo'goch").</summary>
        public static bool IsWoodCategory(string rawMaterialGroup, string categoryName)
        {
            var rawGroup = rawMaterialGroup ?? string.Empty;
            var catName = categoryName ?? string.Empty;
            return rawGroup.IndexOf("yo'goch", StringComparison.OrdinalIgnoreCase) >= 0
                || rawGroup.IndexOf("yog'och", StringComparison.OrdinalIgnoreCase) >= 0
                || catName.IndexOf("yo'goch", StringComparison.OrdinalIgnoreCase) >= 0
                || catName.IndexOf("yog'och", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsWoodCategory(PRODUCT_CATEGORIES category)
        {
            return category != null && IsWoodCategory(category.RawMaterialGroup, category.Name);
        }

        /// <summary>DB Consumption → ko'rsatish (kesish qatori: mm → m; qolganlari o'zicha).</summary>
        public static decimal ConsumptionToDisplay(bool requiresCutting, int cutLengthMm, double? consumptionDb)
        {
            if (!consumptionDb.HasValue || consumptionDb.Value <= 0)
                return 0;
            if (requiresCutting && cutLengthMm > 0)
                return (decimal)(consumptionDb.Value / 1000.0);
            return (decimal)consumptionDb.Value;
        }

        /// <summary>DB WasteAmount (mm) → m.</summary>
        public static decimal WasteToDisplay(double? wasteAmountDb)
        {
            if (!wasteAmountDb.HasValue || wasteAmountDb.Value <= 0)
                return 0;
            return (decimal)(wasteAmountDb.Value / 1000.0);
        }

        /// <summary>
        /// Qator sarfi (m yoki dona): saqlangan Consumption bo'lsa — undan; kesish qatori bo'lsa — kesim × dona;
        /// aks holda — dona.
        /// </summary>
        public static decimal LineConsumption(bool requiresCutting, int cutLengthMm, int piecesPerProduct, double? consumptionDb)
        {
            if (consumptionDb.HasValue && consumptionDb.Value > 0)
                return ConsumptionToDisplay(requiresCutting, cutLengthMm, consumptionDb);
            if (requiresCutting && cutLengthMm > 0)
                return (decimal)(cutLengthMm / 1000.0) * piecesPerProduct;
            return piecesPerProduct;
        }

        /// <summary>
        /// Qator chiqindisi (m): saqlangan WasteAmount bo'lsa — undan; kesish qatori va standart uzunlik bo'lsa —
        /// (standart − kesim) × dona; aks holda 0.
        /// </summary>
        public static decimal LineWaste(bool requiresCutting, double standardLengthMm, int cutLengthMm, int piecesPerProduct, double? wasteAmountDb)
        {
            if (wasteAmountDb.HasValue)
                return WasteToDisplay(wasteAmountDb);
            if (requiresCutting && standardLengthMm > 0 && cutLengthMm > 0)
                return (decimal)(Math.Max(0, standardLengthMm - cutLengthMm) / 1000.0) * piecesPerProduct;
            return 0;
        }

        /// <summary>
        /// Kesiladigan material uchun real ishlatiladigan taxta soni:
        /// ceil( Σ(kesim × dona + chiqindi_mm) / standart uzunlik ). Chiqindi berilmagan qatorda
        /// (standart − kesim) × dona olinadi. Standart uzunlik bo'lmasa — donalar yig'indisi.
        /// Masalan: 4000mm taxta, 1140×12 (chiqindi 2320) + 940×4 (240) + 880×3 (1360) + 720×4 (1120)
        ///          = 28000mm → ceil(28000/4000) = 7 dona.
        /// </summary>
        public static decimal BarQuantity(IEnumerable<CutPiece> pieces, double standardLengthMm)
        {
            var list = pieces?.ToList();
            if (list == null || list.Count == 0)
                return 0;

            decimal totalLengthMm = 0;
            foreach (var x in list)
            {
                var cutPartMm = (decimal)x.CutLengthMm * x.Pieces;
                decimal wasteMm;
                if (x.WasteMm.HasValue)
                    wasteMm = x.WasteMm.Value;
                else if (standardLengthMm > 0 && x.CutLengthMm > 0)
                    wasteMm = (decimal)Math.Max(0, standardLengthMm - (double)x.CutLengthMm) * x.Pieces;
                else
                    wasteMm = 0;
                totalLengthMm += cutPartMm + wasteMm;
            }

            if (standardLengthMm > 0 && totalLengthMm > 0)
                return Math.Ceiling(totalLengthMm / (decimal)standardLengthMm);

            return list.Sum(x => (decimal)x.Pieces);
        }
    }

    /// <summary>Taxta soni hisobi uchun bitta kesim qatori (mm).</summary>
    public struct CutPiece
    {
        public CutPiece(decimal cutLengthMm, decimal pieces, decimal? wasteMm)
        {
            CutLengthMm = cutLengthMm;
            Pieces = pieces;
            WasteMm = wasteMm;
        }

        public decimal CutLengthMm { get; }
        public decimal Pieces { get; }
        /// <summary>Qator bo'yicha jami chiqindi (mm); null — (standart − kesim) × dona.</summary>
        public decimal? WasteMm { get; }
    }
}
