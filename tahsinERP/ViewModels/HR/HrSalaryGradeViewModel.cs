using System;
using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels.HR
{
    public class HrSalaryGradeViewModel
    {
        public int ID { get; set; }
        
        [Required(ErrorMessage = "Код тарифной сетки обязателен")]
        [Display(Name = "Код")]
        public string GradeCode { get; set; }
        
        [Required(ErrorMessage = "Название обязательно")]
        [Display(Name = "Название")]
        public string Name { get; set; }
        
        [Required(ErrorMessage = "Минимальная зарплата обязательна")]
        [Display(Name = "Мин. зарплата")]
        public decimal MinSalary { get; set; }
        
        [Required(ErrorMessage = "Максимальная зарплата обязательна")]
        [Display(Name = "Макс. зарплата")]
        public decimal MaxSalary { get; set; }
        
        [Required(ErrorMessage = "Валюта обязательна")]
        [Display(Name = "Валюта")]
        public string CurrencyCode { get; set; }
        
        [Required(ErrorMessage = "Дата начала действия обязательна")]
        [Display(Name = "Действует с")]
        [DataType(DataType.Date)]
        public DateTime EffectiveFrom { get; set; }
        
        [Display(Name = "Действует до")]
        [DataType(DataType.Date)]
        public DateTime? EffectiveTo { get; set; }
    }
}

