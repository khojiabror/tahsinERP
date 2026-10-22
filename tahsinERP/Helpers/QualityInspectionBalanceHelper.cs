using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using tahsinERP.I18n;
using tahsinERP.Models;

namespace tahsinERP.Helpers
{
    /// <summary>
    /// Sifat nazorati qoldig'i faqat PRODUCT_PRODUCTION (fakt) bo'yicha.
    /// Reja (PRODUCTPLANS_DAILY / ProductPlanDailyID) limitga ta'sir qilmaydi.
    /// Rad etilgan miqdor qayta tekshiruvga chiqadi — limit faqat qabul qilingan yig'indisi bo'yicha.
    /// </summary>
    public static class QualityInspectionBalanceHelper
    {
        /// <summary>
        /// Ushbu ishlab chiqarish loti uchun qabul qilingan miqdor.
        /// Eski reja yozuvlari (ProductPlanDailyID) va joriy fakt sanasidan oldingi tekshiruvlar hisobga olinmaydi.
        /// </summary>
        public static decimal GetAcceptedQtyForProduction(PRODUCT_PRODUCTION production, DBTHSNEntities db)
        {
            if (production == null)
            {
                return 0m;
            }

            var factDate = production.Date.Date;

            return db.QUALITY_INSPECTION
                .Where(q => q.ProductionID == production.ID
                    && !q.IsDeleted
                    && q.ProductPlanDailyID == null
                    && DbFunctions.TruncateTime(q.InspectionDate) >= factDate)
                .Sum(q => (decimal?)q.QtyAccepted) ?? 0m;
        }

        /// <summary>
        /// <see cref="GetAcceptedQtyForProduction"/> ning ko'p ishlab chiqarishli varianti — bitta GROUP BY so'rov
        /// (avval ro'yxatdagi har ishlab chiqarishga 2 ta so'rov ketardi, MES-P16).
        /// </summary>
        public static Dictionary<int, decimal> GetAcceptedQtyByProduction(IEnumerable<int> productionIds, DBTHSNEntities db)
        {
            var ids = productionIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<int, decimal>();
            }

            return (from q in db.QUALITY_INSPECTION
                    join p in db.PRODUCT_PRODUCTION on q.ProductionID equals p.ID
                    where ids.Contains(p.ID)
                          && !q.IsDeleted
                          && q.ProductPlanDailyID == null
                          && DbFunctions.TruncateTime(q.InspectionDate) >= DbFunctions.TruncateTime(p.Date)
                    group q by p.ID into g
                    select new { ProductionID = g.Key, Qty = g.Sum(x => x.QtyAccepted) })
                .ToList()
                .ToDictionary(x => x.ProductionID, x => x.Qty);
        }

        /// <summary>
        /// Fakt ishlab chiqarilgan miqdordan qabul qilinganlarni ayirib qoldiq qaytaradi.
        /// </summary>
        public static decimal GetRemainingQty(PRODUCT_PRODUCTION production, DBTHSNEntities db)
        {
            if (production == null)
            {
                return 0m;
            }

            var producedQty = production.Total;
            var acceptedQty = GetAcceptedQtyForProduction(production, db);
            return producedQty - acceptedQty;
        }

        /// <summary>
        /// Re-inspection: rad etilganlar tekshiriladi, lekin limitga faqat qabul qilinganlar kiradi.
        /// </summary>
        public static bool TryValidateInspectionQty(
            PRODUCT_PRODUCTION production,
            decimal qtyAccepted,
            decimal qtyRejected,
            DBTHSNEntities db,
            out string errorMessage)
        {
            errorMessage = null;
            if (production == null)
            {
                errorMessage = L.T("Mes_Qi_ProductionNotFound");
                return false;
            }

            var inspectedQty = qtyAccepted + qtyRejected;
            if (inspectedQty <= 0)
            {
                return true;
            }

            return TryValidateInspectionQty(production, qtyAccepted, qtyRejected, GetAcceptedQtyForProduction(production, db), out errorMessage);
        }

        /// <summary>
        /// Oldindan hisoblangan "qabul qilingan" miqdor bilan tekshirish (qo'shimcha so'rovsiz).
        /// </summary>
        public static bool TryValidateInspectionQty(
            PRODUCT_PRODUCTION production,
            decimal qtyAccepted,
            decimal qtyRejected,
            decimal acceptedSoFar,
            out string errorMessage)
        {
            errorMessage = null;
            if (production == null)
            {
                errorMessage = L.T("Mes_Qi_ProductionNotFound");
                return false;
            }

            var inspectedQty = qtyAccepted + qtyRejected;
            if (inspectedQty <= 0)
            {
                return true;
            }

            var remaining = production.Total - acceptedSoFar;
            if (inspectedQty > remaining)
            {
                errorMessage = L.T("Mes_Qi_QtyExceedsRemaining",
                    production.PRODUCT?.PNo, production.Total.ToString("N3"),
                    acceptedSoFar.ToString("N3"), remaining.ToString("N3"));
                return false;
            }

            return true;
        }
    }
}
