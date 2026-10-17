using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Services.Tasks
{
    /// <summary>Tasks ro'yxati filtri (Index sahifasi, IndexData va ExportExcel uchun bir xil).</summary>
    public sealed class TaskFilter
    {
        public int? AssigneeUserID { get; set; }
        public int? StatusID { get; set; }
        public int? PriorityID { get; set; }
        public int? ProjectID { get; set; }
        public DateTime? DueDateFrom { get; set; }
        public DateTime? DueDateTo { get; set; }
        public string Search { get; set; }
        public bool ShowOverdue { get; set; }
    }

    /// <summary>
    /// APP_TASKS so'rovlari: filtr, saralash va SQL tomonidagi proyeksiya. Avval Tasks Index/Details,
    /// TaskProject Details va Kanban'da entity to'liq (Description bilan) yuklanib, qo'lda map qilinardi (dublikat #4).
    /// </summary>
    public static class TaskQueries
    {
        /// <summary>Ro'yxat qatori; Description (nvarchar(max)) olinmaydi — kerak bo'lsa alohida.</summary>
        public static readonly Expression<Func<APP_TASKS, TaskViewModel>> Projection = t => new TaskViewModel
        {
            ID = t.ID,
            KeyCode = t.KeyCode,
            Title = t.Title,
            StatusID = t.StatusID,
            StatusName = t.APP_TASK_STATUS_REF.Name,
            PriorityID = t.PriorityID,
            PriorityName = t.APP_TASK_PRIORITY_REF.Name,
            AssigneeUserID = t.AssigneeUserID,
            ReporterUserID = t.ReporterUserID,
            CreatedDT = t.CreatedDT,
            UpdatedDT = t.UpdatedDT,
            DueDate = t.DueDate,
            ProjectID = t.ProjectID,
            ProjectName = t.APP_TASK_PROJECTS.Name
        };

        /// <summary>O'chirilmagan topshiriqlar; oddiy foydalanuvchi uchun faqat o'ziniki.</summary>
        public static IQueryable<APP_TASKS> Visible(DBTHSNEntities db, int currentUserId, bool isAdminOrDeveloper)
        {
            var query = db.APP_TASKS.AsNoTracking().Where(t => t.IsDeleted == false);
            return isAdminOrDeveloper ? query : query.Where(t => t.AssigneeUserID == currentUserId);
        }

        public static IQueryable<APP_TASKS> Apply(IQueryable<APP_TASKS> query, TaskFilter f)
        {
            if (f.AssigneeUserID.HasValue)
                query = query.Where(t => t.AssigneeUserID == f.AssigneeUserID.Value);
            if (f.StatusID.HasValue)
                query = query.Where(t => t.StatusID == f.StatusID.Value);
            if (f.PriorityID.HasValue)
                query = query.Where(t => t.PriorityID == f.PriorityID.Value);
            if (f.ProjectID.HasValue)
                query = query.Where(t => t.ProjectID == f.ProjectID.Value);
            if (f.DueDateFrom.HasValue)
            {
                var from = f.DueDateFrom.Value.Date;
                query = query.Where(t => t.DueDate >= from);
            }
            if (f.DueDateTo.HasValue)
            {
                // "gacha" kun oxirigacha (avval DueDate <= 00:00 bo'lgani uchun shu kun tushib qolardi)
                var to = f.DueDateTo.Value.Date.AddDays(1);
                query = query.Where(t => t.DueDate < to);
            }
            if (!string.IsNullOrWhiteSpace(f.Search))
            {
                var s = f.Search.Trim();
                query = query.Where(t =>
                    t.Title.Contains(s) ||
                    (t.KeyCode != null && t.KeyCode.Contains(s)) ||
                    (t.Description != null && t.Description.Contains(s)));
            }
            if (f.ShowOverdue)
            {
                var today = DateTime.Today;
                query = query.Where(t =>
                    t.DueDate < today &&
                    t.APP_TASK_STATUS_REF.Code != TaskRules.DoneCode &&
                    !t.APP_TASK_STATUS_REF.Name.Contains("bajarildi"));
            }
            return query;
        }

        /// <summary>Index jadvali ustunlari: 0 Kod, 1 Sarlavha, 2 Mas'ul, 3 Prioritet, 4 Status, 5 Yaratilgan, 6 Muddati.</summary>
        public static IQueryable<APP_TASKS> Order(IQueryable<APP_TASKS> query, int column, bool desc)
        {
            switch (column)
            {
                case 0: return desc ? query.OrderByDescending(t => t.ID) : query.OrderBy(t => t.ID);
                case 1: return desc ? query.OrderByDescending(t => t.Title).ThenByDescending(t => t.ID) : query.OrderBy(t => t.Title).ThenBy(t => t.ID);
                case 3: return desc ? query.OrderByDescending(t => t.APP_TASK_PRIORITY_REF.SortOrder).ThenByDescending(t => t.ID) : query.OrderBy(t => t.APP_TASK_PRIORITY_REF.SortOrder).ThenBy(t => t.ID);
                case 4: return desc ? query.OrderByDescending(t => t.APP_TASK_STATUS_REF.SortOrder).ThenByDescending(t => t.ID) : query.OrderBy(t => t.APP_TASK_STATUS_REF.SortOrder).ThenBy(t => t.ID);
                case 6: return desc ? query.OrderByDescending(t => t.DueDate).ThenByDescending(t => t.ID) : query.OrderBy(t => t.DueDate).ThenBy(t => t.ID);
                default: return desc ? query.OrderByDescending(t => t.CreatedDT).ThenByDescending(t => t.ID) : query.OrderBy(t => t.CreatedDT).ThenBy(t => t.ID);
            }
        }

        /// <summary>Mas'ul va muallif ismlari bitta so'rovda (TSK-P01).</summary>
        public static async Task<List<TaskViewModel>> WithNamesAsync(DBTHSNEntities db, List<TaskViewModel> rows)
        {
            var names = await TaskLookups.UserNamesAsync(db, rows.SelectMany(t => new[] { t.AssigneeUserID, t.ReporterUserID }));
            foreach (var r in rows)
            {
                r.AssigneeFullName = TaskLookups.NameOf(names, r.AssigneeUserID);
                r.ReporterFullName = TaskLookups.NameOf(names, r.ReporterUserID);
            }
            return rows;
        }
    }
}
