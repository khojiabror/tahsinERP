using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Controllers;

namespace tahsinERP.Controllers.Finance
{
    public class FinBalanceCategoriesController : Controller
    {
        // Olish so'rovi: FinBalanceCategories
        public ActionResult Index(string searchTerm, int? type, int? parentID)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinBalanceCategories"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.FIN_BALANCE_CATEGORIES.Where(c => c.IsActive);

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(c => c.Name.Contains(searchTerm));
                }

                if (type.HasValue)
                {
                    query = query.Where(c => c.Type == type.Value);
                }

                if (parentID.HasValue)
                {
                    query = query.Where(c => c.ParentID == parentID.Value);
                }
                else if (parentID == 0)
                {
                    query = query.Where(c => c.ParentID == null);
                }

                var categories = query.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToList();
                var tree = BuildCategoryTree(categories, null);

                ViewBag.SearchTerm = searchTerm;
                ViewBag.Type = type;
                ViewBag.ParentID = parentID;
                ViewBag.TypeList = new SelectList(new[] {
                    new { Value = (int?)null, Text = "Barcha" },
                    new { Value = (int?)1, Text = "Aktiv" },
                    new { Value = (int?)2, Text = "Passiv" },
                    new { Value = (int?)3, Text = "Kapital" }
                }, "Value", "Text", type);

