using System;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Services.Bot;
using tahsinERP.Models;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>Pilot xatolar jurnali: yozish (web/bot), daraja K1–K3, holat, yechim.</summary>
    public class DstIssuesController : DstControllerBase
    {
        public sealed class Row
        {
            public Issue Issue { get; set; }
            public string Author { get; set; }
            public string PhotoUrl { get; set; }
        }

        public ActionResult Index(IssueStatus? status, bool all = false)
        {
            using (var db = NewDb())
            {
                var q = db.Issues.AsQueryable();
                if (status.HasValue) q = q.Where(i => i.Status == status);
                else if (!all) q = q.Where(i => i.Status != IssueStatus.Closed);
                var links = FileLinks.For(db, DateTimeOffset.UtcNow);
                ViewBag.Rows = (from i in q
                                orderby i.Status == IssueStatus.Closed, i.Severity == null ? 9 : (int)i.Severity, i.ID descending
                                select new { i, Author = db.Users.Where(u => u.ID == i.CreatedBy).Select(u => u.FullName ?? u.Uname).FirstOrDefault() })
                    .Take(500).ToList()
                    .Select(x => new Row { Issue = x.i, Author = x.Author, PhotoUrl = links.Url(x.i.PhotoKey) }).ToList();
                ViewBag.Counts = db.Issues.GroupBy(i => new { i.Status, i.Severity }).Select(g => new { g.Key.Status, g.Key.Severity, N = g.Count() }).ToList()
                    .Select(x => Tuple.Create(x.Status, x.Severity, x.N)).ToList();
                ViewBag.Status = status;
                ViewBag.All = all;
                return View();
            }
        }

        /// <summary>Yozish — ko'rish huquqi yetarli (pilotda hamma xato yozadi); boshqarish — o'zgartirish huquqi bilan.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [tahsinERP.Filters.SkipPermission]
        public ActionResult Report(IssueInput input)
        {
            if (!RoleHelper.IsPermittedForAction(CurrentUserName, "DstIssues", "Index", false))
                return RedirectToAction("Index", "ViewNotAuthorized", new { area = "" });
            try
            {
                using (var uow = NewUnitOfWork())
                {
                    IssueService.Create(uow.Dst, input, CurrentUserId, AuditActor.Web, DateTime.Now);
                    uow.Commit();
                }
                TempData["DstSaved"] = DstLocalization.T("Saved");
            }
            catch (DstValidationException ex)
            {
                TempData["DstError"] = ex.Message;
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Update(int id, IssueStatus status, byte? severity, string resolution)
        {
            try
            {
                using (var db = NewDb())
                    IssueService.Update(db, id, status, severity, resolution, CurrentUserId, DateTime.Now);
                TempData["DstSaved"] = DstLocalization.T("Saved");
            }
            catch (DstValidationException ex)
            {
                TempData["DstError"] = ex.Message;
            }
            return RedirectToAction("Index");
        }
    }
}
