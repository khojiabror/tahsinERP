using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Foydalanuvchi kirishlarini ko'rish va filterlash
    /// </summary>
    public class UserEntriesController : Controller
    {
        // GET: UserEntries
        [HttpGet]
        public async Task<ActionResult> Index(int? userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Default: so'nggi 30 kun
            startDate = startDate ?? DateTime.Now.AddDays(-30);
            endDate = endDate ?? DateTime.Now;
            endDate = endDate.Value.AddDays(1).AddSeconds(-1); // Kunning oxirigacha

            using (var db = new DBTHSNEntities())
            {
                // Query qurish
                var query = db.USER_ENTRIES
                    .Include(e => e.USER)
                    .Where(e => e.DateTime >= startDate && e.DateTime <= endDate);

                // Filterlar
                if (userId.HasValue)
                {
                    query = query.Where(e => e.UserID == userId.Value);
                }

                // Ma'lumotlarni olish
                var entries = await query
                    .OrderByDescending(e => e.DateTime)
                    .ToListAsync();

                // ViewBag ma'lumotlari
                ViewBag.Users = new SelectList(
                    await db.USERS
                        .Where(u => u.IsDeleted != true && u.IsActive)
                        .OrderBy(u => u.FullName)
                        .ToListAsync(),
                    "ID", "FullName", userId);

                ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");

                return View(entries);
            }
        }
    }
}
