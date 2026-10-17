using System;
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
    public class TaskKanbanController : BaseController
    {
        /// <summary>Yakunlangan topshiriqlar doskada shuncha kun ko'rinadi (showClosed=true bo'lsa — hammasi).</summary>
        private const int ClosedVisibleDays = 30;

        // Olish so'rovi: TaskKanban
        [Permission]
        public async Task<ActionResult> Index(string viewType = "project", bool showClosed = false)
        {
            bool isAdminOrDeveloper = TaskRules.IsAdminOrDeveloper(CurrentUserName);
            int currentUserId = CurrentUserId;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.APP_TASKS.AsNoTracking().Where(t => t.IsDeleted == false);

                // Oddiy user uchun faqat o'z tasklarini ko'rsatish
                if (!isAdminOrDeveloper)
                {
                    query = query.Where(t => t.AssigneeUserID == currentUserId);
                }

                // Avval yillar davomida yopilgan topshiriqlar ham yuklanardi — doska cheksiz o'sardi (TSK-P02)
                if (!showClosed)
                {
                    var closedSince = DateTime.Now.AddDays(-ClosedVisibleDays);
                    query = query.Where(t =>
                        !(t.APP_TASK_STATUS_REF.Code == TaskRules.DoneCode ||
                          t.APP_TASK_STATUS_REF.Code == TaskRules.CancelledCode ||
                          t.APP_TASK_STATUS_REF.Name.Contains("bajarildi")) ||
                        (t.UpdatedDT ?? t.CreatedDT) >= closedSince);
                }

                // SQL'da proyeksiya: faqat kartochka maydonlari, tavsif 101 belgigacha (view 100 tasini ko'rsatadi)
                var tasks = await query
                    .Select(t => new KanbanTaskViewModel
                    {
                        ID = t.ID,
                        Title = t.Title,
                        Description = t.Description.Substring(0, 101),
                        KeyCode = t.KeyCode,
                        StatusID = t.StatusID,
                        StatusName = t.APP_TASK_STATUS_REF.Name,
                        PriorityID = t.PriorityID,
                        PriorityName = t.APP_TASK_PRIORITY_REF.Name,
                        AssigneeUserID = t.AssigneeUserID,
                        DueDate = t.DueDate,
                        CreatedDT = t.CreatedDT,
                        ProjectID = t.ProjectID,
                        ProjectName = t.APP_TASK_PROJECTS.Name
                    })
                    .ToListAsync();

                // Ismlar bitta so'rovda (avval har kartochkaga UserHelper.GetFullName — TSK-P01)
                var names = await TaskLookups.UserNamesAsync(db, tasks.Select(t => t.AssigneeUserID));
                foreach (var t in tasks)
                {
                    t.AssigneeFullName = TaskLookups.NameOf(names, t.AssigneeUserID);
                }

                var viewModel = new TaskKanbanViewModel
                {
                    ViewType = viewType,
                    IsAdminOrDeveloper = isAdminOrDeveloper
                };

                if (viewType == "project")
                {
                    var byProject = tasks.ToLookup(t => t.ProjectID);
                    foreach (var project in TaskLookups.Projects())
                    {
                        var projectTasks = byProject[project.ID].ToList();
                        if (projectTasks.Any() || isAdminOrDeveloper)
                        {
                            viewModel.ProjectBoards.Add(new KanbanBoardViewModel
                            {
                                ID = project.ID.ToString(),
                                Title = project.Name,
                                Tasks = projectTasks
                            });
                        }
                    }

                    // Tasks without project
                    var noProjectTasks = byProject[null].ToList();
                    if (noProjectTasks.Any() || isAdminOrDeveloper)
                    {
                        viewModel.ProjectBoards.Add(new KanbanBoardViewModel
                        {
                            ID = "no-project",
                            Title = "Loyiha yo'q",
                            Tasks = noProjectTasks
                        });
                    }
                }
                else // viewType == "status"
                {
                    var byStatus = tasks.ToLookup(t => t.StatusID);
                    foreach (var status in TaskLookups.Statuses())
                    {
                        viewModel.StatusBoards.Add(new KanbanBoardViewModel
                        {
                            ID = status.ID.ToString(),
                            Title = status.Name,
                            Tasks = byStatus[status.ID].ToList()
                        });
                    }
                }

                ViewBag.ViewType = viewType;
                ViewBag.ShowClosed = showClosed;
                ViewBag.IsAdminOrDeveloper = isAdminOrDeveloper;

                return View(viewModel);
            }
        }

        // POST so'rovi: TaskKanban/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> UpdateStatus(long taskId, int newStatusId)
        {
            // Avval faqat egalik tekshirilardi (TSK-B03). Status o'zgartirish huquqi — Tasks tahrirlash
            // yoki status o'zgartirish sahifasi (TaskChangeStatus) bilan bir xil.
            if (!RoleHelper.IsChangePermitted(CurrentUserName, "Tasks", "Edit") &&
                !RoleHelper.IsViewPermitted(CurrentUserName, "TaskChangeStatus", "ChangeStatus"))
            {
                Response.StatusCode = 403;
                return Json(new { success = false, message = "Ruxsat yo'q" });
            }

            bool isAdminOrDeveloper = TaskRules.IsAdminOrDeveloper(CurrentUserName);
            int currentUserId = CurrentUserId;

            if (!TaskLookups.Statuses().Any(s => s.ID == newStatusId))
            {
                return Json(new { success = false, message = "Noto'g'ri status" });
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var task = await db.APP_TASKS.FindAsync(taskId);
                if (task == null || task.IsDeleted)
                {
                    return Json(new { success = false, message = "Task topilmadi" });
                }

                // Oddiy user boshqalarning tasklarini o'zgartirmasligi kerak
                if (!TaskRules.CanAccess(task.AssigneeUserID, currentUserId, isAdminOrDeveloper))
                {
                    return Json(new { success = false, message = "Siz faqat o'zingizga tegishli tasklarni o'zgartirishingiz mumkin" });
                }

                try
                {
                    task.StatusID = newStatusId;
                    task.UpdatedDT = DateTime.Now;
                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(CurrentUserName, "TaskKanbanController", $"Task {task.KeyCode} status o'zgartirildi: {newStatusId}");

                    return Json(new { success = true, message = "Status muvaffaqiyatli o'zgartirildi" });
                }
                catch (Exception ex)
                {
                    return JsonError(ex, "Statusni o'zgartirib bo'lmadi.");
                }
            }
        }
    }
}
