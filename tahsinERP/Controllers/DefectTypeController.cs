using System.Data.Entity;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Filters;

namespace tahsinERP.Controllers
{
    [Permission]
    public class DefectTypeController : LookupCrudController<DEFECT_TYPES>
    {
        protected override DbSet<DEFECT_TYPES> Set(DBTHSNEntities db) => db.DEFECT_TYPES;

        protected override string[] EditableFields => new[] { "DefectType", "Description" };

        protected override string LogName => "DefectTypeController";

        protected override string LogLabel => "Nuqson Turi";

        protected override string DisplayName(DEFECT_TYPES entity) => entity.DefectType;

        protected override void AfterSoftDelete(DBTHSNEntities db, int id)
        {
            SoftDeleteReferenceService.AfterDefectTypeSoftDeleted(db, id);
        }
    }
}
