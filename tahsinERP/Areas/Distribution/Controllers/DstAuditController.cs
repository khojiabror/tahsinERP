using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Services.Procurement;
using tahsinERP.ViewModels;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>
    /// Audit jurnali (dst.AUDIT_TRAIL, 013): kim, qachon, qaysi yozuvning qaysi maydonini o'zgartirdi — faqat o'qish (05_FINDINGS K3-33).
    /// Yozuvni DistributionDbContext.SaveChanges o'zi qo'shadi (IAuditable). Filtr: obyekt turi, kalit, foydalanuvchi, manba, davr.
    /// </summary>
    public class DstAuditController : DstControllerBase
    {
        public ActionResult Index(string entity, string key)
        {
            using (var db = NewDb())
            {
                ViewBag.Entities = db.AuditTrail.Select(a => a.EntityName).Distinct().OrderBy(x => x).ToList()
                    .Select(x => new SelectListItem { Value = x, Text = x, Selected = x == entity }).ToList();
                ViewBag.Users = db.Users.Where(u => u.IsDeleted != true).OrderBy(u => u.FullName ?? u.Uname).ToList()
                    .Select(u => new SelectListItem { Value = u.ID.ToString(), Text = u.FullName ?? u.Uname }).ToList();
                ViewBag.Entity = entity;
                ViewBag.Key = key;
                return View();
            }
        }

        [HttpGet]
        public async Task<ActionResult> IndexData(string entity, string key, int? userId, string source, DateTime? from, DateTime? to)
        {
            var page = DataTablesPageRequest.Parse(Request, defaultOrderColumn: 0, defaultDescending: true);
            using (var db = NewDb())
            {
                var q = from a in db.AuditTrail.AsNoTracking()
                        select new
                        {
                            a.ID, a.ChangedAt, a.EntityName, a.EntityKey, a.Action, a.FieldName, a.OldValue, a.NewValue, a.UserID, a.Source,
                            User = db.Users.Where(u => u.ID == a.UserID).Select(u => u.FullName ?? u.Uname).FirstOrDefault()
                        };
                if (!string.IsNullOrEmpty(entity)) q = q.Where(x => x.EntityName == entity);
                if (!string.IsNullOrWhiteSpace(key)) { var k = key.Trim(); q = q.Where(x => x.EntityKey == k); }
                if (userId.HasValue) q = q.Where(x => x.UserID == userId);
                if (!string.IsNullOrEmpty(source)) q = q.Where(x => x.Source == source);
                if (from.HasValue) { var f = from.Value.Date; q = q.Where(x => x.ChangedAt >= f); }
                if (to.HasValue) { var t = to.Value.Date.AddDays(1); q = q.Where(x => x.ChangedAt < t); }

                var result = await ServerTablePage.LoadAsync(q, page,
                    (x, s) => x.Where(r => r.EntityKey == s || r.FieldName.Contains(s) || r.NewValue.Contains(s) || r.OldValue.Contains(s)),
                    (x, column, desc) => ServerTablePage.By(x, r => r.ID, desc));

                return Json(new
                {
                    draw = page.Draw, recordsTotal = result.Total, recordsFiltered = result.Filtered,
                    data = result.Rows.Select(r => new
                    {
                        at = r.ChangedAt.ToString("dd.MM.yyyy HH:mm:ss"), entity = r.EntityName, key = r.EntityKey,
                        action = DstLocalization.T("Audit_" + r.Action), field = r.FieldName,
                        oldValue = Cut(r.OldValue), newValue = Cut(r.NewValue), user = r.User ?? (r.UserID.HasValue ? "#" + r.UserID : "—"), source = r.Source
                    })
                }, JsonRequestBehavior.AllowGet);
            }
        }

        private static string Cut(string s) => s == null ? null : s.Length <= 400 ? s : s.Substring(0, 400) + "…";
    }
}
