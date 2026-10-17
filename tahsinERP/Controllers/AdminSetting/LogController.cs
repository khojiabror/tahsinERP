using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Filters;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Tizim loglarini ko'rish va filterlash
    /// </summary>
    [Permission]
    public class LogController : Controller
    {
        // Olish so'rovi: Log
        public async Task<ActionResult> Index(string module, int? userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            // standart: so'nggi 30 kun
            startDate = startDate ?? DateTime.Now.AddDays(-30);
            endDate = endDate ?? DateTime.Now;
            endDate = endDate.Value.AddDays(1).AddSeconds(-1); // Kunning oxirigacha

            using (var db = new DBTHSNEntities())
            {
                // Query qurish
                var query = db.USERLOGS
                    .Include(l => l.USER)
                    .Where(l => l.DateTime >= startDate && l.DateTime <= endDate);

                // Filterlar
                if (userId.HasValue)
                {
                    query = query.Where(l => l.UserID == userId.Value);
                }

                if (!string.IsNullOrEmpty(module))
                {
                    query = query.Where(l => l.ControllerName == module);
                }

                // Ma'lumotlarni olish
                var logs = await query
                    .OrderByDescending(l => l.DateTime)
                    .ToListAsync();

                // ViewBag ma'lumotlari
                ViewBag.Users = new SelectList(
                    await db.USERS
                        .Where(u => u.IsDeleted != true && u.IsActive)
                        .OrderBy(u => u.FullName)
                        .ToListAsync(),
                    "ID", "FullName", userId);

                ViewBag.Modules = new SelectList(
                    await db.USERLOGS
                        .Select(l => l.ControllerName)
                        .Distinct()
                        .OrderBy(m => m)
                        .ToListAsync(),
                    module);

                ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");

                return View(logs);
            }
        }
    }
}

