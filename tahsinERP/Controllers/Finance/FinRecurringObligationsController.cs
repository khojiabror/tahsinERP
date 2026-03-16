using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Controllers;

namespace tahsinERP.Controllers.Finance
{
    /// <summary>
    /// Doimiy harajatlar (ijara, buxgalteriya va h.k.) — har oy shakllanadigan majburiyat, to'lovda kamayadi.
    /// Balans hisobotida "Doimiy harajatlar qarzi" ostida ko'rsatiladi.
    /// </summary>
    public class FinRecurringObligationsController : Controller
    {
        private const string TableName = "FIN_RECURRING_OBLIGATIONS";

        public ActionResult Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinDashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            try
            {
                using (var db = new DBTHSNEntities())
                {
                    var list = db.Database.SqlQuery<RecurringObligationViewModel>(
                        "SELECT o.ID, o.Name, o.MonthlyAmount, o.CurrencyID, o.StartYear, o.StartMonth, o.BalanceCategoryID, o.IsActive, c.Name AS BalanceCategoryName, cur.Shortname AS CurrencyShortname " +
                        "FROM " + TableName + " o " +
                        "LEFT JOIN FIN_BALANCE_CATEGORIES c ON c.ID = o.BalanceCategoryID " +
                        "LEFT JOIN CURRENCy cur ON cur.ID = o.CurrencyID " +
                        "WHERE (o.IsDeleted = 0 OR o.IsDeleted IS NULL) " +
                        "ORDER BY o.Name").ToList();
                    return View(list);
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Doimiy harajatlar jadvali topilmadi. Scripts/Create_FIN_RECURRING_OBLIGATIONS.sql skriptini bajarganingizga ishonch hosil qiling. " + ex.Message;
                return View(new List<RecurringObligationViewModel>());
            }
        }

