using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels
{
    public class TaskChangeStatusViewModel
    {
        public long TaskID { get; set; }
        
        [Display(Name = "Code")]
        public string KeyCode { get; set; }
        
        [Display(Name = "Task_Title")]
        public string Title { get; set; }
        
        [Display(Name = "Task_CurrentStatus")]
        public int CurrentStatusID { get; set; }
        
        [Display(Name = "Task_CurrentStatus")]
        public string CurrentStatusName { get; set; }
        
        [Required(ErrorMessage = "Task_StatusSelectRequired")]
        [Display(Name = "Task_NewStatusStar")]
        public int StatusID { get; set; }
        
        [Display(Name = "Comment")]
        [DataType(DataType.MultilineText)]
        public string UserComment { get; set; }
    }
}
