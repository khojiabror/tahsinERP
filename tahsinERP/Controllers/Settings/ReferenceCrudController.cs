using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.I18n;

namespace tahsinERP.Controllers.Settings
{
    /// <summary>
    /// Oddiy spravochnik CRUD'i (o'lchov birliklari, valyutalar). UnitController va CurrencyController avval
    /// satrma-satr bir xil edi (audit "Duplicates map"); endi ular faqat jadval, maydonlar va matnlarni beradi.
    /// Create/Edit faqat <see cref="EditableFields"/> ro'yxatidagi maydonlarni bog'laydi (overposting'dan himoya).
    /// O'chirish — soft-delete; <see cref="AfterSoftDeletedAsync"/> bog'liq nullable havolalarni tozalaydi.
    /// </summary>
    public abstract class ReferenceCrudController<T> : Controller where T : class, new()
    {
        protected abstract DbSet<T> Set(DBTHSNEntities db);
        protected abstract IQueryable<T> Active(DBTHSNEntities db);
        protected abstract void MarkDeleted(T entity);
        protected abstract bool IsDeleted(T entity);
        protected abstract Task AfterSoftDeletedAsync(DBTHSNEntities db, T entity);

        /// <summary>Formadan bog'lanadigan maydonlar.</summary>
        protected abstract string[] EditableFields { get; }

        /// <summary>Jurnal (LogHelper) bo'limi nomi.</summary>
        protected abstract string LogModule { get; }

        /// <summary>Jurnal matnlari.</summary>
        protected abstract string CreatedLog(T entity);
        protected abstract string UpdatedLog(T entity);
        protected abstract string DeletedLog(T entity);

        protected virtual string NotFoundMessage => L.T("Prd_RecordNotFound");

        /// <summary>O'chirib bo'lmaydigan yozuv (masalan, tizim kodi bor birlik) — true bo'lsa o'chirilmaydi.</summary>
        protected virtual bool IsProtected(T entity) => false;

        private static string SaveFailedMessage => L.T("Prd_SaveFailed");

        public async Task<ActionResult> Index()
        {
            using (var db = new DBTHSNEntities())
            {
                return View(await Active(db).ToListAsync());
            }
        }

        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Create")]
        public async Task<ActionResult> CreatePost()
        {
            var entity = new T();
            using (var db = new DBTHSNEntities())
            {
                try
                {
                    if (TryUpdateModel(entity, "", EditableFields) && ModelState.IsValid)
                    {
                        Set(db).Add(entity);
                        await db.SaveChangesAsync();
                        LogHelper.LogToDatabase(User.Identity.Name, LogModule, CreatedLog(entity));
                        return RedirectToAction("Index");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, ex);
                }
            }
            return View(entity);
        }

        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            using (var db = new DBTHSNEntities())
            {
                var entity = await Set(db).FindAsync(id);
                if (entity == null || IsDeleted(entity))
                    return HttpNotFound();
                return View(entity);
            }
        }

        public async Task<ActionResult> Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            using (var db = new DBTHSNEntities())
            {
                return View(await Set(db).FindAsync(id));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int? id, FormCollection form)
        {
            using (var db = new DBTHSNEntities())
            {
                var entity = await Set(db).FindAsync(id);
                if (entity != null && IsProtected(entity))
                    return RedirectToAction("Delete", new { id });
                if (entity != null)
                {
                    try
                    {
                        MarkDeleted(entity);
                        db.Entry(entity).State = EntityState.Modified;
                        await AfterSoftDeletedAsync(db, entity);
                        await db.SaveChangesAsync();
                        LogHelper.LogToDatabase(User.Identity.Name, LogModule, DeletedLog(entity));
                        return RedirectToAction("Index");
                    }
                    catch (RetryLimitExceededException)
                    {
                        ModelState.AddModelError(string.Empty, SaveFailedMessage);
                    }
                }
                else
                {
                    ModelState.AddModelError("", NotFoundMessage);
                }
            }
            return View();
        }

        public async Task<ActionResult> Edit(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            using (var db = new DBTHSNEntities())
            {
                return View(await Set(db).FindAsync(id));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Edit")]
        public async Task<ActionResult> EditPost(int id)
        {
            using (var db = new DBTHSNEntities())
            {
                var entity = await Set(db).FindAsync(id);
                if (entity == null)
                    return HttpNotFound();

                if (TryUpdateModel(entity, "", EditableFields) && ModelState.IsValid)
                {
                    try
                    {
                        await db.SaveChangesAsync();
                        LogHelper.LogToDatabase(User.Identity.Name, LogModule, UpdatedLog(entity));
                        return RedirectToAction("Index");
                    }
                    catch (RetryLimitExceededException)
                    {
                        ModelState.AddModelError("", SaveFailedMessage);
                    }
                }

                return View(entity);
            }
        }
    }
}
