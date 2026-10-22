using System;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.Models;
using tahsinERP.Services.Tasks;
using tahsinERP.I18n;

namespace tahsinERP.Controllers.TaskSettings
{
    /// <summary>
    /// TaskPriority/TaskStatus/TaskProject CRUD — avval ~98% bir xil uchta controller edi.
    /// Kiritiladigan maydonlar oq ro'yxat bilan (avval entity to'g'ridan-to'g'ri bind qilinib,
    /// State=Modified — IsDeleted ham POST orqali o'zgarar va har tahrirda null bo'lardi, TSK-B11).
    /// Ruxsat — [Permission]: GET Index/Details = ko'rish, Create/Edit/Delete = o'zgartirish.
    /// </summary>
    [Permission]
    public abstract class TaskRefDataController<T> : BaseController where T : class, ITaskRefEntity, new()
    {
        protected abstract DbSet<T> Set(DBTHSNEntities db);

        protected abstract IQueryable<T> Ordered(IQueryable<T> query);

        /// <summary>Formadan qabul qilinadigan maydonlar.</summary>
        protected abstract string[] EditableFields { get; }

        /// <summary>Shu yozuv o'chirilmagan topshiriqlarda ishlatilyaptimi.</summary>
        protected abstract Expression<Func<APP_TASKS, bool>> UsedBy(int id);

        /// <summary>Log uchun nom: "Priority", "Status", "Project".</summary>
        protected abstract string LogLabel { get; }

        protected abstract string InUseMessage { get; }

        protected virtual Task OnCreatingAsync(DBTHSNEntities db, T entity)
        {
            return Task.FromResult(0);
        }

        private string ControllerName => RouteData.Values["controller"].ToString() + "Controller";

        private static IQueryable<T> NotDeleted(IQueryable<T> query)
        {
            return query.Where(x => x.IsDeleted != true);
        }

        // Olish so'rovi: Index
        public virtual async Task<ActionResult> Index()
        {
            using (var db = new DBTHSNEntities())
            {
                return View(await Ordered(NotDeleted(Set(db).AsNoTracking())).ToListAsync());
            }
        }

        // Olish so'rovi: Details/5
        public virtual async Task<ActionResult> Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var entity = await NotDeleted(Set(db).AsNoTracking()).FirstOrDefaultAsync(x => x.ID == id);
                if (entity == null)
                {
                    return HttpNotFound();
                }
                return View(entity);
            }
        }

        // Olish so'rovi: Create
        public ActionResult Create()
        {
            return View();
        }

        // POST so'rovi: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(FormCollection form)
        {
            var entity = new T();
            if (TryUpdateModel(entity, EditableFields))
            {
                using (var db = new DBTHSNEntities())
                {
                    if (await Set(db).AnyAsync(x => x.Code == entity.Code))
                    {
                        ModelState.AddModelError("Code", L.T("Task_CodeExists"));
                        return View(entity);
                    }

                    await OnCreatingAsync(db, entity);
                    Set(db).Add(entity);
                    await db.SaveChangesAsync();
                    TaskLookups.Invalidate();

                    LogHelper.LogToDatabase(CurrentUserName, ControllerName, $"{LogLabel} {entity.Code} - {entity.Name} yaratdi");
                    return RedirectToAction("Index");
                }
            }

            return View(entity);
        }

        // Olish so'rovi: Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var entity = await NotDeleted(Set(db).AsNoTracking()).FirstOrDefaultAsync(x => x.ID == id);
                if (entity == null)
                {
                    return HttpNotFound();
                }
                return View(entity);
            }
        }

        // POST so'rovi: Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(int id, FormCollection form)
        {
            using (var db = new DBTHSNEntities())
            {
                var entity = await NotDeleted(Set(db)).FirstOrDefaultAsync(x => x.ID == id);
                if (entity == null)
                {
                    return HttpNotFound();
                }

                if (TryUpdateModel(entity, EditableFields))
                {
                    if (await Set(db).AnyAsync(x => x.Code == entity.Code && x.ID != id))
                    {
                        ModelState.AddModelError("Code", L.T("Task_CodeExists"));
                        return View(entity);
                    }

                    await db.SaveChangesAsync();
                    TaskLookups.Invalidate();

                    LogHelper.LogToDatabase(CurrentUserName, ControllerName, $"{LogLabel} {entity.Code} - {entity.Name} tahrirladi");
                    return RedirectToAction("Index");
                }

                return View(entity);
            }
        }

        // POST so'rovi: Delete/5 (Index'dagi havola data-post-url orqali POST qiladi)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int id)
        {
            using (var db = new DBTHSNEntities())
            {
                var entity = await NotDeleted(Set(db)).FirstOrDefaultAsync(x => x.ID == id);
                if (entity == null)
                {
                    return HttpNotFound();
                }

                if (await db.APP_TASKS.Where(t => t.IsDeleted == false).AnyAsync(UsedBy(id)))
                {
                    TempData["Error"] = InUseMessage;
                    return RedirectToAction("Index");
                }

                entity.IsDeleted = true;
                await db.SaveChangesAsync();
                TaskLookups.Invalidate();

                LogHelper.LogToDatabase(CurrentUserName, ControllerName, $"{LogLabel} {entity.Code} - {entity.Name} o'chirildi");
                return RedirectToAction("Index");
            }
        }
    }
}
