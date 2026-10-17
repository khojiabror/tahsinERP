using System.Data.Entity;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Filters;

namespace tahsinERP.Controllers
{
    [Permission]
    public class ShopController : LookupCrudController<SHOP>
    {
        protected override DbSet<SHOP> Set(DBTHSNEntities db) => db.SHOPS;

        // CompanyID formadan olinmaydi (avval Edit'da formadan o'zgartirish mumkin edi, MES-B17)
        protected override string[] EditableFields => new[] { "ShopName", "Description" };

        protected override string LogName => "ShopController";

        protected override string LogLabel => "Shop";

        protected override string DisplayName(SHOP entity) => entity.ShopName;

        protected override void BeforeCreate(SHOP entity)
        {
            entity.CompanyID = 1;
        }

        protected override void AfterSoftDelete(DBTHSNEntities db, int id)
        {
            SoftDeleteReferenceService.AfterShopSoftDeleted(db, id);
        }
    }
}
