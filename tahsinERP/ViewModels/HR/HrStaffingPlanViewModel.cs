using System;
using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels.HR
{
    public class HrStaffingPlanViewModel
    {
        public int ID { get; set; }
        
        [Required(ErrorMessage = "Bo'lim tanlanishi shart")]
        [Display(Name = "Bo'lim")]
        public int OrgUnitID { get; set; }
        public string OrgUnitName { get; set; }
        
        [Required(ErrorMessage = "Lavozim tanlanishi shart")]
        [Display(Name = "Lavozim")]
        public int PositionID { get; set; }
        public string PositionName { get; set; }
        
        [Display(Name = "Tarif tarmog'i")]
        public int? SalaryGradeID { get; set; }
        public string SalaryGradeName { get; set; }
        
        [Required(ErrorMessage = "Rejalashtirilgan shtat soni kiritilishi shart")]
        [Display(Name = "Rejalashtirilgan soni")]
        public decimal PlannedHeadcount { get; set; }
        
        [Display(Name = "Ishbay")]
        public bool IsPieceRate { get; set; }
        
        [Display(Name = "Soatbay to'lov")]
        public bool IsHourly { get; set; }
        
        [Display(Name = "Belgilangan oylik")]
        public bool IsFixedSalary { get; set; }
        
        [Required(ErrorMessage = "Amal qilish sanasi kiritilishi shart")]
        [Display(Name = "Amal qiladi")]
        [DataType(DataType.Date)]
        public DateTime EffectiveFrom { get; set; }
        
        [Display(Name = "Amal qiladi to")]
        [DataType(DataType.Date)]
        public DateTime? EffectiveTo { get; set; }
        
        [Display(Name = "Faol")]
        public bool IsActive { get; set; }
        
        public int ActualHeadcount { get; set; }
    }
}

