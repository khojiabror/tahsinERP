using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    public class SupplierBaseController : Controller
    {
        // Olish so'rovi: SupplierBase
        public async Task<ActionResult> Index(string productType)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "SupplierBase", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var productTypes = await db.SUPPLIER_BASE
                    .Where(s => s.IsDeleted != true && s.ProductType != null && s.ProductType != "")
                    .Select(s => s.ProductType)
                    .Distinct()
                    .OrderBy(s => s)
                    .ToListAsync();

                ViewBag.ProductTypeList = new SelectList(productTypes, productType);
                ViewBag.ProductType = productType;

                IQueryable<SUPPLIER_BASE> query = db.SUPPLIER_BASE.Where(s => s.IsDeleted != true);

                if (!string.IsNullOrEmpty(productType))
                {
                    query = query.Where(s => s.ProductType == productType);
                }

                var list = await query
                    .OrderBy(s => s.CompanyName)
                    .ToListAsync();

                return View(list);
            }
        }

        // Olish so'rovi: SupplierBase/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "SupplierBase", "Details"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var supplierBase = await db.SUPPLIER_BASE.FindAsync(id);
                if (supplierBase == null || supplierBase.IsDeleted == true)
                {
                    return HttpNotFound();
                }

                return View(supplierBase);
            }
        }

        // Olish so'rovi: SupplierBase/yaratish
        public ActionResult Create()
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "SupplierBase"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
            return View();
        }

        // POST so'rovi: SupplierBase/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(SUPPLIER_BASE supplierBase, FormCollection collection)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "SupplierBase"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    try
                    {
                        supplierBase.IsDeleted = false;
                        db.SUPPLIER_BASE.Add(supplierBase);
                        await db.SaveChangesAsync();

                        LogHelper.LogToDatabase(User.Identity.Name, "SupplierBase", $"Create - {supplierBase.CompanyName} yaratildi");

                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"Xatolik: {ex.Message}");
                    }
                }
            }

            return View(supplierBase);
        }

        // Olish so'rovi: SupplierBase/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "SupplierBase"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var supplierBase = await db.SUPPLIER_BASE.FindAsync(id);
                if (supplierBase == null || supplierBase.IsDeleted == true)
                {
                    return HttpNotFound();
                }

                return View(supplierBase);
            }
        }

        // POST so'rovi: SupplierBase/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(SUPPLIER_BASE supplierBase, FormCollection collection)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "SupplierBase"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    try
                    {
                        var supplierBaseToUpdate = await db.SUPPLIER_BASE.FindAsync(supplierBase.ID);
                        if (supplierBaseToUpdate == null || supplierBaseToUpdate.IsDeleted == true)
                        {
                            return HttpNotFound();
                        }

                        // Yangilash fields
                        supplierBaseToUpdate.CompanyName = supplierBase.CompanyName;
                        supplierBaseToUpdate.ProductType = supplierBase.ProductType;
                        supplierBaseToUpdate.Country = supplierBase.Country;
                        supplierBaseToUpdate.Province = supplierBase.Province;
                        supplierBaseToUpdate.Region = supplierBase.Region;
                        supplierBaseToUpdate.ContactPerson = supplierBase.ContactPerson;
                        supplierBaseToUpdate.Position = supplierBase.Position;
                        supplierBaseToUpdate.Phone = supplierBase.Phone;
                        supplierBaseToUpdate.Comment = supplierBase.Comment;
                        supplierBaseToUpdate.IsDeleted = false;

                        await db.SaveChangesAsync();

                        LogHelper.LogToDatabase(User.Identity.Name, "SupplierBase", $"Edit - {supplierBaseToUpdate.CompanyName} tahrirlandi");

                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"Xatolik: {ex.Message}");
                    }
                }
            }

            return View(supplierBase);
        }

        // Olish so'rovi: SupplierBase/o'chirish/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "SupplierBase"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var supplierBase = await db.SUPPLIER_BASE.FindAsync(id);
                if (supplierBase == null || supplierBase.IsDeleted == true)
                {
                    return HttpNotFound();
                }

                return View(supplierBase);
            }
        }

        // POST so'rovi: SupplierBase/o'chirish
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id, FormCollection collection)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "SupplierBase"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                try
                {
                    var supplierBase = await db.SUPPLIER_BASE.FindAsync(id);
                    if (supplierBase == null || supplierBase.IsDeleted == true)
                    {
                        return Json(new { success = false, message = "Yozuv topilmadi" });
                    }

                    supplierBase.IsDeleted = true;
                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(User.Identity.Name, "SupplierBase", $"Delete - {supplierBase.CompanyName} o'chirildi");

                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Xatolik: {ex.Message}" });
                }
            }
        }

        // POST so'rovi: SupplierBase/o'chirish (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> DeleteAjax(int id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "SupplierBase"))
            {
                return Json(new { success = false, message = "Ruxsat yo'q" });
            }

            using (var db = new DBTHSNEntities())
            {
                try
                {
                    var supplierBase = await db.SUPPLIER_BASE.FindAsync(id);
                    if (supplierBase == null || supplierBase.IsDeleted == true)
                    {
                        return Json(new { success = false, message = "Yozuv topilmadi" });
                    }

                    supplierBase.IsDeleted = true;
                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(User.Identity.Name, "SupplierBase", $"Delete - {supplierBase.CompanyName} o'chirildi");

                    return Json(new { success = true, message = "Muvaffaqiyatli o'chirildi" });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Xatolik: {ex.Message}" });
                }
            }
        }
    }
}

