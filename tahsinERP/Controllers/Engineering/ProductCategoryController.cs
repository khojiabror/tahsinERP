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

        // GET: ProductCategory
        public ActionResult Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var categories = db.PRODUCT_CATEGORIES
                    .Where(x => x.IsActive == true && (x.IsDeleted == false || x.IsDeleted == null))
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Name)
                    .ToList();

                ViewBag.ChangePermit = RoleHelper.IsChangePermitted(User.Identity.Name, "ProductCategory", "Index");
                return View(categories);
            }
        }

        // GET: ProductCategory/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var category = db.PRODUCT_CATEGORIES
                    .Where(c => c.ID == id && (c.IsDeleted == false || c.IsDeleted == null) && c.IsActive)
                    .FirstOrDefault();
                if (category == null)
                {
                    return HttpNotFound();
                }

                return View(category);
            }
        }

        // GET: ProductCategory/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: ProductCategory/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Code,Name,IsRawMaterial,IsFinishedGood,IsComponent,IsConsumable,RequiresNORM,RequiresCutting,IncludeInMRP,AllowPurchase,AllowSales,RawMaterialGroup,RawMaterialSubGrp,IsActive,CalculateByWeight")] PRODUCT_CATEGORIES category)
        {
            // Remove Code and SortOrder from validation since they will be auto-generated
            //ModelState.Remove("Code");
            //ModelState.Remove("SortOrder");

            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        // Create a new entity instead of using the model directly to avoid concurrency issues
                        var newCategory = new PRODUCT_CATEGORIES
                        {
                            Name = category.Name,
                            IsRawMaterial = category.IsRawMaterial,
                            IsFinishedGood = category.IsFinishedGood,
                            IsComponent = category.IsComponent,
                            IsConsumable = category.IsConsumable,
                            RequiresNORM = category.RequiresNORM,
                            RequiresCutting = category.RequiresCutting,
                            IncludeInMRP = category.IncludeInMRP,
                            AllowPurchase = category.AllowPurchase,
                            AllowSales = category.AllowSales,
                            RawMaterialGroup = category.RawMaterialGroup,
                            RawMaterialSubGrp = category.RawMaterialSubGrp,
                            CalculateByWeight = category.CalculateByWeight ?? false,
                            CreatedDT = DateTime.Now,
                            IsActive = true
                        };

                        db.PRODUCT_CATEGORIES.Add(newCategory);
                        db.SaveChanges();

                        // Auto-generate Code: 5-digit incrementing (00000, 00001, ...)
                        // Find all numeric codes (5-digit) and get the maximum
                        var allCodes = db.PRODUCT_CATEGORIES
                            .Where(c => c.Code != null && c.Code.Length == 5)
                            .Select(c => c.Code)
                            .ToList();

                        int nextCodeNumber = 0;
                        foreach (var code in allCodes)
                        {
                            if (int.TryParse(code, out int codeNum))
                            {
                                if (codeNum >= nextCodeNumber)
                                {
                                    nextCodeNumber = codeNum + 1;
                                }
                            }
                        }

                        // If no valid codes found, start from 00000
                        if (nextCodeNumber == 0 && !allCodes.Any())
                        {
                            nextCodeNumber = 0; // First code will be 00000
                        }

                        newCategory.Code = nextCodeNumber.ToString("D5"); // Format as 5-digit with leading zeros

                        //// Auto-generate SortOrder: Last object + 1
                        var maxSortOrder = db.PRODUCT_CATEGORIES.Any()
                            ? db.PRODUCT_CATEGORIES.Max(x => x.SortOrder)
                            : 0;
                        newCategory.SortOrder = maxSortOrder + 1;


                        db.Entry(newCategory).State = EntityState.Modified;
                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "ProductCategoryController",
                            $"{newCategory.Name} (Kod: {newCategory.Code}) - Maxsulot turini yaratdi");

                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"O'zgarishlarni saqlab bo'lmadi: {ex.Message}. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                    }
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
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var category = db.PRODUCT_CATEGORIES.Find(id);
                if (category == null || !category.IsActive)
                {
                    return HttpNotFound();
                }

                return View(category);
            }
        }

        // POST: ProductCategory/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "ID,Code,Name,IsRawMaterial,IsFinishedGood,IsComponent,IsConsumable,RequiresNORM,RequiresCutting,IncludeInMRP,AllowPurchase,AllowSales,RawMaterialGroup,RawMaterialSubGrp,SortOrder,IsActive,CreatedDT,CalculateByWeight")] PRODUCT_CATEGORIES category)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
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
                        categoryToUpdate.CalculateByWeight = category.CalculateByWeight;
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
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var category = db.PRODUCT_CATEGORIES.Find(id);
                if (category == null || !category.IsActive)
                {
                    return HttpNotFound();
                }

                return View(category);
            }
        }

        // POST: ProductCategory/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var category = db.PRODUCT_CATEGORIES.Find(id);
                if (category == null)
                {
                    return HttpNotFound();
                }
                category.IsDeleted = true;
                db.Entry(category).State = System.Data.Entity.EntityState.Modified;

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
        }

        protected override void Dispose(bool disposing)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (disposing)
                {
                    db.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }
}
