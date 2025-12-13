using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Controllers;

namespace tahsinERP.Controllers.Settings
{
    public class WarehouseController : Controller
    {
        // GET: Warehouse
        public async Task<ActionResult> Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var list = await db.WAREHOUSES
                    .Where(w => w.IsDeleted != true)
                    .Include(w => w.USER)
                    .Include(w => w.SHOP)
                    .OrderBy(w => w.WHName)
                    .ToListAsync();
                return View(list);
            }
        }

        // GET: Warehouse/Create
        public async Task<ActionResult> Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.ShopList = new SelectList(
                    await db.SHOPS.Where(s => s.IsDeleted == false).ToListAsync(), 
                    "ID", "ShopName");
                
                ViewBag.MRPUsers = new SelectList(
                    await db.USERS
                        .Where(u => u.ROLES.Any(r => r.RName == "MRP") && u.IsDeleted != true)
                        .ToListAsync(), 
                    "ID", "FullName");

                return View();
            }
        }

        // POST: Warehouse/Create
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

                // Reload ViewBag on error
                ViewBag.ShopList = new SelectList(
                    await db.SHOPS.Where(s => s.IsDeleted == false).ToListAsync(), 
                    "ID", "ShopName", warehouse.ShopID);
                
                ViewBag.MRPUsers = new SelectList(
                    await db.USERS
                        .Where(u => u.ROLES.Any(r => r.RName == "MRP") && u.IsDeleted != true)
                        .ToListAsync(), 
                    "ID", "FullName", warehouse.MRP);

                return View(warehouse);
            }
        }

        // GET: Warehouse/Details/5
        public async Task<ActionResult> Details(int? id)
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

                return View(warehouse);
            }
        }

        // GET: Warehouse/Edit/5
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
                        .Where(u => u.ROLES.Any(r => r.RName == "MRP") && u.IsDeleted != true)
                        .ToListAsync(), 
                    "ID", "FullName", warehouse.MRP);

                return View(warehouse);
            }
        }

        // POST: Warehouse/Edit/5
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

                // Reload ViewBag on error
                ViewBag.ShopList = new SelectList(
                    await db.SHOPS.Where(s => s.IsDeleted == false).ToListAsync(), 
                    "ID", "ShopName", warehouse.ShopID);
                
                ViewBag.MRPUsers = new SelectList(
                    await db.USERS
                        .Where(u => u.ROLES.Any(r => r.RName == "MRP") && u.IsDeleted != true)
                        .ToListAsync(), 
                    "ID", "FullName", warehouse.MRP);

                return View(warehouse);
            }
        }

        // GET: Warehouse/Delete/5
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

                return View(warehouse);
            }
        }

        // POST: Warehouse/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    var warehouse = await db.WAREHOUSES.FindAsync(id);
                    if (warehouse != null)
                    {
                        // Soft delete
                        warehouse.IsDeleted = true;
                        db.Entry(warehouse).State = EntityState.Modified;
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
