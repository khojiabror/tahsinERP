using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Admin Panel - foydalanuvchilar, rollar va tizim statistikasini boshqarish.
    /// Users/Roles/Permissions/SavePermissions/Logs/UserEntries amallari olib tashlandi: ularning
    /// view'lari yo'q edi; bu bo'limlarni User, Role, PModule, Log va UserEntries controllerlari qoplaydi.
    /// </summary>
    public class AdminPanelController : Controller
    {
        private readonly DBTHSNEntities db = new DBTHSNEntities();

        // Olish so'rovi: AdminPanel
        public async Task<ActionResult> Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Sana diapazoni (TruncateTime indeksni ishlatmasdi — butun jadval skan qilinardi)
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var weekAgo = DateTime.Now.AddDays(-7);

            var model = new AdminDashboardViewModel
            {
                // Foydalanuvchilar statistikasi
                TotalUsers = await db.USERS.CountAsync(u => u.IsDeleted != true),
                ActiveUsers = await db.USERS.CountAsync(u => u.IsDeleted != true && u.IsActive),
                InactiveUsers = await db.USERS.CountAsync(u => u.IsDeleted != true && !u.IsActive),

                // Rollar statistikasi
                TotalRoles = await db.ROLES.CountAsync(),
                TotalPermissionModules = await db.PERMISSIONMODULES.CountAsync(),

                // Log statistikasi
                TodayLogs = await db.USERLOGS.CountAsync(l => l.DateTime >= today && l.DateTime < tomorrow),
                ThisWeekLogs = await db.USERLOGS.CountAsync(l => l.DateTime >= weekAgo),

                // Tizimga kirishlar
                TotalUserEntries = await db.USER_ENTRIES.CountAsync(),
                TodayUserEntries = await db.USER_ENTRIES.CountAsync(e => e.DateTime >= today && e.DateTime < tomorrow),

                // Oxirgi kirishlar (so'nggi 10 ta)
                RecentUserEntries = await db.USER_ENTRIES.AsNoTracking()
                    .Include(e => e.USER)
                    .OrderByDescending(e => e.DateTime)
                    .Take(10)
                    .ToListAsync(),

                // Oxirgi loglar (so'nggi 10 ta)
                RecentLogs = await db.USERLOGS.AsNoTracking()
                    .Include(l => l.USER)
                    .OrderByDescending(l => l.DateTime)
                    .Take(10)
                    .ToListAsync(),

                // Rollar ro'yxati — huquqlar soni bitta so'rovda (view har rol uchun alohida yuklardi)
                Roles = await db.ROLES.AsNoTracking()
                    .Include(r => r.PERMISSIONS)
                    .ToListAsync(),

                // Foydalanuvchilar ro'yxati — view faqat birinchi 10 tasini ko'rsatadi
                Users = await db.USERS.AsNoTracking()
                    .Include(u => u.ROLES)
                    .Where(u => u.IsDeleted != true)
                    .OrderBy(u => u.FullName)
                    .Take(10)
                    .ToListAsync()
            };

            return View(model);
        }

        /// <summary>Firma standart interfeys tili (foydalanuvchi o'zi tanlamagan bo'lsa).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetDefaultLanguage(string language)
        {
            if (!RoleHelper.IsAdminOrDeveloper(User.Identity.Name))
                return RedirectToAction("Index", "ViewNotAuthorized");
            if (tahsinERP.Localization.Languages.IsSupported(language))
            {
                tahsinERP.I18n.UiLanguage.SetFirmDefault(language);
                TempData["LangSaved"] = true;
            }
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
