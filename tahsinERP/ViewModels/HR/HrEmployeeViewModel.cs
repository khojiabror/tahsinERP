using System;
using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels.HR
{
    public class HrEmployeeViewModel
    {
        public int ID { get; set; }
        
        [Required(ErrorMessage = "Табельный номер обязателен")]
        [Display(Name = "Табельный номер")]
        public string TabNo { get; set; }
        
        [Required(ErrorMessage = "ФИО обязательно")]
        [Display(Name = "Ф.И.О.")]
        public string FullName { get; set; }
        
        [Display(Name = "Подразделение")]
        public int? OrgUnitID { get; set; }
        public string OrgUnitName { get; set; }
        
        [Display(Name = "Должность")]
        public int? PositionID { get; set; }
        public string PositionName { get; set; }
        
        [Display(Name = "Базовая зарплата")]
        public decimal? BaseSalary { get; set; }

        [Display(Name = "Моддий жавобгарлик, %")]
        [Range(0, 100, ErrorMessage = "Qiymat 0 dan 100 gacha bo'lishi kerak")]
        public decimal? MaterialResponsibilityPercent { get; set; }

        [Display(Name = "Махсус устама, %")]
        [Range(0, 100, ErrorMessage = "Qiymat 0 dan 100 gacha bo'lishi kerak")]
        public decimal? SpecialAllowancePercent { get; set; }
        
        [Display(Name = "Тарифная сетка")]
        public int? SalaryGradeID { get; set; }
        public string SalaryGradeName { get; set; }
        
        [Display(Name = "Дата приема")]
        [DataType(DataType.Date)]
        public DateTime? EmploymentDate { get; set; }
        
        [Display(Name = "Дата увольнения")]
        [DataType(DataType.Date)]
        public DateTime? DismissalDate { get; set; }
        
        [Display(Name = "Активен")]
        public bool IsActive { get; set; }
    }
}

