using DocumentFormat.OpenXml.Office2010.Excel;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    public class PurchasingItemController : Controller
    {
        public ActionResult Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var list = db.PURCHASING_ITEMS.Include(x => x.UNIT).ToList();
                return View(list);
            }
        }
        public ActionResult Create()
        {
            using(DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                return View();
            }
        }
        [HttpPost]
        public ActionResult Create(PURCHASING_ITEMS model)
        {
            using(DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    if (ModelState.IsValid)
                    {
                        db.PURCHASING_ITEMS.Add(model);
                        LogHelper.LogToDatabase(User.Identity.Name, "PurchasingItemController", $"{model.ItemName} - yaratdi");
                        db.SaveChanges();
                        
                        return RedirectToAction("Index");
                    }
                    else
                    {
                        ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                        ModelState.AddModelError("", "Ma'lumotlarni saqlashni iloji bo'lmadi. Qayta urinib ko'ring, agar muammo yana qaytarilsa, tizim administratoriga murojaat qiling.");
                        return View(model);
                    }
                }
                catch (Exception ex)
                {
                    ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                    ModelState.AddModelError( ex.Message, "Ma'lumotlarni saqlashni iloji bo'lmadi. Qayta urinib ko'ring, agar muammo yana qaytarilsa, tizim administratoriga murojaat qiling.");
                    return View(model);
                }
            }
        }
        public ActionResult Edit(int? id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (id == null)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }

                var model = db.PURCHASING_ITEMS.Find(id);
                if (model == null)
                {
                    return HttpNotFound();
                }
                else
                {
                    ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                    return View(model);
                }
            }
        }
        [HttpPost]
        public ActionResult Edit(PURCHASING_ITEMS model)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    var purchasingItemToUpdate = db.PURCHASING_ITEMS.Find(model.ID);
                    if (purchasingItemToUpdate == null)
                    {
                        return HttpNotFound();
                    }

                    db.Entry(purchasingItemToUpdate).State = EntityState.Modified;
                    db.SaveChanges();

                    var userEmail = User.Identity.Name;
                    LogHelper.LogToDatabase(userEmail, "PurchasingItemController", $"{model.ItemName} - Purchasing tahrirladi");

                    // Update the many-to-many table Prod_Shop_Parts

                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(ex.Message, "Ma'lumotlarni saqlashni iloji bo'lmadi. Qayta urinib ko'ring, agar muammo yana qaytarilsa, tizim administratoriga murojaat qiling.");
                }

                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");

                return View(model);
            }
        }
        public ActionResult Delete(int? ID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ID == null)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }
                var model = db.PURCHASING_ITEMS.Include(x => x.UNIT).FirstOrDefault();
                if (model == null)
                {
                    ModelState.AddModelError("", "bu ID bilan ma'lumot topilmadi.");
                    return View();
                }

                return View(model);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int? ID, FormCollection gfc)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    PURCHASING_ITEMS purchasingItemToDelete = db.PURCHASING_ITEMS.Find(ID);
                    if (purchasingItemToDelete != null)
                    {
                        try
                        {
                            db.PURCHASING_ITEMS.Remove(purchasingItemToDelete);
                            db.SaveChanges();

                            var userEmail = User.Identity.Name;
                            LogHelper.LogToDatabase(userEmail, "PurchasingItemController", $"{purchasingItemToDelete.ItemName} - Purchasingni o'chirdi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", "Bunday PurchasingItem topilmadi.");
                    }
                }
            }
            return View();
        }
        public ActionResult Details(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var model = db.PURCHASING_ITEMS.Include(x => x.UNIT).FirstOrDefault();
                if (model == null)
                    return HttpNotFound();

                return View(model);
            }
        }
    }
}