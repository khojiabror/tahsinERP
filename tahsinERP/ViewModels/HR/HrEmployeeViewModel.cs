using System;
using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels.HR
{
    public class HrEmployeeViewModel
    {
        public int ID { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Hr_TabNumber")]
        public string TabNo { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Hr_FullName")]
        public string FullName { get; set; }
        
        [Display(Name = "Hr_OrgUnit")]
        public int? OrgUnitID { get; set; }
        public string OrgUnitName { get; set; }
        
        [Display(Name = "Hr_Position")]
        public int? PositionID { get; set; }
        public string PositionName { get; set; }
        
        [Display(Name = "Hr_BaseSalary")]
        public decimal? BaseSalary { get; set; }

        [Display(Name = "Hr_MaterialRespPct")]
        [Range(0, 100, ErrorMessage = "Val_Range")]
        public decimal? MaterialResponsibilityPercent { get; set; }

        [Display(Name = "Hr_SpecialAllowancePct")]
        [Range(0, 100, ErrorMessage = "Val_Range")]
        public decimal? SpecialAllowancePercent { get; set; }
        
        [Display(Name = "Hr_SalaryGrade")]
        public int? SalaryGradeID { get; set; }
        public string SalaryGradeName { get; set; }
        
        [Display(Name = "Hr_HireDate")]
        [DataType(DataType.Date)]
        public DateTime? EmploymentDate { get; set; }
        
        [Display(Name = "Hr_DismissalDate")]
        [DataType(DataType.Date)]
        public DateTime? DismissalDate { get; set; }
        
        [Display(Name = "Active")]
        public bool IsActive { get; set; }
    }
}

