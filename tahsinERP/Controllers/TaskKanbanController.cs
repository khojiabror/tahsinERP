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
    public class TaskKanbanController : Controller
    {
        // Olish so'rovi: TaskKanban
        public async Task<ActionResult> Index(string viewType = "project")
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

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.APP_TASKS
                    .Include(t => t.APP_TASK_STATUS_REF)
                    .Include(t => t.APP_TASK_PRIORITY_REF)
                    .Include(t => t.APP_TASK_PROJECTS)
                    .Where(t => t.IsDeleted == false);

                // Oddiy user uchun faqat o'z tasklarini ko'rsatish
                if (!isAdminOrDeveloper)
                {
                    query = query.Where(t => t.AssigneeUserID == currentUserId);
                }

                var tasks = await query.ToListAsync();

                var viewModel = new TaskKanbanViewModel
                {
                    ViewType = viewType,
                    IsAdminOrDeveloper = isAdminOrDeveloper
                };

                if (viewType == "project")
                {
                    // Group by Project
                    var projects = await db.APP_TASK_PROJECTS
                        .Where(p => p.IsActive)
                        .OrderBy(p => p.Name)
                        .ToListAsync();

                    foreach (var project in projects)
                    {
                        var projectTasks = tasks.Where(t => t.ProjectID == project.ID).ToList();
                        if (projectTasks.Any() || isAdminOrDeveloper)
                        {
                            viewModel.ProjectBoards.Add(new KanbanBoardViewModel
                            {
                                ID = project.ID.ToString(),
                                Title = project.Name,
                                Tasks = projectTasks.Select(t => new KanbanTaskViewModel
                                {
                                    ID = t.ID,
                                    Title = t.Title,
                                    Description = t.Description,
                                    KeyCode = t.KeyCode,
                                    StatusID = t.StatusID,
                                    StatusName = t.APP_TASK_STATUS_REF?.Name,
                                    PriorityID = t.PriorityID,
                                    PriorityName = t.APP_TASK_PRIORITY_REF?.Name,
                                    AssigneeUserID = t.AssigneeUserID,
                                    AssigneeFullName = t.AssigneeUserID.HasValue ? UserHelper.GetFullName(userID: t.AssigneeUserID.Value) : null,
                                    DueDate = t.DueDate,
                                    CreatedDT = t.CreatedDT
                                }).ToList()
                            });
                        }
                    }

                    // Tasks without project
                    var noProjectTasks = tasks.Where(t => !t.ProjectID.HasValue).ToList();
                    if (noProjectTasks.Any() || isAdminOrDeveloper)
                    {
                        viewModel.ProjectBoards.Add(new KanbanBoardViewModel
                        {
                            ID = "no-project",
                            Title = "Loyiha yo'q",
                            Tasks = noProjectTasks.Select(t => new KanbanTaskViewModel
                            {
                                ID = t.ID,
                                Title = t.Title,
                                Description = t.Description,
                                KeyCode = t.KeyCode,
                                StatusID = t.StatusID,
                                StatusName = t.APP_TASK_STATUS_REF?.Name,
                                PriorityID = t.PriorityID,
                                PriorityName = t.APP_TASK_PRIORITY_REF?.Name,
                                AssigneeUserID = t.AssigneeUserID,
                                AssigneeFullName = t.AssigneeUserID.HasValue ? UserHelper.GetFullName(userID: t.AssigneeUserID.Value) : null,
                                DueDate = t.DueDate,
                                CreatedDT = t.CreatedDT
                            }).ToList()
                        });
                    }
                }
                else // viewType == "holat"
                {
                    // Group by holat
                    var statuses = await db.APP_TASK_STATUS_REF
                        .Where(s => s.IsActive)
                        .OrderBy(s => s.SortOrder)
                        .ToListAsync();

                    foreach (var status in statuses)
                    {
                        var statusTasks = tasks.Where(t => t.StatusID == status.ID).ToList();
                        viewModel.StatusBoards.Add(new KanbanBoardViewModel
                        {
                            ID = status.ID.ToString(),
                            Title = status.Name,
                            Tasks = statusTasks.Select(t => new KanbanTaskViewModel
                            {
                                ID = t.ID,
                                Title = t.Title,
                                Description = t.Description,
                                KeyCode = t.KeyCode,
                                StatusID = t.StatusID,
                                StatusName = t.APP_TASK_STATUS_REF?.Name,
                                PriorityID = t.PriorityID,
                                PriorityName = t.APP_TASK_PRIORITY_REF?.Name,
                                AssigneeUserID = t.AssigneeUserID,
                                AssigneeFullName = t.AssigneeUserID.HasValue ? UserHelper.GetFullName(userID: t.AssigneeUserID.Value) : null,
                                DueDate = t.DueDate,
                                CreatedDT = t.CreatedDT,
                                ProjectID = t.ProjectID,
                                ProjectName = t.APP_TASK_PROJECTS?.Name
                            }).ToList()
                        });
                    }
                }

                ViewBag.ViewType = viewType;
                ViewBag.IsAdminOrDeveloper = isAdminOrDeveloper;

                return View(viewModel);
            }
        }

        // POST so'rovi: TaskKanban/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> UpdateStatus(long taskId, int newStatusId)
        {
            // Oddiy user uchun faqat o'z tasklarini o'zgartirish
            var userRoles = RoleHelper.GetUserRoles(User.Identity.Name);
            bool isAdminOrDeveloper = userRoles != null && userRoles.Any(r => 
                r.Equals("Admin", StringComparison.OrdinalIgnoreCase) || 
                r.Equals("Developer", StringComparison.OrdinalIgnoreCase));
            
            int currentUserId = UserHelper.GetUserId(User.Identity.Name);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var task = await db.APP_TASKS.FindAsync(taskId);
                if (task == null || task.IsDeleted)
                {
                    return Json(new { success = false, message = "Task topilmadi" });
                }

                // Oddiy user boshqalarning tasklarini o'zgartirmasligi kerak
                if (!isAdminOrDeveloper && task.AssigneeUserID != currentUserId)
                {
                    return Json(new { success = false, message = "Siz faqat o'zingizga tegishli tasklarni o'zgartirishingiz mumkin" });
                }

                try
                {
                    task.StatusID = newStatusId;
                    task.UpdatedDT = DateTime.Now;
                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(User.Identity.Name, "TaskKanbanController", $"Task {task.KeyCode} status o'zgartirildi: {newStatusId}");

                    return Json(new { success = true, message = "Status muvaffaqiyatli o'zgartirildi" });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Xatolik: {ex.Message}" });
                }
            }
        }
    }
}
