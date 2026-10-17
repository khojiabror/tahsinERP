using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace tahsinERP.Services.Tasks
{
    /// <summary>
    /// Status/prioritet ranglari — avval 7 ta view'da (Tasks Index/Details, TaskProject Details,
    /// Kanban, Dashboard) nom bo'yicha bir xil if-zanjiri nusxalangan edi (MNT-01).
    /// Rang va "yopilgan" belgisi ref jadvallardan (ColorHex, IsFinal — MNT-01/TSK-B09); jadvalda bo'lmasa
    /// (masalan, nomi o'zgargan eski yozuv) avvalgi nom qoidasi zaxira sifatida ishlaydi.
    /// </summary>
    public static class TaskBadges
    {
        public const string DoneColor = "#28A745";
        public const string InProgressColor = "#FFC107";
        public const string CancelledColor = "#DC3545";
        public const string TodoColor = "#6C757D";

        private enum StatusKind { Other, Done, InProgress, Cancelled, Todo }

        private static StatusKind Kind(string statusName)
        {
            string n = statusName?.ToLowerInvariant() ?? "";
            if (n.Contains("bajarildi") || n.Contains("done") || n.Contains("tugallangan")) return StatusKind.Done;
            if (n.Contains("jarayonda") || n.Contains("in_progress") || n.Contains("in progress")) return StatusKind.InProgress;
            if (n.Contains("bekor") || n.Contains("cancel")) return StatusKind.Cancelled;
            if (n.Contains("rejada") || n.Contains("todo") || n.Contains("planned")) return StatusKind.Todo;
            return StatusKind.Other;
        }

        /// <summary>Status nomi "bajarilgan"ni bildiradimi (ref jadvalda bo'lmagan nomlar uchun zaxira qoida).</summary>
        public static bool IsDoneName(string statusName) => Kind(statusName) == StatusKind.Done;

        private static readonly System.Text.RegularExpressions.Regex HexColor =
            new System.Text.RegularExpressions.Regex("^#[0-9A-Fa-f]{6}$", System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>Faqat #RRGGBB (rang style atributiga yoziladi — boshqa qiymat e'tiborsiz).</summary>
        private static string ValidColor(string color) => color != null && HexColor.IsMatch(color) ? color : null;

        private static TaskLookups.Item FindStatus(string statusName)
        {
            return statusName == null ? null : TaskLookups.AllStatuses().FirstOrDefault(st => st.Name == statusName);
        }

        private static TaskLookups.Item FindPriority(string priorityName)
        {
            return priorityName == null ? null : TaskLookups.Priorities().FirstOrDefault(p => p.Name == priorityName);
        }

        /// <summary>Yopilgan status (IsFinal); jadvalda topilmasa — nom bo'yicha "bajarildi"/"bekor".</summary>
        public static bool IsClosedName(string statusName)
        {
            var status = FindStatus(statusName);
            if (status != null)
                return status.IsFinal;
            var kind = Kind(statusName);
            return kind == StatusKind.Done || kind == StatusKind.Cancelled;
        }

        /// <summary>Muddati o'tgan va yopilmagan (muddat kuni o'tmaguncha — "Muddatidan o'tgan" filtri bilan bir xil).</summary>
        public static bool IsOverdue(DateTime? dueDate, string statusName)
        {
            return dueDate.HasValue && dueDate.Value < DateTime.Today && !IsClosedName(statusName);
        }

        /// <summary>Status rangi (grafik uchun; noma'lum — kulrang).</summary>
        public static string StatusColor(string statusName)
        {
            var color = ValidColor(FindStatus(statusName)?.ColorHex);
            if (!string.IsNullOrEmpty(color))
                return color;
            switch (Kind(statusName))
            {
                case StatusKind.Done: return DoneColor;
                case StatusKind.InProgress: return InProgressColor;
                case StatusKind.Cancelled: return CancelledColor;
                default: return TodoColor;
            }
        }

        /// <summary>Prioritet rangi; mos kelmasa null.</summary>
        public static string PriorityColorOrNull(string priorityName)
        {
            var color = ValidColor(FindPriority(priorityName)?.ColorHex);
            if (!string.IsNullOrEmpty(color))
                return color;
            string n = priorityName?.ToLowerInvariant() ?? "";
            if (n.Contains("juda muhim") || n.Contains("critical")) return "#DC3545";
            if (n.Contains("muhim") || n.Contains("high")) return "#FF6666";
            if (n.Contains("normal") || n.Contains("medium")) return "#FFCCCC";
            return null;
        }

        public static string PriorityColor(string priorityName) => PriorityColorOrNull(priorityName) ?? "#DC3545";

        public static string StatusBadgeClass(string statusName)
        {
            switch (Kind(statusName))
            {
                case StatusKind.Done: return "badge-success";
                case StatusKind.InProgress: return "badge-warning";
                case StatusKind.Cancelled: return "badge-danger";
                default: return "badge-secondary";
            }
        }

        /// <summary>Status uchun inline style ("" — rangsiz).</summary>
        public static string StatusStyle(string statusName)
        {
            bool hasColor = ValidColor(FindStatus(statusName)?.ColorHex) != null || Kind(statusName) != StatusKind.Other;
            return hasColor ? ColorStyle(StatusColor(statusName)) : "";
        }

        public static IHtmlString TaskStatusBadge(this HtmlHelper html, string statusName, string emptyText = "", string extraClass = null)
        {
            return Badge(StatusBadgeClass(statusName), StatusStyle(statusName), statusName ?? emptyText, extraClass);
        }

        public static IHtmlString TaskPriorityBadge(this HtmlHelper html, string priorityName, string emptyText = "", string extraClass = null)
        {
            string color = PriorityColorOrNull(priorityName);
            return Badge("badge-danger", color == null ? "" : ColorStyle(color), priorityName ?? emptyText, extraClass);
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
