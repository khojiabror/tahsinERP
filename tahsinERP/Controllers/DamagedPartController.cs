using DocumentFormat.OpenXml.EMMA;
using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    public class DamagedPartController : Controller
    {
        public ActionResult Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var damagedParts = db.DAMAGED_PRODUCTS
                                     .Where(dp => dp.IsDeleted == false)
                                     .Include(dp => dp.DEFECT_TYPES)
                                     .Include(dp => dp.PRODUCT)
                                     .ToList();
                return View(damagedParts);
            }
        }


        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var part = db.PRODUCTS.Where(x => x.IsDeleted == false && x.IsRawMaterial == true).ToList();
                ViewBag.Part = new SelectList(part, "ID", "PNo");
                var defect_types = db.DEFECT_TYPES.Where(x => x.IsDeleted == false).ToList();
                ViewBag.DefectType = new SelectList(defect_types, "ID", "DefectType");
                return View();
            }
        }

        [HttpPost]
        public ActionResult Create(DAMAGED_PRODUCTS model)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    if (ModelState.IsValid)
                    {
                        model.IsDeleted = false;
                        model.IssueDateTime = DateTime.Now;
                        db.DAMAGED_PRODUCTS.Add(model);
                        db.SaveChanges();

                        var userEmail = User.Identity.Name;
                        LogHelper.LogToDatabase(userEmail, "DamagedPartController", $"{model.PRODUCT.PNo} - shikastlangan maxsulot tizimga kiritildi");

                        return RedirectToAction("Index");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(ex.Message, "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                }
                return View(model);
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
                var damaged_part = db.DAMAGED_PRODUCTS.Find(id);

                if (damaged_part == null)
                {
                    return HttpNotFound();
                }
                var part = db.PRODUCTS.Where(x => x.IsDeleted == false && x.IsRawMaterial == true).ToList();
                ViewBag.Part = new SelectList(part, "ID", "PNo");
                var defect_types = db.DEFECT_TYPES.Where(x => x.IsDeleted == false).ToList();
                ViewBag.DefectType = new SelectList(defect_types, "ID", "DefectType");

                return View(damaged_part);
            }
        }
        [HttpPost]
        public ActionResult Edit(DAMAGED_PRODUCTS damagedPart)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    var damaged_part = db.DAMAGED_PRODUCTS.Find(damagedPart.ID);
                    damaged_part.ProductID = damagedPart.ProductID;
                    damaged_part.DefectTypeID = damagedPart.DefectTypeID;
                    damaged_part.Quantity = damagedPart.Quantity;
                    db.SaveChanges();

                    var userEmail = User.Identity.Name;
                    LogHelper.LogToDatabase(userEmail, "DamagedPartController", $"{damagedPart.PRODUCT.PNo}  - shikastlangan maxsulotni tahrirladi");

                    return RedirectToAction("Index");
                }
                var part = db.PRODUCTS.Where(x => x.IsDeleted == false && x.IsRawMaterial == true).ToList();
                ViewBag.Part = new SelectList(part, "ID", "PNo", damagedPart.ProductID);
                var defect_types = db.DEFECT_TYPES.Where(x => x.IsDeleted == false).ToList();
                ViewBag.DefectType = new SelectList(defect_types, "ID", "DefectType", damagedPart.DefectTypeID);

                return View(damagedPart);
            }
        }


        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var damagedPart = db.DAMAGED_PRODUCTS
                                    .Include(dp => dp.DEFECT_TYPES)
                                    .Include(dp => dp.PRODUCT)
                                    .FirstOrDefault(dp => dp.ID == id);

                if (damagedPart == null)
                {
                    return HttpNotFound();
                }

                return View(damagedPart);
            }
        }
        [HttpPost, ActionName("Delete")]
        public ActionResult Delete(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var damagedPart = db.DAMAGED_PRODUCTS.Find(id);

                if (damagedPart == null)
                {
                    return HttpNotFound();
                }

                damagedPart.IsDeleted = true;
                db.Entry(damagedPart).State = EntityState.Modified;
                db.SaveChanges();

                var userEmail = User.Identity.Name;
                LogHelper.LogToDatabase(userEmail, "DamagedPartController", $"{damagedPart.PRODUCT.PNo}   - shikastlangan maxsulotni o'chirib yubordi");

                return RedirectToAction("Index");
            }
        }
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var damagedPart = db.DAMAGED_PRODUCTS
                                    .Include(dp => dp.DEFECT_TYPES)
                                    .Include(dp => dp.PRODUCT)
                                    .FirstOrDefault(dp => dp.ID == id);

                if (damagedPart == null)
                {
                    return HttpNotFound();
                }

                return View(damagedPart);
            }
        }


    }
}