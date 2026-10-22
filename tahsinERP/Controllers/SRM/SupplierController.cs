using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.I18n;
using tahsinERP.Models;
using tahsinERP.Services;

namespace tahsinERP.Controllers
{
    public class SupplierController : BaseController
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
                    list = await db.SUPPLIERS.AsNoTracking()
                        .Where(s => s.IsDeleted != true && s.Type == type)
                        .OrderBy(s => s.Name)
                        .ToListAsync();
                }
                else
                {
                    list = await db.SUPPLIERS.AsNoTracking()
                        .Where(s => s.IsDeleted != true)
                        .OrderBy(s => s.Name)
                        .ToListAsync();
                }

                return View(list);
            }
        }

        // POST so'rovi: Supplier/DeleteAjax (Index sahifasidagi AJAX o'chirish).
        // Avval "Delete" nomli ikkinchi POST bilan to'qnashib AmbiguousMatchException berardi (SRM-B02).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> DeleteAjax(int id)
        {
            if (!CanChange("Supplier"))
            {
                return Json(new { success = false, message = L.T("Error_NotAuthorized") });
            }

            try
            {
                var name = await SoftDeleteAsync(id);
                if (name == null)
                {
                    return Json(new { success = false, message = L.T("Srm_SupplierNotFound") });
                }

                return Json(new { success = true, message = L.T("Srm_SupplierDeleted") });
            }
            catch (Exception ex)
            {
                return JsonError(ex);
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
        [ActionViewPermission(change: true)]
        public ActionResult Create()
        {
            if (!CanChange("Supplier"))
            {
                return NotAuthorized();
            }
            return View();
        }

        // POST so'rovi: Supplier/yaratish
        [ActionViewPermission(change: true)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(SUPPLIER supplier)
        {
            if (!CanChange("Supplier"))
            {
                return NotAuthorized();
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    try
                    {
                        // Nom takrorlanmasin. Baza collation'i CI (Cyrillic_General_CI_AS) — ToLower shart emas,
                        // tenglik indeksdan foydalanadi (SRM-P12)
                        var name = (supplier.Name ?? "").Trim();
                        bool exists = await db.SUPPLIERS.AnyAsync(x => x.IsDeleted != true && x.Name == name);

                        if (exists)
                        {
                            ModelState.AddModelError("", L.T("Srm_SupplierNameExists"));
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
                        ModelState.AddModelError("", L.T("Srm_ErrorWithMessage", ex.Message));
                    }
                }
            }

            return View(supplier);
        }

        [ActionViewPermission(change: true)]
        public async Task<ActionResult> Edit(int? id)
        {
            if (!CanChange("Supplier"))
            {
                return NotAuthorized();
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
        [ActionViewPermission(change: true)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(SUPPLIER supplier)
        {
            if (!CanChange("Supplier"))
            {
                return NotAuthorized();
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

                        // Boshqa ta'minotchida shu nom bo'lmasin (CI collation — SRM-P12)
                        var name = (supplier.Name ?? "").Trim();
                        bool exists = await db.SUPPLIERS.AnyAsync(x => x.IsDeleted != true && x.Name == name && x.ID != supplier.ID);

                        if (exists)
                        {
                            ModelState.AddModelError("", L.T("Srm_SupplierNameExistsOther"));
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
                        ModelState.AddModelError("", L.T("Srm_ErrorWithMessage", ex.Message));
                    }
                }
            }

            return View(supplier);
        }

        [ActionViewPermission(change: true)]
        public async Task<ActionResult> Delete(int? id)
        {
            if (!CanChange("Supplier"))
            {
                return NotAuthorized();
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

        [ActionViewPermission(change: true)]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            if (!CanChange("Supplier"))
            {
                return NotAuthorized();
            }

            await SoftDeleteAsync(id);
            return RedirectToAction("Index");
        }

        /// <summary>Ta'minotchini soft-delete qiladi; topilmasa null, aks holda nomini qaytaradi.</summary>
        private async Task<string> SoftDeleteAsync(int id)
        {
            using (var db = new DBTHSNEntities())
            {
                var supplier = await db.SUPPLIERS.FindAsync(id);
                if (supplier == null || supplier.IsDeleted == true)
                {
                    return null;
                }

                supplier.IsDeleted = true;
                await SoftDeleteReferenceService.AfterSupplierSoftDeletedAsync(db, id);
                await db.SaveChangesAsync();

                LogHelper.LogToDatabase(CurrentUserName, "Supplier", $"Delete - {supplier.Name} ta'minotchi o'chirildi");
                return supplier.Name;
            }
        }
    }
}
