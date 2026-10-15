using System;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    public class TaskChangeStatusController : Controller
    {
        // Olish so'rovi: TaskChangeStatus/ChangeStatus/5
        public async Task<ActionResult> ChangeStatus(long? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            // Oddiy user uchun faqat o'z tasklarini ko'rish
            var userRoles = RoleHelper.GetUserRoles(User.Identity.Name);
            bool isAdminOrDeveloper = userRoles != null && userRoles.Any(r => 
                r.Equals("Admin", StringComparison.OrdinalIgnoreCase) || 
                r.Equals("Developer", StringComparison.OrdinalIgnoreCase));
            int currentUserId = UserHelper.GetUserId(User.Identity.Name);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var task = await db.APP_TASKS
                    .Include(t => t.APP_TASK_STATUS_REF)
                    .Include(t => t.APP_TASK_PRIORITY_REF)
                    .Include(t => t.APP_TASK_PROJECTS)
                    .FirstOrDefaultAsync(t => t.ID == id && t.IsDeleted == false);

                if (task == null)
                {
                    return HttpNotFound();
                }

                // Oddiy user boshqalarning tasklarini ko'rmasligi kerak
                if (!isAdminOrDeveloper && task.AssigneeUserID != currentUserId)
                {
                    TempData["Error"] = "Siz faqat o'zingizga tegishli tasklarni ko'rishingiz mumkin";
                    return RedirectToAction("Index", "Tasks");
                }

                var viewModel = new TaskChangeStatusViewModel
                {
                    TaskID = task.ID,
                    KeyCode = task.KeyCode,
                    Title = task.Title,
                    CurrentStatusID = task.StatusID,
                    CurrentStatusName = task.APP_TASK_STATUS_REF?.Name,
                    UserComment = null
                };

                ViewBag.StatusID = new SelectList(
                    await db.APP_TASK_STATUS_REF.Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync(),
                    "ID", "Name", task.StatusID);

                return View(viewModel);
            }
        }

        // POST so'rovi: TaskChangeStatus/ChangeStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangeStatus(TaskChangeStatusViewModel viewModel)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Oddiy user uchun faqat o'z tasklarini o'zgartirish
            var userRoles = RoleHelper.GetUserRoles(User.Identity.Name);
            bool isAdminOrDeveloper = userRoles != null && userRoles.Any(r => 
                r.Equals("Admin", StringComparison.OrdinalIgnoreCase) || 
                r.Equals("Developer", StringComparison.OrdinalIgnoreCase));
            int currentUserId = UserHelper.GetUserId(User.Identity.Name);

            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var task = await db.APP_TASKS.FindAsync(viewModel.TaskID);
                    if (task == null || task.IsDeleted)
                    {
                        return HttpNotFound();
                    }

                    // Oddiy user boshqalarning tasklarini o'zgartirmasligi kerak
                    if (!isAdminOrDeveloper && task.AssigneeUserID != currentUserId)
                    {
                        TempData["Error"] = "Siz faqat o'zingizga tegishli tasklarni o'zgartirishingiz mumkin";
                        return RedirectToAction("Index", "Tasks");
                    }

                    try
                    {
                        // holat o'zgarganini tekshirish
                        bool statusChanged = task.StatusID != viewModel.StatusID;
                        
                        task.StatusID = viewModel.StatusID;
                        task.UpdatedDT = DateTime.Now;

                        // UserComment qo'shish (agar mavjud bo'lsa) - SQL orqali
                        if (!string.IsNullOrWhiteSpace(viewModel.UserComment))
                        {
                            // Olish current UserComment value foydalanib SQL
                            var currentCommentParam = new SqlParameter("@taskId", task.ID);
                            var currentComment = await db.Database.SqlQuery<string>(
                                "SELECT UserComment FROM APP_TASKS WHERE ID = @taskId", 
                                currentCommentParam).FirstOrDefaultAsync();

                            string newComment;
                            if (!string.IsNullOrEmpty(currentComment))
                            {
                                newComment = currentComment + "\n\n[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " - " + UserHelper.GetFullName(User.Identity.Name) + "]:\n" + viewModel.UserComment;
                            }
                            else
                            {
                                newComment = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " - " + UserHelper.GetFullName(User.Identity.Name) + "]:\n" + viewModel.UserComment;
                            }

                            // Yangilash UserComment foydalanib SQL
                            var commentParam = new SqlParameter("@comment", newComment ?? (object)DBNull.Value);
                            var idParam = new SqlParameter("@id", task.ID);
                            await db.Database.ExecuteSqlCommandAsync(
                                "UPDATE APP_TASKS SET UserComment = @comment WHERE ID = @id",
                                commentParam, idParam);
                        }

                        db.Entry(task).State = EntityState.Modified;
                        await db.SaveChangesAsync();

                        LogHelper.LogToDatabase(User.Identity.Name, "TaskChangeStatusController", 
                            $"Task {task.KeyCode} status o'zgartirildi: {task.APP_TASK_STATUS_REF?.Name}");

                        TempData["Success"] = "Status muvaffaqiyatli o'zgartirildi";
                        return RedirectToAction("Details", "Tasks", new { id = task.ID });
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"Xatolik: {ex.Message}");
                    }
                }
            }

            // Reload ViewBag on xatolik
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.StatusID = new SelectList(
                    await db.APP_TASK_STATUS_REF.Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync(),
                    "ID", "Name", viewModel.StatusID);
            }

            return View(viewModel);
        }
    }
}
