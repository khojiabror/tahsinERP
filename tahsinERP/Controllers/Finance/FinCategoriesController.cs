using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Filters;
using tahsinERP.Services.Finance;
using tahsinERP.Controllers;
using tahsinERP.Services.Common;

namespace tahsinERP.Controllers.Finance
{
    [Permission("FinCategories")]
    public class FinCategoriesController : Controller
    {
        // Olish so'rovi: FinCategories
        public ActionResult Index(string searchTerm, int? type, int? parentID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.FIN_CATEGORIES.AsNoTracking()
                    .Include(c => c.FIN_CATEGORIES2) // ota nomi view'da; AsNoTracking'da avtomatik bog'lanmaydi
                    .Where(c => c.IsActive && (c.IsDeleted == false || c.IsDeleted == null));

                // Search filtrlash
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(c => c.Name.Contains(searchTerm));
                }

                // Type filtrlash (1=Income, 2=Expense, 3=Other)
                if (type.HasValue)
                {
                    query = query.Where(c => c.Type == type.Value);
                }

                // Parent filtrlash
                // parentID=0 — faqat ildiz elementlar (avval bu tarmoq HasValue'dan keyin turgani uchun ishlamasdi, FIN-B25)
                if (parentID == 0)
                {
                    query = query.Where(c => c.ParentID == null);
                }
                else if (parentID.HasValue)
                {
                    query = query.Where(c => c.ParentID == parentID.Value);
                }

                var categories = query.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToList();

                // Build tree structure
                // parentID bo'yicha filtrlanganda daraxt shu elementdan quriladi (avval bo'sh chiqardi)
                var tree = BuildCategoryTree(categories, parentID > 0 ? parentID : null);

                ViewBag.SearchTerm = searchTerm;
                ViewBag.Type = type;
                ViewBag.ParentID = parentID;
                ViewBag.TypeList = new SelectList(new[] {
                    new { Value = (int?)null, Text = "Barcha" },
                    new { Value = (int?)1, Text = "Daromad" },
                    new { Value = (int?)2, Text = "Chiqim" },
                    new { Value = (int?)3, Text = "Boshqa" }
                }, "Value", "Text", type);

