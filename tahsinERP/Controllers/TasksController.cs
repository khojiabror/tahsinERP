using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.Models;
using tahsinERP.Services.Common;
using tahsinERP.Services.Tasks;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    [Permission]
    public class TasksController : BaseController
    {
        // Olish so'rovi: Tasks
        public async Task<ActionResult> Index(int? assigneeUserID, int? statusID, int? priorityID, DateTime? dueDateFrom, DateTime? dueDateTo, string searchTerm, string sortOrder, int? page, int? pageSize, bool? showOverdue)
        {
            bool isAdminOrDeveloper = TaskRules.IsAdminOrDeveloper(CurrentUserName);
            int currentUserId = CurrentUserId;
            if (!isAdminOrDeveloper)
            {
                // Oddiy user - faqat o'z tasklarini ko'rsatish
                assigneeUserID = currentUserId;
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.APP_TASKS.AsNoTracking()
                    .Include(t => t.APP_TASK_STATUS_REF)
                    .Include(t => t.APP_TASK_PRIORITY_REF)
                    .Include(t => t.APP_TASK_PROJECTS)
                    .Where(t => t.IsDeleted == false);

                // Filters
                if (assigneeUserID.HasValue)
                {
                    query = query.Where(t => t.AssigneeUserID == assigneeUserID.Value);
                }

                if (statusID.HasValue)
                {
                    query = query.Where(t => t.StatusID == statusID.Value);
                }

                if (priorityID.HasValue)
                {
                    query = query.Where(t => t.PriorityID == priorityID.Value);
                }

                if (dueDateFrom.HasValue)
                {
                    query = query.Where(t => t.DueDate.HasValue && t.DueDate >= dueDateFrom.Value);
                }

                if (dueDateTo.HasValue)
                {
                    query = query.Where(t => t.DueDate.HasValue && t.DueDate <= dueDateTo.Value);
                }

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(t =>
                        t.Title.Contains(searchTerm) ||
                        (t.Description != null && t.Description.Contains(searchTerm)) ||
                        (t.KeyCode != null && t.KeyCode.Contains(searchTerm)));
                }

                // Muddatidan o'tgan tasklar filtr
                if (showOverdue == true)
                {
                    var today = DateTime.Now.Date;
                    query = query.Where(t =>
                        t.DueDate.HasValue &&
                        t.DueDate.Value < today &&
                        t.APP_TASK_STATUS_REF.Code != TaskRules.DoneCode &&
                        !t.APP_TASK_STATUS_REF.Name.Contains("bajarildi"));
                }

                // Sorting
                switch (sortOrder)
                {
                    case "created":
                        query = query.OrderBy(t => t.CreatedDT);
                        break;
                    case "dueDate_desc":
                        query = query.OrderByDescending(t => t.DueDate);
                        break;
                    case "dueDate":
                        query = query.OrderBy(t => t.DueDate);
                        break;
                    case "priority_desc":
                        query = query.OrderByDescending(t => t.APP_TASK_PRIORITY_REF.SortOrder);
                        break;
                    case "priority":
                        query = query.OrderBy(t => t.APP_TASK_PRIORITY_REF.SortOrder);
                        break;
                    default: // created_desc
                        query = query.OrderByDescending(t => t.CreatedDT);
                        break;
                }

                // Pagination
                int itemsPerPage = Math.Max(1, Math.Min(pageSize ?? 25, 500));
                int totalItems = await query.CountAsync();
                int totalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage);
                int currentPage = Math.Max(1, page ?? 1);

                var tasks = await query
                    .Skip((currentPage - 1) * itemsPerPage)
                    .Take(itemsPerPage)
                    .ToListAsync();

                // Bitta so'rov bilan ismlar (avval har qatorga 2 ta GetFullName — TSK-P01)
                var names = await TaskLookups.UserNamesAsync(db, tasks.SelectMany(t => new[] { t.AssigneeUserID, t.ReporterUserID }));
                var taskViewModels = tasks.Select(t => ToViewModel(t, names)).ToList();

                // ViewBag uchun filters
                ViewBag.AssigneeUserID = await TaskLookups.UserListAsync(db, assigneeUserID);
                ViewBag.StatusID = TaskLookups.StatusList(statusID);
                ViewBag.PriorityID = TaskLookups.PriorityList(priorityID);
                ViewBag.CurrentPage = currentPage;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalItems = totalItems;
                ViewBag.PageSize = itemsPerPage;
                ViewBag.SearchTerm = searchTerm;
                ViewBag.AssigneeUserID_Selected = assigneeUserID;
                ViewBag.StatusID_Selected = statusID;
                ViewBag.PriorityID_Selected = priorityID;
                ViewBag.DueDateFrom = dueDateFrom;
                ViewBag.DueDateTo = dueDateTo;
                ViewBag.SortOrder = sortOrder;
                ViewBag.ShowOverdue = showOverdue == true;
                ViewBag.IsAdminOrDeveloper = isAdminOrDeveloper;

                return View(taskViewModels);
            }
        }

        // Olish so'rovi: Tasks/Details/5
        public async Task<ActionResult> Details(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var task = await db.APP_TASKS.AsNoTracking()
                    .Include(t => t.APP_TASK_STATUS_REF)
                    .Include(t => t.APP_TASK_PRIORITY_REF)
                    .Include(t => t.APP_TASK_PROJECTS)
                    .FirstOrDefaultAsync(t => t.ID == id && t.IsDeleted == false);

                if (task == null)
                {
                    return HttpNotFound();
                }

                // Oddiy user boshqalarning tasklarini ko'rmasligi kerak
                if (!TaskRules.CanAccess(task.AssigneeUserID, CurrentUserId, TaskRules.IsAdminOrDeveloper(CurrentUserName)))
                {
                    TempData["Error"] = "Siz faqat o'zingizga tegishli tasklarni ko'rishingiz mumkin";
                    return RedirectToAction("Index");
                }

                var names = await TaskLookups.UserNamesAsync(db, new[] { task.AssigneeUserID, task.ReporterUserID });
                var viewModel = ToViewModel(task, names);
                viewModel.UserComment = task.UserComment;

                return View(viewModel);
            }
        }

        // Olish so'rovi: Tasks/yaratish
        public async Task<ActionResult> Create(int? projectID)
        {
            var viewModel = new TaskViewModel
            {
                CreatedDT = DateTime.Now,
                ReporterUserID = CurrentUserId,
                ReporterFullName = UserHelper.GetFullName(CurrentUserName),
                ProjectID = projectID
            };

            // Standart TODO holat
            var todoStatus = TaskLookups.Statuses().FirstOrDefault(s => s.Code == "TODO");
            if (todoStatus != null)
            {
                viewModel.StatusID = todoStatus.ID;
            }

            await PopulateLookupsAsync(viewModel);
            return View(viewModel);
        }

        // POST so'rovi: Tasks/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(TaskViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        var task = new APP_TASKS
                        {
                            Title = viewModel.Title,
                            Description = viewModel.Description,
                            StatusID = viewModel.StatusID,
                            PriorityID = viewModel.PriorityID,
                            AssigneeUserID = viewModel.AssigneeUserID,
                            ReporterUserID = viewModel.ReporterUserID ?? CurrentUserId,
                            CreatedDT = DateTime.Now,
                            DueDate = viewModel.DueDate,
                            ProjectID = viewModel.ProjectID,
                            IsDeleted = false
                        };

                        using (var tx = db.Database.BeginTransaction())
                        {
                            if (string.IsNullOrEmpty(viewModel.KeyCode))
                            {
                                var projectCode = viewModel.ProjectID.HasValue
                                    ? await db.APP_TASK_PROJECTS.Where(p => p.ID == viewModel.ProjectID.Value).Select(p => p.Code).FirstOrDefaultAsync()
                                    : null;

                                // TASK-9, TASK-10 -> TASK-11: raqamli MAX SQL'da, qulf bilan — parallel yaratishda
                                // takrorlanmaydi (avval barcha kodlar xotiraga yuklanib, qulfsiz hisoblanardi — TSK-P08/TSK-B05)
                                var codePrefix = (string.IsNullOrWhiteSpace(projectCode) ? "TASK" : projectCode).Trim();
                                task.KeyCode = await DocumentNumberService.NextAsync(db, "APP_TASKS", "KeyCode", codePrefix + "-");
                            }
                            else
                            {
                                task.KeyCode = viewModel.KeyCode;
                            }

                            db.APP_TASKS.Add(task);
                            await db.SaveChangesAsync();
                            tx.Commit();
                        }

                        LogHelper.LogToDatabase(CurrentUserName, "TasksController", $"Task {task.KeyCode} - {task.Title} yaratdi");

                        return RedirectToAction("Details", new { id = task.ID });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceError("Tasks Create: {0}", ex);
                        ModelState.AddModelError("", "Saqlashda xatolik yuz berdi: " + InnermostMessage(ex));
                    }
                }
            }

            await PopulateLookupsAsync(viewModel);
            return View(viewModel);
        }

        // Olish so'rovi: Tasks/Edit/5
        public async Task<ActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var task = await db.APP_TASKS.AsNoTracking().FirstOrDefaultAsync(t => t.ID == id && t.IsDeleted == false);
                if (task == null)
                {
                    return HttpNotFound();
                }

                // Oddiy user boshqalarning tasklarini tahrirlamasligi kerak
                if (!TaskRules.CanAccess(task.AssigneeUserID, CurrentUserId, TaskRules.IsAdminOrDeveloper(CurrentUserName)))
                {
                    TempData["Error"] = "Siz faqat o'zingizga tegishli tasklarni tahrirlashingiz mumkin";
                    return RedirectToAction("Index");
                }

                var viewModel = new TaskViewModel
                {
                    ID = task.ID,
                    KeyCode = task.KeyCode,
                    Title = task.Title,
                    Description = task.Description,
                    StatusID = task.StatusID,
                    PriorityID = task.PriorityID,
                    AssigneeUserID = task.AssigneeUserID,
                    ReporterUserID = task.ReporterUserID,
                    DueDate = task.DueDate,
                    ProjectID = task.ProjectID,
                    CreatedDT = task.CreatedDT
                };

                await PopulateLookupsAsync(viewModel, db);
                return View(viewModel);
            }
        }

        // POST so'rovi: Tasks/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(TaskViewModel viewModel)
        {
            bool isAdminOrDeveloper = TaskRules.IsAdminOrDeveloper(CurrentUserName);

            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var task = await db.APP_TASKS.FindAsync(viewModel.ID);
                    if (task == null || task.IsDeleted)
                    {
                        return HttpNotFound();
                    }

                    // Oddiy user boshqalarning tasklarini tahrirlamasligi kerak
                    if (!TaskRules.CanAccess(task.AssigneeUserID, CurrentUserId, isAdminOrDeveloper))
                    {
                        TempData["Error"] = "Siz faqat o'zingizga tegishli tasklarni tahrirlashingiz mumkin";
                        return RedirectToAction("Index");
                    }

                    try
                    {
                        task.Title = viewModel.Title;
                        task.Description = viewModel.Description;
                        task.StatusID = viewModel.StatusID;
                        task.PriorityID = viewModel.PriorityID;

                        // Oddiy user assignee'ni o'zgartira olmaydi
                        if (isAdminOrDeveloper)
                        {
                            task.AssigneeUserID = viewModel.AssigneeUserID;
                        }

                        task.DueDate = viewModel.DueDate;
                        task.ProjectID = viewModel.ProjectID;
                        task.UpdatedDT = DateTime.Now;

                        // Entity kuzatilmoqda — faqat o'zgargan ustunlar yoziladi (UserComment ustiga yozilmaydi)
                        await db.SaveChangesAsync();

                        LogHelper.LogToDatabase(CurrentUserName, "TasksController", $"Task {task.KeyCode} - {task.Title} tahrirladi");

                        return RedirectToAction("Details", new { id = task.ID });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceError("Tasks Edit {0}: {1}", viewModel.ID, ex);
                        ModelState.AddModelError("", "Saqlashda xatolik yuz berdi: " + InnermostMessage(ex));
                    }
                }
            }

            await PopulateLookupsAsync(viewModel);
            return View(viewModel);
        }

        // POST so'rovi: Tasks/o'chirish/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(long id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var task = await db.APP_TASKS.FindAsync(id);
                if (task == null || task.IsDeleted)
                {
                    return HttpNotFound();
                }

                if (!TaskRules.CanAccess(task.AssigneeUserID, CurrentUserId, TaskRules.IsAdminOrDeveloper(CurrentUserName)))
                {
                    TempData["Error"] = "Siz faqat o'zingizga tegishli tasklarni o'chirishingiz mumkin";
                    return RedirectToAction("Index");
                }

                // Biznes qarori (TSK-B06, 2026-10-17): "o'chirish" = bekor qilish — CANCELLED holati bo'lsa
                // status o'zgaradi (tarix saqlanadi), aks holda IsDeleted
                var cancelledStatus = TaskLookups.Statuses().FirstOrDefault(s => s.Code == TaskRules.CancelledCode);
                if (cancelledStatus != null)
                {
                    task.StatusID = cancelledStatus.ID;
                    task.UpdatedDT = DateTime.Now;
                }
                else
                {
                    task.IsDeleted = true;
                }

                await db.SaveChangesAsync();

                LogHelper.LogToDatabase(CurrentUserName, "TasksController", $"Task {task.KeyCode} - {task.Title} o'chirildi");

                return RedirectToAction("Index");
            }
        }

        private static TaskViewModel ToViewModel(APP_TASKS t, Dictionary<int, string> names)
        {
            return new TaskViewModel
            {
                ID = t.ID,
                KeyCode = t.KeyCode,
                Title = t.Title,
                Description = t.Description,
                StatusID = t.StatusID,
                StatusName = t.APP_TASK_STATUS_REF?.Name,
                PriorityID = t.PriorityID,
                PriorityName = t.APP_TASK_PRIORITY_REF?.Name,
                AssigneeUserID = t.AssigneeUserID,
                AssigneeFullName = TaskLookups.NameOf(names, t.AssigneeUserID),
                ReporterUserID = t.ReporterUserID,
                ReporterFullName = TaskLookups.NameOf(names, t.ReporterUserID),
                CreatedDT = t.CreatedDT,
                UpdatedDT = t.UpdatedDT,
                DueDate = t.DueDate,
                ProjectID = t.ProjectID,
                ProjectName = t.APP_TASK_PROJECTS?.Name
            };
        }

        /// <summary>Create/Edit dropdownlari (avval 5 joyda 4 tadan so'rov — TSK-P07).</summary>
        private async Task PopulateLookupsAsync(TaskViewModel viewModel, DBTHSNEntities db = null)
        {
            ViewBag.StatusID = TaskLookups.StatusList(viewModel.StatusID);
            ViewBag.PriorityID = TaskLookups.PriorityList(viewModel.PriorityID);
            ViewBag.ProjectID = TaskLookups.ProjectList(viewModel.ProjectID);

            if (db != null)
            {
                ViewBag.AssigneeUserID = await TaskLookups.UserListAsync(db, viewModel.AssigneeUserID);
                return;
            }
            using (var ctx = new DBTHSNEntities())
            {
                ViewBag.AssigneeUserID = await TaskLookups.UserListAsync(ctx, viewModel.AssigneeUserID);
            }
        }
    }
}