        public ActionResult Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinDashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                ViewBag.CurrencyID = new SelectList(db.CURRENCIES.OrderBy(c => c.Shortname).ToList(), "ID", "Shortname", 1);
                var doimiyCategory = db.FIN_BALANCE_CATEGORIES.FirstOrDefault(c => c.IsActive && c.Name == "Doimiy harajatlar qarzi");
                var balanceCategories = db.FIN_BALANCE_CATEGORIES.Where(c => c.IsActive && c.Type == 2).OrderBy(c => c.Name).ToList();
                ViewBag.BalanceCategoryID = new SelectList(balanceCategories, "ID", "Name", doimiyCategory?.ID);
                var model = new RecurringObligationViewModel
                {
                    StartYear = DateTime.Now.Year,
                    StartMonth = (byte)DateTime.Now.Month,
                    IsActive = true,
                    BalanceCategoryID = doimiyCategory?.ID
                };
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(RecurringObligationViewModel model)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinDashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var userId = UserHelper.GetUserId(User.Identity.Name);
                    using (var db = new DBTHSNEntities())
                    {
                        db.Database.ExecuteSqlCommand(
                            "INSERT INTO " + TableName + " (Name, MonthlyAmount, CurrencyID, StartYear, StartMonth, BalanceCategoryID, IsActive, CreatedByUserID, CreatedDT) " +
                            "VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8)",
                            new SqlParameter("@p0", model.Name ?? ""),
                            new SqlParameter("@p1", model.MonthlyAmount),
                            new SqlParameter("@p2", model.CurrencyID),
                            new SqlParameter("@p3", model.StartYear),
                            new SqlParameter("@p4", model.StartMonth),
                            new SqlParameter("@p5", (object)model.BalanceCategoryID ?? DBNull.Value),
                            new SqlParameter("@p6", model.IsActive),
                            new SqlParameter("@p7", userId),
                            new SqlParameter("@p8", DateTime.Now));
                        LogHelper.LogToDatabase(User.Identity.Name, "FinRecurringObligations", "Create", "Doimiy harajat yaratildi: " + model.Name);
                    }
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Saqlash xatosi: " + ex.Message);
                }
            }

            using (var db = new DBTHSNEntities())
            {
                ViewBag.CurrencyID = new SelectList(db.CURRENCIES.OrderBy(c => c.Shortname).ToList(), "ID", "Shortname", model.CurrencyID);
                ViewBag.BalanceCategoryID = new SelectList(
                    db.FIN_BALANCE_CATEGORIES.Where(c => c.IsActive && c.Type == 2).OrderBy(c => c.Name).ToList(),
                    "ID", "Name", model.BalanceCategoryID);
            }
            return View(model);
        }

        public ActionResult Edit(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinDashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null) return new HttpStatusCodeResult(400);

            try
            {
                using (var db = new DBTHSNEntities())
                {
                    var item = db.Database.SqlQuery<RecurringObligationViewModel>(
                        "SELECT ID, Name, MonthlyAmount, CurrencyID, StartYear, StartMonth, BalanceCategoryID, IsActive FROM " + TableName + " WHERE ID = @p0",
                        new SqlParameter("@p0", id.Value)).FirstOrDefault();
                    if (item == null) return HttpNotFound();

                    ViewBag.CurrencyID = new SelectList(db.CURRENCIES.OrderBy(c => c.Shortname).ToList(), "ID", "Shortname", item.CurrencyID);
                    ViewBag.BalanceCategoryID = new SelectList(
                        db.FIN_BALANCE_CATEGORIES.Where(c => c.IsActive && c.Type == 2).OrderBy(c => c.Name).ToList(),
                        "ID", "Name", item.BalanceCategoryID);
                    return View(item);
                }
            }
            catch
            {
                return HttpNotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(RecurringObligationViewModel model)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinDashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    using (var db = new DBTHSNEntities())
                    {
                        db.Database.ExecuteSqlCommand(
                            "UPDATE " + TableName + " SET Name = @p0, MonthlyAmount = @p1, CurrencyID = @p2, StartYear = @p3, StartMonth = @p4, BalanceCategoryID = @p5, IsActive = @p6 WHERE ID = @p7",
                            new SqlParameter("@p0", model.Name ?? ""),
                            new SqlParameter("@p1", model.MonthlyAmount),
                            new SqlParameter("@p2", model.CurrencyID),
                            new SqlParameter("@p3", model.StartYear),
                            new SqlParameter("@p4", model.StartMonth),
                            new SqlParameter("@p5", (object)model.BalanceCategoryID ?? DBNull.Value),
                            new SqlParameter("@p6", model.IsActive),
                            new SqlParameter("@p7", model.ID));
                        LogHelper.LogToDatabase(User.Identity.Name, "FinRecurringObligations", "Edit", "Doimiy harajat tahrirlandi: " + model.Name);
                    }
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Saqlash xatosi: " + ex.Message);
                }
            }

            using (var db = new DBTHSNEntities())
            {
                ViewBag.CurrencyID = new SelectList(db.CURRENCIES.OrderBy(c => c.Shortname).ToList(), "ID", "Shortname", model.CurrencyID);
                ViewBag.BalanceCategoryID = new SelectList(
                    db.FIN_BALANCE_CATEGORIES.Where(c => c.IsActive && c.Type == 2).OrderBy(c => c.Name).ToList(),
                    "ID", "Name", model.BalanceCategoryID);
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "FinDashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            try
            {
                using (var db = new DBTHSNEntities())
                {
                    db.Database.ExecuteSqlCommand("UPDATE " + TableName + " SET IsDeleted = 1 WHERE ID = @p0", new SqlParameter("@p0", id));
                    LogHelper.LogToDatabase(User.Identity.Name, "FinRecurringObligations", "Delete", "Doimiy harajat o'chirildi, ID: " + id);
                }
            }
            catch { }
            return RedirectToAction("Index");
        }
    }

    public class RecurringObligationViewModel
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public decimal MonthlyAmount { get; set; }
        public int CurrencyID { get; set; }
        public int StartYear { get; set; }
        public byte StartMonth { get; set; }
        public int? BalanceCategoryID { get; set; }
        public bool IsActive { get; set; }
        public string BalanceCategoryName { get; set; }
        public string CurrencyShortname { get; set; }
    }
}
