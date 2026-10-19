using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Hujjat qatorlari uchun umumiy mahsulot qidiruvi (SRM-P05): select2 AJAX formatida 30 tadan,
    /// faqat 3 ustun proyeksiya. Butun PRODUCTS jadvalini sahifaga JSON qilib joylash o'rniga.
    /// Faqat login talab qilinadi (global [Authorize]) — mahsulot katalogi hujjat yaratuvchi
    /// har qanday modul foydalanuvchisiga kerak.
    /// <code>ProductSelect.attach($('select.product-select'), '/ProductLookup/Search');</code>
    /// </summary>
    public class ProductLookupController : Controller
    {
        private const int PageSize = 30;

        // Olish so'rovi: ProductLookup/Search?term=&page=1&raw=true
        [HttpGet]
        public async Task<ActionResult> Search(string term, int page = 1, bool? raw = null)
        {
            if (page < 1) page = 1;
            term = (term ?? "").Trim();

            using (var db = new DBTHSNEntities())
            {
                var query = db.PRODUCTS.AsNoTracking().Where(p => p.IsDeleted == false);
                if (raw.HasValue)
                    query = raw.Value
                        ? query.Where(p => p.IsRawMaterial == true || (p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.IsRawMaterial == true))
                        : query.Where(p => p.IsRawMaterial != true && (p.PRODUCT_CATEGORIES == null || p.PRODUCT_CATEGORIES.IsRawMaterial != true));
                if (term.Length > 0)
                    query = query.Where(p => p.PNo.Contains(term) || p.Name.Contains(term));

                // Bitta ortiqcha qator — keyingi sahifa bormi
                var rows = await query
                    .OrderBy(p => p.PNo)
                    .Skip((page - 1) * PageSize)
                    .Take(PageSize + 1)
                    .Select(p => new { p.ID, p.PNo, p.Name, p.UnitID })
                    .ToListAsync();

                var results = rows.Take(PageSize).Select(p => new
                {
                    id = p.ID.ToString(),
                    text = p.PNo + " - " + (p.Name ?? ""),
                    unitId = p.UnitID
                });

                return Json(new { results, pagination = new { more = rows.Count > PageSize } }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}
