using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Filters;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Common;

namespace tahsinERP.Controllers.Settings
{
    /// <summary>O'lchov birliklari spravochnigi (umumiy CRUD — <see cref="ReferenceCrudController{T}"/>).</summary>
    [Permission]
    public class UnitController : ReferenceCrudController<UNIT>
    {
        protected override DbSet<UNIT> Set(DBTHSNEntities db) => db.UNITS;

        protected override IQueryable<UNIT> Active(DBTHSNEntities db) =>
            db.UNITS.Where(u => u.IsDeleted == false || u.IsDeleted == null);

        protected override bool IsDeleted(UNIT unit) => unit.IsDeleted == true;

        protected override void MarkDeleted(UNIT unit) => unit.IsDeleted = true;

        protected override Task AfterSoftDeletedAsync(DBTHSNEntities db, UNIT unit) =>
            SoftDeleteReferenceService.AfterUnitSoftDeletedAsync(db, unit.ID);

        // Tizim birligi (SystemCode) — kod unga bog'langan, o'chirilmaydi
        protected override bool IsProtected(UNIT unit) => SystemCodes.IsSystem(unit.SystemCode);

        protected override string[] EditableFields => new[] { "UnitName", "ShortName", "Code" };

        protected override string LogModule => "O'lchov birliklar";

        protected override string NotFoundMessage => "Bunday o'lchov birligi topilmadi.";

        protected override string CreatedLog(UNIT unit) => $"{unit.ShortName} - birligini kiritdi tizimga";

        protected override string UpdatedLog(UNIT unit) => $"{unit.UnitName} - ni tahrirladi";

        protected override string DeletedLog(UNIT unit) => $"{unit.UnitName} - o'lchov birligini o'chirdi";
    }
}
