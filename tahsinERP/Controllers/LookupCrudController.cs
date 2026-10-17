using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.Models;

namespace tahsinERP.Models
{
    /// <summary>ID + soft-delete belgisi bor oddiy spravochnik (DEFECT_TYPES, SHOPS, PRODUCTIONPROCESSES).</summary>
    public interface ISoftDeleteLookup
    {
        int ID { get; set; }
        bool? IsDeleted { get; set; }
    }

    // EDMX klasslari partial — interfeys shu yerda qo'shiladi (Models/*.cs o'zgartirilmaydi)
    public partial class DEFECT_TYPES : ISoftDeleteLookup { }
    public partial class SHOP : ISoftDeleteLookup { }
    public partial class PRODUCTIONPROCESS : ISoftDeleteLookup { }
}

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Oddiy spravochnik CRUD'i uchun umumiy asos. Avval DefectType, Shop va ProductionProcess controllerlari
    /// faqat nomlari bilan farq qiladigan nusxalar edi (audit MES dublikat #20). URL va view'lar o'zgarmaydi.
    /// Tahrirlashda faqat <see cref="EditableFields"/> bog'lanadi (IsDeleted/CompanyID formadan olinmaydi — MES-B17).
    /// </summary>
    [Permission]
    public abstract class LookupCrudController<T> : BaseController where T : class, ISoftDeleteLookup, new()
    {
        private const string SaveErrorMessage = "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.";

        protected abstract DbSet<T> Set(DBTHSNEntities db);

        /// <summary>Formadan bog'lanadigan maydonlar.</summary>
        protected abstract string[] EditableFields { get; }

        /// <summary>Log uchun controller nomi.</summary>
        protected abstract string LogName { get; }

        /// <summary>Log matni: "{nom} - {label}ni yaratdi/tahrirladi/o'chirdi".</summary>
        protected abstract string LogLabel { get; }

        protected abstract string DisplayName(T entity);

        protected virtual void BeforeCreate(T entity)
        {
        }

        /// <summary>Soft delete'dan keyingi bog'lanishlar (SoftDeleteReferenceService).</summary>
        protected virtual void AfterSoftDelete(DBTHSNEntities db, int id)
        {
        }

        public ActionResult Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                return View(Set(db).AsNoTracking().Where(x => x.IsDeleted == false).ToList());
            }
        }

        public ActionResult Details(int? id)
        {
            return FindView(id);
        }

        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        [ActionName("Create")]
        public ActionResult CreatePost()
        {
            var entity = new T();
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    if (TryUpdateModel(entity, "", EditableFields) && ModelState.IsValid)
                    {
                        entity.IsDeleted = false;
                        BeforeCreate(entity);
                        Set(db).Add(entity);
                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, LogName, $"{DisplayName(entity)} - {LogLabel}ni yaratdi");
                        return RedirectToAction("Index");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("{0}.Create: {1}", LogName, ex);
                    ModelState.AddModelError("", SaveErrorMessage);
                }
                return View(entity);
            }
        }

        public ActionResult Edit(int? id)
        {
            return FindView(id);
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        [ActionName("Edit")]
        public ActionResult EditPost(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var entity = Set(db).Find(id);
                if (entity == null || entity.IsDeleted == true)
                {
                    return HttpNotFound();
                }

                if (TryUpdateModel(entity, "", EditableFields) && ModelState.IsValid)
                {
                    try
                    {
                        db.SaveChanges();
                        LogHelper.LogToDatabase(User.Identity.Name, LogName, $"{DisplayName(entity)} - {LogLabel}ni tahrirladi");
                        return RedirectToAction("Index");
                    }
                    catch (RetryLimitExceededException)
                    {
                        ModelState.AddModelError("", SaveErrorMessage);
                    }
                }
                return View(entity);
            }
        }

        public ActionResult Delete(int? id)
        {
            return FindView(id);
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        [ActionName("Delete")]
        public ActionResult DeletePost(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var entity = Set(db).Find(id);
                if (entity == null)
                {
                    return HttpNotFound();
                }

                try
                {
                    entity.IsDeleted = true;
                    AfterSoftDelete(db, entity.ID);
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, LogName, $"{DisplayName(entity)} - {LogLabel}ni o'chirdi");
                    return RedirectToAction("Index");
                }
                catch (RetryLimitExceededException)
                {
                    ModelState.AddModelError("", SaveErrorMessage);
                }
                return View(entity);
            }
        }

        private ActionResult FindView(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var entity = Set(db).Find(id);
                if (entity == null)
                {
                    return HttpNotFound();
                }
                return View(entity);
            }
        }
    }
}
