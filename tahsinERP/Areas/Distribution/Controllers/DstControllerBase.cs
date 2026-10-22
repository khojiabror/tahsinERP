using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Controllers;
using tahsinERP.Filters;
using tahsinERP.Models;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>
    /// Distributsiya controllerlari uchun asos: server tomonidagi ruxsat ([Permission], controller/action
    /// bo'yicha), foydalanuvchi tili (uz/ru), maxsus huquqlar va umumiy yordamchilar.
    /// </summary>
    [Permission]
    public abstract class DstControllerBase : BaseController
    {
        // Model binding OnActionExecuting'dan OLDIN bo'ladi — madaniyat shu yerda qo'yiladi
        protected override IAsyncResult BeginExecuteCore(AsyncCallback callback, object state)
        {
            // keyin UiLanguageFilter shu asosga til bo'yicha oy/kun nomlarini qo'yadi
            Thread.CurrentThread.CurrentCulture = DstLocalization.FormCulture;
            return base.BeginExecuteCore(callback, state);
        }

        protected override void ExecuteCore()
        {
            Thread.CurrentThread.CurrentCulture = DstLocalization.FormCulture;
            base.ExecuteCore();
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // UI tili — global tahsinERP.I18n.UiLanguageFilter (butun ERP uchun)
            ViewBag.DstCanSeeCost = DstPermissions.Has(CurrentUserName, DstPermissions.SeeCost);
            ViewBag.DstCanChange = RoleHelper.IsPermittedForAction(CurrentUserName,
                filterContext.ActionDescriptor.ControllerDescriptor.ControllerName, "Index", true);
            base.OnActionExecuting(filterContext);
        }

        protected bool CanSeeCost => DstPermissions.Has(CurrentUserName, DstPermissions.SeeCost);

        protected bool IsRussian => tahsinERP.Localization.Languages.Current == tahsinERP.Localization.Languages.Russian;

        /// <summary>Joriy foydalanuvchi nomidan yozadigan distributsiya konteksti.</summary>
        protected DistributionDbContext NewDb()
        {
            return new DistributionDbContext { Actor = new AuditActor(RoleHelper.GetUserId(CurrentUserName), AuditActor.Web) };
        }

        protected DistributionUnitOfWork NewUnitOfWork()
        {
            return new DistributionUnitOfWork(new AuditActor(RoleHelper.GetUserId(CurrentUserName), AuditActor.Web));
        }

        /// <summary>Ikki tilli nom: ruscha bo'lsa va to'ldirilgan bo'lsa — ruscha.</summary>
        protected string Localized(string uz, string ru)
        {
            return IsRussian && !string.IsNullOrWhiteSpace(ru) ? ru : uz;
        }

        /// <summary>AJAX amal natijasi: biznes xatosi foydalanuvchiga, qolgani — umumiy xabar (logga).</summary>
        protected JsonResult DstJson(Func<object> action)
        {
            try
            {
                var data = action();
                return Json(new { success = true, data });
            }
            catch (DstValidationException ex)
            {
                // credit = true — qarz limiti sabab rad etildi (UI ruxsat so'rash oynasini ochadi)
                return Json(new { success = false, message = ex.Message, credit = ex is Services.Orders.CreditBlockedException });
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                return Json(new { success = false, message = DstLocalization.T("Error_Duplicate") });
            }
            catch (Exception ex)
            {
                return JsonError(ex, DstLocalization.T("Error_SaveFailed"));
            }
        }

        // ---------- ombor hujjatlari (3-bosqich) uchun umumiy ----------

        /// <summary>Foydalanuvchi ombor scope'idagi omborlar.</summary>
        protected List<SelectListItem> WarehouseList(DistributionDbContext db)
        {
            var scope = new ScopeService(db).For(CurrentUserName, ScopeTypes.Warehouse);
            return db.Warehouses.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToList()
                .Where(w => scope.Allows(w.ID))
                .Select(w => new SelectListItem { Value = w.ID.ToString(), Text = w.WHName }).ToList();
        }

        protected void EnsureWarehouse(DistributionDbContext db, int warehouseId)
        {
            if (!new ScopeService(db).For(CurrentUserName, ScopeTypes.Warehouse).Allows(warehouseId))
                throw new DstValidationException(DstLocalization.T("Error_NotFound"));
        }

        /// <summary>select2 uchun tovar qidiruvi (artikul, nom, shtrix-kod); ombor berilsa — mavjud qoldiq bilan.</summary>
        protected JsonResult ProductSearch(string q, int? warehouseId)
        {
            using (var db = NewDb())
            {
                q = q?.Trim();
                var byBarcode = string.IsNullOrEmpty(q) ? new List<int>() : db.ProductBarcodes.Where(b => b.Barcode == q).Select(b => b.ProductID).ToList();
                var rows = (from p in db.Products
                            where !p.IsDeleted && (q == null || q == "" || p.PNo.Contains(q) || p.Name.Contains(q) || byBarcode.Contains(p.ID))
                            join u in db.Units on p.UnitID equals u.ID into uj
                            from u in uj.DefaultIfEmpty()
                            orderby p.Name
                            select new { p.ID, p.PNo, p.Name, Unit = u.ShortName }).Take(30).ToList();
                var levels = warehouseId.HasValue
                    ? Services.Orders.StockService.Levels(db, rows.Select(r => r.ID).ToList(), new[] { warehouseId.Value }).ToDictionary(l => l.ProductID, l => l.Available)
                    : new Dictionary<int, decimal>();
                return Json(new
                {
                    results = rows.Select(r => new
                    {
                        id = r.ID, text = r.PNo + " — " + r.Name, unit = r.Unit,
                        available = levels.TryGetValue(r.ID, out var a) ? a : (decimal?)null
                    })
                }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>DLOC ombor bo'lsa — faol joylashuvlar (kirim uchun), aks holda null.</summary>
        protected static List<SelectListItem> LocationList(int warehouseId)
        {
            using (var erp = new DBTHSNEntities())
            {
                if (!tahsinERP.Services.Stock.StockLedger.IsDlocWarehouse(erp, warehouseId)) return null;
                return erp.WAREHOUSE_LOCATIONS.Where(l => l.WarehouseID == warehouseId && l.IsActive && !l.IsDeleted)
                    .OrderBy(l => l.LocationCode).ToList()
                    .Select(l => new SelectListItem { Value = l.ID.ToString(), Text = l.LocationCode + " " + (l.LocationName ?? "") }).ToList();
            }
        }

        protected static bool IsUniqueViolation(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (e is System.Data.SqlClient.SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
                    return true;
            }
            return false;
        }
    }
}
