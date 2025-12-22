using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers.TaskSettings
{
    public class TaskProjectController : Controller
    {
        // GET: TaskProject
        public async Task<ActionResult> Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var projects = await db.APP_TASK_PROJECTS
                    .OrderBy(p => p.Name)
                    .ToListAsync();
                return View(projects);
            }
        }

        // GET: TaskProject/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Oddiy user uchun faqat o'z tasklarini ko'rsatish
            var userRoles = RoleHelper.GetUserRoles(User.Identity.Name);
            bool isAdminOrDeveloper = userRoles != null && userRoles.Any(r => 
                r.Equals("Admin", StringComparison.OrdinalIgnoreCase) || 
                r.Equals("Developer", StringComparison.OrdinalIgnoreCase));
            int currentUserId = UserHelper.GetUserId(User.Identity.Name);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var project = await db.APP_TASK_PROJECTS.FindAsync(id);
                if (project == null)
                {
                    return HttpNotFound();
                }

                // Get tasks for this project
                var tasksQuery = db.APP_TASKS
                    .Include(t => t.APP_TASK_STATUS_REF)
                    .Include(t => t.APP_TASK_PRIORITY_REF)
                    .Where(t => t.ProjectID == id && t.IsDeleted == false);

                // Oddiy user uchun faqat o'z tasklarini ko'rsatish
                if (!isAdminOrDeveloper)
                {
                    tasksQuery = tasksQuery.Where(t => t.AssigneeUserID == currentUserId);
                }

                var tasks = await tasksQuery
                    .OrderByDescending(t => t.CreatedDT)
                    .ToListAsync();

                // Convert to ViewModels
                var taskViewModels = tasks.Select(t => new ViewModels.TaskViewModel
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
                    ProjectName = project.Name
                }).ToList();

                ViewBag.Tasks = taskViewModels;
                ViewBag.IsAdminOrDeveloper = isAdminOrDeveloper;

                return View(project);
            }
        }

        // GET: TaskProject/Create
        public ActionResult Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            return View();
        }

        // POST: TaskProject/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(APP_TASK_PROJECTS project)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    // Check if code already exists
                    var existing = await db.APP_TASK_PROJECTS.FirstOrDefaultAsync(p => p.Code == project.Code);
                    if (existing != null)
                    {
                        ModelState.AddModelError("Code", "Bu kod allaqachon mavjud");
                        return View(project);
                    }

                    project.CreatedDT = DateTime.Now;
                    project.IsActive = true;

                    db.APP_TASK_PROJECTS.Add(project);
                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(User.Identity.Name, "TaskProjectController", $"Project {project.Code} - {project.Name} yaratdi");
                    return RedirectToAction("Index");
                }
            }

            return View(project);
        }

        // GET: TaskProject/Edit/5
        public async Task<ActionResult> Edit(int? id)
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
                var project = await db.APP_TASK_PROJECTS.FindAsync(id);
                if (project == null)
                {
                    return HttpNotFound();
                }
                return View(project);
            }
        }

        // POST: TaskProject/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(APP_TASK_PROJECTS project)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    // Check if code already exists (excluding current record)
                    var existing = await db.APP_TASK_PROJECTS.FirstOrDefaultAsync(p => p.Code == project.Code && p.ID != project.ID);
                    if (existing != null)
                    {
                        ModelState.AddModelError("Code", "Bu kod allaqachon mavjud");
                        return View(project);
                    }

                    db.Entry(project).State = EntityState.Modified;
                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(User.Identity.Name, "TaskProjectController", $"Project {project.Code} - {project.Name} tahrirladi");
                    return RedirectToAction("Index");
                }
            }

            return View(project);
        }

        // POST: TaskProject/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var project = await db.APP_TASK_PROJECTS.FindAsync(id);
                if (project == null)
                {
                    return HttpNotFound();
                }

                // Check if project is used in tasks
                var usedInTasks = await db.APP_TASKS.AnyAsync(t => t.ProjectID == id && t.IsDeleted == false);
                if (usedInTasks)
                {
                    TempData["Error"] = "Bu loyiha vazifalarda ishlatilmoqda. O'chirish mumkin emas.";
                    return RedirectToAction("Index");
                }

                db.APP_TASK_PROJECTS.Remove(project);
                await db.SaveChangesAsync();

                LogHelper.LogToDatabase(User.Identity.Name, "TaskProjectController", $"Project {project.Code} - {project.Name} o'chirildi");
                return RedirectToAction("Index");
            }
        }
    }
}
