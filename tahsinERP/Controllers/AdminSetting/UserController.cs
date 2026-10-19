using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    public class UserController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();
        private int userPhotoMaxLength = Convert.ToInt32(ConfigurationManager.AppSettings["photoMaxSize"]);

        private static bool IsDeveloperRole(ROLE role)
        {
            return tahsinERP.Services.Common.SystemCodes.Role.IsDeveloper(role);
        }

        // Developer foydalanuvchini faqat Developer o'zgartira/ko'ra oladi
        private bool CanManageTarget(USER target)
        {
            if (target == null || !target.ROLES.Any(IsDeveloperRole)) return true;
            return RoleHelper.IsDeveloper(User.Identity.Name);
        }

        // Olish so'rovi: Users
        public async Task<ActionResult> Index(int? roleID, string status)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            IQueryable<USER> usersQuery = db.USERS
                .Include(u => u.ROLES)
                .Where(u => u.IsDeleted == false);

            if (roleID.HasValue)
            {
                usersQuery = usersQuery.Where(u => u.ROLES.Any(r => r.ID == roleID.Value));
            }

            if (status == "InActive")
            {
                usersQuery = usersQuery.Where(u => u.IsActive == false);
            }
            else
            {
                usersQuery = usersQuery.Where(u => u.IsActive == true);
            }

            var users = await usersQuery.ToListAsync();

            // Developer foydalanuvchilarni faqat Developer rol egasiga ko'rsatamiz
            bool viewerIsDev = tahsinERP.Models.RoleHelper.IsDeveloper(User.Identity.Name);
            if (!viewerIsDev)
            {
                users = users
                    .Where(u => !u.ROLES.Any(IsDeveloperRole))
                    .ToList();
            }

            var userViewModels = users.Select(u => new UserViewModel
            {
                ID = u.ID,
                UName = u.Uname,
                FullName = u.FullName,
                Email = u.Email,
                IsActive = u.IsActive,
                Role = u.ROLES.FirstOrDefault()?.RName ?? "Rol yo'q"
            }).ToList();

            // Rol filtri ro'yxatidan ham Developer'ni yashiramiz
            var roleListQuery = await db.ROLES.Where(r => r.IsDeleted != true).OrderBy(r => r.RName).ToListAsync();
            if (!viewerIsDev)
            {
                roleListQuery = roleListQuery
                    .Where(r => !IsDeveloperRole(r))
                    .ToList();
            }
            ViewBag.RoleList = new SelectList(roleListQuery, "ID", "RName", roleID);
            ViewBag.RoleID = roleID;

            return View(userViewModels);
        }

        public async Task<ActionResult> Create()
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            ViewBag.RoleID = await RolesSelectListAsync(db);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(UserViewModel userVM)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Avval hamma tekshiruvlar, keyin bitta tranzaksiyada yozish (CORE-B03): avval USER saqlanib,
            // keyin rol yoki rasm xatosida rolsiz foydalanuvchi qolardi va qayta urinish "nom band" berardi.
            bool invalid = false;
            var existingUser = await db.USERS.FirstOrDefaultAsync(u => u.Uname == userVM.UName && u.IsDeleted != true);
            if (existingUser != null)
            {
                invalid = true;
                ModelState.AddModelError("UName", "Bu foydalanuvchi nomi allaqachon mavjud.");
            }
            if (string.IsNullOrWhiteSpace(userVM.Password))
            {
                invalid = true;
                ModelState.AddModelError("Password", "Kalit so'zni kiriting");
            }

            ROLE selectedRole = null;
            if (userVM.RoleID > 0)
            {
                selectedRole = await db.ROLES.FindAsync(userVM.RoleID);
                if (selectedRole == null || IsDeveloperRole(selectedRole))
                {
                    invalid = true;
                    ModelState.AddModelError("", "Birinchi martada 'Developer' role biriktirish mumkin emas.");
                }
            }

            byte[] avatar = null;
            var photo = Request.Files["userPhotoUpload"];
            if (photo != null && photo.ContentLength > 0)
            {
                if (photo.InputStream.Length < userPhotoMaxLength)
                {
                    avatar = UploadHelper.ReadAll(photo);
                }
                else
                {
                    invalid = true;
                    ModelState.AddModelError("", "Suratni yuklab bo'lmadi, u 2 MB dan ortiq. Qayta urinib ko'ring va muammo davom etsa, tizim administratoriga murojaat qiling.");
                }
            }

            if (invalid)
            {
                ViewBag.RoleID = await RolesSelectListAsync(db, userVM.RoleID);
                return View(userVM);
            }

            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    USER user = new USER();
                    user.Uname = userVM.UName;
                    string keyNew;
                    user.Password = PasswordHasher.Hash(userVM.Password, out keyNew);
                    user.HashCode = keyNew;
                    user.FullName = userVM.FullName;
                    user.Email = userVM.Email;
                    user.IsActive = userVM.IsActive;
                    user.IsDeleted = false;
                    user.CompanyID = Convert.ToInt32(ConfigurationManager.AppSettings["companyID"]);

                    db.USERS.Add(user);
                    await db.SaveChangesAsync();

                    // Rol raw SQL bilan (USERROLES EF'da faqat o'qish uchun)
                    if (selectedRole != null)
                    {
                        await db.Database.ExecuteSqlCommandAsync(
                            "INSERT INTO USERROLES (UserID, RoleID) VALUES (@p0, @p1)",
                            user.ID, selectedRole.ID);
                    }

                    if (avatar != null)
                    {
                        db.USERIMAGES.Add(new USERIMAGE { UserID = user.ID, Image = avatar });
                        await db.SaveChangesAsync();
                    }

                    tx.Commit();

                    LogHelper.LogToDatabase(User.Identity.Name, "Foydalanuvchilar", $"{user.Uname} - Foydalanuvchini tizimga qo'shdi");
                    TempData["Success"] = "Foydalanuvchi muvaffaqiyatli qo'shildi.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    System.Diagnostics.Trace.TraceError("[User.Create] " + ex);
                    ModelState.AddModelError("", "O'zgarishlarni saqlab bo'lmadi. Qayta urinib ko'ring va muammo davom etsa, tizim administratoriga murojaat qiling.");
                }
            }

            ViewBag.RoleID = await RolesSelectListAsync(db, userVM.RoleID);
            return View(userVM);
        }

        public async Task<ActionResult> Edit(int? ID)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ID == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            USER user = await db.USERS
                .Include(u => u.ROLES)
                .FirstOrDefaultAsync(u => u.ID == ID);

            if (user == null || !CanManageTarget(user))
            {
                return HttpNotFound();
            }

            UserViewModel userviewmodel = new UserViewModel
            {
                ID = user.ID,
                UName = user.Uname,
                Email = user.Email,
                FullName = user.FullName,
                IsActive = user.IsActive,
                RoleID = user.ROLES.FirstOrDefault()?.ID ?? 0
            };

            USERIMAGE userimage = await db.USERIMAGES.FirstOrDefaultAsync(ui => ui.UserID == user.ID);
            if (userimage != null)
            {
                ViewBag.Base64String = "data:image/png;base64," + Convert.ToBase64String(userimage.Image, 0, userimage.Image.Length);
            }

            ViewBag.RoleID = await RolesSelectListAsync(db, userviewmodel.RoleID);
            return View(userviewmodel);
        }

        [HttpPost, ActionName("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(int? ID, UserViewModel uvm)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ID == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            var userToUpdate = await db.USERS.Include(u => u.ROLES).FirstOrDefaultAsync(u => u.ID == ID);
            if (userToUpdate == null || !CanManageTarget(userToUpdate))
            {
                return HttpNotFound();
            }
            string oldUname = userToUpdate.Uname;

            // Tekshirish agar username already mavjud (excluding current user)
            var existingUser = await db.USERS.FirstOrDefaultAsync(u => u.Uname == uvm.UName && u.ID != ID && u.IsDeleted != true);
            if (existingUser != null)
            {
                ModelState.AddModelError("UName", "Bu foydalanuvchi nomi allaqachon mavjud.");
                ViewBag.RoleID = await RolesSelectListAsync(db, uvm.RoleID);
                USERIMAGE userimage = await db.USERIMAGES.FirstOrDefaultAsync(ui => ui.UserID == ID);
                if (userimage != null)
                {
                    ViewBag.Base64String = "data:image/png;base64," + Convert.ToBase64String(userimage.Image, 0, userimage.Image.Length);
                }
                return View(uvm);
            }

            try
            {
                // Yangilash password faqat agar provided
                if (!string.IsNullOrWhiteSpace(uvm.Password))
                {
                    string keyNew;
                    userToUpdate.Password = PasswordHasher.Hash(uvm.Password, out keyNew);
                    userToUpdate.HashCode = keyNew;
                }

                userToUpdate.Uname = uvm.UName;
                userToUpdate.FullName = uvm.FullName;
                userToUpdate.Email = uvm.Email;
                userToUpdate.IsActive = uvm.IsActive;

                // Yangilash role foydalanib raw SQL (USERROLES is read-faqat in EF)
                var userWithRoles = await db.USERS.Include(u => u.ROLES).FirstOrDefaultAsync(u => u.ID == ID);
                if (userWithRoles != null)
                {
                    var currentRole = userWithRoles.ROLES.FirstOrDefault();
                    if (currentRole == null || currentRole.ID != uvm.RoleID)
                    {
                        if (uvm.RoleID > 0)
                        {
                            var selectedRole = await db.ROLES.FindAsync(uvm.RoleID);
                            if (selectedRole != null && (!IsDeveloperRole(selectedRole) || RoleHelper.IsDeveloper(User.Identity.Name)))
                            {
                                // O'chirish existing user roles foydalanib raw SQL
                                if (currentRole != null)
                                {
                                    await db.Database.ExecuteSqlCommandAsync(
                                        "DELETE FROM USERROLES WHERE UserID = @p0 AND RoleID = @p1",
                                        ID, currentRole.ID);
                                }

                                // Insert new user role foydalanib raw SQL
                                await db.Database.ExecuteSqlCommandAsync(
                                    "INSERT INTO USERROLES (UserID, RoleID) VALUES (@p0, @p1)",
                                    ID, uvm.RoleID);
                            }
                        }
                        else if (currentRole != null)
                        {
                            // O'chirish role agar RoleID is 0 yoki invalid
                            await db.Database.ExecuteSqlCommandAsync(
                                "DELETE FROM USERROLES WHERE UserID = @p0 AND RoleID = @p1",
                                ID, currentRole.ID);
                        }
                    }
                }

                // Handle image upload
                if (Request.Files["userPhotoUpload"] != null && Request.Files["userPhotoUpload"].ContentLength > 0)
                {
                    var file = Request.Files["userPhotoUpload"];
                    if (file.InputStream.Length < userPhotoMaxLength)
                    {
                        byte[] avatar = UploadHelper.ReadAll(file);

                        USERIMAGE uImage = await db.USERIMAGES.FirstOrDefaultAsync(ui => ui.UserID == ID);
                        if (uImage == null)
                        {
                            USERIMAGE uImageNew = new USERIMAGE
                            {
                                UserID = (int)ID,
                                Image = avatar
                            };
                            db.USERIMAGES.Add(uImageNew);
                            LogHelper.LogToDatabase(User.Identity.Name, "Foydalanuvchilar", $"{userToUpdate.Uname} uchun Foydalanuvchini-Rasmini o'zgartirdi");
                        }
                        else
                        {
                            uImage.Image = avatar;
                            db.Entry(uImage).State = EntityState.Modified;
                            LogHelper.LogToDatabase(User.Identity.Name, "Foydalanuvchilar", $"{userToUpdate.Uname} Foydalanuvchini-Rasmini tahrirladi");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", "Suratni yuklab bo'lmadi, u 2 MB dan ortiq. Qayta urinib ko'ring va muammo davom etsa, tizim administratoriga murojaat qiling.");
                        ViewBag.RoleID = await RolesSelectListAsync(db, uvm.RoleID);
                        USERIMAGE userimage = await db.USERIMAGES.FirstOrDefaultAsync(ui => ui.UserID == ID);
                        if (userimage != null)
                        {
                            ViewBag.Base64String = "data:image/png;base64," + Convert.ToBase64String(userimage.Image, 0, userimage.Image.Length);
                        }
                        return View(uvm);
                    }
                }

                await db.SaveChangesAsync();

                // Rol/faollik o'zgargan bo'lishi mumkin — ruxsat keshini darhol yangilash
                RoleHelper.ClearUserCache(oldUname);
                RoleHelper.ClearUserCache(userToUpdate.Uname);

                LogHelper.LogToDatabase(User.Identity.Name, "Foydalanuvchilar", $"{userToUpdate.Uname} - Foydalanuvchini ma'lumotlarini tahrirladi");

                TempData["Success"] = "Foydalanuvchi ma'lumotlari muvaffaqiyatli yangilandi.";
                return RedirectToAction("Index");
            }
            catch (RetryLimitExceededException)
            {
                ModelState.AddModelError("", "O'zgarishlarni saqlab bo'lmadi. Qayta urinib ko'ring va muammo davom etsa, tizim administratoriga murojaat qiling.");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Xatolik yuz berdi: " + ex.Message);
            }

            ViewBag.RoleID = await RolesSelectListAsync(db, uvm.RoleID);
            USERIMAGE userimage2 = await db.USERIMAGES.FirstOrDefaultAsync(ui => ui.UserID == ID);
            if (userimage2 != null)
            {
                ViewBag.Base64String = "data:image/png;base64," + Convert.ToBase64String(userimage2.Image, 0, userimage2.Image.Length);
            }
            return View(uvm);
        }

        public async Task<ActionResult> Details(int? ID)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ID == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            USER user = await db.USERS
                .Include(u => u.ROLES)
                .FirstOrDefaultAsync(u => u.ID == ID);

            if (user == null || !CanManageTarget(user))
            {
                return HttpNotFound();
            }

            // Butun log tarixini emas, faqat oxirgi 100 tasini bazadan olamiz
            var recentLogs = await db.USERLOGS.AsNoTracking()
                .Where(l => l.UserID == user.ID)
                .OrderByDescending(l => l.DateTime)
                .Take(100)
                .ToListAsync();

            UserViewModel userviewmodel = new UserViewModel
            {
                ID = user.ID,
                UName = user.Uname,
                Email = user.Email,
                FullName = user.FullName,
                IsActive = user.IsActive,
                Role = user.ROLES.FirstOrDefault()?.RName ?? "Rol yo'q",
                logs = recentLogs
            };

            USERIMAGE userimage = await db.USERIMAGES.FirstOrDefaultAsync(ui => ui.UserID == user.ID);
            if (userimage != null)
            {
                ViewBag.Base64String = "data:image/png;base64," + Convert.ToBase64String(userimage.Image, 0, userimage.Image.Length);
            }

            return View(userviewmodel);
        }

        public async Task<ActionResult> Delete(int? ID)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ID == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            USER user = await db.USERS
                .Include(u => u.ROLES)
                .FirstOrDefaultAsync(u => u.ID == ID);

            if (user == null || !CanManageTarget(user))
            {
                return HttpNotFound();
            }

            UserViewModel userviewmodel = new UserViewModel
            {
                ID = user.ID,
                UName = user.Uname,
                Email = user.Email,
                FullName = user.FullName,
                IsActive = user.IsActive,
                Role = user.ROLES.FirstOrDefault()?.RName ?? "Rol yo'q"
            };

            USERIMAGE userimage = await db.USERIMAGES.FirstOrDefaultAsync(ui => ui.UserID == user.ID);
            if (userimage != null)
            {
                ViewBag.Base64String = "data:image/png;base64," + Convert.ToBase64String(userimage.Image, 0, userimage.Image.Length);
            }

            return View(userviewmodel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int? ID, FormCollection collection)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ID == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            USER user = await db.USERS.Include(u => u.ROLES).FirstOrDefaultAsync(u => u.ID == ID);
            if (user == null || !CanManageTarget(user))
            {
                return HttpNotFound();
            }

            try
            {
                user.IsDeleted = true;
                await SoftDeleteReferenceService.AfterUserSoftDeletedAsync(db, user.ID);
                await db.SaveChangesAsync();
                RoleHelper.ClearUserCache(user.Uname);

                LogHelper.LogToDatabase(User.Identity.Name, "Foydalanuvchilar", $"{user.Uname} - Foydalanuvchini tizimdan o'chirdi");

                TempData["Success"] = "Foydalanuvchi muvaffaqiyatli o'chirildi.";
                return RedirectToAction("Index");
            }
            catch (RetryLimitExceededException)
            {
                ModelState.AddModelError("", "O'zgarishlarni saqlab bo'lmadi. Qayta urinib ko'ring va muammo davom etsa, tizim administratoriga murojaat qiling.");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Xatolik yuz berdi: " + ex.Message);
            }

            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        // Rollar ro'yxati 7 joyda bir xil qurilardi (CORE dublikat: Roles SelectList)
        private static async Task<SelectList> RolesSelectListAsync(DBTHSNEntities db, object selected = null)
        {
            var roles = await db.ROLES.AsNoTracking()
                .Where(r => r.IsDeleted != true)
                .OrderBy(r => r.RName)
                .Select(r => new { r.ID, r.RName })
                .ToListAsync();
            return new SelectList(roles, "ID", "RName", selected);
        }
    }
}
