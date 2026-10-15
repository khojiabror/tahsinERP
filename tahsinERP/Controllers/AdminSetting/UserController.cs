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

            var userViewModels = users.Select(u => new UserViewModel
            {
                ID = u.ID,
                UName = u.Uname,
                FullName = u.FullName,
                Email = u.Email,
                IsActive = u.IsActive,
                Role = u.ROLES.FirstOrDefault()?.RName ?? "Rol yo'q"
            }).ToList();

            ViewBag.RoleList = new SelectList(await db.ROLES.Where(r => r.IsDeleted != true).OrderBy(r => r.RName).ToListAsync(), "ID", "RName", roleID);
            ViewBag.RoleID = roleID;

            return View(userViewModels);
        }

        public async Task<ActionResult> Create()
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            ViewBag.RoleID = new SelectList(await db.ROLES.Where(r => r.IsDeleted != true).OrderBy(r => r.RName).ToListAsync(), "ID", "RName");
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

            try
            {
                // Tekshirish agar username already mavjud
                var existingUser = await db.USERS.FirstOrDefaultAsync(u => u.Uname == userVM.UName && u.IsDeleted != true);
                if (existingUser != null)
                {
                    ModelState.AddModelError("UName", "Bu foydalanuvchi nomi allaqachon mavjud.");
                    ViewBag.RoleID = new SelectList(await db.ROLES.Where(r => r.IsDeleted != true).OrderBy(r => r.RName).ToListAsync(), "ID", "RName", userVM.RoleID);
                    return View(userVM);
                }

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

                LogHelper.LogToDatabase(User.Identity.Name, "Foydalanuvchilar", $"{user.Uname} - Foydalanuvchini tizimga qo'shdi");

                // Assign role foydalanib raw SQL (USERROLES is read-faqat in EF)
                if (userVM.RoleID > 0)
                {
                    var selectedRole = await db.ROLES.FindAsync(userVM.RoleID);
                    if (selectedRole != null && (selectedRole.RName != "Developer" || selectedRole.ID != 1))
                    {
                        // Insert user role foydalanib raw SQL
                        await db.Database.ExecuteSqlCommandAsync(
                            "INSERT INTO USERROLES (UserID, RoleID) VALUES (@p0, @p1)",
                            user.ID, userVM.RoleID);
                    }
                    else
                    {
                        ModelState.AddModelError("", "Birinchi martada 'Developer' role biriktirish mumkin emas.");
                        ViewBag.RoleID = new SelectList(await db.ROLES.Where(r => r.IsDeleted != true).OrderBy(r => r.RName).ToListAsync(), "ID", "RName", userVM.RoleID);
                        return View(userVM);
                    }
                }

                // Handle image upload
                if (Request.Files["userPhotoUpload"] != null && Request.Files["userPhotoUpload"].ContentLength > 0)
                {
                    var file = Request.Files["userPhotoUpload"];
                    if (file.InputStream.Length < userPhotoMaxLength)
                    {
                        byte[] avatar = new byte[file.InputStream.Length];
                        file.InputStream.Read(avatar, 0, avatar.Length);

                        USERIMAGE userImage = new USERIMAGE
                        {
                            UserID = user.ID,
                            Image = avatar
                        };

                        db.USERIMAGES.Add(userImage);
                        await db.SaveChangesAsync();

                        LogHelper.LogToDatabase(User.Identity.Name, "Foydalanuvchilar", $"{user.Uname} uchun Foydalanuvchini-Rasmini biriktirdi");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Suratni yuklab bo'lmadi, u 2 MB dan ortiq. Qayta urinib ko'ring va muammo davom etsa, tizim administratoriga murojaat qiling.");
                        ViewBag.RoleID = new SelectList(await db.ROLES.Where(r => r.IsDeleted != true).OrderBy(r => r.RName).ToListAsync(), "ID", "RName", userVM.RoleID);
                        return View(userVM);
                    }
                }

                TempData["Success"] = "Foydalanuvchi muvaffaqiyatli qo'shildi.";
                return RedirectToAction("Index");
            }
            catch
            {
                ModelState.AddModelError("", "O'zgarishlarni saqlab bo'lmadi. Qayta urinib ko'ring va muammo davom etsa, tizim administratoriga murojaat qiling.");
            }

            ViewBag.RoleID = new SelectList(await db.ROLES.Where(r => r.IsDeleted != true).OrderBy(r => r.RName).ToListAsync(), "ID", "RName", userVM.RoleID);
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

            if (user == null)
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

            ViewBag.RoleID = new SelectList(await db.ROLES.Where(r => r.IsDeleted != true).OrderBy(r => r.RName).ToListAsync(), "ID", "RName", userviewmodel.RoleID);
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

            var userToUpdate = await db.USERS.FindAsync(ID);
            if (userToUpdate == null)
            {
                return HttpNotFound();
            }

            // Tekshirish agar username already mavjud (excluding current user)
            var existingUser = await db.USERS.FirstOrDefaultAsync(u => u.Uname == uvm.UName && u.ID != ID && u.IsDeleted != true);
            if (existingUser != null)
            {
                ModelState.AddModelError("UName", "Bu foydalanuvchi nomi allaqachon mavjud.");
                ViewBag.RoleID = new SelectList(await db.ROLES.Where(r => r.IsDeleted != true).OrderBy(r => r.RName).ToListAsync(), "ID", "RName", uvm.RoleID);
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
                            if (selectedRole != null && (selectedRole.RName != "Developer" || selectedRole.ID != 1))
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
                        byte[] avatar = new byte[file.InputStream.Length];
                        file.InputStream.Read(avatar, 0, avatar.Length);

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
                        ViewBag.RoleID = new SelectList(await db.ROLES.Where(r => r.IsDeleted != true).OrderBy(r => r.RName).ToListAsync(), "ID", "RName", uvm.RoleID);
                        USERIMAGE userimage = await db.USERIMAGES.FirstOrDefaultAsync(ui => ui.UserID == ID);
                        if (userimage != null)
                        {
                            ViewBag.Base64String = "data:image/png;base64," + Convert.ToBase64String(userimage.Image, 0, userimage.Image.Length);
                        }
                        return View(uvm);
                    }
                }

                await db.SaveChangesAsync();

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

            ViewBag.RoleID = new SelectList(await db.ROLES.Where(r => r.IsDeleted != true).OrderBy(r => r.RName).ToListAsync(), "ID", "RName", uvm.RoleID);
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
                .Include(u => u.USERLOGS)
                .FirstOrDefaultAsync(u => u.ID == ID);

            if (user == null)
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
                Role = user.ROLES.FirstOrDefault()?.RName ?? "Rol yo'q",
                logs = user.USERLOGS.OrderByDescending(l => l.DateTime).Take(100).ToList()
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

            if (user == null)
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

            USER user = await db.USERS.FindAsync(ID);
            if (user == null)
            {
                return HttpNotFound();
            }

            try
            {
                user.IsDeleted = true;
                await SoftDeleteReferenceService.AfterUserSoftDeletedAsync(db, user.ID);
                await db.SaveChangesAsync();

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
    }
}
