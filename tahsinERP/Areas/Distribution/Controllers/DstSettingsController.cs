using System.Linq;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Filters;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>Distributsiya sozlamalari: rol bo'yicha chegirma limiti, omborlar, minimal qoldiqlar.</summary>
    public class DstSettingsController : DstControllerBase
    {
        public sealed class RoleLimitRow
        {
            public int RoleID { get; set; }
            public string Role { get; set; }
            public decimal? MaxPercent { get; set; }
        }

        public sealed class WarehouseRow
        {
            public int WarehouseID { get; set; }
            public string Name { get; set; }
            public bool Configured { get; set; }
            public bool IsSalesPoint { get; set; }
            public bool AllowNegative { get; set; }
            public string DeliveryZone { get; set; }
            public int MinLimits { get; set; }
        }

        public sealed class StockLimitRow
        {
            public int WarehouseID { get; set; }
            public string Warehouse { get; set; }
            public int ProductID { get; set; }
            public string Product { get; set; }
            public string Unit { get; set; }
            public decimal MinQty { get; set; }
        }

        public ActionResult Index(int? warehouseId)
        {
            using (var db = NewDb())
            {
                ViewBag.Roles = (from r in db.Roles
                                 where r.IsDeleted != true
                                 orderby r.RName
                                 select new RoleLimitRow
                                 {
                                     RoleID = r.ID, Role = r.RName,
                                     MaxPercent = db.DiscountLimits.Where(d => d.RoleID == r.ID).Select(d => (decimal?)d.MaxPercent).FirstOrDefault()
                                 }).ToList();
                ViewBag.Warehouses = (from w in db.Warehouses
                                      where w.IsDeleted != true
                                      join s in db.WarehouseSettings on w.ID equals s.WarehouseID into sj
                                      from s in sj.DefaultIfEmpty()
                                      orderby w.WHName
                                      select new WarehouseRow
                                      {
                                          WarehouseID = w.ID, Name = w.WHName, Configured = s != null,
                                          IsSalesPoint = s != null && s.IsSalesPoint, AllowNegative = s != null && s.AllowNegative,
                                          DeliveryZone = s.DeliveryZone,
                                          MinLimits = db.ProductStockLimits.Count(l => l.WarehouseID == w.ID)
                                      }).ToList();
                ViewBag.WarehouseId = warehouseId;
                ViewBag.Limits = warehouseId == null
                    ? new System.Collections.Generic.List<StockLimitRow>()
                    : (from l in db.ProductStockLimits
                       where l.WarehouseID == warehouseId
                       join p in db.Products on l.ProductID equals p.ID
                       orderby p.Name
                       select new StockLimitRow
                       {
                           WarehouseID = l.WarehouseID, ProductID = l.ProductID, Product = p.PNo + " — " + p.Name, MinQty = l.MinQty,
                           Unit = db.Units.Where(u => u.ID == p.UnitID).Select(u => u.ShortName).FirstOrDefault()
                       }).ToList();
                return View();
            }
        }

        /// <summary>Bo'sh qiymat — rol uchun limit yo'q (chegirma berolmaydi, faqat DstDiscountUnlimited huquqi bilan).</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SaveDiscountLimit(int roleId, decimal? maxPercent)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    if (!db.Roles.Any(r => r.ID == roleId)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    var row = db.DiscountLimits.Find(roleId);
                    if (maxPercent == null)
                    {
                        if (row != null) db.DiscountLimits.Remove(row);
                    }
                    else
                    {
                        DirectoryRules.ValidateDiscountPercent(maxPercent.Value);
                        if (row == null) db.DiscountLimits.Add(row = new DiscountLimit { RoleID = roleId });
                        row.MaxPercent = maxPercent.Value;
                    }
                    db.SaveChanges();
                    return null;
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SaveWarehouse(int warehouseId, bool isSalesPoint, bool allowNegative, string deliveryZone)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    if (!db.Warehouses.Any(w => w.ID == warehouseId && w.IsDeleted != true))
                        throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    var row = db.WarehouseSettings.Find(warehouseId);
                    if (row == null) db.WarehouseSettings.Add(row = new WarehouseSetting { WarehouseID = warehouseId });
                    row.IsSalesPoint = isSalesPoint;
                    row.AllowNegative = allowNegative;
                    row.DeliveryZone = string.IsNullOrWhiteSpace(deliveryZone) ? null : deliveryZone.Trim();
                    db.SaveChanges();
                    return null;
                }
            });
        }

        /// <summary>Minimal qoldiq (asosiy birlikda); 0 yoki bo'sh — limit olib tashlanadi.</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SaveStockLimit(int warehouseId, int productId, decimal? minQty)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    if (!db.Warehouses.Any(w => w.ID == warehouseId) || !db.Products.Any(p => p.ID == productId && !p.IsDeleted))
                        throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    var row = db.ProductStockLimits.Find(warehouseId, productId);
                    if (minQty == null || minQty <= 0)
                    {
                        if (row != null) db.ProductStockLimits.Remove(row);
                    }
                    else
                    {
                        if (row == null) db.ProductStockLimits.Add(row = new ProductStockLimit { WarehouseID = warehouseId, ProductID = productId });
                        row.MinQty = minQty.Value;
                    }
                    db.SaveChanges();
                    return null;
                }
            });
        }
    }
}
