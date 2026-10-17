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
            bool isAdminOrDeveloper = TaskRules.IsAdminOrDeveloper(CurrentUserName);
            int currentUserId = CurrentUserId;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var project = await db.APP_TASK_PROJECTS.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ID == id && p.IsDeleted != true);
                if (project == null)
                {
                    return HttpNotFound();
                }

                var tasksQuery = db.APP_TASKS.AsNoTracking()
                    .Include(t => t.APP_TASK_STATUS_REF)
                    .Include(t => t.APP_TASK_PRIORITY_REF)
                    .Where(t => t.ProjectID == id && t.IsDeleted == false);

                if (!isAdminOrDeveloper)
                {
                    tasksQuery = tasksQuery.Where(t => t.AssigneeUserID == currentUserId);
                }

                var tasks = await tasksQuery
                    .OrderByDescending(t => t.CreatedDT)
                    .ToListAsync();

                // Ismlar bitta so'rovda (avval har topshiriqqa 2 ta GetFullName — TSK-P01)
                var names = await TaskLookups.UserNamesAsync(db, tasks.SelectMany(t => new[] { t.AssigneeUserID, t.ReporterUserID }));
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
                    AssigneeFullName = TaskLookups.NameOf(names, t.AssigneeUserID),
                    ReporterUserID = t.ReporterUserID,
                    ReporterFullName = TaskLookups.NameOf(names, t.ReporterUserID),
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
    }
}
