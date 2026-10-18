using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Filters;
using tahsinERP.ViewModels.Finance;
using tahsinERP.Services.Finance;
using tahsinERP.Controllers;
using tahsinERP.Services.Common;

namespace tahsinERP.Controllers.Finance
{
    [Permission("FinBalanceCategories")]
    public class FinBalanceCategoriesController : Controller
    {
        // Olish so'rovi: FinBalanceCategories
        public ActionResult Index(string searchTerm, int? type, int? parentID)
        {
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
                // parentID bo'yicha filtrlanganda daraxt shu elementdan quriladi (avval bo'sh chiqardi)
                var tree = BuildCategoryTree(categories, parentID > 0 ? parentID : null);

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

        private static List<BalanceCategoryTreeItem> BuildCategoryTree(List<FIN_BALANCE_CATEGORIES> allCategories, int? parentID)
        {
            return FinCategoryTree.Build<FIN_BALANCE_CATEGORIES, BalanceCategoryTreeItem>(allCategories, c => c.ID, c => c.ParentID, parentID,
                (c, children) => new BalanceCategoryTreeItem { Category = c, Children = children });
        }

        private static SelectList TypeList(byte? selected)
        {
            return new SelectList(new[] {
                new { Value = (byte)1, Text = "Aktiv" },
                new { Value = (byte)2, Text = "Passiv" },
                new { Value = (byte)3, Text = "Kapital" }
            }, "Value", "Text", selected);
        }

        private static SelectList CalculationTypeList(byte? selected)
        {
            return new SelectList(new[] {
                new { Value = (byte)1, Text = "Hisob asosida (Account-Based)" },
                new { Value = (byte)2, Text = "Tranzaksiya asosida (Transaction-Based)" },
                new { Value = (byte)3, Text = "Hujjat asosida (Document-Based)" }
            }, "Value", "Text", selected);
        }

        /// <summary>Create/Edit formasi ro'yxatlari; Edit'da o'zi va avlodlari ota sifatida taklif qilinmaydi.</summary>
        private void PopulateFormLists(DBTHSNEntities db, FIN_BALANCE_CATEGORIES category, bool excludeSelfAndDescendants)
        {
            var excludeIds = excludeSelfAndDescendants ? GetDescendantIds(db, category.ID) : new List<int>();
            if (excludeSelfAndDescendants) excludeIds.Add(category.ID);
            ViewBag.ParentID = new SelectList(
                db.FIN_BALANCE_CATEGORIES.Where(c => c.IsActive && !excludeIds.Contains(c.ID)).OrderBy(c => c.Name).ToList(),
                "ID", "Name", category.ParentID);
            ViewBag.TypeList = TypeList(category.Type);
            ViewBag.CalculationTypeList = CalculationTypeList(category.CalculationType);
        }

        // Olish so'rovi: FinBalanceCategories/Details/5
        public ActionResult Details(int? id)
        {
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

                // FIN-B20: M:N bog'lanish EDMX'da bor — xato bo'sh ro'yxat bilan yashirilmaydi
                var mappedCategories = db.FIN_CATEGORIES
                    .Where(c => c.IsActive && c.FIN_BALANCE_CATEGORIES.Any(b => b.ID == id))
                    .ToList();
                ViewBag.MappedCategories = mappedCategories;

                return View(category);
            }
        }

        // Olish so'rovi: FinBalanceCategories/yaratish
        public ActionResult Create(int? parentID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var model = new FIN_BALANCE_CATEGORIES
                {
                    ParentID = parentID,
                    IsActive = true,
                    CalculationType = 1, // standart: Account-Based
                    SortOrder = db.FIN_BALANCE_CATEGORIES.Where(c => c.ParentID == parentID).Count() + 1
                };

                PopulateFormLists(db, model, false);
                ViewBag.TypeList = TypeList(null);
                return View(model);
            }
        }

        // POST so'rovi: FinBalanceCategories/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(FIN_BALANCE_CATEGORIES category)
        {
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

                PopulateFormLists(db, category, false);
                return View(category);
            }
        }

        // Olish so'rovi: FinBalanceCategories/Edit/5
        public ActionResult Edit(int? id)
        {
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

                PopulateFormLists(db, category, true);
                return View(category);
            }
        }

        // POST so'rovi: FinBalanceCategories/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(FIN_BALANCE_CATEGORIES category)
        {
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
                            PopulateFormLists(db, category, true);
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

                PopulateFormLists(db, category, true);
                return View(category);
            }
        }

        // Olish so'rovi: FinBalanceCategories/o'chirish/5
        public ActionResult Delete(int? id)
        {
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
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var category = db.FIN_BALANCE_CATEGORIES.Find(id);
                if (category != null)
                {
                    // Tizim kategoriyasi (SystemCode) — hisobotlar unga bog'langan, o'chirilmaydi
                    if (SystemCodes.IsSystem(category.SystemCode))
                        return RedirectToAction("Delete", new { id });

                    category.IsActive = false;
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "FinBalanceCategories", "Delete", $"Balans kategoriyasi o'chirildi: {category.Name}");
                }

                return RedirectToAction("Index");
            }
        }

        // Olish so'rovi: FinBalanceCategories/MapCategories/5
        [Permission("FinBalanceCategories", Change = true)]
        public ActionResult MapCategories(int? id)
        {
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

                // FIN-D16: P&L elementlari bilan umumiy view
                return View("~/Views/FinPlItems/MapCategories.cshtml", new MapCategoriesViewModel
                {
                    ID = category.ID,
                    Name = category.Name,
                    Controller = "FinBalanceCategories",
                    ListTitle = "Balans kategoriyalari",
                    AllCategories = db.FIN_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name).ToList(),
                    MappedCategoryIds = db.FIN_CATEGORIES
                        .Where(c => c.FIN_BALANCE_CATEGORIES.Any(b => b.ID == id))
                        .Select(c => c.ID)
                        .ToList()
                });
            }
        }

        // POST so'rovi: FinBalanceCategories/MapCategories/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MapCategories(int id, int[] categoryIds)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var category = db.FIN_BALANCE_CATEGORIES
                    .Include(c => c.FIN_CATEGORIES)
                    .FirstOrDefault(c => c.ID == id);
                if (category == null)
                {
                    return HttpNotFound();
                }

                // FIN-B20: avval xato bo'lsa xom SQL bilan qayta urinilar, u ham xato bersa jimgina yutilardi
                FinCategoryTree.ReplaceMappings(db, category.FIN_CATEGORIES, categoryIds);
                db.SaveChanges();

                LogHelper.LogToDatabase(User.Identity.Name, "FinBalanceCategories", "MapCategories",
                    $"Balans kategoriyasi kategoriyalari yangilandi: {category.Name}");

                return RedirectToAction("Details", new { id = id });
            }
        }

        // FIN-P16: (ID, ParentID) bir marta yuklanadi, daraxt xotirada aylanib chiqiladi
        private static List<int> GetDescendantIds(DBTHSNEntities db, int categoryId)
        {
            return FinCategoryTree.BalanceCategoryDescendants(db, categoryId);
        }
    }

    public class BalanceCategoryTreeItem
    {
        public FIN_BALANCE_CATEGORIES Category { get; set; }
        public List<BalanceCategoryTreeItem> Children { get; set; }
    }
}
