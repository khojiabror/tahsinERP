using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Delivery;
using tahsinERP.Filters;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>
    /// Agent KPI ish haqi: har agent (ekspeditor) sxemasi — % savdo, reja va bonus, jarimalar; oy bo'yicha hisoblash, qo'lda tuzatish
    /// va HR ish haqi hisobiga (qoralama) mukofot/jarima sifatida o'tkazish. Ko'rinish — agent ierarxiyasi bo'yicha.
    /// </summary>
    public class DstKpiController : DstControllerBase
    {
        public sealed class AgentRow
        {
            public int UserID { get; set; }
            public string Name { get; set; }
            public bool IsExpeditor { get; set; }
            public KpiScheme Scheme { get; set; }
            public string Employee { get; set; }
            public KpiResult Result { get; set; }
        }

        public sealed class EmployeeRef
        {
            public int ID { get; set; }
            public string FullName { get; set; }
            public string TabNo { get; set; }
        }

        public ActionResult Index(int? year, int? month)
        {
            var y = year ?? DateTime.Today.Year;
            var m = month ?? DateTime.Today.Month;
            if (m < 1 || m > 12) m = DateTime.Today.Month;
            using (var db = NewDb())
            {
                var agents = from a in db.SalesAgents
                             join u in db.Users on a.UserID equals u.ID
                             where a.IsActive && u.IsDeleted != true
                             select new { a.UserID, a.IsExpeditor, Name = u.FullName ?? u.Uname };
                agents = new ScopeService(db).AgentsFor(CurrentUserName).Apply(agents, a => a.UserID);
                var list = agents.OrderBy(a => a.IsExpeditor).ThenBy(a => a.Name).ToList();
                var ids = list.Select(a => a.UserID).ToList();
                var schemes = db.KpiSchemes.Where(s => ids.Contains(s.AgentUserID)).ToDictionary(s => s.AgentUserID);
                var results = db.KpiResults.Where(r => r.Year == y && r.Month == m && ids.Contains(r.AgentUserID)).ToDictionary(r => r.AgentUserID);
                var employees = Employees(db);
                var names = employees.ToDictionary(e => e.ID, e => e.FullName + (string.IsNullOrEmpty(e.TabNo) ? "" : " (" + e.TabNo + ")"));
                ViewBag.Year = y;
                ViewBag.Month = m;
                ViewBag.Employees = employees.Select(e => new SelectListItem { Value = e.ID.ToString(), Text = names[e.ID] }).ToList();
                return View(list.Select(a =>
                {
                    schemes.TryGetValue(a.UserID, out var s);
                    results.TryGetValue(a.UserID, out var r);
                    return new AgentRow
                    {
                        UserID = a.UserID, Name = a.Name, IsExpeditor = a.IsExpeditor, Scheme = s, Result = r,
                        Employee = s?.EmployeeID != null && names.TryGetValue(s.EmployeeID.Value, out var n) ? n : null
                    };
                }).ToList());
            }
        }

        private static List<EmployeeRef> Employees(DistributionDbContext db) =>
            db.Database.SqlQuery<EmployeeRef>(
                "SELECT ID, FullName, TabNo FROM dbo.HR_EMPLOYEES WHERE IsActive = 1 AND ISNULL(IsDeleted, 0) = 0 ORDER BY FullName").ToList();

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SaveScheme(KpiScheme input)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    EnsureAgent(db, input.AgentUserID);
                    KpiService.SaveScheme(db, input, UserId(), DateTime.Now);
                    return new { ok = true };
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Recalculate(int year, int month)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    var rows = KpiService.Calculate(db, year, month, UserId(), DateTime.Now);
                    return new { count = rows.Count };
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SaveAdjustment(int id, decimal manualBonus, decimal manualPenalty, string note)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    EnsureResult(db, id);
                    KpiService.Adjust(db, id, manualBonus, manualPenalty, note);
                    return new { ok = true };
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult ApplyPayroll(int id)
        {
            return DstJson(() =>
            {
                using (var uow = NewUnitOfWork())
                {
                    EnsureResult(uow.Dst, id);
                    KpiService.Apply(uow, id, UserId(), DateTime.Now);
                    uow.Commit();
                    return new { ok = true };
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult RevertPayroll(int id)
        {
            return DstJson(() =>
            {
                using (var uow = NewUnitOfWork())
                {
                    EnsureResult(uow.Dst, id);
                    KpiService.Revert(uow, id, UserId(), DateTime.Now);
                    uow.Commit();
                    return new { ok = true };
                }
            });
        }

        private void EnsureAgent(DistributionDbContext db, int agentId)
        {
            if (!new ScopeService(db).AgentsFor(CurrentUserName).Allows(agentId)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
        }

        private void EnsureResult(DistributionDbContext db, int id)
        {
            var agent = db.KpiResults.Where(r => r.ID == id).Select(r => (int?)r.AgentUserID).FirstOrDefault()
                        ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            EnsureAgent(db, agent);
        }

        private int UserId() => tahsinERP.Models.RoleHelper.GetUserId(CurrentUserName) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
    }
}
