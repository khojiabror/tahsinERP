using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    public class TasksController : Controller
    {
        // GET: Tasks
        public async Task<ActionResult> Index(int? assigneeUserID, int? statusID, int? priorityID, DateTime? dueDateFrom, DateTime? dueDateTo, string searchTerm, string sortOrder, int? page, int? pageSize)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.APP_TASKS
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

                // Sorting
                switch (sortOrder)
                {
                    case "created_desc":
                        query = query.OrderByDescending(t => t.CreatedDT);
                        break;
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
                    default:
                        query = query.OrderByDescending(t => t.CreatedDT);
                        break;
                }

                // Pagination
                int currentPage = page ?? 1;
                int itemsPerPage = pageSize ?? 25;
                int totalItems = await query.CountAsync();
                int totalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage);

                var tasks = await query
                    .Skip((currentPage - 1) * itemsPerPage)
                    .Take(itemsPerPage)
                    .ToListAsync();

                // Convert to ViewModels
                var taskViewModels = tasks.Select(t => new TaskViewModel
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
                    AssigneeFullName = t.AssigneeUserID.HasValue ? UserHelper.GetFullName(userID: t.AssigneeUserID.Value) : null,
                    ReporterUserID = t.ReporterUserID,
                    ReporterFullName = t.ReporterUserID.HasValue ? UserHelper.GetFullName(userID: t.ReporterUserID.Value) : null,
                    CreatedDT = t.CreatedDT,
                    UpdatedDT = t.UpdatedDT,
                    DueDate = t.DueDate,
                    ProjectID = t.ProjectID,
                    ProjectName = t.APP_TASK_PROJECTS?.Name
                }).ToList();

                // ViewBag for filters
                ViewBag.AssigneeUserID = new SelectList(await db.USERS.Where(u => u.IsDeleted != true && u.IsActive).ToListAsync(), "ID", "FullName", assigneeUserID);
                ViewBag.StatusID = new SelectList(await db.APP_TASK_STATUS_REF.Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync(), "ID", "Name", statusID);
                ViewBag.PriorityID = new SelectList(await db.APP_TASK_PRIORITY_REF.OrderBy(p => p.SortOrder).ToListAsync(), "ID", "Name", priorityID);
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

                return View(taskViewModels);
            }
        }

        // GET: Tasks/Details/5
        public async Task<ActionResult> Details(long? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

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

                var viewModel = new TaskViewModel
                {
                    ID = task.ID,
                    KeyCode = task.KeyCode,
                    Title = task.Title,
                    Description = task.Description,
                    StatusID = task.StatusID,
                    StatusName = task.APP_TASK_STATUS_REF?.Name,
                    PriorityID = task.PriorityID,
                    PriorityName = task.APP_TASK_PRIORITY_REF?.Name,
                    AssigneeUserID = task.AssigneeUserID,
                    AssigneeFullName = task.AssigneeUserID.HasValue ? UserHelper.GetFullName(userID: task.AssigneeUserID.Value) : null,
                    ReporterUserID = task.ReporterUserID,
                    ReporterFullName = task.ReporterUserID.HasValue ? UserHelper.GetFullName(userID: task.ReporterUserID.Value) : null,
                    CreatedDT = task.CreatedDT,
                    UpdatedDT = task.UpdatedDT,
                    DueDate = task.DueDate,
                    ProjectID = task.ProjectID,
                    ProjectName = task.APP_TASK_PROJECTS?.Name
                };

                return View(viewModel);
            }
        }

        // GET: Tasks/Create
        public async Task<ActionResult> Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var viewModel = new TaskViewModel
                {
                    CreatedDT = DateTime.Now,
                    ReporterUserID = UserHelper.GetUserId(User.Identity.Name),
                    ReporterFullName = UserHelper.GetFullName(User.Identity.Name)
                };

                // Get default TODO status
                var todoStatus = await db.APP_TASK_STATUS_REF.FirstOrDefaultAsync(s => s.Code == "TODO" && s.IsActive);
                if (todoStatus != null)
                {
                    viewModel.StatusID = todoStatus.ID;
                }

                ViewBag.StatusID = new SelectList(await db.APP_TASK_STATUS_REF.Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync(), "ID", "Name", viewModel.StatusID);
                ViewBag.PriorityID = new SelectList(await db.APP_TASK_PRIORITY_REF.OrderBy(p => p.SortOrder).ToListAsync(), "ID", "Name");
                ViewBag.AssigneeUserID = new SelectList(await db.USERS.Where(u => u.IsDeleted != true && u.IsActive).ToListAsync(), "ID", "FullName");
                ViewBag.ProjectID = new SelectList(await db.APP_TASK_PROJECTS.Where(p => p.IsActive).ToListAsync(), "ID", "Name");

                return View(viewModel);
            }
        }

        // POST: Tasks/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(TaskViewModel viewModel)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

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
                            ReporterUserID = viewModel.ReporterUserID ?? UserHelper.GetUserId(User.Identity.Name),
                            CreatedDT = DateTime.Now,
                            DueDate = viewModel.DueDate,
                            ProjectID = viewModel.ProjectID,
                            IsDeleted = false
                        };

                        // Generate KeyCode if not provided
                        if (string.IsNullOrEmpty(viewModel.KeyCode))
                        {
                            var projectCode = viewModel.ProjectID.HasValue 
                                ? (await db.APP_TASK_PROJECTS.FindAsync(viewModel.ProjectID.Value))?.Code ?? "TASK"
                                : "TASK";
                            var maxKey = await db.APP_TASKS
                                .Where(t => t.KeyCode != null && t.KeyCode.StartsWith(projectCode))
                                .Select(t => t.KeyCode)
                                .OrderByDescending(k => k)
                                .FirstOrDefaultAsync();
                            
                            int nextNumber = 1;
                            if (maxKey != null)
                            {
                                var parts = maxKey.Split('-');
                                if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out int lastNum))
                                {
                                    nextNumber = lastNum + 1;
                                }
                            }
                            task.KeyCode = $"{projectCode}-{nextNumber}";
                        }
                        else
                        {
                            task.KeyCode = viewModel.KeyCode;
                        }

                        db.APP_TASKS.Add(task);
                        await db.SaveChangesAsync();

                        LogHelper.LogToDatabase(User.Identity.Name, "TasksController", $"Task {task.KeyCode} - {task.Title} yaratdi");

                        return RedirectToAction("Details", new { id = task.ID });
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"Xatolik: {ex.Message}");
                    }
                }
            }

            // Reload ViewBag on error
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.StatusID = new SelectList(await db.APP_TASK_STATUS_REF.Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync(), "ID", "Name", viewModel.StatusID);
                ViewBag.PriorityID = new SelectList(await db.APP_TASK_PRIORITY_REF.OrderBy(p => p.SortOrder).ToListAsync(), "ID", "Name", viewModel.PriorityID);
                ViewBag.AssigneeUserID = new SelectList(await db.USERS.Where(u => u.IsDeleted != true && u.IsActive).ToListAsync(), "ID", "FullName", viewModel.AssigneeUserID);
                ViewBag.ProjectID = new SelectList(await db.APP_TASK_PROJECTS.Where(p => p.IsActive).ToListAsync(), "ID", "Name", viewModel.ProjectID);
            }

            return View(viewModel);
        }

        // GET: Tasks/Edit/5
        public async Task<ActionResult> Edit(long? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var task = await db.APP_TASKS.FindAsync(id);
                if (task == null || task.IsDeleted)
                {
                    return HttpNotFound();
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

                ViewBag.StatusID = new SelectList(await db.APP_TASK_STATUS_REF.Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync(), "ID", "Name", viewModel.StatusID);
                ViewBag.PriorityID = new SelectList(await db.APP_TASK_PRIORITY_REF.OrderBy(p => p.SortOrder).ToListAsync(), "ID", "Name", viewModel.PriorityID);
                ViewBag.AssigneeUserID = new SelectList(await db.USERS.Where(u => u.IsDeleted != true && u.IsActive).ToListAsync(), "ID", "FullName", viewModel.AssigneeUserID);
                ViewBag.ProjectID = new SelectList(await db.APP_TASK_PROJECTS.Where(p => p.IsActive).ToListAsync(), "ID", "Name", viewModel.ProjectID);

                return View(viewModel);
            }
        }

        // POST: Tasks/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(TaskViewModel viewModel)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var task = await db.APP_TASKS.FindAsync(viewModel.ID);
                    if (task == null || task.IsDeleted)
                    {
                        return HttpNotFound();
                    }

                    try
                    {
                        task.Title = viewModel.Title;
                        task.Description = viewModel.Description;
                        task.StatusID = viewModel.StatusID;
                        task.PriorityID = viewModel.PriorityID;
                        task.AssigneeUserID = viewModel.AssigneeUserID;
                        task.DueDate = viewModel.DueDate;
                        task.ProjectID = viewModel.ProjectID;
                        task.UpdatedDT = DateTime.Now;

                        // If status changed to DONE, set CompletedDT
                        var doneStatus = await db.APP_TASK_STATUS_REF.FirstOrDefaultAsync(s => s.Code == "DONE" && s.IsActive);
                        if (doneStatus != null && task.StatusID == doneStatus.ID && !task.UpdatedDT.HasValue)
                        {
                            // Status is now DONE
                        }

                        db.Entry(task).State = EntityState.Modified;
                        await db.SaveChangesAsync();

                        LogHelper.LogToDatabase(User.Identity.Name, "TasksController", $"Task {task.KeyCode} - {task.Title} tahrirladi");

                        return RedirectToAction("Details", new { id = task.ID });
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"Xatolik: {ex.Message}");
                    }
                }
            }

            // Reload ViewBag on error
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.StatusID = new SelectList(await db.APP_TASK_STATUS_REF.Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync(), "ID", "Name", viewModel.StatusID);
                ViewBag.PriorityID = new SelectList(await db.APP_TASK_PRIORITY_REF.OrderBy(p => p.SortOrder).ToListAsync(), "ID", "Name", viewModel.PriorityID);
                ViewBag.AssigneeUserID = new SelectList(await db.USERS.Where(u => u.IsDeleted != true && u.IsActive).ToListAsync(), "ID", "FullName", viewModel.AssigneeUserID);
                ViewBag.ProjectID = new SelectList(await db.APP_TASK_PROJECTS.Where(p => p.IsActive).ToListAsync(), "ID", "Name", viewModel.ProjectID);
            }

            return View(viewModel);
        }

        // POST: Tasks/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(long id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var task = await db.APP_TASKS.FindAsync(id);
                if (task == null || task.IsDeleted)
                {
                    return HttpNotFound();
                }

                // Soft delete - set status to CANCELLED if possible, otherwise just mark as deleted
                var cancelledStatus = await db.APP_TASK_STATUS_REF.FirstOrDefaultAsync(s => s.Code == "CANCELLED" && s.IsActive);
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

                LogHelper.LogToDatabase(User.Identity.Name, "TasksController", $"Task {task.KeyCode} - {task.Title} o'chirildi");

                return RedirectToAction("Index");
            }
        }
    }
}
