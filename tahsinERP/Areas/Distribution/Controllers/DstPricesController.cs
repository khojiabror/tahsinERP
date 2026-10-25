using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Filters;
using tahsinERP.Services.Procurement;
using tahsinERP.ViewModels;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>
    /// Narx varag'i: tovar × narx turi (asosiy birlik uchun, so'm). Har o'zgarish — yangi qator (tarix).
    /// Ommaviy o'zgartirish kategoriya bo'yicha %, mijozning maxsus narxlari.
    /// </summary>
    public class DstPricesController : DstControllerBase
    {
        public ActionResult Index(int? productId)
        {
            using (var db = NewDb())
            {
                ViewBag.PriceTypes = db.PriceTypes.Where(t => t.IsActive).OrderBy(t => t.SortOrder).ToList();
                ViewBag.Categories = db.Categories.Where(c => c.IsDeleted != true).OrderBy(c => c.Name).ToList()
                    .Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.Name }).ToList();
                ViewBag.ProductId = productId;
                ViewBag.ProductName = productId.HasValue
                    ? db.Products.Where(p => p.ID == productId).Select(p => p.PNo + " — " + p.Name).FirstOrDefault()
                    : null;
                return View();
            }
        }

        [HttpGet]
        public async Task<ActionResult> IndexData(int? categoryId, int? productId, string missing)
        {
            var page = DataTablesPageRequest.Parse(Request, defaultOrderColumn: 1, defaultDescending: false);
            using (var db = NewDb())
            {
                var defaultType = db.PriceTypes.Where(t => t.IsDefault).Select(t => (int?)t.ID).FirstOrDefault();
                var now = DateTime.Now;
                var q = from p in db.Products
                        where !p.IsDeleted
                        join pf in db.ProductProfiles on p.ID equals pf.ProductID into pfj
                        from pf in pfj.DefaultIfEmpty()
                        where pf == null || pf.IsSaleActive
                        select new
                        {
                            p.ID, p.PNo, p.Name, p.CategoryID, NameRu = pf != null ? pf.NameRu : null,
                            Unit = db.Units.Where(u => u.ID == p.UnitID).Select(u => u.ShortName).FirstOrDefault(),
                            HasDefault = db.PriceListItems.Any(x => x.ProductID == p.ID && x.PriceTypeID == defaultType && x.ValidFrom <= now)
                        };
                if (categoryId.HasValue) q = q.Where(x => x.CategoryID == categoryId);
                if (productId.HasValue) q = q.Where(x => x.ID == productId);
                if (missing == "yes") q = q.Where(x => !x.HasDefault);

                var result = await ServerTablePage.LoadAsync(q, page,
                    (x, s) => x.Where(r => r.PNo.Contains(s) || r.Name.Contains(s)),
                    (x, column, desc) => column == 0
                        ? ServerTablePage.By(x, r => r.PNo, desc).ThenBy(r => r.ID)
                        : ServerTablePage.By(x, r => r.Name, desc).ThenBy(r => r.ID));

                // Sahifadagi tovarlar narxlari bitta so'rovda
                var ids = result.Rows.Select(r => r.ID).ToList();
                var current = db.PriceListItems.AsNoTracking()
                    .Where(x => ids.Contains(x.ProductID) && x.ValidFrom <= now)
                    .ToList()
                    .GroupBy(x => new { x.ProductID, x.PriceTypeID })
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ValidFrom).ThenByDescending(x => x.ID).First());

                return Json(new
                {
                    draw = page.Draw,
                    recordsTotal = result.Total,
                    recordsFiltered = result.Filtered,
                    data = result.Rows.Select(r => new
                    {
                        id = r.ID, pno = r.PNo, name = Localized(r.Name, r.NameRu), unit = r.Unit,
                        prices = current.Where(c => c.Key.ProductID == r.ID)
                            .ToDictionary(c => c.Key.PriceTypeID.ToString(), c => new { price = c.Value.Price, min = c.Value.MinPrice })
                    })
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SetPrice(int productId, int priceTypeId, decimal price, decimal? minPrice)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    if (!db.Products.Any(p => p.ID == productId && !p.IsDeleted) || !db.PriceTypes.Any(t => t.ID == priceTypeId && t.IsActive))
                        throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    var item = new PricingService(db).SetPrice(priceTypeId, productId, price, minPrice, CurrentUserId, DateTime.Now);
                    db.SaveChanges();
                    return new { changed = item != null };
                }
            });
        }

        [HttpGet]
        public JsonResult History(int productId)
        {
            using (var db = NewDb())
            {
                var rows = (from x in db.PriceListItems
                            where x.ProductID == productId
                            join t in db.PriceTypes on x.PriceTypeID equals t.ID
                            orderby x.ValidFrom descending, x.ID descending
                            select new
                            {
                                x.ValidFrom, Type = t.Name, TypeRu = t.NameRu, x.Price, x.MinPrice,
                                User = db.Users.Where(u => u.ID == x.CreatedBy).Select(u => u.FullName ?? u.Uname).FirstOrDefault()
                            }).Take(200).ToList();
                return Json(rows.Select(r => new
                {
                    date = r.ValidFrom.ToString("dd.MM.yyyy HH:mm"), type = Localized(r.Type, r.TypeRu),
                    price = r.Price, min = r.MinPrice, user = r.User
                }), JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Ommaviy o'zgartirish: tanlangan narx turida (va kategoriyada) joriy narxi bor tovarlarga +/- %.
        /// Minimal narx ham shu foizda o'zgaradi. Hammasi bitta tranzaksiyada.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult MassChange(int priceTypeId, int? categoryId, decimal percent, decimal roundTo)
        {
            return DstJson(() =>
            {
                if (percent == 0) throw new DstValidationException(DstLocalization.T("Error_PercentRange"));
                using (var db = NewDb())
                using (var tx = db.Database.BeginTransaction())
                {
                    var now = DateTime.Now;
                    var productIds = db.Products
                        .Where(p => !p.IsDeleted && (categoryId == null || p.CategoryID == categoryId))
                        .Select(p => p.ID).ToList();
                    var latest = db.PriceListItems.AsNoTracking()
                        .Where(x => x.PriceTypeID == priceTypeId && productIds.Contains(x.ProductID) && x.ValidFrom <= now)
                        .ToList()
                        .GroupBy(x => x.ProductID)
                        .Select(g => g.OrderByDescending(x => x.ValidFrom).ThenByDescending(x => x.ID).First())
                        .ToList();

                    var pricing = new PricingService(db);
                    int changed = 0;
                    foreach (var item in latest)
                    {
                        var price = PriceResolver.ApplyPercent(item.Price, percent, roundTo);
                        var min = item.MinPrice.HasValue ? (decimal?)Math.Min(price, PriceResolver.ApplyPercent(item.MinPrice.Value, percent, roundTo)) : null;
                        if (pricing.SetPrice(priceTypeId, item.ProductID, price, min, CurrentUserId, now) != null)
                            changed++;
                    }
                    db.SaveChanges();
                    tx.Commit();
                    return new { changed };
                }
            });
        }

        // ---------- mijozning maxsus narxlari ----------

        public ActionResult Customer(int id)
        {
            using (var db = NewDb())
            {
                var customer = db.Customers.FirstOrDefault(c => c.ID == id && c.IsDeleted != true);
                if (customer == null) return HttpNotFound();
                ViewBag.Customer = customer;
                var rows = (from cp in db.CustomerPrices
                            where cp.CustomerID == id
                            join p in db.Products on cp.ProductID equals p.ID
                            orderby p.Name, cp.ValidFrom descending
                            select new CustomerPriceRow
                            {
                                ID = cp.ID, Product = p.PNo + " — " + p.Name, Price = cp.Price, ValidFrom = cp.ValidFrom, ValidTo = cp.ValidTo
                            }).ToList();
                return View(rows);
            }
        }

        public sealed class CustomerPriceRow
        {
            public int ID { get; set; }
            public string Product { get; set; }
            public decimal Price { get; set; }
            public DateTime ValidFrom { get; set; }
            public DateTime? ValidTo { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult AddCustomerPrice(int customerId, int productId, decimal price, DateTime validFrom, DateTime? validTo)
        {
            return DstJson(() =>
            {
                if (price < 0) throw new DstValidationException(DstLocalization.T("Error_PriceNegative"));
                if (validTo.HasValue && validTo < validFrom) throw new DstValidationException(DstLocalization.T("Error_DateRange"));
                using (var db = NewDb())
                {
                    if (!db.Customers.Any(c => c.ID == customerId && c.IsDeleted != true) || !db.Products.Any(p => p.ID == productId && !p.IsDeleted))
                        throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    db.CustomerPrices.Add(new CustomerPrice
                    {
                        CustomerID = customerId, ProductID = productId, Price = price, ValidFrom = validFrom.Date,
                        ValidTo = validTo?.Date, CreatedBy = CurrentUserId, CreatedAt = DateTime.Now
                    });
                    db.SaveChanges();
                    return null;
                }
            });
        }

        /// <summary>
        /// Maxsus narxni to'xtatish: kechagidan oldin boshlangani kechagi sana bilan yopiladi (narxlar tarixida qoladi); bugun yoki kelajakda
        /// boshlangani o'chiriladi (sana oralig'i bo'sh bo'lib qolardi) — o'chirish audit jurnalida qoladi, buyurtmalar o'z narxini saqlaydi (05_FINDINGS 47).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult CloseCustomerPrice(int id)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    var row = db.CustomerPrices.Find(id) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    var yesterday = DateTime.Today.AddDays(-1);
                    if (row.ValidFrom > yesterday)
                        db.CustomerPrices.Remove(row);       // hali kuchga kirmagan — o'chiriladi
                    else if (row.ValidTo == null || row.ValidTo > yesterday)
                        row.ValidTo = yesterday;
                    db.SaveChanges();
                    return null;
                }
            });
        }

        [HttpGet]
        public JsonResult SearchProducts(string q)
        {
            using (var db = NewDb())
            {
                var rows = db.Products.Where(p => !p.IsDeleted && (q == null || p.PNo.Contains(q) || p.Name.Contains(q)))
                    .OrderBy(p => p.Name).Take(30)
                    .Select(p => new { p.ID, p.PNo, p.Name, NameRu = db.ProductProfiles.Where(pf => pf.ProductID == p.ID).Select(pf => pf.NameRu).FirstOrDefault() })
                    .ToList().Select(p => new { id = p.ID, text = p.PNo + " — " + Localized(p.Name, p.NameRu) }).ToList();
                return Json(new { results = rows }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}
