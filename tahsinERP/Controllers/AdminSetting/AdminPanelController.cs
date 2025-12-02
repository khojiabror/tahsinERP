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
    /// Admin Panel - foydalanuvchilar, rollar va tizim statistikasini boshqarish
    /// </summary>
    public class AdminPanelController : Controller
    {
        private readonly DBTHSNEntities db = new DBTHSNEntities();

        // GET: AdminPanel
        public async Task<ActionResult> Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var model = new AdminDashboardViewModel
            {
                // Foydalanuvchilar statistikasi
                TotalUsers = await db.USERS.CountAsync(u => u.IsDeleted != true),
                ActiveUsers = await db.USERS.CountAsync(u => u.IsDeleted != true && u.IsActive),
                InactiveUsers = await db.USERS.CountAsync(u => u.IsDeleted != true && !u.IsActive),

                // Rollar statistikasi
                TotalRoles = await db.ROLES.CountAsync(),
                TotalPermissionModules = await db.PERMISSIONMODULES.CountAsync(),
                TotalPermissions = await db.PERMISSIONS.CountAsync(),

                // Log statistikasi
                TotalLogs = await db.USERLOGS.CountAsync(),
                TodayLogs = await db.USERLOGS.CountAsync(l => DbFunctions.TruncateTime(l.DateTime) == DbFunctions.TruncateTime(DateTime.Now)),
                ThisWeekLogs = await db.USERLOGS.CountAsync(l => l.DateTime >= DbFunctions.AddDays(DateTime.Now, -7)),

                // Tizimga kirishlar
                TotalUserEntries = await db.USER_ENTRIES.CountAsync(),
                TodayUserEntries = await db.USER_ENTRIES.CountAsync(e => DbFunctions.TruncateTime(e.DateTime) == DbFunctions.TruncateTime(DateTime.Now)),

                // Oxirgi kirishlar (so'nggi 10 ta)
                RecentUserEntries = await db.USER_ENTRIES
                    .Include(e => e.USER)
                    .OrderByDescending(e => e.DateTime)
                    .Take(10)
                    .ToListAsync(),

                // Oxirgi loglar (so'nggi 10 ta)
                RecentLogs = await db.USERLOGS
                    .Include(l => l.USER)
                    .OrderByDescending(l => l.DateTime)
                    .Take(10)
                    .ToListAsync(),

                // Rollar ro'yxati
                Roles = await db.ROLES.ToListAsync(),

                // Foydalanuvchilar ro'yxati
                Users = await db.USERS
                    .Include(u => u.ROLES)
                    .Where(u => u.IsDeleted != true)
                    .OrderBy(u => u.FullName)
                    .ToListAsync()
            };

            return View(model);
        }

        // GET: AdminPanel/Users
        public async Task<ActionResult> Users()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var users = await db.USERS
                .Include(u => u.ROLES)
                .Where(u => u.IsDeleted != true)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            return View(users);
        }

        // GET: AdminPanel/Roles
        public async Task<ActionResult> Roles()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var roles = await db.ROLES
                .Include(r => r.PERMISSIONS)
                .ToListAsync();

            return View(roles);
        }

        // GET: AdminPanel/Permissions/{roleId}
        public async Task<ActionResult> Permissions(int? roleId)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            ViewBag.Roles = await db.ROLES.ToListAsync();
            ViewBag.SelectedRoleId = roleId;

            if (!roleId.HasValue)
            {
                return View(new PermissionManagementViewModel());
            }

            var role = await db.ROLES.FindAsync(roleId.Value);
            if (role == null)
            {
                return HttpNotFound();
            }

            var modules = await db.PERMISSIONMODULES
                .OrderBy(m => m.Module)
                .ThenBy(m => m.Controller)
                .ToListAsync();

            var permissions = await db.PERMISSIONS
                .Where(p => p.RoleID == roleId.Value)
                .ToListAsync();

            var model = new PermissionManagementViewModel
            {
                RoleId = roleId.Value,
                RoleName = role.RName,
                Modules = modules.Select(m => new ModulePermissionViewModel
                {
                    ModuleId = m.ID,
                    ModuleName = m.Module,
                    Controller = m.Controller,
                    Action = m.Action,
                    Parameter = m.Parameter,
                    ViewPermit = permissions.FirstOrDefault(p => p.PermissionModuleID == m.ID)?.ViewPermit ?? false,
                    ChangePermit = permissions.FirstOrDefault(p => p.PermissionModuleID == m.ID)?.ChangePermit ?? false
                }).ToList()
            };

            return View(model);
        }

        // POST: AdminPanel/SavePermissions
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SavePermissions(PermissionManagementViewModel model)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (model.Modules == null || !model.Modules.Any())
            {
                TempData["Error"] = "Saqlash uchun ma'lumot topilmadi.";
                return RedirectToAction("Permissions", new { roleId = model.RoleId });
            }

            try
            {
                foreach (var moduleVm in model.Modules)
                {
                    var existingPermission = await db.PERMISSIONS
                        .FirstOrDefaultAsync(p => p.RoleID == model.RoleId && p.PermissionModuleID == moduleVm.ModuleId);

                    if (existingPermission != null)
                    {
                        existingPermission.ViewPermit = moduleVm.ViewPermit;
                        existingPermission.ChangePermit = moduleVm.ChangePermit;
                        existingPermission.DeletePermit = false; // Default value
                    }
                    else
                    {
                        db.PERMISSIONS.Add(new PERMISSION
                        {
                            RoleID = model.RoleId,
                            PermissionModuleID = moduleVm.ModuleId,
                            ViewPermit = moduleVm.ViewPermit,
                            ChangePermit = moduleVm.ChangePermit,
                            DeletePermit = false // Default value
                        });
                    }
                }

                await db.SaveChangesAsync();

                // Cache ni tozalash - barcha foydalanuvchilar uchun
                RoleHelper.ClearAllCache();

                TempData["Success"] = "Huquqlar muvaffaqiyatli saqlandi.";
                LogHelper.LogToDatabase(User.Identity.Name, "AdminPanel", $"Role #{model.RoleId} uchun huquqlar yangilandi");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Xatolik yuz berdi: {ex.Message}";
            }

            return RedirectToAction("Permissions", new { roleId = model.RoleId });
        }

        // GET: AdminPanel/Logs
        public async Task<ActionResult> Logs(int page = 1, int pageSize = 50)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var totalLogs = await db.USERLOGS.CountAsync();
            var logs = await db.USERLOGS
                .Include(l => l.USER)
                .OrderByDescending(l => l.DateTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalLogs / pageSize);
            ViewBag.TotalLogs = totalLogs;

            return View(logs);
        }

        // GET: AdminPanel/UserEntries
        public async Task<ActionResult> UserEntries(int page = 1, int pageSize = 50)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var totalEntries = await db.USER_ENTRIES.CountAsync();
            var entries = await db.USER_ENTRIES
                .Include(e => e.USER)
                .OrderByDescending(e => e.DateTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalEntries / pageSize);
            ViewBag.TotalEntries = totalEntries;

            return View(entries);
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
