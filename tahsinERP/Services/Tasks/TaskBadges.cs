using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;

namespace tahsinERP.Services.Tasks
{
    /// <summary>
    /// Status/prioritet badge'lari va ranglari — avval 7 ta view'da (Tasks Index/Details, TaskProject Details,
    /// Kanban, Dashboard) nom bo'yicha bir xil if-zanjiri nusxalangan edi (MNT-01).
    /// Faqat ID/Code va ref jadval ustunlari (ColorHex, IsFinal) ishlatiladi — nom bo'yicha qidiruv yo'q
    /// (status nomi o'zgarsa yoki boshqa tilda bo'lsa ham to'g'ri ishlaydi).
    /// </summary>
    public static class TaskBadges
    {
        public const string DoneColor = "#28A745";
        public const string InProgressColor = "#FFC107";
        public const string CancelledColor = "#DC3545";
        public const string TodoColor = "#6C757D";
        public const string PriorityDefaultColor = "#DC3545";

        private static readonly Regex HexColor = new Regex("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

        /// <summary>Faqat #RRGGBB (rang style atributiga yoziladi — boshqa qiymat e'tiborsiz).</summary>
        private static string ValidColor(string color) => color != null && HexColor.IsMatch(color) ? color : null;

        private static TaskLookups.Item Status(int? statusId)
        {
            return statusId == null ? null : TaskLookups.AllStatuses().FirstOrDefault(st => st.ID == statusId.Value);
        }

        private static TaskLookups.Item Priority(int? priorityId)
        {
            return priorityId == null ? null : TaskLookups.AllPriorities().FirstOrDefault(p => p.ID == priorityId.Value);
        }

        /// <summary>Kod bo'yicha standart rang (ColorHex bo'sh bo'lsa).</summary>
        private static string DefaultStatusColor(string code)
        {
            switch (code)
            {
                case TaskRules.DoneCode: return DoneColor;
                case "IN_PROGRESS": return InProgressColor;
                case TaskRules.CancelledCode: return CancelledColor;
                default: return TodoColor;
            }
        }

        private static string DefaultPriorityColor(string code)
        {
            switch (code)
            {
                case "CRITICAL": return "#DC3545";
                case "HIGH": return "#FF6666";
                case "NORMAL":
                case "MEDIUM": return "#FFCCCC";
                default: return null;
            }
        }

        private static string StatusColor(TaskLookups.Item status)
        {
            return ValidColor(status?.ColorHex) ?? DefaultStatusColor(status?.Code);
        }

        private static string PriorityColorOrNull(TaskLookups.Item priority)
        {
            return ValidColor(priority?.ColorHex) ?? DefaultPriorityColor(priority?.Code);
        }

        /// <summary>Muddati o'tgan va yopilmagan (IsFinal) — "Muddatidan o'tgan" filtri bilan bir xil (kun bo'yicha).</summary>
        public static bool IsOverdue(DateTime? dueDate, int? statusId)
        {
            return dueDate.HasValue && dueDate.Value < DateTime.Today && !TaskRules.IsClosedStatus(Status(statusId));
        }

        /// <summary>Status rangi (grafik uchun).</summary>
        public static string StatusColor(int? statusId) => StatusColor(Status(statusId));

        /// <summary>Status rangi kod bo'yicha (Dashboard guruhlari kod bilan keladi).</summary>
        public static string StatusColorByCode(string code)
        {
            return StatusColor(TaskLookups.AllStatuses().FirstOrDefault(st => st.Code == code)) ?? DefaultStatusColor(code);
        }

        /// <summary>Prioritet rangi; aniqlanmasa null.</summary>
        public static string PriorityColorOrNull(int? priorityId) => PriorityColorOrNull(Priority(priorityId));

        /// <summary>Prioritet rangi kod bo'yicha (grafik uchun; aniqlanmasa standart qizil).</summary>
        public static string PriorityColorByCode(string code)
        {
            var priority = TaskLookups.AllPriorities().FirstOrDefault(p => p.Code == code);
            return (priority != null ? PriorityColorOrNull(priority) : DefaultPriorityColor(code)) ?? PriorityDefaultColor;
        }

        public static string StatusBadgeClass(int? statusId)
        {
            switch (Status(statusId)?.Code)
            {
                case TaskRules.DoneCode: return "badge-success";
                case "IN_PROGRESS": return "badge-warning";
                case TaskRules.CancelledCode: return "badge-danger";
                default: return "badge-secondary";
            }
        }

        /// <summary>Status uchun inline style ("" — status topilmasa).</summary>
        public static string StatusStyle(int? statusId)
        {
            var status = Status(statusId);
            return status == null ? "" : ColorStyle(StatusColor(status));
        }

        public static IHtmlString TaskStatusBadge(this HtmlHelper html, int? statusId, string emptyText = "", string extraClass = null)
        {
            return Badge(StatusBadgeClass(statusId), StatusStyle(statusId), Status(statusId)?.Name ?? emptyText, extraClass);
        }

        public static IHtmlString TaskPriorityBadge(this HtmlHelper html, int? priorityId, string emptyText = "", string extraClass = null)
        {
            var priority = Priority(priorityId);
            string color = PriorityColorOrNull(priority);
            return Badge("badge-danger", color == null ? "" : ColorStyle(color), priority?.Name ?? emptyText, extraClass);
        }

        private static string ColorStyle(string color)
        {
            return "background-color: " + color + " !important; border-color: " + color + " !important;";
        }

        private static IHtmlString Badge(string cssClass, string style, string text, string extraClass)
        {
            var tag = new TagBuilder("span");
            tag.AddCssClass("badge");
            tag.AddCssClass(cssClass);
            if (!string.IsNullOrEmpty(extraClass))
                tag.AddCssClass(extraClass);
            if (!string.IsNullOrEmpty(style))
                tag.MergeAttribute("style", style);
            tag.SetInnerText(text ?? "");
            return new HtmlString(tag.ToString());
        }
    }
}
