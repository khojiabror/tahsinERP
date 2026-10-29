using System;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Filters;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>
    /// Savdo sozlamalari: rol bo'yicha chegirma limiti, omborlar, minimal qoldiqlar; dala (agent) sozlamalari faqat Distribution
    /// paketda ko'rinadi. Telegram bot nomi va manzili — Sotuv kanallari (DstChannels).
    /// </summary>
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
            public decimal? MaxQty { get; set; }
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
                ViewBag.FieldEnabled = tahsinERP.Services.Editions.EditionGate.IsControllerAllowed("DstField");
                ViewBag.General = db.Settings.ToList().ToDictionary(x => x.Key, x => x.Value);
                ViewBag.Accounts = db.FinAccounts.Where(a => a.IsActive && a.IsDeleted != true && (a.IsCash || a.IsBank)).OrderBy(a => a.Name).ToList()
                    .Select(a => new SelectListItem { Value = a.ID.ToString(), Text = a.Name }).ToList();
                ViewBag.Limits = warehouseId == null
                    ? new System.Collections.Generic.List<StockLimitRow>()
                    : (from l in db.ProductStockLimits
                       where l.WarehouseID == warehouseId
                       join p in db.Products on l.ProductID equals p.ID
                       orderby p.Name
                       select new StockLimitRow
                       {
                           WarehouseID = l.WarehouseID, ProductID = l.ProductID, Product = p.PNo + " — " + p.Name, MinQty = l.MinQty, MaxQty = l.MaxQty,
                           Unit = db.Units.Where(u => u.ID == p.UnitID).Select(u => u.ShortName).FirstOrDefault()
                       }).ToList();
                return View();
            }
        }

        /// <summary>Umumiy sozlamalar (dst.SETTINGS).</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SaveGeneral(decimal? minOrderAmount, decimal? largeOrderAmount, int? visitRadiusM, int? agentCashAccountId,
            bool requireSelfie = false, bool checkInLiveOnly = false, int? defectWarehouseId = null)
        {
            return DstJson(() =>
            {
                if (minOrderAmount < 0 || largeOrderAmount < 0 || visitRadiusM < 20 || visitRadiusM > 5000)
                    throw new DstValidationException(DstLocalization.T("Error_NumberFormat"));
                using (var db = NewDb())
                {
                    if (agentCashAccountId.HasValue && !db.FinAccounts.Any(a => a.ID == agentCashAccountId && a.IsActive && a.IsDeleted != true))
                        throw new DstValidationException(DstLocalization.T("Payment_AccountRequired"));
                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    Services.Bot.DstSettings.Set(db, DstSetting.MinOrderAmount, (minOrderAmount ?? 0).ToString(inv));
                    Services.Bot.DstSettings.Set(db, DstSetting.LargeOrderAmount, (largeOrderAmount ?? 0).ToString(inv));
                    Services.Bot.DstSettings.Set(db, DstSetting.VisitRadiusM, (visitRadiusM ?? 200).ToString(inv));
                    Services.Bot.DstSettings.Set(db, DstSetting.AgentCashAccountId, agentCashAccountId?.ToString(inv));
                    Services.Bot.DstSettings.Set(db, DstSetting.RequireSelfie, requireSelfie ? "1" : "0");
                    Services.Bot.DstSettings.Set(db, DstSetting.CheckInLiveOnly, checkInLiveOnly ? "1" : "0");
                    if (defectWarehouseId.HasValue && !db.Warehouses.Any(w => w.ID == defectWarehouseId && w.IsDeleted != true))
                        throw new DstValidationException(DstLocalization.T("Order_NoWarehouse"));
                    Services.Bot.DstSettings.Set(db, DstSetting.DefectWarehouseId, defectWarehouseId?.ToString(inv));
                    db.SaveChanges();
                    return null;
                }
            });
        }

        /// <summary>Agent to'lov usullari: qaysi usul ruxsat, naqdsiz usul — qaysi hisobga tushadi (bo'sh — usul ishlatilmaydi).</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SaveAgentPayments(string[] methods, int? accountCard, int? accountClick, int? accountPayme, int? accountUzum, int? accountTransfer)
        {
            return DstJson(() =>
            {
                var chosen = (methods ?? new string[0]).Select(m => (m ?? "").Trim().ToUpperInvariant()).Where(m => PaymentMethods.All.Contains(m)).Distinct().ToList();
                var accounts = new System.Collections.Generic.Dictionary<string, int?>
                {
                    { PaymentMethods.Card, accountCard }, { PaymentMethods.Click, accountClick }, { PaymentMethods.Payme, accountPayme },
                    { PaymentMethods.Uzum, accountUzum }, { PaymentMethods.Transfer, accountTransfer }
                };
                using (var db = NewDb())
                {
                    foreach (var a in accounts)
                    {
                        if (a.Value.HasValue && !db.FinAccounts.Any(f => f.ID == a.Value && f.IsActive && f.IsDeleted != true))
                            throw new DstValidationException(DstLocalization.T("Payment_AccountRequired"));
                        if (chosen.Contains(a.Key) && !a.Value.HasValue)
                            throw new DstValidationException(DstLocalization.T("Payment_MethodNoAccount") + " (" + DstLocalization.T("PayMethod_" + a.Key) + ")");
                        Services.Bot.DstSettings.Set(db, Services.Money.AgentPayments.AccountKey(a.Key), a.Value?.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    Services.Bot.DstSettings.Set(db, DstSetting.AgentPayMethods, string.Join(",", chosen));
                    db.SaveChanges();
                    return null;
                }
            });
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
        /// <summary>
        /// Ombor sozlamasi — faqat "savdo nuqtasi". "Minusga ruxsat" va "Yetkazish zonasi" hech qayerda ishlatilmasdi (FIFO minusga
        /// ruxsat bermaydi, zona reyslarda yo'q) — UI'dan olib tashlandi, ustunlar bazada qoladi (05_FINDINGS K3-30).
        /// </summary>
        public JsonResult SaveWarehouse(int warehouseId, bool isSalesPoint)
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
                    db.SaveChanges();
                    return null;
                }
            });
        }

        /// <summary>Minimal qoldiq (asosiy birlikda); 0 yoki bo'sh — limit olib tashlanadi.</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SaveStockLimit(int warehouseId, int productId, decimal? minQty, decimal? maxQty)
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
                        if (maxQty.HasValue && maxQty > 0 && maxQty < minQty) throw new DstValidationException(DstLocalization.T("MinStock_MaxBelowMin"));
                        row.MinQty = minQty.Value;
                        row.MaxQty = maxQty > 0 ? maxQty : null;
                    }
                    db.SaveChanges();
                    return null;
                }
            });
        }
    }
}
