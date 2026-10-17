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
    public class FinPlItemsController : Controller
    {
        // Olish so'rovi: FinPlItems
        public ActionResult Index(string searchTerm, int? parentID)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinPlItems"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.FIN_PL_ITEMS
                    .Where(i => i.IsActive && (i.IsDeleted == false || i.IsDeleted == null));

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(i => i.Name.Contains(searchTerm) || (i.Code != null && i.Code.Contains(searchTerm)));
                }

                // parentID=0 — faqat ildiz elementlar (avval bu tarmoq HasValue'dan keyin turgani uchun ishlamasdi, FIN-B25)
                if (parentID == 0)
                {
                    query = query.Where(i => i.ParentID == null);
                }
                else if (parentID.HasValue)
                {
                    query = query.Where(i => i.ParentID == parentID.Value);
                }

                var items = query.OrderBy(i => i.SortOrder).ThenBy(i => i.Name).ToList();
                var tree = BuildItemTree(items, parentID > 0 ? parentID : null);

                ViewBag.SearchTerm = searchTerm;
                ViewBag.ParentID = parentID;

                return View(tree);
            }
        }

        private List<PlItemTreeItem> BuildItemTree(List<FIN_PL_ITEMS> allItems, int? parentID)
        {
            var result = new List<PlItemTreeItem>();
            var children = allItems.Where(i => i.ParentID == parentID).OrderBy(i => i.SortOrder).ThenBy(i => i.Name);

            foreach (var item in children)
            {
                var treeItem = new PlItemTreeItem
                {
                    Item = item,
                    Children = BuildItemTree(allItems, item.ID)
                };
                result.Add(treeItem);
            }

            return result;
        }

        // Olish so'rovi: FinPlItems/Details/5
        public ActionResult Details(int? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinPlItems"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var item = db.FIN_PL_ITEMS
                    .Where(i => i.ID == id && (i.IsDeleted == false || i.IsDeleted == null))
                    .Include(i => i.FIN_PL_ITEMS2)
                    .FirstOrDefault();

                if (item == null)
                {
                    return HttpNotFound();
                }

                ViewBag.ChildItems = db.FIN_PL_ITEMS
                    .Where(i => i.ParentID == id && i.IsActive)
                    .OrderBy(i => i.SortOrder)
                    .ToList();

                // Olish mapped categories
                ViewBag.MappedCategories = db.FIN_CATEGORIES
                    .Where(c => c.FIN_PL_ITEMS.Any(pi => pi.ID == id) && c.IsActive)
                    .ToList();

                // Olish plans count
                ViewBag.PlanCount = db.FIN_PL_PLANS.Count(p => p.ItemID == id);

                return View(item);
            }
        }

        // Olish so'rovi: FinPlItems/yaratish
        public ActionResult Create(int? parentID)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinPlItems"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.ParentID = new SelectList(
                    db.FIN_PL_ITEMS.Where(i => i.IsActive).OrderBy(i => i.Name).ToList(),
                    "ID", "Name", parentID);

                ViewBag.SignList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "+ (Musbat)" },
                    new { Value = (byte)2, Text = "- (Manfiy)" }
                }, "Value", "Text");

                var model = new FIN_PL_ITEMS
                {
                    ParentID = parentID,
                    IsActive = true,
                    Sign = 1, // standart positive
                    SortOrder = db.FIN_PL_ITEMS.Where(i => i.ParentID == parentID).Count() + 1
                };

                return View(model);
            }
        }

        // POST so'rovi: FinPlItems/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(FIN_PL_ITEMS item)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinPlItems"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    item.IsActive = true;
                    if (item.SortOrder == 0)
                    {
                        item.SortOrder = db.FIN_PL_ITEMS
                            .Where(i => i.ParentID == item.ParentID)
                            .Count() + 1;
                    }

                    db.FIN_PL_ITEMS.Add(item);
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "FinPlItems", "Create", $"P&L element yaratildi: {item.Name}");

                    return RedirectToAction("Index");
                }

                ViewBag.ParentID = new SelectList(
                    db.FIN_PL_ITEMS.Where(i => i.IsActive).OrderBy(i => i.Name).ToList(),
                    "ID", "Name", item.ParentID);

                ViewBag.SignList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "+ (Musbat)" },
                    new { Value = (byte)2, Text = "- (Manfiy)" }
                }, "Value", "Text", item.Sign);

                return View(item);
            }
        }

        // Olish so'rovi: FinPlItems/Edit/5
        public ActionResult Edit(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinPlItems"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var item = db.FIN_PL_ITEMS.Find(id);
                if (item == null)
                {
                    return HttpNotFound();
                }

                var excludeIds = GetDescendantIds(db, id.Value);
                excludeIds.Add(id.Value);

                ViewBag.ParentID = new SelectList(
                    db.FIN_PL_ITEMS.Where(i => i.IsActive && !excludeIds.Contains(i.ID)).OrderBy(i => i.Name).ToList(),
                    "ID", "Name", item.ParentID);

                ViewBag.SignList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "+ (Musbat)" },
                    new { Value = (byte)2, Text = "- (Manfiy)" }
                }, "Value", "Text", item.Sign);

                return View(item);
            }
        }

        // POST so'rovi: FinPlItems/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(FIN_PL_ITEMS item)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinPlItems"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    var itemToUpdate = db.FIN_PL_ITEMS.Find(item.ID);
                    if (itemToUpdate != null)
                    {
                        var excludeIds = GetDescendantIds(db, item.ID);
                        excludeIds.Add(item.ID);
                        if (item.ParentID.HasValue && excludeIds.Contains(item.ParentID.Value))
                        {
                            ModelState.AddModelError("ParentID", "Ota element sifatida o'zini yoki farzandini tanlash mumkin emas.");
                            ViewBag.ParentID = new SelectList(
                                db.FIN_PL_ITEMS.Where(i => i.IsActive && !excludeIds.Contains(i.ID)).OrderBy(i => i.Name).ToList(),
                                "ID", "Name", item.ParentID);
                            ViewBag.SignList = new SelectList(new[] {
                                new { Value = (byte)1, Text = "+ (Musbat)" },
                                new { Value = (byte)2, Text = "- (Manfiy)" }
                            }, "Value", "Text", item.Sign);
                            return View(item);
                        }

                        itemToUpdate.Code = item.Code;
                        itemToUpdate.Name = item.Name;
                        itemToUpdate.Sign = item.Sign;
                        itemToUpdate.ParentID = item.ParentID;
                        itemToUpdate.SortOrder = item.SortOrder;

                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "FinPlItems", "Edit", $"P&L element tahrirlandi: {itemToUpdate.Name}");

                        return RedirectToAction("Index");
                    }
                }

                var excludeIds2 = GetDescendantIds(db, item.ID);
                excludeIds2.Add(item.ID);
                ViewBag.ParentID = new SelectList(
                    db.FIN_PL_ITEMS.Where(i => i.IsActive && !excludeIds2.Contains(i.ID)).OrderBy(i => i.Name).ToList(),
                    "ID", "Name", item.ParentID);
                ViewBag.SignList = new SelectList(new[] {
                    new { Value = (byte)1, Text = "+ (Musbat)" },
                    new { Value = (byte)2, Text = "- (Manfiy)" }
                }, "Value", "Text", item.Sign);

                return View(item);
            }
        }

        // Olish so'rovi: FinPlItems/o'chirish/5
        public ActionResult Delete(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinPlItems"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var item = db.FIN_PL_ITEMS.Find(id);
                if (item == null)
                {
                    return HttpNotFound();
                }

                ViewBag.HasChildren = db.FIN_PL_ITEMS.Any(i => i.ParentID == id && i.IsActive);
                ViewBag.HasPlans = db.FIN_PL_PLANS.Any(p => p.ItemID == id);
                ViewBag.HasCategories = db.FIN_CATEGORIES.Any(c => c.FIN_PL_ITEMS.Any(pi => pi.ID == id));

                return View(item);
            }
        }

        // POST so'rovi: FinPlItems/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinPlItems"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var item = db.FIN_PL_ITEMS.Find(id);
                if (item != null)
                {
                    item.IsActive = false;
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "FinPlItems", "Delete", $"P&L element o'chirildi: {item.Name}");
                }

                return RedirectToAction("Index");
            }
        }

        // Olish so'rovi: FinPlItems/MapCategories/5
        public ActionResult MapCategories(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinPlItems"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var item = db.FIN_PL_ITEMS.Find(id);
                if (item == null)
                {
                    return HttpNotFound();
                }

                var allCategories = db.FIN_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
                var mappedCategoryIds = db.FIN_CATEGORIES
                    .Where(c => c.FIN_PL_ITEMS.Any(pi => pi.ID == id))
                    .Select(c => c.ID)
                    .ToList();

                ViewBag.AllCategories = allCategories;
                ViewBag.MappedCategoryIds = mappedCategoryIds;

                return View(item);
            }
        }

        // POST so'rovi: FinPlItems/MapCategories/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MapCategories(int id, int[] categoryIds)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinPlItems"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var item = db.FIN_PL_ITEMS
                    .Include(i => i.FIN_CATEGORIES)
                    .FirstOrDefault(i => i.ID == id);

                if (item != null)
                {
                    // Clear existing mappings
                    item.FIN_CATEGORIES.Clear();

                    // Qo'shish new mappings
                    if (categoryIds != null && categoryIds.Length > 0)
                    {
                        var categories = db.FIN_CATEGORIES.Where(c => categoryIds.Contains(c.ID)).ToList();
                        foreach (var category in categories)
                        {
                            item.FIN_CATEGORIES.Add(category);
                        }
                    }

                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "FinPlItems", "MapCategories", $"P&L element kategoriyalari yangilandi: {item.Name}");
                }

                return RedirectToAction("Details", new { id = id });
            }
        }

        private List<int> GetDescendantIds(DBTHSNEntities db, int itemId)
        {
            var result = new List<int>();
            var children = db.FIN_PL_ITEMS.Where(i => i.ParentID == itemId && i.IsActive).Select(i => i.ID).ToList();
            result.AddRange(children);
            foreach (var childId in children)
            {
                result.AddRange(GetDescendantIds(db, childId));
            }
            return result;
        }
    }

    public class PlItemTreeItem
    {
        public FIN_PL_ITEMS Item { get; set; }
        public List<PlItemTreeItem> Children { get; set; }
    }
}
