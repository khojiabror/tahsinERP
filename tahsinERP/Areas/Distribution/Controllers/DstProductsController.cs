using System;
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
    /// Tovarlarning distributsiya kartasi: profil, qo'shimcha o'lchov birliklari, shtrix-kodlar.
    /// Tovarning o'zi (PNo, nom, asosiy birlik, kategoriya) — mavjud Product modulida yoki Excel importida.
    /// </summary>
    public class DstProductsController : DstControllerBase
    {
        [ModelBinder(typeof(DstModelBinder))]
        public class ProfileInput
        {
            public int ProductID { get; set; }
            public string NameRu { get; set; }
            public int? BrandID { get; set; }
            public int? ManufacturerID { get; set; }
            public int? SubCategoryID { get; set; }
            public bool IsSaleActive { get; set; }
            public bool IsBulky { get; set; }
            public bool IsWeighted { get; set; }
            public decimal? BaseWeightKg { get; set; }
            public decimal? BaseVolumeM3 { get; set; }
        }

        public ActionResult Index()
        {
            using (var db = NewDb())
            {
                FillLookups(db, null);
                return View();
            }
        }

        [HttpGet]
        public async Task<ActionResult> IndexData(int? categoryId, int? brandId, string sale)
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
                        select new
                        {
                            p.ID, p.PNo, p.Name, p.CategoryID, NameRu = pf.NameRu, BrandID = (int?)pf.BrandID,
                            HasProfile = pf != null, IsSaleActive = pf != null && pf.IsSaleActive,
                            Category = db.Categories.Where(c => c.ID == p.CategoryID).Select(c => c.Name).FirstOrDefault(),
                            Unit = db.Units.Where(u => u.ID == p.UnitID).Select(u => u.ShortName).FirstOrDefault(),
                            Brand = db.Brands.Where(b => b.ID == pf.BrandID).Select(b => b.Name).FirstOrDefault(),
                            Units = db.ProductUnits.Count(u => u.ProductID == p.ID && u.IsActive),
                            Barcodes = db.ProductBarcodes.Count(b => b.ProductID == p.ID),
                            Price = db.PriceListItems
                                .Where(x => x.ProductID == p.ID && x.PriceTypeID == defaultType && x.ValidFrom <= now)
                                .OrderByDescending(x => x.ValidFrom).ThenByDescending(x => x.ID)
                                .Select(x => (decimal?)x.Price).FirstOrDefault()
                        };

                if (categoryId.HasValue) q = q.Where(x => x.CategoryID == categoryId);
                if (brandId.HasValue) q = q.Where(x => x.BrandID == brandId);
                if (sale == "yes") q = q.Where(x => x.IsSaleActive);
                if (sale == "no") q = q.Where(x => !x.IsSaleActive);

                var result = await ServerTablePage.LoadAsync(q, page,
                    (x, s) => x.Where(r => r.PNo.Contains(s) || r.Name.Contains(s) || r.NameRu.Contains(s)
                                           || db.ProductBarcodes.Any(b => b.ProductID == r.ID && b.Barcode == s)),
                    (x, column, desc) =>
                    {
                        switch (column)
                        {
                            case 0: return ServerTablePage.By(x, r => r.PNo, desc).ThenBy(r => r.ID);
                            case 2: return ServerTablePage.By(x, r => r.Category, desc).ThenBy(r => r.ID);
                            case 3: return ServerTablePage.By(x, r => r.Brand, desc).ThenBy(r => r.ID);
                            case 6: return ServerTablePage.By(x, r => r.Price, desc).ThenBy(r => r.ID);
                            default: return ServerTablePage.By(x, r => r.Name, desc).ThenBy(r => r.ID);
                        }
                    });

                return Json(new
                {
                    draw = page.Draw,
                    recordsTotal = result.Total,
                    recordsFiltered = result.Filtered,
                    data = result.Rows.Select(r => new
                    {
                        id = r.ID, pno = r.PNo, name = r.Name, nameRu = r.NameRu, category = r.Category, brand = r.Brand,
                        unit = r.Unit, units = r.Units, barcodes = r.Barcodes, price = r.Price,
                        hasProfile = r.HasProfile, sale = r.IsSaleActive
                    })
                }, JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult Edit(int id)
        {
            using (var db = NewDb())
            {
                var product = db.Products.FirstOrDefault(p => p.ID == id && !p.IsDeleted);
                if (product == null) return HttpNotFound();

                var profile = db.ProductProfiles.Find(id);
                ViewBag.Product = product;
                ViewBag.BaseUnit = db.Units.Where(u => u.ID == product.UnitID).Select(u => u.UnitName).FirstOrDefault();
                ViewBag.Category = db.Categories.Where(c => c.ID == product.CategoryID).Select(c => c.Name).FirstOrDefault();
                ViewBag.Units = new UnitService(db).UnitsOf(id);
                ViewBag.Barcodes = (from b in db.ProductBarcodes
                                    where b.ProductID == id
                                    join pu in db.ProductUnits on b.ProductUnitID equals pu.ID into puj
                                    from pu in puj.DefaultIfEmpty()
                                    orderby b.ID
                                    select new BarcodeRow
                                    {
                                        ID = b.ID, Barcode = b.Barcode,
                                        Unit = pu == null ? null : db.Units.Where(u => u.ID == pu.UnitID).Select(u => u.ShortName).FirstOrDefault()
                                    }).ToList();
                ViewBag.Prices = new PricingService(db).CurrentByType(id, DateTime.Now)
                    .Select(x => new PriceRow { PriceType = Localized(x.Type.Name, x.Type.NameRu), Price = x.Item?.Price, MinPrice = x.Item?.MinPrice })
                    .ToList();
                FillLookups(db, product.CategoryID);

                return View(new ProfileInput
                {
                    ProductID = id,
                    NameRu = profile?.NameRu,
                    BrandID = profile?.BrandID,
                    ManufacturerID = profile?.ManufacturerID,
                    SubCategoryID = profile?.SubCategoryID,
                    IsSaleActive = profile?.IsSaleActive ?? true,
                    IsBulky = profile?.IsBulky ?? false,
                    IsWeighted = profile?.IsWeighted ?? false,
                    BaseWeightKg = profile?.BaseWeightKg,
                    BaseVolumeM3 = profile?.BaseVolumeM3
                });
            }
        }

        public sealed class BarcodeRow
        {
            public int ID { get; set; }
            public string Barcode { get; set; }
            public string Unit { get; set; }
        }

        public sealed class PriceRow
        {
            public string PriceType { get; set; }
            public decimal? Price { get; set; }
            public decimal? MinPrice { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(ProfileInput input)
        {
            try
            {
                using (var db = NewDb())
                {
                    if (!db.Products.Any(p => p.ID == input.ProductID && !p.IsDeleted)) return HttpNotFound();
                    if (input.BaseWeightKg < 0 || input.BaseVolumeM3 < 0)
                        throw new DstValidationException(DstLocalization.T("Error_NumberFormat"));

                    var profile = db.ProductProfiles.Find(input.ProductID);
                    if (profile == null)
                        db.ProductProfiles.Add(profile = new ProductProfile { ProductID = input.ProductID });

                    profile.NameRu = string.IsNullOrWhiteSpace(input.NameRu) ? null : input.NameRu.Trim();
                    profile.BrandID = input.BrandID;
                    profile.ManufacturerID = input.ManufacturerID;
                    profile.SubCategoryID = input.SubCategoryID;
                    profile.IsSaleActive = input.IsSaleActive;
                    profile.IsBulky = input.IsBulky;
                    profile.IsWeighted = input.IsWeighted;
                    profile.BaseWeightKg = input.BaseWeightKg;
                    profile.BaseVolumeM3 = input.BaseVolumeM3;
                    db.SaveChanges();
                }
                TempData["DstSaved"] = true;
            }
            catch (DstValidationException ex)
            {
                TempData["DstError"] = ex.Message;
            }
            return RedirectToAction("Edit", new { id = input.ProductID });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SaveUnit(int productId, int? id, int unitId, decimal factor, decimal? weightKg, decimal? volumeM3, bool isDefaultSale)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    var baseUnit = db.Products.Where(p => p.ID == productId && !p.IsDeleted).Select(p => (int?)p.UnitID).FirstOrDefault()
                                   ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    var others = db.ProductUnits.Where(u => u.ProductID == productId && u.ID != (id ?? 0)).Select(u => u.UnitID).ToList();
                    UnitService.Validate(baseUnit, unitId, factor, others);

                    var row = id.HasValue
                        ? db.ProductUnits.FirstOrDefault(u => u.ID == id && u.ProductID == productId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"))
                        : db.ProductUnits.Add(new ProductUnit { ProductID = productId });
                    row.UnitID = unitId;
                    row.Factor = factor;
                    row.WeightKg = weightKg;
                    row.VolumeM3 = volumeM3;
                    row.IsDefaultSale = isDefaultSale;
                    row.IsActive = true;
                    if (isDefaultSale)
                        foreach (var other in db.ProductUnits.Where(u => u.ProductID == productId && u.ID != (id ?? 0) && u.IsDefaultSale))
                            other.IsDefaultSale = false;
                    db.SaveChanges();
                    return null;
                }
            });
        }

        /// <summary>Birlik shtrix-kod yoki hujjatlarda ishlatilgan bo'lishi mumkin — o'chirilmaydi, nofaol qilinadi.</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult DeactivateUnit(int id)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    var row = db.ProductUnits.Find(id) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    row.IsActive = false;
                    row.IsDefaultSale = false;
                    db.SaveChanges();
                    return null;
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult AddBarcode(int productId, string barcode, int? productUnitId)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    if (!db.Products.Any(p => p.ID == productId && !p.IsDeleted))
                        throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    var code = DirectoryRules.NormalizeBarcode(barcode);
                    var owner = db.ProductBarcodes.Where(b => b.Barcode == code).Select(b => (int?)b.ProductID).FirstOrDefault();
                    if (owner.HasValue)
                        throw new DstValidationException(DstLocalization.T("Error_BarcodeTaken", owner.Value));
                    if (productUnitId.HasValue && !db.ProductUnits.Any(u => u.ID == productUnitId && u.ProductID == productId))
                        throw new DstValidationException(DstLocalization.T("Error_NotFound"));

                    db.ProductBarcodes.Add(new ProductBarcode { ProductID = productId, Barcode = code, ProductUnitID = productUnitId });
                    db.SaveChanges();
                    return null;
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult RemoveBarcode(int id)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    var row = db.ProductBarcodes.Find(id) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    db.ProductBarcodes.Remove(row);
                    db.SaveChanges();
                    return null;
                }
            });
        }

        private void FillLookups(DistributionDbContext db, int? categoryId)
        {
            ViewBag.Categories = db.Categories.Where(c => c.IsDeleted != true).OrderBy(c => c.Name).ToList()
                .Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.Name }).ToList();
            ViewBag.Brands = db.Brands.Where(b => b.IsActive).OrderBy(b => b.Name).ToList()
                .Select(b => new SelectListItem { Value = b.ID.ToString(), Text = b.Name }).ToList();
            ViewBag.Manufacturers = db.Manufacturers.Where(b => b.IsActive).OrderBy(b => b.Name).ToList()
                .Select(b => new SelectListItem { Value = b.ID.ToString(), Text = b.Name }).ToList();
            ViewBag.SubCategories = db.SubCategories.Where(s => s.IsActive && (categoryId == null || s.CategoryID == categoryId))
                .OrderBy(s => s.Name).ToList()
                .Select(s => new SelectListItem { Value = s.ID.ToString(), Text = Localized(s.Name, s.NameRu) }).ToList();
            ViewBag.AllUnits = db.Units.Where(u => u.IsDeleted != true).OrderBy(u => u.UnitName).ToList()
                .Select(u => new SelectListItem { Value = u.ID.ToString(), Text = u.UnitName + " (" + u.ShortName + ")" }).ToList();
        }
    }
}
