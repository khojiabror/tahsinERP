using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.Filters;
using tahsinERP.Services.Common;
using tahsinERP.Services.Finance;
using tahsinERP.Controllers;
using ClosedXML.Excel;
using System.IO;
using System.Web;
using tahsinERP.I18n;

namespace tahsinERP.Controllers.Finance
{
    [Permission("FinPlPlans")]
    public class FinPlPlansController : Controller
    {
        // Olish so'rovi: FinPlPlans
        public ActionResult Index(int? year, int? month, int? itemID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                year = year ?? DateTime.Now.Year;
                month = month ?? DateTime.Now.Month;

                var query = db.FIN_PL_PLANS.AsNoTracking()
                    .Where(p => (p.IsDeleted == false || p.IsDeleted == null) && p.Year == year.Value && p.Month == month.Value)
                    .Include(p => p.FIN_PL_ITEMS)
                    .Include(p => p.USER);

                if (itemID.HasValue)
                {
                    query = query.Where(p => p.ItemID == itemID.Value);
                }

                var plans = query.OrderBy(p => p.FIN_PL_ITEMS.SortOrder).ToList();

                ViewBag.Year = year;
                ViewBag.Month = month;
                ViewBag.ItemID = itemID;
                ViewBag.YearList = FinSelectLists.Years(year);
                ViewBag.MonthList = FinSelectLists.Months(month);
                ViewBag.ItemList = new SelectList(
                    db.FIN_PL_ITEMS.AsNoTracking().Where(i => i.IsActive).OrderBy(i => i.SortOrder).ToList(),
                    "ID", "Name", itemID);

                return View(plans);
            }
        }

