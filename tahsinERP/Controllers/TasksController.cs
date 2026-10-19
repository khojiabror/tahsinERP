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
        // Olish so'rovi: Tasks — faqat filtr va jadval "qobig'i"; qatorlar IndexData orqali server-side (TSK-P10).
        // Avval 25 qatorli server sahifasi ustida mijoz DataTables ishlardi: qidiruv/saralash/eksport faqat joriy sahifaga ta'sir qilardi.
        public async Task<ActionResult> Index(int? assigneeUserID, int? statusID, int? priorityID, DateTime? dueDateFrom, DateTime? dueDateTo, string searchTerm, bool? showOverdue)
        {
            bool isAdminOrDeveloper = RoleHelper.IsAdminOrDeveloper(CurrentUserName);
            if (!isAdminOrDeveloper)
            {
                // Oddiy user - faqat o'z tasklari
                assigneeUserID = CurrentUserId;
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.AssigneeUserID = await TaskLookups.UserListAsync(db, assigneeUserID);
            }
            ViewBag.StatusID = TaskLookups.StatusList(statusID);
            ViewBag.PriorityID = TaskLookups.PriorityList(priorityID);
            ViewBag.SearchTerm = searchTerm;
            ViewBag.DueDateFrom = dueDateFrom;
            ViewBag.DueDateTo = dueDateTo;
            ViewBag.ShowOverdue = showOverdue == true;
            ViewBag.IsAdminOrDeveloper = isAdminOrDeveloper;
            return View();
        }

        // Olish so'rovi: Tasks/IndexData (DataTables server-side)
        [HttpGet]
        public async Task<ActionResult> IndexData(int? assigneeUserID, int? statusID, int? priorityID, DateTime? dueDateFrom, DateTime? dueDateTo, bool? showOverdue)
        {
            var page = DataTablesPageRequest.Parse(Request, defaultOrderColumn: 5);
            var filter = BuildFilter(assigneeUserID, statusID, priorityID, dueDateFrom, dueDateTo, page.Search, showOverdue);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var visible = TaskQueries.Visible(db, CurrentUserId, RoleHelper.IsAdminOrDeveloper(CurrentUserName));
                int total = await visible.CountAsync();
                var filtered = TaskQueries.Apply(visible, filter);
                int filteredCount = await filtered.CountAsync();

                var rows = await TaskQueries.WithNamesAsync(db, await TaskQueries.Order(filtered, page.OrderColumn, page.OrderDescending)
                    .Skip(page.Start)
                    .Take(page.Length)
                    .Select(TaskQueries.Projection)
                    .ToListAsync());

                return Json(new
                {
                    draw = page.Draw,
                    recordsTotal = total,
                    recordsFiltered = filteredCount,
                    data = rows.Select(r => new
                    {
                        id = r.ID,
                        code = r.KeyCode ?? r.ID.ToString(),
                        title = r.Title ?? "",
                        assignee = r.AssigneeFullName ?? "-",
                        priority = r.PriorityName,
                        priorityStyle = TaskBadges.PriorityColorOrNull(r.PriorityID),
                        status = r.StatusName,
                        statusClass = TaskBadges.StatusBadgeClass(r.StatusID),
                        statusStyle = TaskBadges.StatusStyle(r.StatusID),
                        created = r.CreatedDT.ToString("yyyy-MM-dd"),
                        due = r.DueDate.HasValue ? r.DueDate.Value.ToString("yyyy-MM-dd") : null,
                        overdue = TaskBadges.IsOverdue(r.DueDate, r.StatusID),
                        closed = TaskRules.IsClosedStatus(TaskLookups.AllStatuses().FirstOrDefault(st => st.ID == r.StatusID))
                    })
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // Olish so'rovi: Tasks/ExportExcel — filtr bo'yicha BARCHA topshiriqlar (avval faqat joriy 25 qator eksport qilinardi)
        [HttpGet]
        public async Task<ActionResult> ExportExcel(int? assigneeUserID, int? statusID, int? priorityID, DateTime? dueDateFrom, DateTime? dueDateTo, bool? showOverdue, string q)
        {
            var filter = BuildFilter(assigneeUserID, statusID, priorityID, dueDateFrom, dueDateTo, q, showOverdue);
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var visible = TaskQueries.Visible(db, CurrentUserId, RoleHelper.IsAdminOrDeveloper(CurrentUserName));
                var rows = await TaskQueries.WithNamesAsync(db, await TaskQueries.Order(TaskQueries.Apply(visible, filter), 5, true)
                    .Select(TaskQueries.Projection)
                    .ToListAsync());

                var bytes = ExcelExport.Create("Vazifalar", rows,
                    ExcelExport.Col<TaskViewModel>("Kod", r => r.KeyCode ?? r.ID.ToString()),
                    ExcelExport.Col<TaskViewModel>("Sarlavha", r => r.Title),
                    ExcelExport.Col<TaskViewModel>("Loyiha", r => r.ProjectName),
                    ExcelExport.Col<TaskViewModel>("Mas'ul", r => r.AssigneeFullName),
                    ExcelExport.Col<TaskViewModel>("Muallif", r => r.ReporterFullName),
                    ExcelExport.Col<TaskViewModel>("Prioritet", r => r.PriorityName),
                    ExcelExport.Col<TaskViewModel>("Status", r => r.StatusName),
                    ExcelExport.Col<TaskViewModel>("Yaratilgan", r => r.CreatedDT, "dd.MM.yyyy"),
                    ExcelExport.Col<TaskViewModel>("Muddati", r => r.DueDate, "dd.MM.yyyy"));

                return File(bytes, ExcelExport.ContentType, $"Vazifalar_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
        }

        private TaskFilter BuildFilter(int? assigneeUserID, int? statusID, int? priorityID, DateTime? dueDateFrom, DateTime? dueDateTo, string search, bool? showOverdue)
        {
            return new TaskFilter
            {
                // Oddiy foydalanuvchi uchun Visible() allaqachon o'ziniki bilan cheklaydi
                AssigneeUserID = assigneeUserID,
                StatusID = statusID,
                PriorityID = priorityID,
                DueDateFrom = dueDateFrom,
                DueDateTo = dueDateTo,
                Search = search,
                ShowOverdue = showOverdue == true
            };
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
                var viewModel = await db.APP_TASKS.AsNoTracking()
                    .Where(t => t.ID == id && t.IsDeleted == false)
                    .Select(TaskQueries.Projection)
                    .FirstOrDefaultAsync();

                if (viewModel == null)
                {
                    return HttpNotFound();
                }

                // Oddiy user boshqalarning tasklarini ko'rmasligi kerak
                if (!TaskRules.CanAccess(viewModel.AssigneeUserID, CurrentUserId, RoleHelper.IsAdminOrDeveloper(CurrentUserName)))
                {
                    TempData["Error"] = "Siz faqat o'zingizga tegishli tasklarni ko'rishingiz mumkin";
                    return RedirectToAction("Index");
                }

                var text = await db.APP_TASKS.AsNoTracking()
                    .Where(t => t.ID == viewModel.ID)
                    .Select(t => new { t.Description, t.UserComment })
                    .FirstAsync();
                viewModel.Description = text.Description;
                viewModel.UserComment = text.UserComment;
                await TaskQueries.WithNamesAsync(db, new List<TaskViewModel> { viewModel });

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
                if (!TaskRules.CanAccess(task.AssigneeUserID, CurrentUserId, RoleHelper.IsAdminOrDeveloper(CurrentUserName)))
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
            bool isAdminOrDeveloper = RoleHelper.IsAdminOrDeveloper(CurrentUserName);

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

                if (!TaskRules.CanAccess(task.AssigneeUserID, CurrentUserId, RoleHelper.IsAdminOrDeveloper(CurrentUserName)))
                {
                    TempData["Error"] = "Siz faqat o'zingizga tegishli tasklarni bekor qilishingiz mumkin";
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

                LogHelper.LogToDatabase(CurrentUserName, "TasksController",
                    $"Task {task.KeyCode} - {task.Title} " + (cancelledStatus != null ? "bekor qilindi" : "o'chirildi"));
                TempData["Success"] = cancelledStatus != null ? "Topshiriq bekor qilindi" : "Topshiriq o'chirildi";

                return RedirectToAction("Index");
            }
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