                return View(tree);
            }
        }

        private static List<CategoryTreeItem> BuildCategoryTree(List<FIN_CATEGORIES> allCategories, int? parentID)
        {
            return FinCategoryTree.Build<FIN_CATEGORIES, CategoryTreeItem>(allCategories, c => c.ID, c => c.ParentID, parentID,
                (c, children) => new CategoryTreeItem { Category = c, Children = children });
        }

        // Olish so'rovi: FinCategories/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var category = db.FIN_CATEGORIES.AsNoTracking()
                    .Where(c => c.ID == id && (c.IsDeleted == false || c.IsDeleted == null))
                    .Include(c => c.FIN_CATEGORIES2)
                    .FirstOrDefault();

                if (category == null)
                {
                    return HttpNotFound();
                }

                // Olish child categories
                ViewBag.ChildCategories = db.FIN_CATEGORIES.AsNoTracking()
                    .Where(c => c.ParentID == id && c.IsActive)
                    .OrderBy(c => c.SortOrder)
                    .ToList();

                // Olish transaction count
                ViewBag.TransactionCount = db.FIN_TRANSACTIONS.Count(t => t.CategoryID == id && t.IsDeleted != true);

                return View(category);
            }
        }

        // Olish so'rovi: FinCategories/yaratish
        public ActionResult Create(int? parentID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.ParentID = new SelectList(
                    db.FIN_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name).ToList(),
                    "ID", "Name", parentID);

                ViewBag.TypeList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "Daromad" },
                    new { Value = (byte)2, Text = "Chiqim" },
                    new { Value = (byte)3, Text = "Boshqa" }
                }, "Value", "Text");

                var model = new FIN_CATEGORIES
                {
                    ParentID = parentID,
                    IsActive = true,
                    SortOrder = db.FIN_CATEGORIES.Where(c => c.ParentID == parentID).Count() + 1
                };

                return View(model);
            }
        }

        // POST so'rovi: FinCategories/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(FIN_CATEGORIES category)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    category.IsActive = true;
                    if (category.SortOrder == 0)
                    {
                        category.SortOrder = db.FIN_CATEGORIES
                            .Where(c => c.ParentID == category.ParentID)
                            .Count() + 1;
                    }

                    db.FIN_CATEGORIES.Add(category);
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "FinCategories", "Create", $"Kategoriya yaratildi: {category.Name}");

                    return RedirectToAction("Index");
                }

                ViewBag.ParentID = new SelectList(
                    db.FIN_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name).ToList(),
                    "ID", "Name", category.ParentID);

                ViewBag.TypeList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "Daromad" },
                    new { Value = (byte)2, Text = "Chiqim" },
                    new { Value = (byte)3, Text = "Boshqa" }
                }, "Value", "Text", category.Type);

                return View(category);
            }
        }

        // Olish so'rovi: FinCategories/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var category = db.FIN_CATEGORIES.Find(id);
                if (category == null)
                {
                    return HttpNotFound();
                }

                // Exclude self va descendants dan parent list
                var excludeIds = GetDescendantIds(db, id.Value);
                excludeIds.Add(id.Value);

                ViewBag.ParentID = new SelectList(
                    db.FIN_CATEGORIES.Where(c => c.IsActive && !excludeIds.Contains(c.ID)).OrderBy(c => c.Name).ToList(),
                    "ID", "Name", category.ParentID);

                ViewBag.TypeList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "Daromad" },
                    new { Value = (byte)2, Text = "Chiqim" },
                    new { Value = (byte)3, Text = "Boshqa" }
                }, "Value", "Text", category.Type);

                return View(category);
            }
        }

        // POST so'rovi: FinCategories/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(FIN_CATEGORIES category)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    var categoryToUpdate = db.FIN_CATEGORIES.Find(category.ID);
                    if (categoryToUpdate != null)
                    {
                        // Prevent circular reference
                        var excludeIds = GetDescendantIds(db, category.ID);
                        excludeIds.Add(category.ID);
                        if (category.ParentID.HasValue && excludeIds.Contains(category.ParentID.Value))
                        {
                            ModelState.AddModelError("ParentID", "Ota kategoriya sifatida o'zini yoki farzandini tanlash mumkin emas.");
                            ViewBag.ParentID = new SelectList(
                                db.FIN_CATEGORIES.Where(c => c.IsActive && !excludeIds.Contains(c.ID)).OrderBy(c => c.Name).ToList(),
                                "ID", "Name", category.ParentID);
                            ViewBag.TypeList = new SelectList(new[] {
                                new { Value = (byte)1, Text = "Daromad" },
                                new { Value = (byte)2, Text = "Chiqim" },
                                new { Value = (byte)3, Text = "Boshqa" }
                            }, "Value", "Text", category.Type);
                            return View(category);
                        }

                        categoryToUpdate.Name = category.Name;
                        categoryToUpdate.Type = category.Type;
                        categoryToUpdate.ParentID = category.ParentID;
                        categoryToUpdate.SortOrder = category.SortOrder;

                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "FinCategories", "Edit", $"Kategoriya tahrirlandi: {categoryToUpdate.Name}");

                        return RedirectToAction("Index");
                    }
                }

                var excludeIds2 = GetDescendantIds(db, category.ID);
                excludeIds2.Add(category.ID);
                ViewBag.ParentID = new SelectList(
                    db.FIN_CATEGORIES.Where(c => c.IsActive && !excludeIds2.Contains(c.ID)).OrderBy(c => c.Name).ToList(),
                    "ID", "Name", category.ParentID);
                ViewBag.TypeList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "Daromad" },
                    new { Value = (byte)2, Text = "Chiqim" },
                    new { Value = (byte)3, Text = "Boshqa" }
                }, "Value", "Text", category.Type);

                return View(category);
            }
        }

        // Olish so'rovi: FinCategories/o'chirish/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var category = db.FIN_CATEGORIES.Find(id);
                if (category == null)
                {
                    return HttpNotFound();
                }

                // Tekshirish agar category has children
                ViewBag.HasChildren = db.FIN_CATEGORIES.Any(c => c.ParentID == id && c.IsActive);
                // Tekshirish agar category has transactions
                ViewBag.HasTransactions = db.FIN_TRANSACTIONS.Any(t => t.CategoryID == id && t.IsDeleted != true);

                return View(category);
            }
        }

        // POST so'rovi: FinCategories/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var category = db.FIN_CATEGORIES.Find(id);
                if (category != null)
                {
                    // Tizim kategoriyasi (SystemCode) — hisobotlar unga bog'langan, o'chirilmaydi
                    if (SystemCodes.IsSystem(category.SystemCode))
                        return RedirectToAction("Delete", new { id });

                    // Soft o'chirish
                    category.IsActive = false;
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "FinCategories", "Delete", $"Kategoriya o'chirildi: {category.Name}");
                }

                return RedirectToAction("Index");
            }
        }

        // FIN-P16: (ID, ParentID) bir marta yuklanadi, daraxt xotirada aylanib chiqiladi
        private static List<int> GetDescendantIds(DBTHSNEntities db, int categoryId)
        {
            return FinCategoryTree.CategoryDescendants(db, categoryId);
        }
    }

    public class CategoryTreeItem
    {
        public FIN_CATEGORIES Category { get; set; }
        public List<CategoryTreeItem> Children { get; set; }
    }
}
