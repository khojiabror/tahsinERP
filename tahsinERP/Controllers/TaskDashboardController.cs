using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.Models;
using tahsinERP.Services.Tasks;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    [Permission]
    public class TaskDashboardController : BaseController
    {
        /// <summary>Bitta GROUP BY qatori: shu kombinatsiyadagi topshiriqlar soni.</summary>
        private sealed class TaskGroup
        {
            public string StatusCode { get; set; }
            public string StatusName { get; set; }
            public string PriorityCode { get; set; }
            public string PriorityName { get; set; }
            public string ProjectName { get; set; }
            public int? AssigneeUserID { get; set; }
            public bool IsDone { get; set; }
            public bool IsOverdue { get; set; }
            public bool IsOverdueByDate { get; set; }
            public bool IsOnTime { get; set; }
            public bool IsRecent { get; set; }
            public int Count { get; set; }
        }

        // Olish so'rovi: TaskDashboard
        public async Task<ActionResult> Index(int? userId, int? productID)
        {
            bool isAdminOrDeveloper = TaskRules.IsAdminOrDeveloper(CurrentUserName);
            int currentUserId = CurrentUserId;
            if (!isAdminOrDeveloper)
            {
                // Oddiy user - faqat o'z tasklarini ko'rsatish
                userId = currentUserId;
            }
            // Admin/Developer uchun userId null bo'lsa, barcha tasklar ko'rsatiladi

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var viewModel = new TaskDashboardViewModel();

                var query = db.APP_TASKS.AsNoTracking().Where(t => t.IsDeleted == false);
                if (userId.HasValue)
                {
                    query = query.Where(t => t.AssigneeUserID == userId.Value);
                }

                // Avval barcha topshiriqlar (Include bilan) xotiraga yuklanib, keyin hisoblanardi va
                // loyiha nomi lazy-load, har xodim uchun alohida USERS so'rovi ketardi (TSK-P03/P04/P05).
                // Endi bitta GROUP BY: natija qatorlari soni topshiriqlar soniga emas, kombinatsiyalarga bog'liq.
                var now = DateTime.Now;
                var today = now.Date;
                var sevenDaysAgo = now.AddDays(-7);
                var doneIds = TaskRules.DoneStatusIds();
                var closedIds = TaskRules.ClosedStatusIds();

                var groups = await query
                    .Select(t => new
                    {
                        StatusCode = t.APP_TASK_STATUS_REF.Code,
                        StatusName = t.APP_TASK_STATUS_REF.Name,
                        PriorityCode = t.APP_TASK_PRIORITY_REF.Code,
                        PriorityName = t.APP_TASK_PRIORITY_REF.Name,
                        ProjectName = t.APP_TASK_PROJECTS.Name,
                        t.AssigneeUserID,
                        t.DueDate,
                        t.UpdatedDT,
                        t.CreatedDT,
                        IsDone = doneIds.Contains(t.StatusID),
                        IsClosed = closedIds.Contains(t.StatusID)
                    })
                    .GroupBy(t => new
                    {
                        t.StatusCode,
                        t.StatusName,
                        t.PriorityCode,
                        t.PriorityName,
                        t.ProjectName,
                        t.AssigneeUserID,
                        t.IsDone,
                        IsOverdue = t.DueDate.HasValue && t.DueDate.Value < now && !t.IsClosed,
                        IsOverdueByDate = t.DueDate.HasValue && t.DueDate.Value < today && !t.IsClosed,
                        IsOnTime = t.IsDone && t.DueDate.HasValue && t.UpdatedDT.HasValue &&
                                   DbFunctions.TruncateTime(t.UpdatedDT) <= DbFunctions.TruncateTime(t.DueDate),
                        IsRecent = t.CreatedDT >= sevenDaysAgo
                    })
                    .Select(g => new TaskGroup
                    {
                        StatusCode = g.Key.StatusCode,
                        StatusName = g.Key.StatusName,
                        PriorityCode = g.Key.PriorityCode,
                        PriorityName = g.Key.PriorityName,
                        ProjectName = g.Key.ProjectName,
                        AssigneeUserID = g.Key.AssigneeUserID,
                        IsDone = g.Key.IsDone,
                        IsOverdue = g.Key.IsOverdue,
                        IsOverdueByDate = g.Key.IsOverdueByDate,
                        IsOnTime = g.Key.IsOnTime,
                        IsRecent = g.Key.IsRecent,
                        Count = g.Count()
                    })
                    .ToListAsync();

                int total = groups.Sum(g => g.Count);

                // holat Statistics (Pie Chart ma'lumot)
                viewModel.StatusStatistics = groups
                    .GroupBy(g => g.StatusCode ?? "UNKNOWN")
                    .Select(s => new TaskStatusStatViewModel
                    {
                        StatusCode = s.Key,
                        StatusName = s.First().StatusName ?? "Noma'lum",
                        Count = s.Sum(g => g.Count),
                        Percentage = (decimal)s.Sum(g => g.Count) * 100 / (total > 0 ? total : 1)
                    })
                    .OrderBy(s => s.StatusCode)
                    .ToList();

                // Priority Statistics
                viewModel.PriorityStatistics = groups
                    .GroupBy(g => g.PriorityCode ?? "UNKNOWN")
                    .Select(p => new TaskPriorityStatViewModel
                    {
                        PriorityCode = p.Key,
                        PriorityName = p.First().PriorityName ?? "Noma'lum",
                        TotalCount = p.Sum(g => g.Count),
                        DoneCount = p.Where(g => g.IsDone).Sum(g => g.Count),
                        InProgressCount = p.Where(g => g.StatusCode == "IN_PROGRESS").Sum(g => g.Count),
                        TodoCount = p.Where(g => g.StatusCode == "TODO").Sum(g => g.Count)
                    })
                    .OrderBy(p => p.PriorityCode)
                    .ToList();

                // Umumiy (yoki tanlangan foydalanuvchi) ko'rsatkichlari — query allaqachon userId bo'yicha filtrlangan
                viewModel.UserTaskCount = total;
                viewModel.UserDoneCount = groups.Where(g => g.IsDone).Sum(g => g.Count);
                viewModel.UserInProgressCount = groups.Where(g => g.StatusCode == "IN_PROGRESS").Sum(g => g.Count);
                viewModel.UserTodoCount = groups.Where(g => g.StatusCode == "TODO").Sum(g => g.Count);
                viewModel.OverdueCount = groups.Where(g => g.IsOverdue).Sum(g => g.Count);
                viewModel.RecentTasksCount = groups.Where(g => g.IsRecent).Sum(g => g.Count);

                // Overdue tasks by project (muddatidan o'tgan tasklar)
                viewModel.OverdueTasksStatistics = groups
                    .Where(g => g.IsOverdue)
                    .GroupBy(g => g.ProjectName ?? "Loyiha yo'q")
                    .Select(p => new TaskOverdueStatViewModel
                    {
                        ProjectName = p.Key,
                        OverdueCount = p.Sum(g => g.Count)
                    })
                    .OrderByDescending(s => s.OverdueCount)
                    .ToList();

                // Employee Rating Statistics (Xodimlar rating) — ismlar bitta so'rovda
                var assigneeIds = groups.Where(g => g.AssigneeUserID.HasValue).Select(g => g.AssigneeUserID).Distinct().ToList();
                var names = await TaskLookups.UserNamesAsync(db, assigneeIds);

                var employeeRatings = new List<EmployeeRatingViewModel>();
                foreach (var assigneeId in assigneeIds)
                {
                    string fullName;
                    if (!names.TryGetValue(assigneeId.Value, out fullName))
                        continue;

                    var userGroups = groups.Where(g => g.AssigneeUserID == assigneeId).ToList();

                    // Muddatida bajarilgan (UpdatedDT <= DueDate va holat = bajarildi)
                    int onTimeCompleted = userGroups.Where(g => g.IsOnTime).Sum(g => g.Count);
                    // Muddati o'tgan va bajarilmagan
                    int overdueNotCompleted = userGroups.Where(g => g.IsOverdueByDate).Sum(g => g.Count);

                    employeeRatings.Add(new EmployeeRatingViewModel
                    {
                        UserID = assigneeId.Value,
                        FullName = fullName ?? "Noma'lum",
                        OnTimeCompletedCount = onTimeCompleted,
                        OverdueNotCompletedCount = overdueNotCompleted,
                        // Muddatida bajarilganlar bo'lsa — ular soni; aks holda kamroq kechikkan yuqorida
                        RatingScore = onTimeCompleted > 0 ? onTimeCompleted : -overdueNotCompleted
                    });
                }

                viewModel.EmployeeRatings = employeeRatings.Any(r => r.OnTimeCompletedCount > 0)
                    ? employeeRatings.OrderByDescending(r => r.OnTimeCompletedCount).ThenBy(r => r.OverdueNotCompletedCount).ToList()
                    : employeeRatings.OrderBy(r => r.OverdueNotCompletedCount).ToList();

                if (userId.HasValue)
                {
                    viewModel.UserName = await db.USERS
                        .Where(u => u.ID == userId.Value)
                        .Select(u => u.FullName ?? u.Uname)
                        .FirstOrDefaultAsync();
                }
                else
                {
                    viewModel.UserName = "Barcha foydalanuvchilar";
                }

                // User dropdown: admin — hammasi, oddiy user — faqat o'zi
                ViewBag.UserID = isAdminOrDeveloper
                    ? await TaskLookups.UserListAsync(db, userId)
                    : await TaskLookups.UserListAsync(db, currentUserId, onlyUserId: currentUserId);

                ViewBag.SelectedUserID = userId;
                ViewBag.CurrentUserID = currentUserId;
                ViewBag.IsAdminOrDeveloper = isAdminOrDeveloper;

                return View(viewModel);
            }
        }
    }
}
