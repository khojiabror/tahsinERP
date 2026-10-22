using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services.Common;
using tahsinERP.Controllers;
using tahsinERP.Services.DlocFifo;
using tahsinERP.ViewModels;
using tahsinERP.I18n;

namespace tahsinERP.Controllers.Settings
{
    public class WarehouseLocationsController : Controller
    {
        public ActionResult Index(int? warehouseId)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "Warehouse", "Index"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            using (var db = new DBTHSNEntities())
            {
                if (!DlocFifoSchema.IsEnabled(db))
                {
                    ViewBag.SchemaMissing = true;
                    return View(Enumerable.Empty<WarehouseLocationDto>().ToList());
                }

                var list = DlocFifoDataAccess.GetLocations(db, warehouseId, activeOnly: false);
                ViewBag.Warehouses = ReferenceDataCache.SelectList(ReferenceDataCache.Warehouses, warehouseId);
                ViewBag.WarehouseId = warehouseId;
                ViewBag.ChangePermit = RoleHelper.IsChangePermitted(User.Identity.Name, "Warehouse", "Index");
                if (warehouseId.HasValue)
                {
                    var wh = db.WAREHOUSES.Find(warehouseId.Value);
                    ViewBag.SelectedWarehouseName = wh?.WHName;
                }
                return View(list);
            }
        }

        public ActionResult Create(int? warehouseId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "Warehouse", "Index"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            using (var db = new DBTHSNEntities())
            {
                ViewBag.Warehouses = ReferenceDataCache.SelectList(ReferenceDataCache.Warehouses, warehouseId);
                ViewBag.LockWarehouse = warehouseId.HasValue && warehouseId.Value > 0;
                return View(new WarehouseLocationEditViewModel { WarehouseID = warehouseId ?? 0, IsActive = true });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(WarehouseLocationEditViewModel model)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "Warehouse", "Index"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            if (model.MaxCapacity < model.MinCapacity)
                ModelState.AddModelError("MaxCapacity", L.T("Wh_Loc_MaxLessThanMin"));

            if (!ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    ViewBag.Warehouses = ReferenceDataCache.SelectList(ReferenceDataCache.Warehouses, model.WarehouseID);
                    ViewBag.LockWarehouse = model.WarehouseID > 0;
                }
                return View(model);
            }

            using (var db = new DBTHSNEntities())
            {
                DlocFifoDataAccess.InsertLocation(db, new WarehouseLocationDto
                {
                    WarehouseID = model.WarehouseID,
                    LocationCode = model.LocationCode?.Trim(),
                    LocationName = model.LocationName,
                    MinCapacity = model.MinCapacity,
                    MaxCapacity = model.MaxCapacity,
                    AllowMixedLots = model.AllowMixedLots,
                    AllowMixedProducts = model.AllowMixedProducts,
                    IsActive = model.IsActive
                });
                TempData["Success"] = L.T("Wh_Loc_Created");
                return RedirectToAction("Index", new { warehouseId = model.WarehouseID });
            }
        }

        public ActionResult Edit(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "Warehouse", "Index"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            using (var db = new DBTHSNEntities())
            {
                var loc = DlocFifoDataAccess.GetLocation(db, id);
                if (loc == null)
                    return HttpNotFound();

                ViewBag.Warehouses = ReferenceDataCache.SelectList(ReferenceDataCache.Warehouses, loc.WarehouseID);

                return View(new WarehouseLocationEditViewModel
                {
                    ID = loc.ID,
                    WarehouseID = loc.WarehouseID,
                    LocationCode = loc.LocationCode,
                    LocationName = loc.LocationName,
                    MinCapacity = loc.MinCapacity,
                    MaxCapacity = loc.MaxCapacity,
                    AllowMixedLots = loc.AllowMixedLots,
                    AllowMixedProducts = loc.AllowMixedProducts,
                    IsActive = loc.IsActive
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(WarehouseLocationEditViewModel model)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "Warehouse", "Index"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            if (model.MaxCapacity < model.MinCapacity)
                ModelState.AddModelError("MaxCapacity", L.T("Wh_Loc_MaxLessThanMin"));

            // SRM-B17: joyda qoldiq bor bo'lsa sig'imni undan kamaytirish yoki joyni nofaol qilish mumkin emas
            // (nofaol/o'chirilgan joydagi lotlar FIFO va ledger yig'indisidan tushib qoladi).
            using (var dbCheck = new DBTHSNEntities())
            {
                var currentQty = DlocFifoDataAccess.GetLocationCurrentQty(dbCheck, model.ID);
                if (currentQty > 0 && model.MaxCapacity < currentQty)
                    ModelState.AddModelError("MaxCapacity", L.T("Wh_Loc_MaxBelowCurrent", currentQty.ToString("N2")));
                if (currentQty > 0 && !model.IsActive)
                    ModelState.AddModelError("", L.T("Wh_Loc_HasStockMoveFirst", currentQty.ToString("N2")));
            }

            if (!ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    ViewBag.Warehouses = ReferenceDataCache.SelectList(ReferenceDataCache.Warehouses, model.WarehouseID);
                }
                return View(model);
            }

            using (var db = new DBTHSNEntities())
            {
                DlocFifoDataAccess.UpdateLocation(db, new WarehouseLocationDto
                {
                    ID = model.ID,
                    WarehouseID = model.WarehouseID,
                    LocationCode = model.LocationCode?.Trim(),
                    LocationName = model.LocationName,
                    MinCapacity = model.MinCapacity,
                    MaxCapacity = model.MaxCapacity,
                    AllowMixedLots = model.AllowMixedLots,
                    AllowMixedProducts = model.AllowMixedProducts,
                    IsActive = model.IsActive
                });
                TempData["Success"] = L.T("Wh_Loc_Updated");
                return RedirectToAction("Index", new { warehouseId = model.WarehouseID });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "Warehouse", "Index"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            using (var db = new DBTHSNEntities())
            {
                var loc = DlocFifoDataAccess.GetLocation(db, id);
                var whId = loc?.WarehouseID;

                // SRM-B17: qoldig'i bor joy o'chirilmaydi (lotlar ledger'dan "yo'qolib", PRODUCT_STOCKS bilan nomuvofiqlik bo'lardi)
                var currentQty = DlocFifoDataAccess.GetLocationCurrentQty(db, id);
                if (currentQty > 0)
                {
                    TempData["Error"] = L.T("Wh_Loc_HasStockNoDelete", currentQty.ToString("N2"));
                    return RedirectToAction("Index", new { warehouseId = whId });
                }

                DlocFifoDataAccess.SoftDeleteLocation(db, id);
                TempData["Success"] = L.T("Wh_Loc_Deleted");
                return RedirectToAction("Index", new { warehouseId = whId });
            }
        }
    }
}
