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
        // GET: FinBalanceCategories
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

        // GET: FinBalanceCategories/Details/5
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
                    .Include(c => c.FIN_BALANCE_CATEGORIES2)
                    .FirstOrDefault(c => c.ID == id);

                if (category == null)
                {
                    return HttpNotFound();
                }

                ViewBag.ChildCategories = db.FIN_BALANCE_CATEGORIES
                    .Where(c => c.ParentID == id && c.IsActive)
                    .OrderBy(c => c.SortOrder)
                    .ToList();

                ViewBag.SnapshotCount = db.FIN_BALANCE_SNAPSHOTS.Count(s => s.CategoryID == id);

                return View(category);
            }
        }

        // GET: FinBalanceCategories/Create
        public ActionResult Create(int? parentID)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinBalanceCategories"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.ParentID = new SelectList(
                    db.FIN_BALANCE_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name),
                    "ID", "Name", parentID);

                ViewBag.TypeList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "Aktiv" },
                    new { Value = (byte)2, Text = "Passiv" },
                    new { Value = (byte)3, Text = "Kapital" }
                }, "Value", "Text");

                var model = new FIN_BALANCE_CATEGORIES
                {
                    ParentID = parentID,
                    IsActive = true,
                    SortOrder = db.FIN_BALANCE_CATEGORIES.Where(c => c.ParentID == parentID).Count() + 1
                };

                return View(model);
            }
        }

        // POST: FinBalanceCategories/Create
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
                    db.FIN_BALANCE_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name),
                    "ID", "Name", category.ParentID);

                ViewBag.TypeList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "Aktiv" },
                    new { Value = (byte)2, Text = "Passiv" },
                    new { Value = (byte)3, Text = "Kapital" }
                }, "Value", "Text", category.Type);

                return View(category);
            }
        }

        // GET: FinBalanceCategories/Edit/5
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
                    db.FIN_BALANCE_CATEGORIES.Where(c => c.IsActive && !excludeIds.Contains(c.ID)).OrderBy(c => c.Name),
                    "ID", "Name", category.ParentID);

                ViewBag.TypeList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "Aktiv" },
                    new { Value = (byte)2, Text = "Passiv" },
                    new { Value = (byte)3, Text = "Kapital" }
                }, "Value", "Text", category.Type);

                return View(category);
            }
        }

        // POST: FinBalanceCategories/Edit/5
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
                                db.FIN_BALANCE_CATEGORIES.Where(c => c.IsActive && !excludeIds.Contains(c.ID)).OrderBy(c => c.Name),
                                "ID", "Name", category.ParentID);
                            ViewBag.TypeList = new SelectList(new[] {
                                new { Value = (byte)1, Text = "Aktiv" },
                                new { Value = (byte)2, Text = "Passiv" },
                                new { Value = (byte)3, Text = "Kapital" }
                            }, "Value", "Text", category.Type);
                            return View(category);
                        }

                        categoryToUpdate.Name = category.Name;
                        categoryToUpdate.Type = category.Type;
                        categoryToUpdate.ParentID = category.ParentID;
                        categoryToUpdate.SortOrder = category.SortOrder;

                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "FinBalanceCategories", "Edit", $"Balans kategoriyasi tahrirlandi: {categoryToUpdate.Name}");

                        return RedirectToAction("Index");
                    }
                }

                var excludeIds2 = GetDescendantIds(db, category.ID);
                excludeIds2.Add(category.ID);
                ViewBag.ParentID = new SelectList(
                    db.FIN_BALANCE_CATEGORIES.Where(c => c.IsActive && !excludeIds2.Contains(c.ID)).OrderBy(c => c.Name),
                    "ID", "Name", category.ParentID);
                ViewBag.TypeList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "Aktiv" },
                    new { Value = (byte)2, Text = "Passiv" },
                    new { Value = (byte)3, Text = "Kapital" }
                }, "Value", "Text", category.Type);

                return View(category);
            }
        }

        // GET: FinBalanceCategories/Delete/5
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

        // POST: FinBalanceCategories/Delete/5
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
