using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.Controllers;
using ClosedXML.Excel;
using System.IO;
using System.Web;

namespace tahsinERP.Controllers.Finance
{
    public class FinPlPlansController : Controller
    {
        // Olish so'rovi: FinPlPlans
        public ActionResult Index(int? year, int? month, int? itemID)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinPlPlans"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                year = year ?? DateTime.Now.Year;
                month = month ?? DateTime.Now.Month;

                var query = db.FIN_PL_PLANS
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
                var years = Enumerable.Range(DateTime.Now.Year - 5, 11).ToList();
                ViewBag.YearList = new SelectList(years, year);
                var months = Enumerable.Range(1, 12).Select(m => new { Value = m, Text = new DateTime(2000, m, 1).ToString("MMMM") }).ToList();
                ViewBag.MonthList = new SelectList(months, "Value", "Text", month);
                ViewBag.ItemList = new SelectList(
                    db.FIN_PL_ITEMS.Where(i => i.IsActive).OrderBy(i => i.SortOrder).ToList(),
                    "ID", "Name", itemID);

                return View(plans);
            }
        }

        // Olish so'rovi: FinPlPlans/yaratish
        public ActionResult Create(int? year, int? month, int? itemID)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinPlPlans"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                year = year ?? DateTime.Now.Year;
                month = month ?? DateTime.Now.Month;

                ViewBag.Year = year;
                ViewBag.Month = month;
                var years = Enumerable.Range(DateTime.Now.Year - 5, 11).ToList();
                ViewBag.YearList = new SelectList(years, year);
                var months = Enumerable.Range(1, 12).Select(m => new { Value = m, Text = new DateTime(2000, m, 1).ToString("MMMM") }).ToList();
                ViewBag.MonthList = new SelectList(months, "Value", "Text", month);
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
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinPlPlans"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Tekshirish agar plan already mavjud
                var existing = db.FIN_PL_PLANS
                    .FirstOrDefault(p => p.Year == plan.Year && p.Month == plan.Month && p.ItemID == plan.ItemID);

                if (existing != null)
                {
                    ModelState.AddModelError("", "Bu oy va element uchun reja allaqachon mavjud. Tahrirlash sahifasiga o'ting.");
                }

                if (ModelState.IsValid)
                {
                    plan.CreatedByUserID = UserHelper.GetUserId(User.Identity.Name);
                    plan.CreatedDT = DateTime.Now;

                    db.FIN_PL_PLANS.Add(plan);
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "FinPlPlans", "Create", $"P&L rejasi yaratildi: {plan.Year}-{plan.Month}, ElementID: {plan.ItemID}");

                    return RedirectToAction("Index", new { year = plan.Year, month = plan.Month });
                }

                ViewBag.Year = plan.Year;
                ViewBag.Month = plan.Month;
                var years = Enumerable.Range(DateTime.Now.Year - 5, 11).ToList();
                ViewBag.YearList = new SelectList(years, plan.Year);
                var months = Enumerable.Range(1, 12).Select(m => new { Value = m, Text = new DateTime(2000, m, 1).ToString("MMMM") }).ToList();
                ViewBag.MonthList = new SelectList(months, "Value", "Text", plan.Month);
                ViewBag.ItemID = new SelectList(
                    db.FIN_PL_ITEMS.Where(i => i.IsActive).OrderBy(i => i.SortOrder),
                    "ID", "Name", plan.ItemID);

                return View(plan);
            }
        }

        // Olish so'rovi: FinPlPlans/Edit/5
        public ActionResult Edit(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinPlPlans"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

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

                var years = Enumerable.Range(DateTime.Now.Year - 5, 11).ToList();
                ViewBag.YearList = new SelectList(years, plan.Year);
                var months = Enumerable.Range(1, 12).Select(m => new { Value = m, Text = new DateTime(2000, m, 1).ToString("MMMM") }).ToList();
                ViewBag.MonthList = new SelectList(months, "Value", "Text", plan.Month);
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
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinPlPlans"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Declare years once at the beginning ga avoid scope conflicts
                var years = Enumerable.Range(DateTime.Now.Year - 5, 11).ToList();
                var months = Enumerable.Range(1, 12).Select(m => new { Value = m, Text = new DateTime(2000, m, 1).ToString("MMMM") }).ToList();
                
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
                            ModelState.AddModelError("", "Bu oy va element uchun boshqa reja allaqachon mavjud.");
                            ViewBag.YearList = new SelectList(years, plan.Year);
                            ViewBag.MonthList = new SelectList(months, "Value", "Text", plan.Month);
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

                ViewBag.YearList = new SelectList(years, plan.Year);
                ViewBag.MonthList = new SelectList(months, "Value", "Text", plan.Month);
                ViewBag.ItemID = new SelectList(
                    db.FIN_PL_ITEMS.Where(i => i.IsActive).OrderBy(i => i.SortOrder),
                    "ID", "Name", plan.ItemID);

                return View(plan);
            }
        }

        // Olish so'rovi: FinPlPlans/o'chirish/5
        public ActionResult Delete(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinPlPlans"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

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
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinPlPlans"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

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
        public ActionResult BulkEdit(int? year, int? month)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinPlPlans"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                year = year ?? DateTime.Now.Year;
                month = month ?? DateTime.Now.Month;

                var items = db.FIN_PL_ITEMS.Where(i => i.IsActive).OrderBy(i => i.SortOrder).ToList();
                var plans = db.FIN_PL_PLANS
                    .Where(p => p.Year == year.Value && p.Month == month.Value)
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
                var years = Enumerable.Range(DateTime.Now.Year - 5, 11).ToList();
                ViewBag.YearList = new SelectList(years, year);
                var months = Enumerable.Range(1, 12).Select(m => new { Value = m, Text = new DateTime(2000, m, 1).ToString("MMMM") }).ToList();
                ViewBag.MonthList = new SelectList(months, "Value", "Text", month);

                return View(model);
            }
        }

        // POST so'rovi: FinPlPlans/BulkEdit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BulkEdit(int year, int month, BulkPlanItem[] plans)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinPlPlans"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var userId = UserHelper.GetUserId(User.Identity.Name);
                var now = DateTime.Now;

                foreach (var planData in plans)
                {
                    if (planData.AmountPlan != 0) // faqat saqlash non-zero plans
                    {
                        var existing = db.FIN_PL_PLANS
                            .FirstOrDefault(p => p.Year == year && p.Month == month && p.ItemID == planData.ItemID);

                        if (existing != null)
                        {
                            existing.AmountPlan = planData.AmountPlan;
                        }
                        else
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
                }

                db.SaveChanges();

                LogHelper.LogToDatabase(User.Identity.Name, "FinPlPlans", "BulkEdit", $"P&L rejalari to'plamda yangilandi: {year}-{month}");

                return RedirectToAction("Index", new { year = year, month = month });
            }
        }

        // Olish so'rovi: FinPlPlans/ExportExcel
        public ActionResult ExportExcel(int? year, int? month)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinPlPlans"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            year = year ?? DateTime.Now.Year;
            month = month ?? DateTime.Now.Month;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var plans = db.FIN_PL_PLANS
                    .Include(p => p.FIN_PL_ITEMS)
                    .Where(p => p.Year == year.Value && p.Month == month.Value)
                    .OrderBy(p => p.FIN_PL_ITEMS.SortOrder)
                    .ToList();

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("P&L Plans");
                    worksheet.Cell(1, 1).Value = "Kod";
                    worksheet.Cell(1, 2).Value = "Nomi";
                    worksheet.Cell(1, 3).Value = "Reja summasi";

                    int row = 2;
                    foreach (var plan in plans)
                    {
                        worksheet.Cell(row, 1).Value = plan.FIN_PL_ITEMS.Code;
                        worksheet.Cell(row, 2).Value = plan.FIN_PL_ITEMS.Name;
                        worksheet.Cell(row, 3).Value = plan.AmountPlan;
                        row++;
                    }

                    worksheet.Columns().AdjustToContents();

                    var stream = new MemoryStream();
                    workbook.SaveAs(stream);
                    stream.Position = 0;

                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"P&L_Plans_{year}_{month}.xlsx");
                }
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
