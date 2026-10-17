// EF Database-First entity'larini (Models/*.cs, generatsiya qilinadi) o'zgartirmasdan partial orqali interfeys qo'shiladi.
namespace tahsinERP.Models
{
    /// <summary>Topshiriq spravochniklari (status, prioritet, loyiha) uchun umumiy ko'rinish.</summary>
    public interface ITaskRefEntity
    {
        int ID { get; set; }
        string Code { get; set; }
        string Name { get; set; }
        bool? IsDeleted { get; set; }
    }

    public partial class APP_TASK_STATUS_REF : ITaskRefEntity { }

    public partial class APP_TASK_PRIORITY_REF : ITaskRefEntity { }

    public partial class APP_TASK_PROJECTS : ITaskRefEntity { }
}
