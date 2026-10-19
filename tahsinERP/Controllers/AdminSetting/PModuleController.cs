using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.Filters;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Permission Module CRUD - Ruxsat modullarini boshqarish
    /// </summary>
    public class PModuleController : Controller
    {
        // Controller turlari ilova davomida o'zgarmaydi — reflection bir marta (CORE-P16)
        private static readonly Lazy<List<Type>> ControllerTypes = new Lazy<List<Type>>(() =>
            Assembly.GetExecutingAssembly().GetTypes()
                .Where(type => typeof(Controller).IsAssignableFrom(type) && !type.IsAbstract)
                .ToList());

        private static List<string> ControllerNames()
        {
            return ControllerTypes.Value
                .Select(type => type.Name.Replace("Controller", ""))
                .OrderBy(c => c)
                .ToList();
        }

        // Action: NonAction emas va ActionResult (yoki Task<ActionResult>) qaytaradi — JsonResult,
        // FileResult va boshqa hosilalar ham (avval operator ustuvorligi xatosi bor edi, CORE-B07)
        private static bool IsActionMethod(MethodInfo m)
        {
            if (m.IsDefined(typeof(NonActionAttribute)) || m.IsSpecialName)
                return false;
            var returnType = m.ReturnType;
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                returnType = returnType.GetGenericArguments()[0];
            return typeof(ActionResult).IsAssignableFrom(returnType);
        }

        // Details va Delete sahifalari uchun bir xil model (avval ikki joyda ko'chirilgan)
        private static PermissionModuleViewModel ToViewModel(PERMISSIONMODULE module, IEnumerable<PERMISSION> permissions)
        {
            var roleNames = permissions
                .Select(p => p.ROLE?.RName)
                .Where(r => r != null)
                .Distinct();

            return new PermissionModuleViewModel
            {
                ID = module.ID,
                Module = module.Module,
                Controller = module.Controller,
                Action = module.Action,
                Parametr = module.Parameter,
                RoleNames = string.Join(", ", roleNames)
            };
        }

        private static bool IsAdminRole(string roleName)
        {
            return string.Equals(roleName, "Admin", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(roleName, "Developer", System.StringComparison.OrdinalIgnoreCase);
        }

        // Olish so'rovi: PModule
        public async Task<ActionResult> Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                // Optimized query - barcha ma'lumotlarni bitta query bilan olish
                var permissionModules = await db.PERMISSIONMODULES
                    .OrderBy(pm => pm.Module)
                    .ThenBy(pm => pm.Controller)
                    .ThenBy(pm => pm.Action)
                    .ToListAsync();

                var permissionModuleIds = permissionModules.Select(pm => pm.ID).ToList();

                // Barcha permissions va rollarni bitta query bilan olish
                var permissions = await db.PERMISSIONS
                    .Where(p => permissionModuleIds.Contains(p.PermissionModuleID))
                    .Include(p => p.ROLE)
                    .ToListAsync();

                var roleIds = permissions.Select(p => p.RoleID).Distinct().ToList();
                var roles = await db.ROLES
                    .Where(r => roleIds.Contains(r.ID))
                    .ToDictionaryAsync(r => r.ID, r => r.RName);

                // ViewModel yaratish
                var viewModel = permissionModules.Select(pm =>
                {
                    var relatedPermissions = permissions.Where(p => p.PermissionModuleID == pm.ID).ToList();
                    var roleNames = relatedPermissions
                        .Select(p => roles.ContainsKey(p.RoleID) ? roles[p.RoleID] : null)
                        .Where(r => r != null)
                        .Distinct()
                        .ToList();

                    return new PermissionModuleViewModel
                    {
                        ID = pm.ID,
                        Module = pm.Module,
                        Controller = pm.Controller,
                        Action = pm.Action,
                        Parametr = pm.Parameter,
                        RoleNames = string.Join(", ", roleNames)
                    };
                }).ToList();

                return View(viewModel);
            }
        }

        // Olish so'rovi: PModule/MissingControllers
        public async Task<ActionResult> MissingControllers()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var allControllers = ControllerNames();

            using (var db = new DBTHSNEntities())
            {
                var existingControllers = await db.PERMISSIONMODULES
                    .Select(pm => pm.Controller)
                    .ToListAsync();

                var existingSet = new HashSet<string>(
                    existingControllers
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .Select(c => c.Trim()),
                    StringComparer.OrdinalIgnoreCase);

                var missing = allControllers
                    .Where(c => !existingSet.Contains(c))
                    .ToList();

                return View(missing);
            }
        }

        // POST so'rovi: PModule/CreateDefaultModules
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateDefaultModules(string controllerName)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (string.IsNullOrWhiteSpace(controllerName))
            {
                TempData["Error"] = "Controller nomi topilmadi.";
                return RedirectToAction("MissingControllers");
            }

            var trimmedController = controllerName.Trim();
            var defaultActions = new List<string> { "Create", "Edit", "Details", "Delete" };

            try
            {
                using (var db = new DBTHSNEntities())
                {
                    var lowerController = trimmedController.ToLower();
                    var hasAny = await db.PERMISSIONMODULES
                        .AnyAsync(pm => (pm.Controller ?? "").Trim().ToLower() == lowerController);

                    if (hasAny)
                    {
                        TempData["Error"] = "Bu controller uchun permission modullari allaqachon mavjud.";
                        return RedirectToAction("MissingControllers");
                    }

                    var newModules = defaultActions.Select(action => new PERMISSIONMODULE
                    {
                        Module = trimmedController,
                        Controller = trimmedController,
                        Action = action,
                        Parameter = null
                    }).ToList();

                    db.PERMISSIONMODULES.AddRange(newModules);
                    await db.SaveChangesAsync();

                    // Yangi modul faqat Admin/Developer'ga ochiq; boshqa rollarga admin o'zi beradi (CORE-S04)
                    var roles = await db.ROLES
                        .Where(r => r.IsDeleted != true)
                        .Select(r => new { r.ID, r.RName })
                        .ToListAsync();

                    var permissionsToAdd = new List<PERMISSION>();
                    foreach (var module in newModules)
                    {
                        permissionsToAdd.AddRange(roles.Select(role => RoleHelper.NewPermission(module.ID, role.ID, IsAdminRole(role.RName))));
                    }

                    if (permissionsToAdd.Any())
                    {
                        db.PERMISSIONS.AddRange(permissionsToAdd);
                        await db.SaveChangesAsync();
                    }

                    RoleHelper.ClearAllCache();

                    LogHelper.LogToDatabase(User.Identity.Name, "PModule", $"{trimmedController} - default permission modullari yaratildi");
                    TempData["Success"] = $"{trimmedController} uchun 4 ta default permission moduli yaratildi";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Xatolik yuz berdi: {ex.Message}";
            }

            return RedirectToAction("MissingControllers");
        }

        // Olish so'rovi: PModule/yaratish
        public ActionResult Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            ViewBag.ControllerNames = GetAllControllerNames();
            ViewBag.Parametr = ConfigurationManager.AppSettings["Parametr"]?.Split(',').ToList() ?? new List<string>();

            return View();
        }

        // POST so'rovi: PModule/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(PERMISSIONMODULE permissions)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (string.IsNullOrWhiteSpace(permissions.Module))
            {
                ModelState.AddModelError("Module", "Modul nomi kiritilishi shart");
            }

            if (string.IsNullOrWhiteSpace(permissions.Controller))
            {
                ModelState.AddModelError("Controller", "Controller tanlanishi shart");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    using (var db = new DBTHSNEntities())
                    {
                        // Takrorlanishni tekshirish (Action null yoki bo'sh bo'lishi mumkin)
                        // Action bo'sh yoki null bo'lsa, uni null sifatida saqlash
                        var moduleValue = permissions.Module?.Trim();
                        var controllerValue = permissions.Controller?.Trim();
                        var actionValue = string.IsNullOrWhiteSpace(permissions.Action) ? null : permissions.Action.Trim();
                        
                        // Butun jadvalni emas, faqat shu controller'ning qatorlarini yuklaymiz
                        var existing = await db.PERMISSIONMODULES
                            .Where(pm => pm.Controller.Trim() == controllerValue)
                            .ToListAsync();
                        
                        var existingMatch = existing.FirstOrDefault(pm => 
                            (pm.Module ?? "").Trim() == moduleValue &&
                            (pm.Controller ?? "").Trim() == controllerValue &&
                            ((string.IsNullOrWhiteSpace(pm.Action) && string.IsNullOrWhiteSpace(actionValue)) ||
                             ((pm.Action ?? "").Trim() == actionValue))
                        );

                        if (existingMatch != null)
                        {
                            var actionText = string.IsNullOrWhiteSpace(existingMatch.Action) ? "(bo'sh)" : existingMatch.Action;
                            ModelState.AddModelError("", $"Bu modul, controller va action kombinatsiyasi allaqachon mavjud. Mavjud: Module='{existingMatch.Module}', Controller='{existingMatch.Controller}', Action='{actionText}'");
                        }
                        else
                        {
                            // Action bo'sh bo'lsa null qilish
                            permissions.Action = actionValue;
                            permissions.Module = permissions.Module?.Trim();
                            permissions.Controller = permissions.Controller?.Trim();
                            permissions.Parameter = permissions.Parameter?.Trim();
                            
                            db.PERMISSIONMODULES.Add(permissions);
                            await db.SaveChangesAsync();

                            // Barcha rollarga permission biriktirish
                            // Yangi modul faqat Admin/Developer'ga ochiq (CORE-S04)
                            var roles = await db.ROLES
                                .Where(r => r.IsDeleted != true)
                                .Select(r => new { r.ID, r.RName })
                                .ToListAsync();

                            var permissionsToAdd = roles.Select(role => RoleHelper.NewPermission(permissions.ID, role.ID, IsAdminRole(role.RName))).ToList();

                            if (permissionsToAdd.Any())
                            {
                                db.PERMISSIONS.AddRange(permissionsToAdd);
                                await db.SaveChangesAsync();
                            }

                            // Cache ni tozalash
                            RoleHelper.ClearAllCache();

                            LogHelper.LogToDatabase(User.Identity.Name, "PModule", $"{permissions.Module} - Permission moduli yaratildi");
                            TempData["Success"] = "Permission moduli muvaffaqiyatli yaratildi";

                            return RedirectToAction("Index");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Xatolik yuz berdi: {ex.Message}");
                }
            }

            // xatolik bo'lsa, ViewBag ni qayta yuklash
            ViewBag.ControllerNames = GetAllControllerNames();
            ViewBag.Parametr = ConfigurationManager.AppSettings["Parametr"]?.Split(',').ToList() ?? new List<string>();

            return View(permissions);
        }

        // POST so'rovi: PModule/GetActions
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult GetActions(string controller)
        {
            try
            {
                if (string.IsNullOrEmpty(controller))
                {
                    return Json(new List<string>());
                }

                var controllerType = ControllerTypes.Value
                    .FirstOrDefault(type => type.Name == $"{controller}Controller");

                if (controllerType != null)
                {
                    var actions = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public)
                        .Where(IsActionMethod)
                        .Select(m => m.Name)
                        .Distinct()
                        .OrderBy(a => a)
                        .ToList();

                    return Json(actions);
                }

                return Json(new List<string>());
            }
            catch 
            {
                return Json(new List<string>());
            }
        }

        // Olish so'rovi: PModule/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var pModule = await db.PERMISSIONMODULES.FindAsync(id);
                if (pModule == null)
                {
                    return HttpNotFound();
                }

                var roles = await db.ROLES
                    .Where(r => r.IsDeleted != true)
                    .OrderBy(r => r.RName)
                    .ToListAsync();

                var selectedRoleIDs = await db.PERMISSIONS
                    .Where(p => p.PermissionModuleID == id)
                    .Select(p => p.RoleID)
                    .ToListAsync();

                ViewBag.Roles = new MultiSelectList(roles, "ID", "RName", selectedRoleIDs);
                ViewBag.ControllerNames = new SelectList(GetAllControllerNames(), "Value", "Text", pModule.Controller);
                ViewBag.Parametr = ConfigurationManager.AppSettings["Parametr"]?.Split(',').ToList() ?? new List<string>();

                return View(pModule);
            }
        }

        // POST so'rovi: PModule/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(PERMISSIONMODULE permissions, int[] RoleID)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (string.IsNullOrWhiteSpace(permissions.Module))
            {
                ModelState.AddModelError("Module", "Modul nomi kiritilishi shart");
            }

            if (string.IsNullOrWhiteSpace(permissions.Controller))
            {
                ModelState.AddModelError("Controller", "Controller tanlanishi shart");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    using (var db = new DBTHSNEntities())
                    {
                        var pModule = await db.PERMISSIONMODULES.FindAsync(permissions.ID);
                        if (pModule == null)
                        {
                            return HttpNotFound();
                        }

                        // Takrorlanishni tekshirish (o'zidan boshqa)
                        var existing = await db.PERMISSIONMODULES
                            .FirstOrDefaultAsync(pm => pm.ID != permissions.ID &&
                                                      pm.Module == permissions.Module &&
                                                      pm.Controller == permissions.Controller &&
                                                      pm.Action == permissions.Action);

                        if (existing != null)
                        {
                            ModelState.AddModelError("", "Bu modul, controller va action kombinatsiyasi allaqachon mavjud");
                        }
                        else
                        {
                            // PermissionModule ni yangilash
                            pModule.Module = permissions.Module?.Trim();
                            pModule.Controller = permissions.Controller?.Trim();
                            pModule.Action = permissions.Action?.Trim();
                            pModule.Parameter = permissions.Parameter?.Trim();

                            // Mavjud permissions ni olish
                            var existingPermissions = await db.PERMISSIONS
                                .Where(p => p.PermissionModuleID == pModule.ID)
                                .ToListAsync();

                            // Valid role IDs
                            var validRoleIds = RoleID != null && RoleID.Length > 0
                                ? await db.ROLES
                                    .Where(r => RoleID.Contains(r.ID) && r.IsDeleted != true)
                                    .Select(r => r.ID)
                                    .ToListAsync()
                                : new List<int>();

                            // O'chirish kerak bo'lgan permissions
                            var permissionsToRemove = existingPermissions
                                .Where(p => !validRoleIds.Contains(p.RoleID))
                                .ToList();

                            if (permissionsToRemove.Any())
                            {
                                db.PERMISSIONS.RemoveRange(permissionsToRemove);
                            }

                            // Yangi qo'shish kerak bo'lgan permissions
                            var existingRoleIds = existingPermissions.Select(p => p.RoleID).ToList();
                            var permissionsToAdd = validRoleIds
                                .Where(roleId => !existingRoleIds.Contains(roleId))
                                .Select(roleId => RoleHelper.NewPermission(pModule.ID, roleId, true)).ToList();

                            if (permissionsToAdd.Any())
                            {
                                db.PERMISSIONS.AddRange(permissionsToAdd);
                            }

                            await db.SaveChangesAsync();

                            // Cache ni tozalash
                            RoleHelper.ClearAllCache();

                            LogHelper.LogToDatabase(User.Identity.Name, "PModule", $"{pModule.Module} - Permission moduli yangilandi");
                            TempData["Success"] = "Permission moduli muvaffaqiyatli yangilandi";

                            return RedirectToAction("Index");
                        }
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    ModelState.AddModelError("", "Ma'lumotlar boshqa foydalanuvchi tomonidan o'zgartirilgan yoki o'chirilgan. Qayta urinib ko'ring.");
                }
                catch (RetryLimitExceededException)
                {
                    ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Xatolik yuz berdi: {ex.Message}");
                }
            }

            // xatolik bo'lsa, ViewBag ni qayta yuklash
            using (var db = new DBTHSNEntities())
            {
                var roles = await db.ROLES
                    .Where(r => r.IsDeleted != true)
                    .OrderBy(r => r.RName)
                    .ToListAsync();

                ViewBag.Roles = new MultiSelectList(roles, "ID", "RName", RoleID);
                ViewBag.ControllerNames = new SelectList(GetAllControllerNames(), "Value", "Text", permissions.Controller);
                ViewBag.Parametr = ConfigurationManager.AppSettings["Parametr"]?.Split(',').ToList() ?? new List<string>();
            }

            return View(permissions);
        }

        // Olish so'rovi: PModule/o'chirish/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var permissionModule = await db.PERMISSIONMODULES.FindAsync(id);
                if (permissionModule == null)
                {
                    return HttpNotFound();
                }

                var permissions = await db.PERMISSIONS
                    .Where(p => p.PermissionModuleID == id)
                    .Include(p => p.ROLE)
                    .ToListAsync();

                var viewModel = ToViewModel(permissionModule, permissions);

                ViewBag.PermissionsCount = permissions.Count;

                return View(viewModel);
            }
        }

        // POST so'rovi: PModule/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            try
            {
                using (var db = new DBTHSNEntities())
                {
                    var permissionModule = await db.PERMISSIONMODULES.FindAsync(id);
                    if (permissionModule == null)
                    {
                        return HttpNotFound();
                    }

                    // Barcha permissions ni o'chirish
                    var permissions = await db.PERMISSIONS
                        .Where(p => p.PermissionModuleID == id)
                        .Include(p => p.ROLE)
                        .ToListAsync();

                    if (permissions.Any())
                    {
                        db.PERMISSIONS.RemoveRange(permissions);
                    }

                    // PermissionModule ni o'chirish
                    db.PERMISSIONMODULES.Remove(permissionModule);
                    await db.SaveChangesAsync();

                    // Cache ni tozalash
                    RoleHelper.ClearAllCache();

                    LogHelper.LogToDatabase(User.Identity.Name, "PModule", $"{permissionModule.Module} - Permission moduli o'chirildi");
                    TempData["Success"] = "Permission moduli muvaffaqiyatli o'chirildi";

                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Xatolik yuz berdi: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Olish so'rovi: PModule/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var permissionModule = await db.PERMISSIONMODULES.FindAsync(id);
                if (permissionModule == null)
                {
                    return HttpNotFound();
                }

                var permissions = await db.PERMISSIONS
                    .Where(p => p.PermissionModuleID == id)
                    .Include(p => p.ROLE)
                    .ToListAsync();

                var viewModel = ToViewModel(permissionModule, permissions);

                return View(viewModel);
            }
        }

        /// <summary>
        /// Barcha controller nomlarini olish
        /// </summary>
        private List<SelectListItem> GetAllControllerNames()
        {
            var controllers = ControllerNames();

            return controllers.Select(c => new SelectListItem { Text = c, Value = c }).ToList();
        }
    }
}
