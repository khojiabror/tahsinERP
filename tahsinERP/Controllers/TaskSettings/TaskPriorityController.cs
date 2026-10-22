using System;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using tahsinERP.Models;
using tahsinERP.I18n;

namespace tahsinERP.Controllers.TaskSettings
{
    public class TaskPriorityController : TaskRefDataController<APP_TASK_PRIORITY_REF>
    {
        private static readonly string[] Fields = { "Code", "Name", "SortOrder", "ColorHex" };

        protected override DbSet<APP_TASK_PRIORITY_REF> Set(DBTHSNEntities db) => db.APP_TASK_PRIORITY_REF;

        protected override IQueryable<APP_TASK_PRIORITY_REF> Ordered(IQueryable<APP_TASK_PRIORITY_REF> query) => query.OrderBy(p => p.SortOrder);

        protected override string[] EditableFields => Fields;

        protected override Expression<Func<APP_TASKS, bool>> UsedBy(int id) => t => t.PriorityID == id;

        protected override string LogLabel => "Priority";

        protected override string InUseMessage => L.T("Task_PriorityInUse");

        protected override async Task OnCreatingAsync(DBTHSNEntities db, APP_TASK_PRIORITY_REF entity)
        {
            // SortOrder kiritilmasa — oxiriga
            if (entity.SortOrder == 0)
            {
                entity.SortOrder = (await db.APP_TASK_PRIORITY_REF.MaxAsync(p => (int?)p.SortOrder) ?? 0) + 1;
            }
        }
    }
}
