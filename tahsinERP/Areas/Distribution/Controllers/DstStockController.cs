using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Services.Procurement;
using tahsinERP.ViewModels;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>
    /// Qoldiqlar: haqiqiy (dbo.PRODUCT_STOCKS), rezerv (tasdiqlangan buyurtmalar), mavjud = haqiqiy − rezerv;
    /// ombor bo'yicha yoki barcha omborlar jami; minimal qoldiqdan pastlari ajratiladi.
    /// </summary>
    public class DstStockController : DstControllerBase
    {
        public ActionResult Index()
        {
            using (var db = NewDb())
            {
                var scope = new ScopeService(db).For(CurrentUserName, ScopeTypes.Warehouse);
                ViewBag.Warehouses = db.Warehouses.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToList()
                    .Where(w => scope.Allows(w.ID))
                    .Select(w => new SelectListItem { Value = w.ID.ToString(), Text = w.WHName }).ToList();
                ViewBag.Categories = db.Categories.Where(c => c.IsDeleted != true).OrderBy(c => c.Name).ToList()
                    .Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.Name }).ToList();
                return View();
            }
        }

        [HttpGet]
        public async Task<ActionResult> IndexData(int? warehouseId, int? categoryId, string filter)
        {
            var page = DataTablesPageRequest.Parse(Request, defaultOrderColumn: 1, defaultDescending: false);
            using (var db = NewDb())
            {
                var whScope = new ScopeService(db).For(CurrentUserName, ScopeTypes.Warehouse);

                // ombor × tovar qatorlari (rezerv va minimal qoldiq bilan)
                var rows = from s in db.Stocks
                           join p in db.Products on s.ProductID equals p.ID
                           where !p.IsDeleted
                           select new
                           {
                               s.WHID, s.ProductID, p.PNo, p.Name, p.CategoryID, OnHand = s.Amount,
                               NameRu = db.ProductProfiles.Where(pf => pf.ProductID == p.ID).Select(pf => pf.NameRu).FirstOrDefault(),
                               Reserved = db.StockReservations.Where(r => r.WarehouseID == s.WHID && r.ProductID == s.ProductID).Sum(r => (decimal?)r.QtyBase) ?? 0,
                               MinQty = db.ProductStockLimits.Where(l => l.WarehouseID == s.WHID && l.ProductID == s.ProductID).Select(l => (decimal?)l.MinQty).FirstOrDefault(),
                               Unit = db.Units.Where(u => u.ID == p.UnitID).Select(u => u.ShortName).FirstOrDefault()
                           };
                rows = whScope.Apply(rows, x => x.WHID);
                if (warehouseId.HasValue) rows = rows.Where(x => x.WHID == warehouseId);
                if (categoryId.HasValue) rows = rows.Where(x => x.CategoryID == categoryId);

                // barcha omborlar jami (ombor tanlanmasa) — tovar bo'yicha guruh
                var q = warehouseId.HasValue
                    ? rows.Select(x => new { x.ProductID, x.PNo, x.Name, x.NameRu, x.Unit, x.OnHand, x.Reserved, MinQty = x.MinQty })
                    : rows.GroupBy(x => new { x.ProductID, x.PNo, x.Name, x.NameRu, x.Unit })
                        .Select(g => new
                        {
                            g.Key.ProductID, g.Key.PNo, g.Key.Name, g.Key.NameRu, g.Key.Unit,
                            OnHand = g.Sum(x => x.OnHand), Reserved = g.Sum(x => x.Reserved), MinQty = g.Sum(x => x.MinQty)
                        });
                if (filter == "low") q = q.Where(x => x.MinQty != null && x.OnHand - x.Reserved < x.MinQty);
                if (filter == "nonzero") q = q.Where(x => x.OnHand != 0 || x.Reserved != 0);
                if (filter == "short") q = q.Where(x => x.OnHand - x.Reserved < 0);

                var result = await ServerTablePage.LoadAsync(q, page,
                    (x, s) => x.Where(r => r.PNo.Contains(s) || r.Name.Contains(s)),
                    (x, column, desc) =>
                    {
                        switch (column)
                        {
                            case 0: return ServerTablePage.By(x, r => r.PNo, desc).ThenBy(r => r.ProductID);
                            case 2: return ServerTablePage.By(x, r => r.OnHand, desc).ThenBy(r => r.ProductID);
                            case 3: return ServerTablePage.By(x, r => r.Reserved, desc).ThenBy(r => r.ProductID);
                            case 4: return ServerTablePage.By(x, r => r.OnHand - r.Reserved, desc).ThenBy(r => r.ProductID);
                            default: return ServerTablePage.By(x, r => r.Name, desc).ThenBy(r => r.ProductID);
                        }
                    });

                return Json(new
                {
                    draw = page.Draw,
                    recordsTotal = result.Total,
                    recordsFiltered = result.Filtered,
                    data = result.Rows.Select(r => new
                    {
                        productId = r.ProductID, pno = r.PNo, name = Localized(r.Name, r.NameRu), unit = r.Unit,
                        onHand = r.OnHand, reserved = r.Reserved, available = r.OnHand - r.Reserved, min = r.MinQty,
                        low = r.MinQty.HasValue && r.OnHand - r.Reserved < r.MinQty
                    })
                }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}
