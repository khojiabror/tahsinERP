using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;

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
