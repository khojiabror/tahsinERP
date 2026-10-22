using System;
using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels
{
    public class TaskViewModel
    {
        public long ID { get; set; }

        [Display(Name = "Code")]
        public string KeyCode { get; set; }

        [Required(ErrorMessage = "Task_TitleRequired")]
        [Display(Name = "Task_Title")]
        [StringLength(500)]
        public string Title { get; set; }

        [Display(Name = "Description")]
        [DataType(DataType.MultilineText)]
        public string Description { get; set; }

        [Required(ErrorMessage = "Task_StatusRequired")]
        [Display(Name = "Status")]
        public int StatusID { get; set; }

        [Display(Name = "Status")]
        public string StatusName { get; set; }

        [Required(ErrorMessage = "Task_PriorityRequired")]
        [Display(Name = "Task_Priority")]
        public int PriorityID { get; set; }

        [Display(Name = "Task_Priority")]
        public string PriorityName { get; set; }

        [Display(Name = "Task_Assignee")]
        public int? AssigneeUserID { get; set; }

        [Display(Name = "Task_Assignee")]
        public string AssigneeFullName { get; set; }

        [Display(Name = "Task_Reporter")]
        public int? ReporterUserID { get; set; }

        [Display(Name = "Task_Reporter")]
        public string ReporterFullName { get; set; }

        [Display(Name = "CreatedAt")]
        [DataType(DataType.DateTime)]
        public DateTime CreatedDT { get; set; }

        [Display(Name = "Task_UpdatedAt")]
        [DataType(DataType.DateTime)]
        public DateTime? UpdatedDT { get; set; }

        [Display(Name = "Task_DueDate")]
        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }

        [Display(Name = "Task_Project")]
        public int? ProjectID { get; set; }

        [Display(Name = "Task_Project")]
        public string ProjectName { get; set; }

        [Display(Name = "Comment")]
        public string UserComment { get; set; }
    }
}
