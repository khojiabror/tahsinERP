using System;
using System.Collections.Generic;

namespace tahsinERP.ViewModels
{
    public class TaskKanbanViewModel
    {
        public string ViewType { get; set; } = "project"; // "project" or "status"
        public bool IsAdminOrDeveloper { get; set; }
        public List<KanbanBoardViewModel> ProjectBoards { get; set; } = new List<KanbanBoardViewModel>();
        public List<KanbanBoardViewModel> StatusBoards { get; set; } = new List<KanbanBoardViewModel>();
    }

    public class KanbanBoardViewModel
    {
        public string ID { get; set; }
        public string Title { get; set; }
        public List<KanbanTaskViewModel> Tasks { get; set; } = new List<KanbanTaskViewModel>();
    }

    public class KanbanTaskViewModel
    {
        public long ID { get; set; }
        public string KeyCode { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int StatusID { get; set; }
        public string StatusName { get; set; }
        public int? PriorityID { get; set; }
        public string PriorityName { get; set; }
        public int? AssigneeUserID { get; set; }
        public string AssigneeFullName { get; set; }
        public int? ProjectID { get; set; }
        public string ProjectName { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime CreatedDT { get; set; }
    }
}
