using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Filters;

namespace tahsinERP.Controllers
{
    [Permission]
    public class DamagedProductController : BaseController
    {
        public ActionResult Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var damagedProducts = db.DAMAGED_PRODUCTS.AsNoTracking()
                                        .Where(dp => dp.IsDeleted == false)
                                        .Include(dp => dp.DEFECT_TYPES)
                                        .Include(dp => dp.PRODUCT)
                                        .OrderByDescending(dp => dp.IssueDateTime)
                                        .ToList();
                return View(damagedProducts);
            }
        }

        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                PopulateLookups(db, null, null);
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        // Faqat formadagi maydonlar bog'lanadi (avval butun entity — IsDeleted, IssueDateTime ham, MES-B17)
        public ActionResult Create([Bind(Include = "ProductID,DefectTypeID,Quantity")] DAMAGED_PRODUCTS model)
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

                        // model.PRODUCT bog'lanmagan (null) — avval SaveChanges'dan keyin NRE bo'lib, saqlangan yozuv
                        // "saqlab bo'lmadi" deb ko'rsatilardi va qayta urinishda dublikat paydo bo'lardi (MES-B12)
                        LogHelper.LogToDatabase(User.Identity.Name, "DamagedProductController", $"{ProductPNo(db, model.ProductID)} Buzilgan Maxsulotni yaratdi");

                        return RedirectToAction("Index");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("DamagedProduct.Create: {0}", ex);
                    ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                }
                PopulateLookups(db, model.ProductID, model.DefectTypeID);
                return View(model);
            }
        }

        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var damaged_product = db.DAMAGED_PRODUCTS.AsNoTracking().FirstOrDefault(x => x.ID == id && x.IsDeleted == false);
                if (damaged_product == null)
                {
                    return HttpNotFound();
                }

                PopulateLookups(db, damaged_product.ProductID, damaged_product.DefectTypeID);
                return View(damaged_product);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public ActionResult Edit([Bind(Include = "ID,ProductID,DefectTypeID,Quantity")] DAMAGED_PRODUCTS damagedProduct)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    var damaged_product = db.DAMAGED_PRODUCTS.FirstOrDefault(x => x.ID == damagedProduct.ID && x.IsDeleted == false);
                    if (damaged_product == null)
                    {
                        return HttpNotFound();
                    }
                    damaged_product.ProductID = damagedProduct.ProductID;
                    damaged_product.DefectTypeID = damagedProduct.DefectTypeID;
                    damaged_product.Quantity = damagedProduct.Quantity;
                    db.SaveChanges();

                    // damagedProduct.PRODUCT bog'lanmagan — avval YSOD (MES-B12)
                    LogHelper.LogToDatabase(User.Identity.Name, "DamagedProductController", $"{ProductPNo(db, damaged_product.ProductID)} Buzilgan Maxsulotni tahrirladi");

                    return RedirectToAction("Index");
                }

                PopulateLookups(db, damagedProduct.ProductID, damagedProduct.DefectTypeID);
                return View(damagedProduct);
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
                var damagedProduct = db.DAMAGED_PRODUCTS.AsNoTracking()
                                       .Include(dp => dp.DEFECT_TYPES)
                                       .Include(dp => dp.PRODUCT)
                                       .FirstOrDefault(dp => dp.ID == id);

                if (damagedProduct == null)
                {
                    return HttpNotFound();
                }

                return View(damagedProduct);
            }
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryHeader]
        public ActionResult DeleteConfirmed(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var damagedProduct = db.DAMAGED_PRODUCTS.Find(id);

                if (damagedProduct == null)
                {
                    return HttpNotFound();
                }

                damagedProduct.IsDeleted = true;
                db.SaveChanges();

                LogHelper.LogToDatabase(User.Identity.Name, "DamagedProductController", $"{ProductPNo(db, damagedProduct.ProductID)} ID ega Buzilgan Maxsulotni o'chirdi");

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
                var damagedProduct = db.DAMAGED_PRODUCTS.AsNoTracking()
                                       .Include(dp => dp.DEFECT_TYPES)
                                       .Include(dp => dp.PRODUCT)
                                       .FirstOrDefault(dp => dp.ID == id);

                if (damagedProduct == null)
                {
                    return HttpNotFound();
                }

                return View(damagedProduct);
            }
        }

        private static string ProductPNo(DBTHSNEntities db, int productId)
        {
            return db.PRODUCTS.Where(p => p.ID == productId).Select(p => p.PNo).FirstOrDefault() ?? productId.ToString();
        }

        /// <summary>Dropdownlar — proyeksiya (avval to'liq PRODUCT entity'lari), xato holatida ham to'ldiriladi.</summary>
        private void PopulateLookups(DBTHSNEntities db, int? productId, int? defectTypeId)
        {
            var products = db.PRODUCTS.AsNoTracking()
                .Where(x => x.IsDeleted == false)
                .OrderBy(x => x.PNo)
                .Select(x => new { x.ID, x.PNo })
                .ToList();
            ViewBag.Product = new SelectList(products, "ID", "PNo", productId);

            var defectTypes = db.DEFECT_TYPES.AsNoTracking()
                .Where(x => x.IsDeleted == false)
                .Select(x => new { x.ID, x.DefectType })
                .ToList();
            ViewBag.DefectType = new SelectList(defectTypes, "ID", "DefectType", defectTypeId);
        }
    }
}