        // Olish so'rovi: FinPlPlans/yaratish
        public ActionResult Create(int? year, int? month, int? itemID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                year = year ?? DateTime.Now.Year;
                month = month ?? DateTime.Now.Month;

                ViewBag.Year = year;
                ViewBag.Month = month;
                ViewBag.YearList = FinSelectLists.Years(year);
                ViewBag.MonthList = FinSelectLists.Months(month);
                ViewBag.ItemID = new SelectList(
                    db.FIN_PL_ITEMS.Where(i => i.IsActive).OrderBy(i => i.SortOrder).ToList(),
                    "ID", "Name", itemID);

                var model = new FIN_PL_PLANS
                {
                    Year = year.Value,
                    Month = (byte)month.Value,
                    ItemID = itemID ?? 0,
                    AmountPlan = 0,
                    CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                    CreatedDT = DateTime.Now
                };

                return View(model);
            }
        }

        // POST so'rovi: FinPlPlans/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(FIN_PL_PLANS plan)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Tekshirish agar plan already mavjud
                var existing = db.FIN_PL_PLANS
                    .FirstOrDefault(p => p.Year == plan.Year && p.Month == plan.Month && p.ItemID == plan.ItemID);

                if (existing != null && existing.IsDeleted != true)
                {
                    ModelState.AddModelError("", L.T("Fin_PlanExists"));
                }

                if (ModelState.IsValid)
                {
                    if (existing != null)
                    {
                        // O'chirilgan reja qayta tiklanadi (avval "allaqachon mavjud" deb rad etilardi, FIN-B11)
                        existing.AmountPlan = plan.AmountPlan;
                        existing.IsDeleted = false;
                        plan = existing;
                    }
                    else
                    {
                        plan.CreatedByUserID = UserHelper.GetUserId(User.Identity.Name);
                        plan.CreatedDT = DateTime.Now;
                        db.FIN_PL_PLANS.Add(plan);
                    }
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "FinPlPlans", "Create", $"P&L rejasi yaratildi: {plan.Year}-{plan.Month}, ElementID: {plan.ItemID}");

                    return RedirectToAction("Index", new { year = plan.Year, month = plan.Month });
                }

                ViewBag.Year = plan.Year;
                ViewBag.Month = plan.Month;
                ViewBag.YearList = FinSelectLists.Years(plan.Year);
                ViewBag.MonthList = FinSelectLists.Months(plan.Month);
                ViewBag.ItemID = new SelectList(
                    db.FIN_PL_ITEMS.Where(i => i.IsActive).OrderBy(i => i.SortOrder),
                    "ID", "Name", plan.ItemID);

                return View(plan);
            }
        }

        // Olish so'rovi: FinPlPlans/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var plan = db.FIN_PL_PLANS.Find(id);
                if (plan == null)
                {
                    return HttpNotFound();
                }

                ViewBag.YearList = FinSelectLists.Years(plan.Year);
                ViewBag.MonthList = FinSelectLists.Months(plan.Month);
                ViewBag.ItemID = new SelectList(
                    db.FIN_PL_ITEMS.Where(i => i.IsActive).OrderBy(i => i.SortOrder),
                    "ID", "Name", plan.ItemID);

                return View(plan);
            }
        }

        // POST so'rovi: FinPlPlans/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(FIN_PL_PLANS plan)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                
                if (ModelState.IsValid)
                {
                    var planToUpdate = db.FIN_PL_PLANS.Find(plan.ID);
                    if (planToUpdate != null)
                    {
                        // Tekshirish agar another plan mavjud bilan same Year/Month/ItemID
                        var existing = db.FIN_PL_PLANS
                            .FirstOrDefault(p => p.Year == plan.Year && p.Month == plan.Month && p.ItemID == plan.ItemID && p.ID != plan.ID);

                        if (existing != null)
                        {
                            ModelState.AddModelError("", L.T("Fin_PlanExistsOther"));
                            ViewBag.YearList = FinSelectLists.Years(plan.Year);
                            ViewBag.MonthList = FinSelectLists.Months(plan.Month);
                            ViewBag.ItemID = new SelectList(
                                db.FIN_PL_ITEMS.Where(i => i.IsActive).OrderBy(i => i.SortOrder),
                                "ID", "Name", plan.ItemID);
                            return View(plan);
                        }

                        planToUpdate.Year = plan.Year;
                        planToUpdate.Month = plan.Month;
                        planToUpdate.ItemID = plan.ItemID;
                        planToUpdate.AmountPlan = plan.AmountPlan;

                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "FinPlPlans", "Edit", $"P&L rejasi tahrirlandi: {planToUpdate.Year}-{planToUpdate.Month}, ElementID: {planToUpdate.ItemID}");

                        return RedirectToAction("Index", new { year = plan.Year, month = plan.Month });
                    }
                }

                ViewBag.YearList = FinSelectLists.Years(plan.Year);
                ViewBag.MonthList = FinSelectLists.Months(plan.Month);
                ViewBag.ItemID = new SelectList(
                    db.FIN_PL_ITEMS.Where(i => i.IsActive).OrderBy(i => i.SortOrder),
                    "ID", "Name", plan.ItemID);

                return View(plan);
            }
        }

        // Olish so'rovi: FinPlPlans/o'chirish/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var plan = db.FIN_PL_PLANS
                    .Include(p => p.FIN_PL_ITEMS)
                    .FirstOrDefault(p => p.ID == id);

                if (plan == null)
                {
                    return HttpNotFound();
                }

                return View(plan);
            }
        }

        // POST so'rovi: FinPlPlans/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var plan = db.FIN_PL_PLANS.Find(id);
                if (plan != null)
                {
                    var year = plan.Year;
                    var month = plan.Month;

                    plan.IsDeleted = true;
                    db.Entry(plan).State = System.Data.Entity.EntityState.Modified;
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "FinPlPlans", "Delete", $"P&L rejasi o'chirildi: {year}-{month}, ElementID: {plan.ItemID}");

                    return RedirectToAction("Index", new { year = year, month = month });
                }

                return RedirectToAction("Index");
            }
        }

        // Olish so'rovi: FinPlPlans/BulkEdit
        [Permission("FinPlPlans", Change = true)]
        public ActionResult BulkEdit(int? year, int? month)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                year = year ?? DateTime.Now.Year;
                month = month ?? DateTime.Now.Month;

                var items = db.FIN_PL_ITEMS.Where(i => i.IsActive).OrderBy(i => i.SortOrder).ToList();
                var plans = db.FIN_PL_PLANS
                    .Where(p => p.Year == year.Value && p.Month == month.Value && p.IsDeleted != true)
                    .ToDictionary(p => p.ItemID, p => p.AmountPlan);

                var model = items.Select(item => new BulkPlanItem
                {
                    ItemID = item.ID,
                    ItemName = item.Name,
                    ItemCode = item.Code,
                    AmountPlan = plans.ContainsKey(item.ID) ? plans[item.ID] : 0
                }).ToList();

                ViewBag.Year = year;
                ViewBag.Month = month;
                ViewBag.YearList = FinSelectLists.Years(year);
                ViewBag.MonthList = FinSelectLists.Months(month);

                return View(model);
            }
        }

        // POST so'rovi: FinPlPlans/BulkEdit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BulkEdit(int year, int month, BulkPlanItem[] plans)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var userId = UserHelper.GetUserId(User.Identity.Name);
                var now = DateTime.Now;

                // Oyning mavjud rejalari bir marta yuklanadi (avval har band uchun alohida so'rov, FIN-P15)
                var existingByItem = db.FIN_PL_PLANS
                    .Where(p => p.Year == year && p.Month == month)
                    .ToList()
                    .GroupBy(p => p.ItemID)
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (var planData in plans ?? new BulkPlanItem[0])
                {
                    existingByItem.TryGetValue(planData.ItemID, out var existing);
                    if (existing != null)
                    {
                        // Mavjud reja 0 ga ham o'zgartiriladi; o'chirilgani tiklanadi (FIN-B11)
                        if (existing.IsDeleted == true && planData.AmountPlan == 0)
                            continue;
                        existing.AmountPlan = planData.AmountPlan;
                        existing.IsDeleted = false;
                    }
                    else if (planData.AmountPlan != 0) // yangi reja faqat nol bo'lmasa
                    {
                        db.FIN_PL_PLANS.Add(new FIN_PL_PLANS
                        {
                            Year = year,
                            Month = (byte)month,
                            ItemID = planData.ItemID,
                            AmountPlan = planData.AmountPlan,
                            CreatedByUserID = userId,
                            CreatedDT = now
                        });
                    }
                }

                db.SaveChanges();

                LogHelper.LogToDatabase(User.Identity.Name, "FinPlPlans", "BulkEdit", $"P&L rejalari to'plamda yangilandi: {year}-{month}");

                return RedirectToAction("Index", new { year = year, month = month });
            }
        }

        // Olish so'rovi: FinPlPlans/ExportExcel
        public ActionResult ExportExcel(int? year, int? month)
        {
            year = year ?? DateTime.Now.Year;
            month = month ?? DateTime.Now.Month;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var plans = db.FIN_PL_PLANS
                    .Include(p => p.FIN_PL_ITEMS)
                    .Where(p => p.Year == year.Value && p.Month == month.Value && p.IsDeleted != true)
                    .OrderBy(p => p.FIN_PL_ITEMS.SortOrder)
                    .ToList();

                // FIN-D22: umumiy ExcelExport
                var bytes = ExcelExport.Create("P&L Plans", plans,
                    ExcelExport.Col<FIN_PL_PLANS>(L.T("Code"), x => x.FIN_PL_ITEMS.Code),
                    ExcelExport.Col<FIN_PL_PLANS>(L.T("Name"), x => x.FIN_PL_ITEMS.Name),
                    ExcelExport.Col<FIN_PL_PLANS>(L.T("Fin_PlanAmount"), x => x.AmountPlan));
                return File(bytes, ExcelExport.ContentType, $"P&L_Plans_{year}_{month}.xlsx");
            }
        }
    }

    public class BulkPlanItem
    {
        public int ItemID { get; set; }
        public string ItemName { get; set; }
        public string ItemCode { get; set; }
        public decimal AmountPlan { get; set; }
    }
}
