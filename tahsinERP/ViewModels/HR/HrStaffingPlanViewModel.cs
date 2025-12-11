using System;
using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels.HR
{
    public class HrStaffingPlanViewModel
    {
        public int ID { get; set; }
        
        [Required(ErrorMessage = "Подразделение обязательно")]
        [Display(Name = "Подразделение")]
        public int OrgUnitID { get; set; }
        public string OrgUnitName { get; set; }
        
        [Required(ErrorMessage = "Должность обязательна")]
        [Display(Name = "Должность")]
        public int PositionID { get; set; }
        public string PositionName { get; set; }
        
        [Display(Name = "Тарифная сетка")]
        public int? SalaryGradeID { get; set; }
        public string SalaryGradeName { get; set; }
        
        [Required(ErrorMessage = "Плановое количество штатных единиц обязательно")]
        [Display(Name = "Плановое количество")]
        public decimal PlannedHeadcount { get; set; }
        
        [Display(Name = "Сдельная оплата")]
        public bool IsPieceRate { get; set; }
        
        [Display(Name = "Почасовая оплата")]
        public bool IsHourly { get; set; }
        
        [Display(Name = "Фиксированная зарплата")]
        public bool IsFixedSalary { get; set; }
        
        [Required(ErrorMessage = "Дата начала действия обязательна")]
        [Display(Name = "Действует с")]
        [DataType(DataType.Date)]
        public DateTime EffectiveFrom { get; set; }
        
        [Display(Name = "Действует до")]
        [DataType(DataType.Date)]
        public DateTime? EffectiveTo { get; set; }
        
        [Display(Name = "Активен")]
        public bool IsActive { get; set; }
        
        public int ActualHeadcount { get; set; }
    }
}