                return View(tree);
            }
        }

        private List<BalanceCategoryTreeItem> BuildCategoryTree(List<FIN_BALANCE_CATEGORIES> allCategories, int? parentID)
        {
            var result = new List<BalanceCategoryTreeItem>();
            var children = allCategories.Where(c => c.ParentID == parentID).OrderBy(c => c.SortOrder).ThenBy(c => c.Name);

            foreach (var category in children)
            {
                var item = new BalanceCategoryTreeItem
                {
                    Category = category,
                    Children = BuildCategoryTree(allCategories, category.ID)
                };
                result.Add(item);
            }

            return result;
        }

        // Olish so'rovi: FinBalanceCategories/Details/5
        public ActionResult Details(int? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinBalanceCategories"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var category = db.FIN_BALANCE_CATEGORIES
                    .Where(c => c.ID == id && (c.IsDeleted == false || c.IsDeleted == null))
                    .Include(c => c.FIN_BALANCE_CATEGORIES2)
                    .FirstOrDefault();

                if (category == null)
                {
                    return HttpNotFound();
                }

                ViewBag.ChildCategories = db.FIN_BALANCE_CATEGORIES
                    .Where(c => c.ParentID == id && c.IsActive)
                    .OrderBy(c => c.SortOrder)
                    .ToList();

                ViewBag.SnapshotCount = db.FIN_BALANCE_SNAPSHOTS.Count(s => s.CategoryID == id);

                // Olish mapped categories - handle case where relationship might not be configured
                List<FIN_CATEGORIES> mappedCategories = new List<FIN_CATEGORIES>();
                try
                {
                    var balanceCategoryWithCategories = db.FIN_BALANCE_CATEGORIES
                        .Include(c => c.FIN_CATEGORIES)
                        .FirstOrDefault(c => c.ID == id);
                        
                    if (balanceCategoryWithCategories?.FIN_CATEGORIES != null)
                    {
                        mappedCategories = balanceCategoryWithCategories.FIN_CATEGORIES
                            .Where(c => c.IsActive)
                            .ToList();
                    }
                }
                catch
                {
                    // agar relationship is not configured, return empty list
                    mappedCategories = new List<FIN_CATEGORIES>();
                }
                
                ViewBag.MappedCategories = mappedCategories;

                return View(category);
            }
        }

        // Olish so'rovi: FinBalanceCategories/yaratish
        public ActionResult Create(int? parentID)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinBalanceCategories"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.ParentID = new SelectList(
                    db.FIN_BALANCE_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name).ToList(),
                    "ID", "Name", parentID);

                ViewBag.TypeList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "Aktiv" },
                    new { Value = (byte)2, Text = "Passiv" },
                    new { Value = (byte)3, Text = "Kapital" }
                }, "Value", "Text");

                ViewBag.CalculationTypeList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "Hisob asosida (Account-Based)" },
                    new { Value = (byte)2, Text = "Tranzaksiya asosida (Transaction-Based)" },
                    new { Value = (byte)3, Text = "Hujjat asosida (Document-Based)" }
                }, "Value", "Text", 1); // standart: Account-Based

                var model = new FIN_BALANCE_CATEGORIES
                {
                    ParentID = parentID,
                    IsActive = true,
                    CalculationType = 1, // standart: Account-Based
                    SortOrder = db.FIN_BALANCE_CATEGORIES.Where(c => c.ParentID == parentID).Count() + 1
                };

                return View(model);
            }
        }

        // POST so'rovi: FinBalanceCategories/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(FIN_BALANCE_CATEGORIES category)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinBalanceCategories"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    category.IsActive = true;
                    if (category.SortOrder == 0)
                    {
                        category.SortOrder = db.FIN_BALANCE_CATEGORIES
                            .Where(c => c.ParentID == category.ParentID)
                            .Count() + 1;
                    }

                    db.FIN_BALANCE_CATEGORIES.Add(category);
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "FinBalanceCategories", "Create", $"Balans kategoriyasi yaratildi: {category.Name}");

                    return RedirectToAction("Index");
                }

                ViewBag.ParentID = new SelectList(
                    db.FIN_BALANCE_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name).ToList(),
                    "ID", "Name", category.ParentID);

                ViewBag.TypeList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "Aktiv" },
                    new { Value = (byte)2, Text = "Passiv" },
                    new { Value = (byte)3, Text = "Kapital" }
                }, "Value", "Text", category.Type);

                ViewBag.CalculationTypeList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "Hisob asosida (Account-Based)" },
                    new { Value = (byte)2, Text = "Tranzaksiya asosida (Transaction-Based)" },
                    new { Value = (byte)3, Text = "Hujjat asosida (Document-Based)" }
                }, "Value", "Text", category.CalculationType);

                return View(category);
            }
        }

        // Olish so'rovi: FinBalanceCategories/Edit/5
        public ActionResult Edit(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinBalanceCategories"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var category = db.FIN_BALANCE_CATEGORIES.Find(id);
                if (category == null)
                {
                    return HttpNotFound();
                }

                var excludeIds = GetDescendantIds(db, id.Value);
                excludeIds.Add(id.Value);

                ViewBag.ParentID = new SelectList(
                    db.FIN_BALANCE_CATEGORIES.Where(c => c.IsActive && !excludeIds.Contains(c.ID)).OrderBy(c => c.Name).ToList(),
                    "ID", "Name", category.ParentID);

                ViewBag.TypeList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "Aktiv" },
                    new { Value = (byte)2, Text = "Passiv" },
                    new { Value = (byte)3, Text = "Kapital" }
                }, "Value", "Text", category.Type);

                ViewBag.CalculationTypeList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "Hisob asosida (Account-Based)" },
                    new { Value = (byte)2, Text = "Tranzaksiya asosida (Transaction-Based)" },
                    new { Value = (byte)3, Text = "Hujjat asosida (Document-Based)" }
                }, "Value", "Text", category.CalculationType);

                return View(category);
            }
        }

        // POST so'rovi: FinBalanceCategories/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(FIN_BALANCE_CATEGORIES category)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinBalanceCategories"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    var categoryToUpdate = db.FIN_BALANCE_CATEGORIES.Find(category.ID);
                    if (categoryToUpdate != null)
                    {
                        var excludeIds = GetDescendantIds(db, category.ID);
                        excludeIds.Add(category.ID);
                        if (category.ParentID.HasValue && excludeIds.Contains(category.ParentID.Value))
                        {
                            ModelState.AddModelError("ParentID", "Ota kategoriya sifatida o'zini yoki farzandini tanlash mumkin emas.");
                            ViewBag.ParentID = new SelectList(
                                db.FIN_BALANCE_CATEGORIES.Where(c => c.IsActive && !excludeIds.Contains(c.ID)).OrderBy(c => c.Name).ToList(),
                                "ID", "Name", category.ParentID);
                            ViewBag.TypeList = new SelectList(new[] {
                                new { Value = (byte)1, Text = "Aktiv" },
                                new { Value = (byte)2, Text = "Passiv" },
                                new { Value = (byte)3, Text = "Kapital" }
                            }, "Value", "Text", category.Type);
                            ViewBag.CalculationTypeList = new SelectList(new[] {
                                new { Value = (byte)1, Text = "Hisob asosida (Account-Based)" },
                                new { Value = (byte)2, Text = "Tranzaksiya asosida (Transaction-Based)" },
                                new { Value = (byte)3, Text = "Hujjat asosida (Document-Based)" }
                            }, "Value", "Text", category.CalculationType);
                            return View(category);
                        }

                        categoryToUpdate.Name = category.Name;
                        categoryToUpdate.Type = category.Type;
                        categoryToUpdate.ParentID = category.ParentID;
                        categoryToUpdate.SortOrder = category.SortOrder;
                        categoryToUpdate.CalculationType = category.CalculationType;

                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "FinBalanceCategories", "Edit", $"Balans kategoriyasi tahrirlandi: {categoryToUpdate.Name}");

                        return RedirectToAction("Index");
                    }
                }

                var excludeIds2 = GetDescendantIds(db, category.ID);
                excludeIds2.Add(category.ID);
                ViewBag.ParentID = new SelectList(
                    db.FIN_BALANCE_CATEGORIES.Where(c => c.IsActive && !excludeIds2.Contains(c.ID)).OrderBy(c => c.Name).ToList(),
                    "ID", "Name", category.ParentID);
                ViewBag.TypeList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "Aktiv" },
                    new { Value = (byte)2, Text = "Passiv" },
                    new { Value = (byte)3, Text = "Kapital" }
                }, "Value", "Text", category.Type);

                ViewBag.CalculationTypeList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "Hisob asosida (Account-Based)" },
                    new { Value = (byte)2, Text = "Tranzaksiya asosida (Transaction-Based)" },
                    new { Value = (byte)3, Text = "Hujjat asosida (Document-Based)" }
                }, "Value", "Text", category.CalculationType);

                return View(category);
            }
        }

        // Olish so'rovi: FinBalanceCategories/o'chirish/5
        public ActionResult Delete(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinBalanceCategories"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var category = db.FIN_BALANCE_CATEGORIES.Find(id);
                if (category == null)
                {
                    return HttpNotFound();
                }

                ViewBag.HasChildren = db.FIN_BALANCE_CATEGORIES.Any(c => c.ParentID == id && c.IsActive);
                ViewBag.HasSnapshots = db.FIN_BALANCE_SNAPSHOTS.Any(s => s.CategoryID == id);

                return View(category);
            }
        }

        // POST so'rovi: FinBalanceCategories/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinBalanceCategories"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var category = db.FIN_BALANCE_CATEGORIES.Find(id);
                if (category != null)
                {
                    category.IsActive = false;
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "FinBalanceCategories", "Delete", $"Balans kategoriyasi o'chirildi: {category.Name}");
                }

                return RedirectToAction("Index");
            }
        }

        // Olish so'rovi: FinBalanceCategories/MapCategories/5
        public ActionResult MapCategories(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinBalanceCategories"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var category = db.FIN_BALANCE_CATEGORIES.Find(id);
                    
                if (category == null)
                {
                    return HttpNotFound();
                }

                var allCategories = db.FIN_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
                
                // Olish mapped category IDs dan relationship - handle case where relationship might not be configured
                List<int> mappedCategoryIds = new List<int>();
                try
                {
                    var categoryWithMappings = db.FIN_BALANCE_CATEGORIES
                        .Include(c => c.FIN_CATEGORIES)
                        .FirstOrDefault(c => c.ID == id);
                        
                    if (categoryWithMappings?.FIN_CATEGORIES != null)
                    {
                        mappedCategoryIds = categoryWithMappings.FIN_CATEGORIES
                            .Select(c => c.ID)
                            .ToList();
                    }
                }
                catch
                {
                    // agar relationship is not configured, return empty list
                    mappedCategoryIds = new List<int>();
                }

                ViewBag.AllCategories = allCategories;
                ViewBag.MappedCategoryIds = mappedCategoryIds;

                return View(category);
            }
        }

        // POST so'rovi: FinBalanceCategories/MapCategories/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MapCategories(int id, int[] categoryIds)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinBalanceCategories"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var category = db.FIN_BALANCE_CATEGORIES.Find(id);
                    
                if (category == null)
                {
                    return HttpNotFound();
                }

                try
                {
                    // Urinish ga use relationship agar it mavjud
                    var categoryWithMappings = db.FIN_BALANCE_CATEGORIES
                        .Include(c => c.FIN_CATEGORIES)
                        .FirstOrDefault(c => c.ID == id);
                        
                    if (categoryWithMappings?.FIN_CATEGORIES != null)
                    {
                        // Clear existing mappings
                        categoryWithMappings.FIN_CATEGORIES.Clear();

                        // Qo'shish new mappings
                        if (categoryIds != null && categoryIds.Length > 0)
                        {
                            var categories = db.FIN_CATEGORIES.Where(c => categoryIds.Contains(c.ID)).ToList();
                            foreach (var cat in categories)
                            {
                                categoryWithMappings.FIN_CATEGORIES.Add(cat);
                            }
                        }

                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "FinBalanceCategories", "MapCategories", 
                            $"Balans kategoriyasi kategoriyalari yangilandi: {category.Name}");
                    }
                }
                catch (Exception)
                {
                    // agar relationship doesn't work, try direct SQL approach
                    try
                    {
                        // Avval, o'chirish existing mappings
                        var deleteSql = @"
                            DELETE FROM FIN_BALANCE_CATEGORY_CATEGORIES 
                            WHERE BalanceCategoryID = @p0";
                        db.Database.ExecuteSqlCommand(deleteSql, id);

                        // So'ng insert new mappings
                        if (categoryIds != null && categoryIds.Length > 0)
                        {
                            foreach (var catId in categoryIds)
                            {
                                var insertSql = @"
                                    INSERT INTO FIN_BALANCE_CATEGORY_CATEGORIES (BalanceCategoryID, CategoryID)
                                    VALUES (@p0, @p1)";
                                db.Database.ExecuteSqlCommand(insertSql, id, catId);
                            }
                        }

                        LogHelper.LogToDatabase(User.Identity.Name, "FinBalanceCategories", "MapCategories", 
                            $"Balans kategoriyasi kategoriyalari yangilandi (SQL): {category.Name}");
                    }
                    catch (Exception sqlEx)
                    {
                        // Log xatolik agar both approaches fail
                        LogHelper.LogToDatabase(User.Identity.Name, "FinBalanceCategories", "MapCategories", 
                            $"Xatolik: {sqlEx.Message}");
                    }
                }

                return RedirectToAction("Details", new { id = id });
            }
        }

        private List<int> GetDescendantIds(DBTHSNEntities db, int categoryId)
        {
            var result = new List<int>();
            var children = db.FIN_BALANCE_CATEGORIES.Where(c => c.ParentID == categoryId && c.IsActive).Select(c => c.ID).ToList();
            result.AddRange(children);
            foreach (var childId in children)
            {
                result.AddRange(GetDescendantIds(db, childId));
            }
            return result;
        }
    }

    public class BalanceCategoryTreeItem
    {
        public FIN_BALANCE_CATEGORIES Category { get; set; }
        public List<BalanceCategoryTreeItem> Children { get; set; }
    }
}
