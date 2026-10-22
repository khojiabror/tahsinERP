using System;
using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels.HR
{
    public class HrStaffingPlanViewModel
    {
        public int ID { get; set; }
        
        [Required(ErrorMessage = "Hr_Val_SelectRequired")]
        [Display(Name = "Hr_OrgUnit")]
        public int OrgUnitID { get; set; }
        public string OrgUnitName { get; set; }
        
        [Required(ErrorMessage = "Hr_Val_SelectRequired")]
        [Display(Name = "Hr_Position")]
        public int PositionID { get; set; }
        public string PositionName { get; set; }
        
        [Display(Name = "Hr_SalaryGrade")]
        public int? SalaryGradeID { get; set; }
        public string SalaryGradeName { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Hr_PlannedCount")]
        public decimal PlannedHeadcount { get; set; }
        
        [Display(Name = "Hr_PayPiece")]
        public bool IsPieceRate { get; set; }
        
        [Display(Name = "Hr_PayHourlyFull")]
        public bool IsHourly { get; set; }
        
        [Display(Name = "Hr_PayFixedMonthly")]
        public bool IsFixedSalary { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Hr_EffectiveFrom")]
        [DataType(DataType.Date)]
        public DateTime EffectiveFrom { get; set; }
        
        [Display(Name = "Hr_EffectiveTo")]
        [DataType(DataType.Date)]
        public DateTime? EffectiveTo { get; set; }
        
        [Display(Name = "Active")]
        public bool IsActive { get; set; }
        
        public int ActualHeadcount { get; set; }
    }
}

