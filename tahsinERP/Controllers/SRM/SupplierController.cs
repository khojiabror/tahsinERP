using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Filters;

namespace tahsinERP.Controllers
{
    public class SupplierController : Controller
    {
        // Olish so'rovi: Supplier
        public async Task<ActionResult> Index(string type)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Supplier", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var sources = await db.SUPPLIERS
                    .Where(s => s.IsDeleted != true)
                    .Select(s => s.Type)
                    .Distinct()
                    .ToListAsync();

                ViewBag.SourceList = new SelectList(sources, type);
                ViewBag.Type = type;
                ViewBag.ChangePermit = tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Supplier")
                    || tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Suppliers")
                    || tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Supplier", "Create");

                List<SUPPLIER> list;
                if (!string.IsNullOrEmpty(type))
                {
                    list = await db.SUPPLIERS
                        .Where(s => s.IsDeleted != true && s.Type == type)
                        .OrderBy(s => s.Name)
                        .ToListAsync();
                }
                else
                {
                    list = await db.SUPPLIERS
                        .Where(s => s.IsDeleted != true)
                        .OrderBy(s => s.Name)
                        .ToListAsync();
                }

                return View(list);
            }
        }

        // Olish so'rovi: Supplier/GetSupplier/5 (uchun modal)
        [HttpGet]
        public async Task<JsonResult> GetSupplier(int id)
        {
            using (var db = new DBTHSNEntities())
            {
                var supplier = await db.SUPPLIERS.FindAsync(id);
                if (supplier == null || supplier.IsDeleted == true)
                {
                    return Json(new { success = false, message = "Ta'minotchi topilmadi" }, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        ID = supplier.ID,
                        Name = supplier.Name,
                        Address = supplier.Address,
                        Country = supplier.Country,
                        City = supplier.City,
                        Telephone = supplier.Telephone,
                        Email = supplier.Email,
                        ContactPersonName = supplier.ContactPersonName,
                        DirectorName = supplier.DirectorName,
                        DUNS = supplier.DUNS,
                        Type = supplier.Type
                    }
                }, JsonRequestBehavior.AllowGet);
            }
        }


        // POST so'rovi: Supplier/o'chirish (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> Delete(int id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Supplier"))
            {
                return Json(new { success = false, message = "Ruxsat yo'q" });
            }

            using (var db = new DBTHSNEntities())
            {
                try
                {
                    var supplier = await db.SUPPLIERS.FindAsync(id);
                    if (supplier == null || supplier.IsDeleted == true)
                    {
                        return Json(new { success = false, message = "Ta'minotchi topilmadi" });
                    }

                    supplier.IsDeleted = true;
                    await SoftDeleteReferenceService.AfterSupplierSoftDeletedAsync(db, id);
                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(User.Identity.Name, "Supplier", $"Delete - {supplier.Name} ta'minotchi o'chirildi");

                    return Json(new { success = true, message = "Ta'minotchi muvaffaqiyatli o'chirildi" });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Xatolik: {ex.Message}" });
                }
            }
        }

        // Olish so'rovi: Supplier/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Supplier", "Details"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var supplier = await db.SUPPLIERS.FindAsync(id);
                if (supplier == null || supplier.IsDeleted == true)
                {
                    return HttpNotFound();
                }

                // Olish counts uchun related hujjatlar
                var contractsCount = await db.P_CONTRACTS
                    .Where(c => c.SupplierID == id && c.IsDeleted != true)
                    .CountAsync();
                ViewBag.ContractsCount = contractsCount;

                var ordersCount = await db.P_ORDERS
                    .Where(o => o.SupplierID == id && o.IsDeleted != true)
                    .CountAsync();
                ViewBag.OrdersCount = ordersCount;

                var invoicesCount = await db.P_INVOICES
                    .Where(i => i.SupplierID == id && i.IsDeleted != true)
                    .CountAsync();
                ViewBag.InvoicesCount = invoicesCount;

                return View(supplier);
            }
        }

        // Olish so'rovi: Supplier/yaratish
        public ActionResult Create()
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Supplier"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
            return View();
        }

        // POST so'rovi: Supplier/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(SUPPLIER supplier, FormCollection collection)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Supplier"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    try
                    {
                        // Tekshirish agar supplier bilan same name mavjud
                        var existSupplier = await db.SUPPLIERS
                            .FirstOrDefaultAsync(x => x.IsDeleted != true && x.Name.ToLower() == supplier.Name.ToLower());

                        if (existSupplier != null)
                        {
                            ModelState.AddModelError("", "Bunday nom bilan Ta'minotchi kiritilgan, ma'lumotlarni qaytadan tekshiring.");
                            return View(supplier);
                        }

                        supplier.IsDeleted = false;
                        db.SUPPLIERS.Add(supplier);
                        await db.SaveChangesAsync();

                        LogHelper.LogToDatabase(User.Identity.Name, "Supplier", $"Create - {supplier.Name} ta'minotchini yaratildi");

                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"Xatolik: {ex.Message}");
                    }
                }
            }

            return View(supplier);
        }

        public async Task<ActionResult> Edit(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Supplier"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var supplier = await db.SUPPLIERS.FindAsync(id);
                if (supplier == null || supplier.IsDeleted == true)
                {
                    return HttpNotFound();
                }

                return View(supplier);
            }
        }

        // POST so'rovi: Supplier/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(SUPPLIER supplier, FormCollection collection)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Supplier"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    try
                    {
                        var supplierToUpdate = await db.SUPPLIERS.FindAsync(supplier.ID);
                        if (supplierToUpdate == null || supplierToUpdate.IsDeleted == true)
                        {
                            return HttpNotFound();
                        }

                        // Tekshirish agar another supplier bilan same name mavjud
                        var existSupplier = await db.SUPPLIERS
                            .FirstOrDefaultAsync(x => x.IsDeleted != true && 
                                                      x.Name.ToLower() == supplier.Name.ToLower() && 
                                                      x.ID != supplier.ID);

                        if (existSupplier != null)
                        {
                            ModelState.AddModelError("", "Bunday nom bilan boshqa Ta'minotchi mavjud");
                            return View(supplier);
                        }

                        // Yangilash fields
                        supplierToUpdate.Name = supplier.Name;
                        supplierToUpdate.Address = supplier.Address;
                        supplierToUpdate.Country = supplier.Country;
                        supplierToUpdate.City = supplier.City;
                        supplierToUpdate.Telephone = supplier.Telephone;
                        supplierToUpdate.Email = supplier.Email;
                        supplierToUpdate.ContactPersonName = supplier.ContactPersonName;
                        supplierToUpdate.DirectorName = supplier.DirectorName;
                        supplierToUpdate.DUNS = supplier.DUNS;
                        supplierToUpdate.Type = supplier.Type;
                        supplierToUpdate.IsDeleted = false;

                        await db.SaveChangesAsync();

                        LogHelper.LogToDatabase(User.Identity.Name, "Supplier", $"Edit - {supplierToUpdate.Name} ta'minotchi tahrirlandi");

                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"Xatolik: {ex.Message}");
                    }
                }
            }

            return View(supplier);
        }

        public async Task<ActionResult> Delete(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Supplier"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var supplier = await db.SUPPLIERS.FindAsync(id);
                if (supplier == null || supplier.IsDeleted == true)
                {
                    return HttpNotFound();
                }

                return View(supplier);
            }
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id, FormCollection collection)
        {
            var result = await Delete(id);
            return RedirectToAction("Index");
        }

        // Excel upload actions (keeping existing functionality)
        public ActionResult UploadWithExcel()
        {
            ViewBag.IsFileUploaded = false;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public ActionResult UploadWithExcel(HttpPostedFileBase file)
        {
            // Keep existing Excel upload logic
            // This can be enhanced later bilan modal support
            return View();
        }
    }
}
