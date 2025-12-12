using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers.TaskSettings
{
    public class TaskStatusController : Controller
    {
        // GET: TaskStatus
        public async Task<ActionResult> Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var statuses = await db.APP_TASK_STATUS_REF
                    .OrderBy(s => s.SortOrder)
                    .ToListAsync();
                return View(statuses);
            }
        }

        // GET: TaskStatus/Details/5
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
                var status = await db.APP_TASK_STATUS_REF.FindAsync(id);
                if (status == null)
                {
                    return HttpNotFound();
                }
                return View(status);
            }
        }

        // GET: TaskStatus/Create
        public ActionResult Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            return View();
        }

        // POST: TaskStatus/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(APP_TASK_STATUS_REF status)
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
                    var existing = await db.APP_TASK_STATUS_REF.FirstOrDefaultAsync(s => s.Code == status.Code);
                    if (existing != null)
                    {
                        ModelState.AddModelError("Code", "Bu kod allaqachon mavjud");
                        return View(status);
                    }

                    // Get max SortOrder if not provided
                    if (status.SortOrder == 0)
                    {
                        var maxSort = await db.APP_TASK_STATUS_REF
                            .Select(s => (int?)s.SortOrder)
                            .DefaultIfEmpty(0)
                            .MaxAsync();
                        status.SortOrder = (maxSort ?? 0) + 1;
                    }

                    db.APP_TASK_STATUS_REF.Add(status);
                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(User.Identity.Name, "TaskStatusController", $"Status {status.Code} - {status.Name} yaratdi");
                    return RedirectToAction("Index");
                }
            }

            return View(status);
        }

        // GET: TaskStatus/Edit/5
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
                var status = await db.APP_TASK_STATUS_REF.FindAsync(id);
                if (status == null)
                {
                    return HttpNotFound();
                }
                return View(status);
            }
        }

        // POST: TaskStatus/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(APP_TASK_STATUS_REF status)
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
                    var existing = await db.APP_TASK_STATUS_REF.FirstOrDefaultAsync(s => s.Code == status.Code && s.ID != status.ID);
                    if (existing != null)
                    {
                        ModelState.AddModelError("Code", "Bu kod allaqachon mavjud");
                        return View(status);
                    }

                    db.Entry(status).State = EntityState.Modified;
                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(User.Identity.Name, "TaskStatusController", $"Status {status.Code} - {status.Name} tahrirladi");
                    return RedirectToAction("Index");
                }
            }

            return View(status);
        }

        // POST: TaskStatus/Delete/5
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
                var status = await db.APP_TASK_STATUS_REF.FindAsync(id);
                if (status == null)
                {
                    return HttpNotFound();
                }

                // Check if status is used in tasks
                var usedInTasks = await db.APP_TASKS.AnyAsync(t => t.StatusID == id && t.IsDeleted == false);
                if (usedInTasks)
                {
                    TempData["Error"] = "Bu status vazifalarda ishlatilmoqda. O'chirish mumkin emas.";
                    return RedirectToAction("Index");
                }

                db.APP_TASK_STATUS_REF.Remove(status);
                await db.SaveChangesAsync();

                LogHelper.LogToDatabase(User.Identity.Name, "TaskStatusController", $"Status {status.Code} - {status.Name} o'chirildi");
                return RedirectToAction("Index");
            }
        }
    }
}
