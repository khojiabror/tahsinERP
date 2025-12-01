using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Rollar va huquqlarni boshqarish
    /// </summary>
    public class RoleController : Controller
    {
        // GET: Role
        public async Task<ActionResult> Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var roles = await db.ROLES
                    .Include(r => r.PERMISSIONS)
                    .Include(r => r.USERS)
                    .Where(r => r.IsDeleted != true)
                    .OrderBy(r => r.RName)
                    .ToListAsync();

                // Foydalanuvchilar sonini hisoblash (navigation property orqali)
                var userCountsDict = new Dictionary<int, int>();
                foreach (var role in roles)
                {
                    var count = role.USERS != null 
                        ? role.USERS.Count(u => u.IsDeleted != true) 
                        : 0;
                    userCountsDict[role.ID] = count;
                }

                ViewBag.UserCounts = userCountsDict;

                return View(roles);
            }
        }

        // GET: Role/Create
        public ActionResult Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            return View();
        }

        // POST: Role/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(ROLE role)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (string.IsNullOrWhiteSpace(role.RName))
            {
                ModelState.AddModelError("RName", "Rol nomi kiritilishi shart");
                return View(role);
            }

            try
            {
                using (var db = new DBTHSNEntities())
                {
                    // Rol nomi takrorlanmasligini tekshirish
                    var existingRole = await db.ROLES
                        .FirstOrDefaultAsync(r => r.RName == role.RName && r.IsDeleted != true);

                    if (existingRole != null)
                    {
                        ModelState.AddModelError("RName", "Bu nomli rol allaqachon mavjud");
                        return View(role);
                    }

                    // Yangi rol yaratish
                    var newRole = new ROLE
                    {
                        RName = role.RName.Trim(),
                        Description = role.Description?.Trim(),
                        IsDeleted = false
                    };

                    db.ROLES.Add(newRole);
                    await db.SaveChangesAsync();

                    // Barcha modullar uchun default permission yaratish (ViewPermit = true, ChangePermit = false)
                    var modules = await db.PERMISSIONMODULES.ToListAsync();
                    foreach (var module in modules)
                    {
                        var permission = new PERMISSION
                        {
                            RoleID = newRole.ID,
                            PermissionModuleID = module.ID,
                            ViewPermit = true,
                            ChangePermit = false
                        };
                        db.PERMISSIONS.Add(permission);
                    }

                    await db.SaveChangesAsync();

                    // Cache ni tozalash
                    RoleHelper.ClearAllCache();

                    LogHelper.LogToDatabase(User.Identity.Name, "Role", $"{newRole.RName} roli yaratildi");
                    TempData["Success"] = $"'{newRole.RName}' roli muvaffaqiyatli yaratildi";

                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Xatolik yuz berdi: {ex.Message}");
                return View(role);
            }
        }

        // GET: Role/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var role = await db.ROLES.FindAsync(id);
                if (role == null || role.IsDeleted == true)
                {
                    return HttpNotFound();
                }

                // Developer rolini faqat developer o'zgartirishi mumkin
                if (User.Identity.Name != "developer" && role.RName == "developer")
                {
                    TempData["Error"] = "Faqat dasturchilar 'developer' rolini o'zgartira oladi";
                    return RedirectToAction("Index");
                }

                return View(role);
            }
        }

        // POST: Role/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(int id, ROLE role)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (string.IsNullOrWhiteSpace(role.RName))
            {
                ModelState.AddModelError("RName", "Rol nomi kiritilishi shart");
                return View(role);
            }

            try
            {
                using (var db = new DBTHSNEntities())
                {
                    var roleToUpdate = await db.ROLES.FindAsync(id);
                    if (roleToUpdate == null || roleToUpdate.IsDeleted == true)
                    {
                        return HttpNotFound();
                    }

                    // Developer rolini faqat developer o'zgartirishi mumkin
                    if (User.Identity.Name != "developer" && roleToUpdate.RName == "developer")
                    {
                        TempData["Error"] = "Faqat dasturchilar 'developer' rolini o'zgartira oladi";
                        return RedirectToAction("Index");
                    }

                    // Rol nomi takrorlanmasligini tekshirish (o'zidan boshqa)
                    var existingRole = await db.ROLES
                        .FirstOrDefaultAsync(r => r.RName == role.RName && r.ID != id && r.IsDeleted != true);

                    if (existingRole != null)
                    {
                        ModelState.AddModelError("RName", "Bu nomli rol allaqachon mavjud");
                        return View(role);
                    }

                    // Yangilash
                    roleToUpdate.RName = role.RName.Trim();
                    roleToUpdate.Description = role.Description?.Trim();

                    await db.SaveChangesAsync();

                    // Cache ni tozalash
                    RoleHelper.ClearAllCache();

                    LogHelper.LogToDatabase(User.Identity.Name, "Role", $"{roleToUpdate.RName} roli yangilandi");
                    TempData["Success"] = "Rol muvaffaqiyatli yangilandi";

                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Xatolik yuz berdi: {ex.Message}");
                return View(role);
            }
        }

        // GET: Role/Delete/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var role = await db.ROLES
                    .Include(r => r.PERMISSIONS)
                    .FirstOrDefaultAsync(r => r.ID == id && r.IsDeleted != true);

                if (role == null)
                {
                    return HttpNotFound();
                }

                // Developer rolini o'chirib bo'lmaydi
                if (role.RName == "developer")
                {
                    TempData["Error"] = "'developer' rolini o'chirib bo'lmaydi";
                    return RedirectToAction("Index");
                }

                // Bu rolga ega foydalanuvchilar borligini tekshirish
                var usersWithRole = await db.USERS
                    .Where(u => u.ROLES.Any(r => r.ID == id) && u.IsDeleted != true)
                    .CountAsync();

                ViewBag.UsersCount = usersWithRole;

                return View(role);
            }
        }

        // POST: Role/Delete/5
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
                    var role = await db.ROLES.FindAsync(id);
                    if (role == null || role.IsDeleted == true)
                    {
                        return HttpNotFound();
                    }

                    // Developer rolini o'chirib bo'lmaydi
                    if (role.RName == "developer")
                    {
                        TempData["Error"] = "'developer' rolini o'chirib bo'lmaydi";
                        return RedirectToAction("Index");
                    }

                    // Soft delete
                    role.IsDeleted = true;
                    await db.SaveChangesAsync();

                    // Cache ni tozalash
                    RoleHelper.ClearAllCache();

                    LogHelper.LogToDatabase(User.Identity.Name, "Role", $"{role.RName} roli o'chirildi");
                    TempData["Success"] = "Rol muvaffaqiyatli o'chirildi";

                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Xatolik yuz berdi: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // GET: Role/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var role = await db.ROLES
                    .Include(r => r.PERMISSIONS.Select(p => p.PERMISSIONMODULE))
                    .FirstOrDefaultAsync(r => r.ID == id && r.IsDeleted != true);

                if (role == null)
                {
                    return HttpNotFound();
                }

                return View(role);
            }
        }

        // GET: Role/Permissions/5
        public async Task<ActionResult> Permissions(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var role = await db.ROLES
                    .Include(r => r.PERMISSIONS.Select(p => p.PERMISSIONMODULE))
                    .FirstOrDefaultAsync(r => r.ID == id && r.IsDeleted != true);

                if (role == null)
                {
                    return HttpNotFound();
                }

                // Barcha modullarni olish va permission bor-yo'qligini tekshirish
                var allModules = await db.PERMISSIONMODULES
                    .OrderBy(m => m.Module)
                    .ThenBy(m => m.Controller)
                    .ThenBy(m => m.Action)
                    .ToListAsync();

                var viewModel = new RolePermissionsViewModel
                {
                    RoleID = role.ID,
                    RoleName = role.RName,
                    Description = role.Description,
                    Permissions = allModules.Select(module =>
                    {
                        var existingPermission = role.PERMISSIONS
                            .FirstOrDefault(p => p.PermissionModuleID == module.ID);

                        return new PermissionViewModel
                        {
                            ID = existingPermission?.ID ?? 0,
                            PermissionModuleID = module.ID,
                            Module = module.Module ?? "",
                            Controller = module.Controller ?? "",
                            Action = module.Action ?? "",
                            Parameter = module.Parameter ?? "",
                            ViewPermit = existingPermission?.ViewPermit ?? false,
                            ChangePermit = existingPermission?.ChangePermit ?? false
                        };
                    }).ToList()
                };

                return View(viewModel);
            }
        }

        // POST: Role/Permissions
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Permissions(RolePermissionsViewModel model)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (model == null || model.Permissions == null || !model.Permissions.Any())
            {
                TempData["Error"] = "Ma'lumot topilmadi";
                return RedirectToAction("Permissions", new { id = model?.RoleID ?? 0 });
            }

            try
            {
                using (var db = new DBTHSNEntities())
                {
                    var role = await db.ROLES
                        .Include(r => r.PERMISSIONS)
                        .FirstOrDefaultAsync(r => r.ID == model.RoleID && r.IsDeleted != true);

                    if (role == null)
                    {
                        TempData["Error"] = "Rol topilmadi";
                        return RedirectToAction("Index", "Role");
                    }

                    // Barcha mavjud permissions ni dictionary ga olish (tezroq qidirish uchun)
                    var existingPermissionsDict = role.PERMISSIONS
                        .ToDictionary(p => p.PermissionModuleID, p => p);

                    // PermissionModuleID lar ro'yxati (validation uchun)
                    var validModuleIds = await db.PERMISSIONMODULES
                        .Select(m => m.ID)
                        .ToListAsync();

                    var permissionsToAdd = new List<PERMISSION>();
                    var permissionsToUpdate = new List<PERMISSION>();

                    // Har bir permission ni tekshirish va yangilash/yaratish
                    foreach (var permVm in model.Permissions)
                    {
                        // Validation
                        if (permVm.PermissionModuleID <= 0 || !validModuleIds.Contains(permVm.PermissionModuleID))
                        {
                            continue; // Noto'g'ri module ID ni o'tkazib yuborish
                        }

                        if (existingPermissionsDict.TryGetValue(permVm.PermissionModuleID, out var existingPermission))
                        {
                            // Mavjud permission ni yangilash
                            if (existingPermission.ViewPermit != permVm.ViewPermit || 
                                existingPermission.ChangePermit != permVm.ChangePermit)
                            {
                                existingPermission.ViewPermit = permVm.ViewPermit;
                                existingPermission.ChangePermit = permVm.ChangePermit;
                                permissionsToUpdate.Add(existingPermission);
                            }
                        }
                        else
                        {
                            // Yangi permission yaratish
                            var newPermission = new PERMISSION
                            {
                                RoleID = model.RoleID,
                                PermissionModuleID = permVm.PermissionModuleID,
                                ViewPermit = permVm.ViewPermit,
                                ChangePermit = permVm.ChangePermit
                            };
                            permissionsToAdd.Add(newPermission);
                        }
                    }

                    // Bulk operations
                    if (permissionsToAdd.Any())
                    {
                        db.PERMISSIONS.AddRange(permissionsToAdd);
                    }

                    if (permissionsToUpdate.Any())
                    {
                        foreach (var perm in permissionsToUpdate)
                        {
                            db.Entry(perm).State = System.Data.Entity.EntityState.Modified;
                        }
                    }

                    await db.SaveChangesAsync();

                    // Cache ni tozalash
                    RoleHelper.ClearAllCache();

                    LogHelper.LogToDatabase(User.Identity.Name, "Role", $"{role.RName} roli uchun {permissionsToAdd.Count} ta yangi va {permissionsToUpdate.Count} ta yangilangan huquq saqlandi");
                    TempData["Success"] = $"Huquqlar muvaffaqiyatli saqlandi ({permissionsToAdd.Count} ta yangi, {permissionsToUpdate.Count} ta yangilangan)";

                    return RedirectToAction("Permissions", new { id = model.RoleID });
                }
            }
            catch (DbUpdateException dbEx)
            {
                TempData["Error"] = $"Ma'lumotlar bazasi xatosi: {dbEx.Message}";
                if (dbEx.InnerException != null)
                {
                    TempData["Error"] += $" | {dbEx.InnerException.Message}";
                }
                return RedirectToAction("Permissions", new { id = model.RoleID });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Xatolik yuz berdi: {ex.Message}";
                return RedirectToAction("Permissions", new { id = model.RoleID });
            }
        }
    }
}

