using System;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Orders;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>Aksiyalar: hajmga qarab / muddatli chegirma va N+M sovg'a (3-bosqich). Buyurtmada avtomatik qo'llanadi.</summary>
    public class DstPromotionsController : DstControllerBase
    {
        public sealed class Row
        {
            public Promotion Promo { get; set; }
            public string Product { get; set; }
            public string FreeProduct { get; set; }
            public string Category { get; set; }
            public string PriceType { get; set; }
            public int Used { get; set; }
        }

        public ActionResult Index(bool all = false)
        {
            using (var db = NewDb())
            {
                var today = DateTime.Today;
                var q = db.Promotions.AsQueryable();
                if (!all) q = q.Where(p => p.IsActive && (p.DateTo == null || p.DateTo >= today));
                ViewBag.Rows = (from p in q
                                orderby p.DateFrom descending, p.ID descending
                                select new Row
                                {
                                    Promo = p,
                                    Product = db.Products.Where(x => x.ID == p.ProductID).Select(x => x.PNo + " — " + x.Name).FirstOrDefault(),
                                    FreeProduct = db.Products.Where(x => x.ID == p.FreeProductID).Select(x => x.PNo + " — " + x.Name).FirstOrDefault(),
                                    Category = db.Categories.Where(c => c.ID == p.CategoryID).Select(c => c.Name).FirstOrDefault(),
                                    PriceType = db.PriceTypes.Where(t => t.ID == p.PriceTypeID).Select(t => t.Name).FirstOrDefault(),
                                    Used = db.OrderLines.Count(l => l.PromotionID == p.ID)
                                }).Take(500).ToList();
                ViewBag.All = all;
                return View();
            }
        }

        public ActionResult Edit(int? id)
        {
            using (var db = NewDb())
            {
                Promotion p = id.HasValue ? db.Promotions.Find(id.Value) : new Promotion { Kind = PromotionKinds.Discount, DateFrom = DateTime.Today, IsActive = true, Repeatable = true };
                if (p == null) return HttpNotFound();
                ViewBag.Promo = p;
                ViewBag.ProductText = p.ProductID.HasValue ? db.Products.Where(x => x.ID == p.ProductID).Select(x => x.PNo + " — " + x.Name).FirstOrDefault() : null;
                ViewBag.FreeText = p.FreeProductID.HasValue ? db.Products.Where(x => x.ID == p.FreeProductID).Select(x => x.PNo + " — " + x.Name).FirstOrDefault() : null;
                ViewBag.Categories = db.Categories.Where(c => c.IsDeleted != true && c.IsActive).OrderBy(c => c.Name).ToList()
                    .Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.Name, Selected = c.ID == p.CategoryID }).ToList();
                ViewBag.PriceTypes = db.PriceTypes.Where(t => t.IsActive).OrderBy(t => t.Name).ToList()
                    .Select(t => new SelectListItem { Value = t.ID.ToString(), Text = t.Name, Selected = t.ID == p.PriceTypeID }).ToList();
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Save(int? id, string name, string kind, DateTime dateFrom, DateTime? dateTo, bool isActive, int? productId, int? categoryId,
            int? priceTypeId, string minQty, string discountPct, string buyQty, string freeQty, int? freeProductId, bool repeatable, string note)
        {
            try
            {
                using (var db = NewDb())
                {
                    var p = id.HasValue ? db.Promotions.Find(id.Value) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound")) : new Promotion { CreatedBy = CurrentUserId, CreatedAt = DateTime.Now };
                    p.Name = name?.Trim();
                    p.Kind = kind;
                    p.DateFrom = dateFrom.Date;
                    p.DateTo = dateTo?.Date;
                    p.IsActive = isActive;
                    p.ProductID = productId;
                    p.CategoryID = categoryId;
                    p.PriceTypeID = priceTypeId;
                    p.MinQtyBase = Num(minQty) ?? 0;
                    p.DiscountPct = Num(discountPct);
                    p.BuyQtyBase = Num(buyQty);
                    p.FreeQtyBase = Num(freeQty);
                    p.FreeProductID = freeProductId;
                    p.Repeatable = repeatable;
                    p.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
                    if (p.Name != null && p.Name.Length > 150) p.Name = p.Name.Substring(0, 150);
                    if (p.Note != null && p.Note.Length > 300) p.Note = p.Note.Substring(0, 300);
                    PromotionService.Validate(db, p);
                    if (id.HasValue) p.UpdatedAt = DateTime.Now;
                    else db.Promotions.Add(p);
                    db.SaveChanges();
                }
                TempData["DstSaved"] = DstLocalization.T("Saved");
                return RedirectToAction("Index");
            }
            catch (DstValidationException ex)
            {
                TempData["DstError"] = ex.Message;
                return RedirectToAction("Edit", new { id });
            }
        }

        /// <summary>Faollik: o'chirish o'rniga to'xtatish (buyurtmalarda iz qoladi).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateActive(int id, bool isActive)
        {
            using (var db = NewDb())
            {
                var p = db.Promotions.Find(id);
                if (p == null) return HttpNotFound();
                p.IsActive = isActive;
                p.UpdatedAt = DateTime.Now;
                db.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        [HttpGet]
        public JsonResult SearchProducts(string q) => ProductSearch(q, null);

        private static decimal? Num(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return DstModelBinder.TryParseDecimal(s, out var d) ? d : throw new DstValidationException(DstLocalization.T("Error_NumberFormat") + ": " + s);
        }
    }
}
