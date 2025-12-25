using System;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using tahsinERP.Models;
using System.Data.Entity;

namespace tahsinERP.Controllers
{
    public class ProductCategoryController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();

        // GET: ProductCategory
        public ActionResult Index()
        {
            var categories = db.PRODUCT_CATEGORIES
                .Where(x => x.IsActive == true)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .ToList();

            ViewBag.ChangePermit = RoleHelper.IsChangePermitted(User.Identity.Name, "ProductCategory", "Index");
            return View(categories);
        }

        // GET: ProductCategory/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var category = db.PRODUCT_CATEGORIES.Find(id);
            if (category == null || !category.IsActive)
            {
                return HttpNotFound();
            }

            return View(category);
        }

        // GET: ProductCategory/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: ProductCategory/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Code,Name,IsRawMaterial,IsFinishedGood,IsComponent,IsConsumable,RequiresNORM,RequiresCutting,IncludeInMRP,AllowPurchase,AllowSales,RawMaterialGroup,RawMaterialSubGrp,SortOrder,IsActive")] PRODUCT_CATEGORIES category)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    category.CreatedDT = DateTime.Now;
                    category.IsActive = true;
                    
                    // Set default SortOrder if not provided
                    if (category.SortOrder == 0)
                    {
                        var maxSortOrder = db.PRODUCT_CATEGORIES.Any() 
                            ? db.PRODUCT_CATEGORIES.Max(x => x.SortOrder) 
                            : 0;
                        category.SortOrder = maxSortOrder + 1;
                    }

                    db.PRODUCT_CATEGORIES.Add(category);
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "ProductCategoryController", 
                        $"{category.Name} - Maxsulot turini yaratdi");

                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "O'zgarishlarni saqlab bo'lmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                }
            }

            return View(category);
        }

        // GET: ProductCategory/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var category = db.PRODUCT_CATEGORIES.Find(id);
            if (category == null || !category.IsActive)
            {
                return HttpNotFound();
            }

            return View(category);
        }

        // POST: ProductCategory/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "ID,Code,Name,IsRawMaterial,IsFinishedGood,IsComponent,IsConsumable,RequiresNORM,RequiresCutting,IncludeInMRP,AllowPurchase,AllowSales,RawMaterialGroup,RawMaterialSubGrp,SortOrder,IsActive,CreatedDT")] PRODUCT_CATEGORIES category)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var categoryToUpdate = db.PRODUCT_CATEGORIES.Find(category.ID);
                    if (categoryToUpdate == null)
                    {
                        return HttpNotFound();
                    }

                    categoryToUpdate.Code = category.Code;
                    categoryToUpdate.Name = category.Name;
                    categoryToUpdate.IsRawMaterial = category.IsRawMaterial;
                    categoryToUpdate.IsFinishedGood = category.IsFinishedGood;
                    categoryToUpdate.IsComponent = category.IsComponent;
                    categoryToUpdate.IsConsumable = category.IsConsumable;
                    categoryToUpdate.RequiresNORM = category.RequiresNORM;
                    categoryToUpdate.RequiresCutting = category.RequiresCutting;
                    categoryToUpdate.IncludeInMRP = category.IncludeInMRP;
                    categoryToUpdate.AllowPurchase = category.AllowPurchase;
                    categoryToUpdate.AllowSales = category.AllowSales;
                    categoryToUpdate.RawMaterialGroup = category.RawMaterialGroup;
                    categoryToUpdate.RawMaterialSubGrp = category.RawMaterialSubGrp;
                    categoryToUpdate.SortOrder = category.SortOrder;
                    categoryToUpdate.IsActive = category.IsActive;
                    categoryToUpdate.UpdatedDT = DateTime.Now;

                    db.Entry(categoryToUpdate).State = EntityState.Modified;
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "ProductCategoryController", 
                        $"{categoryToUpdate.Name} - Maxsulot turini tahrirladi");

                    return RedirectToAction("Index");
                }
                catch (RetryLimitExceededException)
                {
                    ModelState.AddModelError("", "O'zgarishlarni saqlab bo'lmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                }
            }

            return View(category);
        }

        // GET: ProductCategory/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var category = db.PRODUCT_CATEGORIES.Find(id);
            if (category == null || !category.IsActive)
            {
                return HttpNotFound();
            }

            return View(category);
        }

        // POST: ProductCategory/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var category = db.PRODUCT_CATEGORIES.Find(id);
            if (category == null)
            {
                return HttpNotFound();
            }

            try
            {
                // Soft delete - set IsActive to false
                category.IsActive = false;
                category.UpdatedDT = DateTime.Now;

                db.Entry(category).State = EntityState.Modified;
                db.SaveChanges();

                LogHelper.LogToDatabase(User.Identity.Name, "ProductCategoryController", 
                    $"{category.Name} - Maxsulot turini o'chirdi");

                return RedirectToAction("Index");
            }
            catch (RetryLimitExceededException)
            {
                ModelState.AddModelError("", "O'zgarishlarni saqlab bo'lmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
            }

            return View(category);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
