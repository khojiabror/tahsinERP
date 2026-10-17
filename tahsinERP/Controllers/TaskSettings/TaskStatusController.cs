using System;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Controllers.TaskSettings
{
    public class TaskStatusController : TaskRefDataController<APP_TASK_STATUS_REF>
    {
        private static readonly string[] Fields = { "Code", "Name", "SortOrder", "IsActive" };

        protected override DbSet<APP_TASK_STATUS_REF> Set(DBTHSNEntities db) => db.APP_TASK_STATUS_REF;

        protected override IQueryable<APP_TASK_STATUS_REF> Ordered(IQueryable<APP_TASK_STATUS_REF> query) => query.OrderBy(s => s.SortOrder);

        protected override string[] EditableFields => Fields;

        protected override Expression<Func<APP_TASKS, bool>> UsedBy(int id) => t => t.StatusID == id;

        protected override string LogLabel => "Status";

        protected override string InUseMessage => "Bu status vazifalarda ishlatilmoqda. O'chirish mumkin emas.";

        protected override async Task OnCreatingAsync(DBTHSNEntities db, APP_TASK_STATUS_REF entity)
        {
            // SortOrder kiritilmasa — oxiriga
            if (entity.SortOrder == 0)
            {
                entity.SortOrder = (await db.APP_TASK_STATUS_REF.MaxAsync(s => (int?)s.SortOrder) ?? 0) + 1;
            }
        }
    }
}
