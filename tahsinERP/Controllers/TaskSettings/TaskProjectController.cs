using System;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services.Tasks;

namespace tahsinERP.Controllers.TaskSettings
{
    public class TaskProjectController : TaskRefDataController<APP_TASK_PROJECTS>
    {
        private static readonly string[] Fields = { "Code", "Name", "IsActive" };

        protected override DbSet<APP_TASK_PROJECTS> Set(DBTHSNEntities db) => db.APP_TASK_PROJECTS;

        // Index avval o'chirilgan loyihalarni ham ko'rsatardi (TSK-B04) — endi asos filtrlaydi
        protected override IQueryable<APP_TASK_PROJECTS> Ordered(IQueryable<APP_TASK_PROJECTS> query) => query.OrderBy(p => p.Name);

        protected override string[] EditableFields => Fields;

        protected override Expression<Func<APP_TASKS, bool>> UsedBy(int id) => t => t.ProjectID == id;

        protected override string LogLabel => "Project";

        protected override string InUseMessage => "Bu loyiha vazifalarda ishlatilmoqda. O'chirish mumkin emas.";

        protected override Task OnCreatingAsync(DBTHSNEntities db, APP_TASK_PROJECTS entity)
        {
            entity.CreatedDT = DateTime.Now;
            entity.IsActive = true;
            return Task.FromResult(0);
        }

        // Olish so'rovi: TaskProject/Details/5 — loyiha va uning topshiriqlari
        public override async Task<ActionResult> Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Oddiy user uchun faqat o'z tasklarini ko'rsatish
            bool isAdminOrDeveloper = RoleHelper.IsAdminOrDeveloper(CurrentUserName);
            int currentUserId = CurrentUserId;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var project = await db.APP_TASK_PROJECTS.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ID == id && p.IsDeleted != true);
                if (project == null)
                {
                    return HttpNotFound();
                }

                // SQL proyeksiya va ismlar bitta so'rovda (dublikat #4, TSK-P01)
                var taskViewModels = await TaskQueries.WithNamesAsync(db, await TaskQueries.Visible(db, currentUserId, isAdminOrDeveloper)
                    .Where(t => t.ProjectID == id)
                    .OrderByDescending(t => t.CreatedDT)
                    .Select(TaskQueries.Projection)
                    .ToListAsync());

                ViewBag.Tasks = taskViewModels;
                ViewBag.IsAdminOrDeveloper = isAdminOrDeveloper;

                return View(project);
            }
        }
    }
}
