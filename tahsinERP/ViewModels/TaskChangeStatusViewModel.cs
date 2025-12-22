using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels
{
    public class TaskChangeStatusViewModel
    {
        public long TaskID { get; set; }
        
        [Display(Name = "Kod")]
        public string KeyCode { get; set; }
        
        [Display(Name = "Sarlavha")]
        public string Title { get; set; }
        
        [Display(Name = "Joriy Status")]
        public int CurrentStatusID { get; set; }
        
        [Display(Name = "Joriy Status")]
        public string CurrentStatusName { get; set; }
        
        [Required(ErrorMessage = "Status tanlash majburiy")]
        [Display(Name = "Yangi Status *")]
        public int StatusID { get; set; }
        
        [Display(Name = "Izoh")]
        [DataType(DataType.MultilineText)]
        public string UserComment { get; set; }
    }
}
