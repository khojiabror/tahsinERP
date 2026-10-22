using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.DlocFifo;
using tahsinERP.Services.Stock;
using tahsinERP.Controllers;
using tahsinERP.Filters;
using tahsinERP.I18n;

namespace tahsinERP.Controllers.Settings
{
    [Permission]
    public class WarehouseController : Controller
    {
        // Olish so'rovi: Warehouse
        public async Task<ActionResult> Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "Warehouse", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // MRP foydalanuvchisi va sex bitta so'rovda (avval har ombor uchun 2 ta Reference().Load() — SRM-P13)
                var list = await db.WAREHOUSES
                    .AsNoTracking()
                    .Include(w => w.USER)
                    .Include(w => w.SHOP)
                    .Where(w => w.IsDeleted != true)
                    .OrderBy(w => w.WHName)
                    .ToListAsync();

                ViewBag.ChangePermit = RoleHelper.IsChangePermitted(User.Identity.Name, "Warehouse", "Index");
                ViewBag.DlocEnabled = DlocFifoSchema.IsEnabled(db);
                return View(list);
            }
        }

        // Olish so'rovi: Warehouse/yaratish
        public async Task<ActionResult> Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.ShopList = new SelectList(
                    await db.SHOPS.Where(s => s.IsDeleted == false).ToListAsync(), 
                    "ID", "ShopName");

                ViewBag.MRPUsers = new SelectList(
                    await db.USERS
                        .Where(u => u.IsDeleted != true && u.IsActive == true)
                        .ToListAsync(),
                    "ID", "FullName");
                ViewBag.WarehouseTypes = WarehouseRoles.SelectList(null);

                return View();
            }
        }

        /// <summary>SRM-B12: tur faqat ruxsat etilgan qiymatlardan; PRODUCTION/SALES bittadan ortiq bo'lsa ogohlantiradi.</summary>
        private static void ValidateWarehouseType(DBTHSNEntities db, Warehouse warehouse, ModelStateDictionary modelState)
        {
            if (string.IsNullOrWhiteSpace(warehouse.WarehouseType))
            {
                warehouse.WarehouseType = null;
                return;
            }

            warehouse.WarehouseType = warehouse.WarehouseType.Trim().ToUpperInvariant();
            if (warehouse.WarehouseType != WarehouseRoles.Supply
                && warehouse.WarehouseType != WarehouseRoles.Production
                && warehouse.WarehouseType != WarehouseRoles.Sales)
            {
                modelState.AddModelError("WarehouseType", L.T("Wh_InvalidWarehouseType"));
                return;
            }

            if (warehouse.WarehouseType == WarehouseRoles.Sales
                && db.WAREHOUSES.Any(w => w.ID != warehouse.ID && w.IsDeleted != true && w.WarehouseType == WarehouseRoles.Sales))
            {
                modelState.AddModelError("WarehouseType", L.T("Wh_SalesWarehouseExists"));
            }
        }

        // POST so'rovi: Warehouse/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(Warehouse warehouse)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    ValidateWarehouseType(db, warehouse, ModelState);
                    if (ModelState.IsValid)
                    {
                        warehouse.IsDeleted = false;
                        db.WAREHOUSES.Add(warehouse);
                        await db.SaveChangesAsync();
                        WarehouseRoles.Invalidate();

                        LogHelper.LogToDatabase(User.Identity.Name, "Omborlar", 
                            $"{warehouse.WHName} - omborini yaratdi");

                        TempData["Success"] = L.T("Wh_WarehouseCreated");
                        return RedirectToAction("Index");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", L.T("Wh_ErrorWithMessage", ex.Message));
                }

                // Reload ViewBag on xatolik
                ViewBag.ShopList = new SelectList(
                    await db.SHOPS.Where(s => s.IsDeleted == false).ToListAsync(), 
                    "ID", "ShopName", warehouse.ShopID);
                
                ViewBag.MRPUsers = new SelectList(
                    await db.USERS
                        .Where(u => u.IsDeleted != true && u.IsActive == true)
                        .ToListAsync(), 
                    "ID", "FullName", warehouse.MRP);
                ViewBag.WarehouseTypes = WarehouseRoles.SelectList(warehouse.WarehouseType);

                return View(warehouse);
            }
        }

        // Olish so'rovi: Warehouse/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // 1️⃣ Asl warehouse ni oling
                var warehouse = await db.WAREHOUSES
                    .FirstOrDefaultAsync(w => w.ID == id && w.IsDeleted != true);

                if (warehouse == null)
                {
                    return HttpNotFound();
                }

                // 2️⃣ Lazy yuklash related entities (null-safe)
                await db.Entry(warehouse).Reference(w => w.USER).LoadAsync();   // USER nullable bo‘lsa ham ok
                await db.Entry(warehouse).Reference(w => w.SHOP).LoadAsync();   // SHOP nullable bo‘lsa ham ok

                return View(warehouse);
            }
        }


        // Olish so'rovi: Warehouse/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var warehouse = await db.WAREHOUSES.FindAsync(id);
                if (warehouse == null)
                {
                    return HttpNotFound();
                }

                ViewBag.ShopList = new SelectList(
                    await db.SHOPS.Where(s => s.IsDeleted == false).ToListAsync(), 
                    "ID", "ShopName", warehouse.ShopID);

                ViewBag.MRPUsers = new SelectList(
                    await db.USERS
                        .Where(u => u.IsDeleted != true && u.IsActive == true)
                        .ToListAsync(),
                    "ID", "FullName", warehouse.MRP);
                ViewBag.WarehouseTypes = WarehouseRoles.SelectList(warehouse.WarehouseType);

                return View(warehouse);
            }
        }

        // POST so'rovi: Warehouse/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "ID,WHName,Description,IsDeleted,MRP,ShopID,Account,WarehouseType")] Warehouse warehouse)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ValidateWarehouseType(db, warehouse, ModelState);
                if (ModelState.IsValid)
                {
                    try
                    {
                        warehouse.IsDeleted = false;
                        db.Entry(warehouse).State = EntityState.Modified;
                        await db.SaveChangesAsync();
                        WarehouseRoles.Invalidate();

                        LogHelper.LogToDatabase(User.Identity.Name, "Omborlar", 
                            $"{warehouse.WHName} - omborni tahrirladi");

                        TempData["Success"] = L.T("Wh_WarehouseUpdated");
                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", L.T("Wh_ErrorWithMessage", ex.Message));
                    }
                }

                // Reload ViewBag on xatolik
                ViewBag.ShopList = new SelectList(
                    await db.SHOPS.Where(s => s.IsDeleted == false).ToListAsync(), 
                    "ID", "ShopName", warehouse.ShopID);

                ViewBag.MRPUsers = new SelectList(
                    await db.USERS
                        .Where(u => u.IsDeleted != true && u.IsActive == true)
                        .ToListAsync(),
                    "ID", "FullName", warehouse.MRP);
                ViewBag.WarehouseTypes = WarehouseRoles.SelectList(warehouse.WarehouseType);

                return View(warehouse);
            }
        }

        // Olish so'rovi: Warehouse/o'chirish/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var warehouse = await db.WAREHOUSES
                    .Include(w => w.USER)
                    .Include(w => w.SHOP)
                    .FirstOrDefaultAsync(w => w.ID == id);

                if (warehouse == null)
                {
                    return HttpNotFound();
                }

                var blockReason = await GetDeleteBlockReasonAsync(db, warehouse.ID);
                if (blockReason != null)
                {
                    TempData["Error"] = blockReason;
                    return RedirectToAction("Index");
                }

                return View(warehouse);
            }
        }

        /// <summary>
        /// SRM-B11: qoldig'i, DLOC lotlari yoki yakunlanmagan o'tkazmalari bor omborni o'chirib bo'lmaydi —
        /// aks holda qoldiq "yo'qoladi" va kutilayotgan kirimlarni hech kim qabul qila olmaydi.
        /// </summary>
        private static async Task<string> GetDeleteBlockReasonAsync(DBTHSNEntities db, int warehouseId)
        {
            var stockQty = await db.PRODUCT_STOCKS
                .Where(s => s.WHID == warehouseId && s.Amount > 0)
                .Select(s => (double?)s.Amount)
                .SumAsync() ?? 0;
            if (stockQty > 0.000001)
                return L.T("Wh_DelBlock_Stock", stockQty.ToString("N2"));

            if (DlocFifoSchema.IsEnabled(db))
            {
                var dlocQty = DlocFifoDataAccess.GetLocations(db, warehouseId, activeOnly: false)
                    .Sum(l => DlocFifoDataAccess.GetLocationCurrentQty(db, l.ID));
                if (dlocQty > 0.000001m)
                    return L.T("Wh_DelBlock_Dloc", dlocQty.ToString("N2"));
            }

            var pendingIncomes = await db.INCOMES.CountAsync(i =>
                i.ReceivingWHID == warehouseId && i.IsDeleted != true && i.RecieveStatus != true);
            if (pendingIncomes > 0)
                return L.T("Wh_DelBlock_PendingIncomes", pendingIncomes);

            var pendingTransfers = await db.EXPENSES.CountAsync(e =>
                e.IsDeleted != true && e.SendStatus != true && e.ReceiverWhID != null
                && (e.SenderWHID == warehouseId || e.ReceiverWhID == warehouseId));
            if (pendingTransfers > 0)
                return L.T("Wh_DelBlock_PendingTransfers", pendingTransfers);

            return null;
        }

        // POST so'rovi: Warehouse/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    var warehouse = await db.WAREHOUSES.FindAsync(id);
                    var blockReason = warehouse != null ? await GetDeleteBlockReasonAsync(db, warehouse.ID) : null;
                    if (blockReason != null)
                    {
                        TempData["Error"] = blockReason;
                    }
                    else if (warehouse != null)
                    {
                        // Soft o'chirish + nullable ombor havolalarini tozalash
                        warehouse.IsDeleted = true;
                        db.Entry(warehouse).State = EntityState.Modified;
                        await SoftDeleteReferenceService.AfterWarehouseSoftDeletedAsync(db, id);
                        await db.SaveChangesAsync();
                        WarehouseRoles.Invalidate();

                        LogHelper.LogToDatabase(User.Identity.Name, "Omborlar", 
                            $"{warehouse.WHName} - omborni o'chirdi");

                        TempData["Success"] = L.T("Wh_WarehouseDeleted");
                    }
                    else
                    {
                        TempData["Error"] = L.T("Wh_WarehouseNotFound");
                    }
                }
                catch (Exception ex)
                {
                    TempData["Error"] = L.T("Wh_ErrorWithMessage", ex.Message);
                }

                return RedirectToAction("Index");
            }
        }
    }
}
