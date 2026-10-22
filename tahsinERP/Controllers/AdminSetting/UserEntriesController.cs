using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.I18n;
using tahsinERP.Models;
using tahsinERP.Services.Common;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Foydalanuvchi kirishlarini ko'rish va filterlash.
    /// Qatorlar Data() orqali sahifalab (DataTables serverSide), to'liq ro'yxat ExportExcel() orqali (CORE-P09).
    /// </summary>
    [Permission]
    public class UserEntriesController : Controller
    {
        // Olish so'rovi: UserEntries
        [HttpGet]
        public async Task<ActionResult> Index(int? userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var range = NormalizeRange(startDate, endDate);
            using (var db = new DBTHSNEntities())
            {
                ViewBag.Users = new SelectList(
                    await db.USERS
                        .Where(u => u.IsDeleted != true && u.IsActive)
                        .OrderBy(u => u.FullName)
                        .Select(u => new { u.ID, u.FullName })
                        .ToListAsync(),
                    "ID", "FullName", userId);
            }
            ViewBag.UserId = userId;
            ViewBag.StartDate = range.Item1.ToString("yyyy-MM-dd");
            ViewBag.EndDate = range.Item2.ToString("yyyy-MM-dd");
            return View();
        }

        // Olish so'rovi: UserEntries/Data (DataTables serverSide)
        [HttpGet]
        public async Task<ActionResult> Data(int? userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var dt = DataTablesRequest.From(Request);
            using (var db = new DBTHSNEntities())
            {
                var query = Filter(db, userId, startDate, endDate);
                int total = await query.CountAsync();
                if (dt.Search.Length > 0)
                {
                    string s = dt.Search;
                    query = query.Where(e => e.IP.Contains(s) || e.USER.FullName.Contains(s) || e.USER.Uname.Contains(s));
                }
                int filtered = dt.Search.Length > 0 ? await query.CountAsync() : total;

                IOrderedQueryable<USER_ENTRIES> ordered;
                switch (dt.OrderColumn)
                {
                    case 0: ordered = dt.OrderDesc ? query.OrderByDescending(e => e.USER.FullName) : query.OrderBy(e => e.USER.FullName); break;
                    case 1: ordered = dt.OrderDesc ? query.OrderByDescending(e => e.IP) : query.OrderBy(e => e.IP); break;
                    default: ordered = dt.OrderDesc ? query.OrderByDescending(e => e.DateTime) : query.OrderBy(e => e.DateTime); break;
                }

                var page = await ordered.ThenByDescending(e => e.ID)
                    .Skip(dt.Start).Take(dt.Length)
                    .Select(e => new { User = e.USER.FullName ?? e.USER.Uname, e.IP, e.MAC, e.DateTime })
                    .ToListAsync();

                var rows = page.Select(e => new
                {
                    user = e.User ?? L.T("Core_Unknown"),
                    ip = e.IP,
                    mac = e.MAC,
                    date = e.DateTime.ToString("dd.MM.yyyy"),
                    time = e.DateTime.ToString("HH:mm:ss")
                });
                return Json(dt.Response(total, filtered, rows), JsonRequestBehavior.AllowGet);
            }
        }

        // Olish so'rovi: UserEntries/ExportExcel — filtrdagi barcha qatorlar
        [HttpGet]
        public async Task<ActionResult> ExportExcel(int? userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            using (var db = new DBTHSNEntities())
            {
                var rows = await Filter(db, userId, startDate, endDate)
                    .OrderByDescending(e => e.DateTime)
                    .Select(e => new EntryRow { User = e.USER.FullName ?? e.USER.Uname, IP = e.IP, Mac = e.MAC, DateTime = e.DateTime })
                    .ToListAsync();

                var bytes = ExcelExport.Create(L.T("Core_Entries"), rows,
                    ExcelExport.Col<EntryRow>(L.T("Core_User"), x => x.User ?? L.T("Core_Unknown")),
                    ExcelExport.Col<EntryRow>(L.T("Core_IpAddress"), x => x.IP),
                    ExcelExport.Col<EntryRow>(L.T("Core_MacAddress"), x => x.Mac),
                    ExcelExport.Col<EntryRow>(L.T("Date"), x => x.DateTime, "dd.MM.yyyy HH:mm:ss"));
                return File(bytes, ExcelExport.ContentType, L.T("Core_FileUserEntries") + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx");
            }
        }

        private sealed class EntryRow
        {
            public string User { get; set; }
            public string IP { get; set; }
            public string Mac { get; set; }
            public DateTime DateTime { get; set; }
        }

        private static IQueryable<USER_ENTRIES> Filter(DBTHSNEntities db, int? userId, DateTime? startDate, DateTime? endDate)
        {
            var range = NormalizeRange(startDate, endDate);
            DateTime from = range.Item1, to = range.Item2.AddDays(1);
            var query = db.USER_ENTRIES.AsNoTracking().Where(e => e.DateTime >= from && e.DateTime < to);
            if (userId.HasValue)
                query = query.Where(e => e.UserID == userId.Value);
            return query;
        }

        // standart: so'nggi 30 kun
        private static Tuple<DateTime, DateTime> NormalizeRange(DateTime? startDate, DateTime? endDate)
        {
            var end = (endDate ?? DateTime.Today).Date;
            var start = (startDate ?? end.AddDays(-30)).Date;
            return Tuple.Create(start, end);
        }
    }
}
