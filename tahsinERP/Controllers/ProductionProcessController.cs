using System.Data.Entity;
using tahsinERP.Models;
using tahsinERP.Filters;

namespace tahsinERP.Controllers
{
    [Permission]
    public class ProductionProcessController : LookupCrudController<PRODUCTIONPROCESS>
    {
        protected override DbSet<PRODUCTIONPROCESS> Set(DBTHSNEntities db) => db.PRODUCTIONPROCESSES;

        protected override string[] EditableFields => new[] { "ProcessName", "Description" };

        protected override string LogName => "ProductionProccessController";

        protected override string LogLabel => "ProductProccess";

        protected override string DisplayName(PRODUCTIONPROCESS entity) => entity.ProcessName;
    }
}
