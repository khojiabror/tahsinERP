using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services.Tasks;

namespace tahsinERP.Controllers.TaskSettings
{
    public class TaskPriorityController : Controller
    {
        // Olish so'rovi: TaskPriority
        public async Task<ActionResult> Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var priorities = await db.APP_TASK_PRIORITY_REF
                    .Where(p => p.IsDeleted == false || p.IsDeleted == null)
                    .OrderBy(p => p.SortOrder)
                    .ToListAsync();
                return View(priorities);
            }
        }

        // Olish so'rovi: TaskPriority/Details/5
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
                var priority = await db.APP_TASK_PRIORITY_REF
                    .Where(p => p.ID == id && (p.IsDeleted == false || p.IsDeleted == null))
                    .FirstOrDefaultAsync();
                if (priority == null)
                {
                    return HttpNotFound();
                }
                return View(priority);
            }
        }

        // Olish so'rovi: TaskPriority/yaratish
        public ActionResult Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            return View();
        }

        // POST so'rovi: TaskPriority/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(APP_TASK_PRIORITY_REF priority)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    // Tekshirish agar code already mavjud
                    var existing = await db.APP_TASK_PRIORITY_REF.FirstOrDefaultAsync(p => p.Code == priority.Code);
                    if (existing != null)
                    {
                        ModelState.AddModelError("Code", "Bu kod allaqachon mavjud");
                        return View(priority);
                    }

                    // Olish max SortOrder agar not provided
                    if (priority.SortOrder == 0 && await db.APP_TASK_PRIORITY_REF.AnyAsync())
                    {
                        var maxSort = await db.APP_TASK_PRIORITY_REF
                            .Select(p => (int?)p.SortOrder)
                            .DefaultIfEmpty(0)
                            .MaxAsync();
                        priority.SortOrder = (maxSort ?? 0) + 1;
                    }

                    db.APP_TASK_PRIORITY_REF.Add(priority);
                    await db.SaveChangesAsync();
                    TaskLookups.Invalidate();

                    LogHelper.LogToDatabase(User.Identity.Name, "TaskPriorityController", $"Priority {priority.Code} - {priority.Name} yaratdi");
                    return RedirectToAction("Index");
                }
            }

            return View(priority);
        }

        // Olish so'rovi: TaskPriority/Edit/5
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
                var priority = await db.APP_TASK_PRIORITY_REF.FindAsync(id);
                if (priority == null)
                {
                    return HttpNotFound();
                }
                return View(priority);
            }
        }

        // POST so'rovi: TaskPriority/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(APP_TASK_PRIORITY_REF priority)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    // Tekshirish agar code already mavjud (excluding current record)
                    var existing = await db.APP_TASK_PRIORITY_REF.FirstOrDefaultAsync(p => p.Code == priority.Code && p.ID != priority.ID);
                    if (existing != null)
                    {
                        ModelState.AddModelError("Code", "Bu kod allaqachon mavjud");
                        return View(priority);
                    }

                    db.Entry(priority).State = EntityState.Modified;
                    await db.SaveChangesAsync();
                    TaskLookups.Invalidate();

                    LogHelper.LogToDatabase(User.Identity.Name, "TaskPriorityController", $"Priority {priority.Code} - {priority.Name} tahrirladi");
                    return RedirectToAction("Index");
                }
            }

            return View(priority);
        }

        // POST so'rovi: TaskPriority/o'chirish/5
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
                var priority = await db.APP_TASK_PRIORITY_REF.FindAsync(id);
                if (priority == null)
                {
                    return HttpNotFound();
                }

                // Tekshirish agar priority is used in tasks
                var usedInTasks = await db.APP_TASKS.AnyAsync(t => t.PriorityID == id && t.IsDeleted == false);
                if (usedInTasks)
                {
                    TempData["Error"] = "Bu prioritet vazifalarda ishlatilmoqda. O'chirish mumkin emas.";
                    return RedirectToAction("Index");
                }

                priority.IsDeleted = true;
                db.Entry(priority).State = System.Data.Entity.EntityState.Modified;
                await db.SaveChangesAsync();
                TaskLookups.Invalidate();

                LogHelper.LogToDatabase(User.Identity.Name, "TaskPriorityController", $"Priority {priority.Code} - {priority.Name} o'chirildi");
                return RedirectToAction("Index");
            }
        }
    }
}
