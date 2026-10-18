using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Runtime.Caching;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Filters;
using tahsinERP.Services.Common;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Tizim loglarini ko'rish va filterlash.
    /// USERLOGS tez o'sadi: sahifa faqat filtrlarni beradi, qatorlar Data() orqali sahifalab
    /// (DataTables serverSide), to'liq ro'yxat esa ExportExcel() orqali olinadi (CORE-P09).
    /// </summary>
    [Permission]
    public class LogController : Controller
    {
        private static readonly MemoryCache ModuleCache = new MemoryCache("tahsinERP.LogModules");

        // Olish so'rovi: Log
        public async Task<ActionResult> Index(string module, int? userId, DateTime? startDate = null, DateTime? endDate = null)
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

                ViewBag.Modules = new SelectList(await GetModulesAsync(db), module);
            }

            ViewBag.Module = module;
            ViewBag.UserId = userId;
            ViewBag.StartDate = range.Item1.ToString("yyyy-MM-dd");
            ViewBag.EndDate = range.Item2.ToString("yyyy-MM-dd");
            return View();
        }

        // Olish so'rovi: Log/Data (DataTables serverSide)
        public async Task<ActionResult> Data(string module, int? userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var dt = DataTablesRequest.From(Request);

            using (var db = new DBTHSNEntities())
            {
                db.Configuration.AutoDetectChangesEnabled = false;
                var query = Filter(db, module, userId, startDate, endDate);
                int total = await query.CountAsync();

                if (dt.Search.Length > 0)
                {
                    string s = dt.Search;
                    query = query.Where(l => l.ControllerName.Contains(s) || l.ActionName.Contains(s) ||
                                             l.IP.Contains(s) || l.USER.FullName.Contains(s) || l.USER.Uname.Contains(s));
                }
                int filtered = dt.Search.Length > 0 ? await query.CountAsync() : total;

                IOrderedQueryable<USERLOG> ordered;
                switch (dt.OrderColumn)
                {
                    case 0: ordered = dt.OrderDesc ? query.OrderByDescending(l => l.USER.FullName) : query.OrderBy(l => l.USER.FullName); break;
                    case 1: ordered = dt.OrderDesc ? query.OrderByDescending(l => l.ControllerName) : query.OrderBy(l => l.ControllerName); break;
                    case 2: ordered = dt.OrderDesc ? query.OrderByDescending(l => l.ActionName) : query.OrderBy(l => l.ActionName); break;
                    case 3: ordered = dt.OrderDesc ? query.OrderByDescending(l => l.IP) : query.OrderBy(l => l.IP); break;
                    default: ordered = dt.OrderDesc ? query.OrderByDescending(l => l.DateTime) : query.OrderBy(l => l.DateTime); break;
                }

                var page = await ordered.ThenByDescending(l => l.ID)
                    .Skip(dt.Start).Take(dt.Length)
                    .Select(l => new { User = l.USER.FullName ?? l.USER.Uname, l.ControllerName, l.ActionName, l.IP, l.MacAddr, l.DateTime })
                    .ToListAsync();

                var rows = page.Select(l => new
                {
                    user = l.User ?? "Noma'lum",
                    controller = l.ControllerName,
                    action = l.ActionName,
                    ip = l.IP,
                    mac = l.MacAddr,
                    date = l.DateTime.ToString("dd.MM.yyyy"),
                    time = l.DateTime.ToString("HH:mm:ss")
                });

                return Json(dt.Response(total, filtered, rows), JsonRequestBehavior.AllowGet);
            }
        }

        // Olish so'rovi: Log/ExportExcel — filtrdagi barcha qatorlar
        public async Task<ActionResult> ExportExcel(string module, int? userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            using (var db = new DBTHSNEntities())
            {
                var rows = await Filter(db, module, userId, startDate, endDate)
                    .OrderByDescending(l => l.DateTime)
                    .Select(l => new LogRow { User = l.USER.FullName ?? l.USER.Uname, Controller = l.ControllerName, Action = l.ActionName, IP = l.IP, Mac = l.MacAddr, DateTime = l.DateTime })
                    .ToListAsync();

                var bytes = ExcelExport.Create("Tizim loglari", rows,
                    ExcelExport.Col<LogRow>("Foydalanuvchi", x => x.User ?? "Noma'lum"),
                    ExcelExport.Col<LogRow>("Controller", x => x.Controller),
                    ExcelExport.Col<LogRow>("Action", x => x.Action),
                    ExcelExport.Col<LogRow>("IP manzili", x => x.IP),
                    ExcelExport.Col<LogRow>("MAC manzili", x => x.Mac),
                    ExcelExport.Col<LogRow>("Sana", x => x.DateTime, "dd.MM.yyyy HH:mm:ss"));

                return File(bytes, ExcelExport.ContentType, "Tizim_loglari_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx");
            }
        }

        private sealed class LogRow
        {
            public string User { get; set; }
            public string Controller { get; set; }
            public string Action { get; set; }
            public string IP { get; set; }
            public string Mac { get; set; }
            public DateTime DateTime { get; set; }
        }

        private static IQueryable<USERLOG> Filter(DBTHSNEntities db, string module, int? userId, DateTime? startDate, DateTime? endDate)
        {
            var range = NormalizeRange(startDate, endDate);
            DateTime from = range.Item1, to = range.Item2.AddDays(1);

            var query = db.USERLOGS.AsNoTracking().Where(l => l.DateTime >= from && l.DateTime < to);
            if (userId.HasValue)
                query = query.Where(l => l.UserID == userId.Value);
            if (!string.IsNullOrEmpty(module))
                query = query.Where(l => l.ControllerName == module);
            return query;
        }

        // standart: so'nggi 30 kun; sanalar kun boshiga keltiriladi
        private static Tuple<DateTime, DateTime> NormalizeRange(DateTime? startDate, DateTime? endDate)
        {
            var end = (endDate ?? DateTime.Today).Date;
            var start = (startDate ?? end.AddDays(-30)).Date;
            return Tuple.Create(start, end);
        }

        // Modullar ro'yxati butun USERLOGS bo'yicha DISTINCT — 1 soat keshlanadi
        private static async Task<List<string>> GetModulesAsync(DBTHSNEntities db)
        {
            if (ModuleCache.Get("modules") is List<string> cached)
                return cached;

            var modules = await db.USERLOGS
                .Select(l => l.ControllerName)
                .Distinct()
                .OrderBy(m => m)
                .ToListAsync();
            ModuleCache.Set("modules", modules, DateTimeOffset.Now.AddHours(1));
            return modules;
        }
    }
}
