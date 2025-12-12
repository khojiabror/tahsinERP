using System.Collections.Generic;

namespace tahsinERP.ViewModels
{
    public class TaskDashboardViewModel
    {
        public int UserTaskCount { get; set; }
        public int UserDoneCount { get; set; }
        public int UserInProgressCount { get; set; }
        public int UserTodoCount { get; set; }
        public string UserName { get; set; }
        public int OverdueCount { get; set; }
        public int RecentTasksCount { get; set; }
        public List<TaskStatusStatViewModel> StatusStatistics { get; set; } = new List<TaskStatusStatViewModel>();
        public List<TaskPriorityStatViewModel> PriorityStatistics { get; set; } = new List<TaskPriorityStatViewModel>();
    }

    public class TaskStatusStatViewModel
    {
        public string StatusCode { get; set; }
        public string StatusName { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }

    public class TaskPriorityStatViewModel
    {
        public string PriorityCode { get; set; }
        public string PriorityName { get; set; }
        public int TotalCount { get; set; }
        public int DoneCount { get; set; }
        public int InProgressCount { get; set; }
        public int TodoCount { get; set; }
    }
}
