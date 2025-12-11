using System;
using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels
{
    public class TaskViewModel
    {
        public long ID { get; set; }

        [Display(Name = "Key Code")]
        public string KeyCode { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [Display(Name = "Title")]
        [StringLength(500)]
        public string Title { get; set; }

        [Display(Name = "Description")]
        [DataType(DataType.MultilineText)]
        public string Description { get; set; }

        [Required(ErrorMessage = "Status is required")]
        [Display(Name = "Status")]
        public int StatusID { get; set; }

        [Display(Name = "Status")]
        public string StatusName { get; set; }

        [Required(ErrorMessage = "Priority is required")]
        [Display(Name = "Priority")]
        public int PriorityID { get; set; }

        [Display(Name = "Priority")]
        public string PriorityName { get; set; }

        [Display(Name = "Assignee")]
        public int? AssigneeUserID { get; set; }

        [Display(Name = "Assignee")]
        public string AssigneeFullName { get; set; }

        [Display(Name = "Reporter")]
        public int? ReporterUserID { get; set; }

        [Display(Name = "Reporter")]
        public string ReporterFullName { get; set; }

        [Display(Name = "Created Date")]
        [DataType(DataType.DateTime)]
        public DateTime CreatedDT { get; set; }

        [Display(Name = "Updated Date")]
        [DataType(DataType.DateTime)]
        public DateTime? UpdatedDT { get; set; }

        [Display(Name = "Due Date")]
        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }

        [Display(Name = "Project")]
        public int? ProjectID { get; set; }

        [Display(Name = "Project")]
        public string ProjectName { get; set; }
    }
}
