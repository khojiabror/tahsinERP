using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.DlocFifo;
using tahsinERP.Controllers;
using tahsinERP.Filters;

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

                return View();
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
                    if (ModelState.IsValid)
                    {
                        warehouse.IsDeleted = false;
                        db.WAREHOUSES.Add(warehouse);
                        await db.SaveChangesAsync();

                        LogHelper.LogToDatabase(User.Identity.Name, "Omborlar", 
                            $"{warehouse.WHName} - omborini yaratdi");

                        TempData["Success"] = "Ombor muvaffaqiyatli qo'shildi";
                        return RedirectToAction("Index");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Xatolik: {ex.Message}");
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

                return View(warehouse);
            }
        }

        // POST so'rovi: Warehouse/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "ID,WHName,Description,IsDeleted,MRP,ShopID,Account")] Warehouse warehouse)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    try
                    {
                        warehouse.IsDeleted = false;
                        db.Entry(warehouse).State = EntityState.Modified;
                        await db.SaveChangesAsync();

                        LogHelper.LogToDatabase(User.Identity.Name, "Omborlar", 
                            $"{warehouse.WHName} - omborni tahrirladi");

                        TempData["Success"] = "Ombor muvaffaqiyatli yangilandi";
                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"Xatolik: {ex.Message}");
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
                return $"Omborda qoldiq bor ({stockQty:N2}). Avval qoldiqni boshqa omborga o'tkazing yoki inventarizatsiya qiling.";

            if (DlocFifoSchema.IsEnabled(db))
            {
                var dlocQty = DlocFifoDataAccess.GetLocations(db, warehouseId, activeOnly: false)
                    .Sum(l => DlocFifoDataAccess.GetLocationCurrentQty(db, l.ID));
                if (dlocQty > 0.000001m)
                    return $"Ombor DLOC joylarida lot qoldig'i bor ({dlocQty:N2}).";
            }

            var pendingIncomes = await db.INCOMES.CountAsync(i =>
                i.ReceivingWHID == warehouseId && i.IsDeleted != true && i.RecieveStatus != true);
            if (pendingIncomes > 0)
                return $"Omborga {pendingIncomes} ta qabul qilinmagan kirim bor. Avval ularni qabul qiling yoki rad eting.";

            var pendingTransfers = await db.EXPENSES.CountAsync(e =>
                e.IsDeleted != true && e.SendStatus != true && e.ReceiverWhID != null
                && (e.SenderWHID == warehouseId || e.ReceiverWhID == warehouseId));
            if (pendingTransfers > 0)
                return $"Ombor ishtirokidagi {pendingTransfers} ta yakunlanmagan o'tkazma bor.";

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

                        LogHelper.LogToDatabase(User.Identity.Name, "Omborlar", 
                            $"{warehouse.WHName} - omborni o'chirdi");

                        TempData["Success"] = "Ombor muvaffaqiyatli o'chirildi";
                    }
                    else
                    {
                        TempData["Error"] = "Ombor topilmadi";
                    }
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Xatolik: {ex.Message}";
                }

                return RedirectToAction("Index");
            }
        }
    }
}
