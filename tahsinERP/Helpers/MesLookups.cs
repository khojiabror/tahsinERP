using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Helpers
{
    /// <summary>
    /// O'zbekcha oy nomlari (avval TannarxController, TannarxAnalyticsController ×2, TannarxService,
    /// TannarxAnalyticsService'da alohida nusxalar bor edi — audit MES dublikat #15).
    /// </summary>
    public static class UzMonthNames
    {
        private static readonly string[] Names =
        {
            "Yanvar", "Fevral", "Mart", "Aprel", "May", "Iyun",
            "Iyul", "Avgust", "Sentabr", "Oktabr", "Noyabr", "Dekabr"
        };

        public static string Get(int month)
        {
            return month >= 1 && month <= 12 ? Names[month - 1] : month.ToString();
        }

        public static SelectList SelectList(object selectedValue)
        {
            var items = Enumerable.Range(1, 12)
                .Select(m => new SelectListItem { Value = m.ToString(), Text = Names[m - 1] })
                .ToList();
            return new SelectList(items, "Value", "Text", selectedValue);
        }
    }

    /// <summary>
    /// MES/Engineering dropdownlari uchun proyeksiyalangan so'rovlar (to'liq PRODUCT entity'lari yuklanmaydi —
    /// audit MES-P19, dublikat #16).
    /// </summary>
    public static class MesLookups
    {
        /// <summary>Tayyor mahsulot: PRODUCT.IsFinishedGood yoki PRODUCT_CATEGORIES.IsFinishedGood.</summary>
        public static IQueryable<PRODUCT> FinishedGoods(DBTHSNEntities db)
        {
            return db.PRODUCTS.AsNoTracking()
                .Where(p => p.IsDeleted == false
                    && (p.IsFinishedGood == true
                        || (p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.IsFinishedGood == true)));
        }

        /// <summary>"PNo - Name" ko'rinishidagi tayyor mahsulotlar ro'yxati (PNo bo'yicha).</summary>
        public static async Task<List<LookupItem>> FinishedGoodItemsAsync(DBTHSNEntities db)
        {
            var rows = await FinishedGoods(db)
                .OrderBy(p => p.PNo)
                .Select(p => new { p.ID, p.PNo, p.Name })
                .ToListAsync();
            return rows.Select(p => new LookupItem { ID = p.ID, Name = p.PNo + " - " + p.Name }).ToList();
        }
    }

    public sealed class LookupItem
    {
        public int ID { get; set; }
        public string Name { get; set; }
    }
}
