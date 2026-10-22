using System;
using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels.HR
{
    public class HrSalaryGradeViewModel
    {
        public int ID { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Code")]
        public string GradeCode { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Name")]
        public string Name { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Hr_MinSalary")]
        public decimal MinSalary { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Hr_MaxSalary")]
        public decimal MaxSalary { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Currency")]
        public string CurrencyCode { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Hr_EffectiveFrom")]
        [DataType(DataType.Date)]
        public DateTime EffectiveFrom { get; set; }
        
        [Display(Name = "Hr_EffectiveTo")]
        [DataType(DataType.Date)]
        public DateTime? EffectiveTo { get; set; }
    }
}

