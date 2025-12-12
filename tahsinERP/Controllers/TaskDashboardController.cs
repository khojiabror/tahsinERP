using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    public class TaskDashboardController : Controller
    {
        // GET: TaskDashboard
        public async Task<ActionResult> Index(int? userId)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Oddiy user uchun faqat o'z tasklarini ko'rsatish
            var userRoles = RoleHelper.GetUserRoles(User.Identity.Name);
            bool isAdminOrDeveloper = userRoles != null && userRoles.Any(r => 
                r.Equals("Admin", StringComparison.OrdinalIgnoreCase) || 
                r.Equals("Developer", StringComparison.OrdinalIgnoreCase));
            
            int currentUserId = UserHelper.GetUserId(User.Identity.Name);
            if (!isAdminOrDeveloper)
            {
                // Oddiy user - faqat o'z tasklarini ko'rsatish
                userId = currentUserId;
            }
            else
            {
                // Admin/Developer - agar userId ko'rsatilmagan bo'lsa, current user
                userId = userId ?? currentUserId;
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var viewModel = new TaskDashboardViewModel();

                // Base query for tasks
                var allTasksQuery = db.APP_TASKS
                    .Include(t => t.APP_TASK_STATUS_REF)
                    .Include(t => t.APP_TASK_PRIORITY_REF)
                    .Where(t => t.IsDeleted == false);

                // Filter by user
                if (userId.HasValue)
                {
                    allTasksQuery = allTasksQuery.Where(t => t.AssigneeUserID == userId.Value);
                }

                var allTasks = await allTasksQuery.ToListAsync();

                // Status Statistics (Pie Chart Data)
                var statusStats = allTasks
                    .GroupBy(t => t.APP_TASK_STATUS_REF?.Code ?? "UNKNOWN")
                    .Select(g => new TaskStatusStatViewModel
                    {
                        StatusCode = g.Key,
                        StatusName = g.First().APP_TASK_STATUS_REF?.Name ?? "Noma'lum",
                        Count = g.Count(),
                        Percentage = (decimal)g.Count() * 100 / (allTasks.Any() ? allTasks.Count : 1)
                    })
                    .OrderBy(s => s.StatusCode)
                    .ToList();

                viewModel.StatusStatistics = statusStats;

                // Priority Statistics
                var priorityStats = allTasks
                    .GroupBy(t => t.APP_TASK_PRIORITY_REF?.Code ?? "UNKNOWN")
                    .Select(g => new TaskPriorityStatViewModel
                    {
                        PriorityCode = g.Key,
                        PriorityName = g.First().APP_TASK_PRIORITY_REF?.Name ?? "Noma'lum",
                        TotalCount = g.Count(),
                        DoneCount = g.Count(t => t.APP_TASK_STATUS_REF?.Code == "DONE"),
                        InProgressCount = g.Count(t => t.APP_TASK_STATUS_REF?.Code == "IN_PROGRESS"),
                        TodoCount = g.Count(t => t.APP_TASK_STATUS_REF?.Code == "TODO")
                    })
                    .OrderBy(p => p.PriorityCode)
                    .ToList();

                viewModel.PriorityStatistics = priorityStats;

                // User-specific statistics (if user selected)
                if (userId.HasValue)
                {
                    var userTasks = allTasks.Where(t => t.AssigneeUserID == userId.Value).ToList();
                    viewModel.UserTaskCount = userTasks.Count;
                    viewModel.UserDoneCount = userTasks.Count(t => t.APP_TASK_STATUS_REF?.Code == "DONE");
                    viewModel.UserInProgressCount = userTasks.Count(t => t.APP_TASK_STATUS_REF?.Code == "IN_PROGRESS");
                    viewModel.UserTodoCount = userTasks.Count(t => t.APP_TASK_STATUS_REF?.Code == "TODO");
                    viewModel.UserName = await db.USERS
                        .Where(u => u.ID == userId.Value)
                        .Select(u => u.FullName ?? u.Uname)
                        .FirstOrDefaultAsync();
                }
                else
                {
                    // Overall statistics
                    viewModel.UserTaskCount = allTasks.Count;
                    viewModel.UserDoneCount = allTasks.Count(t => t.APP_TASK_STATUS_REF?.Code == "DONE");
                    viewModel.UserInProgressCount = allTasks.Count(t => t.APP_TASK_STATUS_REF?.Code == "IN_PROGRESS");
                    viewModel.UserTodoCount = allTasks.Count(t => t.APP_TASK_STATUS_REF?.Code == "TODO");
                    viewModel.UserName = "Barcha foydalanuvchilar";
                }

                // Overdue tasks count
                viewModel.OverdueCount = allTasks.Count(t => 
                    t.DueDate.HasValue && 
                    t.DueDate.Value < DateTime.Now && 
                    t.APP_TASK_STATUS_REF?.Code != "DONE");

                // Recent tasks (last 7 days)
                var sevenDaysAgo = DateTime.Now.AddDays(-7);
                viewModel.RecentTasksCount = allTasks.Count(t => t.CreatedDT >= sevenDaysAgo);

                // ViewBag for user dropdown - oddiy user uchun dropdown ko'rsatilmaydi
                if (isAdminOrDeveloper)
                {
                    ViewBag.UserID = new SelectList(
                        await db.USERS.Where(u => u.IsDeleted != true && u.IsActive)
                            .OrderBy(u => u.FullName ?? u.Uname)
                            .ToListAsync(),
                        "ID", "FullName", userId);
                }
                else
                {
                    ViewBag.UserID = new SelectList(
                        await db.USERS.Where(u => u.ID == currentUserId && u.IsDeleted != true && u.IsActive)
                            .OrderBy(u => u.FullName ?? u.Uname)
                            .ToListAsync(),
                        "ID", "FullName", userId);
                }

                ViewBag.SelectedUserID = userId;
                ViewBag.CurrentUserID = currentUserId;
                ViewBag.IsAdminOrDeveloper = isAdminOrDeveloper;

                LogHelper.LogToDatabase(User.Identity.Name, "TaskDashboardController", "Index");
                return View(viewModel);
            }
        }
    }
}
