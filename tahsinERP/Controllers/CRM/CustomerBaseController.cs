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
    public class CustomerBaseController : Controller
    {
        // GET: CustomerBase
        public async Task<ActionResult> Index(string productType)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "CustomerBase", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var productTypes = await db.CUSTOMER_BASE
                    .Where(s => s.IsDeleted != true && s.ProductType != null && s.ProductType != "")
                    .Select(s => s.ProductType)
                    .Distinct()
                    .OrderBy(s => s)
                    .ToListAsync();

                ViewBag.ProductTypeList = new SelectList(productTypes, productType);
                ViewBag.ProductType = productType;

                IQueryable<CUSTOMER_BASE> query = db.CUSTOMER_BASE.Where(s => s.IsDeleted != true);

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

        // GET: CustomerBase/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "CustomerBase", "Details"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var customerBase = await db.CUSTOMER_BASE.FindAsync(id);
                if (customerBase == null || customerBase.IsDeleted == true)
                {
                    return HttpNotFound();
                }

                return View(customerBase);
            }
        }

        // GET: CustomerBase/Create
        public ActionResult Create()
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "CustomerBase"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
            return View();
        }

        // POST: CustomerBase/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(CUSTOMER_BASE customerBase, FormCollection collection)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "CustomerBase"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    try
                    {
                        customerBase.IsDeleted = false;
                        db.CUSTOMER_BASE.Add(customerBase);
                        await db.SaveChangesAsync();

                        LogHelper.LogToDatabase(User.Identity.Name, "CustomerBase", $"Create - {customerBase.CompanyName} yaratildi");

                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"Xatolik: {ex.Message}");
                    }
                }
            }

            return View(customerBase);
        }

        // GET: CustomerBase/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "CustomerBase"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var customerBase = await db.CUSTOMER_BASE.FindAsync(id);
                if (customerBase == null || customerBase.IsDeleted == true)
                {
                    return HttpNotFound();
                }

                return View(customerBase);
            }
        }

        // POST: CustomerBase/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(CUSTOMER_BASE customerBase, FormCollection collection)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "CustomerBase"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    try
                    {
                        var customerBaseToUpdate = await db.CUSTOMER_BASE.FindAsync(customerBase.ID);
                        if (customerBaseToUpdate == null || customerBaseToUpdate.IsDeleted == true)
                        {
                            return HttpNotFound();
                        }

                        // Update fields
                        customerBaseToUpdate.CompanyName = customerBase.CompanyName;
                        customerBaseToUpdate.ProductType = customerBase.ProductType;
                        customerBaseToUpdate.Country = customerBase.Country;
                        customerBaseToUpdate.Province = customerBase.Province;
                        customerBaseToUpdate.Region = customerBase.Region;
                        customerBaseToUpdate.ContactPerson = customerBase.ContactPerson;
                        customerBaseToUpdate.Position = customerBase.Position;
                        customerBaseToUpdate.Phone = customerBase.Phone;
                        customerBaseToUpdate.Comment = customerBase.Comment;
                        customerBaseToUpdate.IsDeleted = false;

                        await db.SaveChangesAsync();

                        LogHelper.LogToDatabase(User.Identity.Name, "CustomerBase", $"Edit - {customerBaseToUpdate.CompanyName} tahrirlandi");

                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"Xatolik: {ex.Message}");
                    }
                }
            }

            return View(customerBase);
        }

        // GET: CustomerBase/Delete/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "CustomerBase"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var customerBase = await db.CUSTOMER_BASE.FindAsync(id);
                if (customerBase == null || customerBase.IsDeleted == true)
                {
                    return HttpNotFound();
                }

                return View(customerBase);
            }
        }

        // POST: CustomerBase/Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id, FormCollection collection)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "CustomerBase"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                try
                {
                    var customerBase = await db.CUSTOMER_BASE.FindAsync(id);
                    if (customerBase == null || customerBase.IsDeleted == true)
                    {
                        return Json(new { success = false, message = "Yozuv topilmadi" });
                    }

                    customerBase.IsDeleted = true;
                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(User.Identity.Name, "CustomerBase", $"Delete - {customerBase.CompanyName} o'chirildi");

                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Xatolik: {ex.Message}" });
                }
            }
        }

        // POST: CustomerBase/Delete (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> DeleteAjax(int id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "CustomerBase"))
            {
                return Json(new { success = false, message = "Ruxsat yo'q" });
            }

            using (var db = new DBTHSNEntities())
            {
                try
                {
                    var customerBase = await db.CUSTOMER_BASE.FindAsync(id);
                    if (customerBase == null || customerBase.IsDeleted == true)
                    {
                        return Json(new { success = false, message = "Yozuv topilmadi" });
                    }

                    customerBase.IsDeleted = true;
                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(User.Identity.Name, "CustomerBase", $"Delete - {customerBase.CompanyName} o'chirildi");

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

