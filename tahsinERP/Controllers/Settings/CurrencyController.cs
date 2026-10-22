using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Filters;
using tahsinERP.Models;
using tahsinERP.Services;

namespace tahsinERP.Controllers.Settings
{
    /// <summary>Valyutalar spravochnigi (umumiy CRUD — <see cref="ReferenceCrudController{T}"/>).</summary>
    [Permission]
    public class CurrencyController : ReferenceCrudController<CURRENCy>
    {
        protected override DbSet<CURRENCy> Set(DBTHSNEntities db) => db.CURRENCIES;

        protected override IQueryable<CURRENCy> Active(DBTHSNEntities db) =>
            db.CURRENCIES.Where(c => c.IsDeleted == false || c.IsDeleted == null);

        protected override bool IsDeleted(CURRENCy currency) => currency.IsDeleted == true;

        protected override void MarkDeleted(CURRENCy currency) => currency.IsDeleted = true;

        protected override Task AfterSoftDeletedAsync(DBTHSNEntities db, CURRENCy currency) =>
            SoftDeleteReferenceService.AfterCurrencySoftDeletedAsync(db, currency.ID);

        protected override string[] EditableFields => new[] { "Longname", "Shortname", "Description" };

        protected override string LogModule => "Valyutalar";

        protected override string NotFoundMessage => tahsinERP.I18n.L.T("Prd_CurrencyNotFound");

        protected override string CreatedLog(CURRENCy currency) => $"{currency.Shortname} - valyuta birligini kiritdi tizimga";

        protected override string UpdatedLog(CURRENCy currency) => $"{currency.Shortname} - valyutasini tahrirladi";

        protected override string DeletedLog(CURRENCy currency) => $"{currency.Shortname} - valyutani o'chirdi";
    }
}
