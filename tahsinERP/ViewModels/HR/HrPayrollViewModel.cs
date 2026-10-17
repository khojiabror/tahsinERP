using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels.HR
{
    public class HrPayrollHeaderViewModel
    {
        public int ID { get; set; }
        
        [Required(ErrorMessage = "Год обязателен")]
        [Display(Name = "Год")]
        public int Year { get; set; }
        
        [Required(ErrorMessage = "Месяц обязателен")]
        [Display(Name = "Месяц")]
        public int Month { get; set; }
        
        [Display(Name = "Подразделение")]
        public int? OrgUnitID { get; set; }
        public string OrgUnitName { get; set; }
        
        [Display(Name = "Статус")]
        public string Status { get; set; }
        
        [Display(Name = "Тип расчета")]
        public string PayrollType { get; set; }
        
        public int? TimesheetID { get; set; }
        public string TimesheetInfo { get; set; }
        
        public int? CreatedByUserID { get; set; }
        public DateTime CreatedDT { get; set; }
        public int? ApprovedByUserID { get; set; }
        public DateTime? ApprovedDT { get; set; }
        
        public List<HrPayrollLineViewModel> Lines { get; set; }
        
        public decimal TotalGrossSalary { get; set; }
        public decimal TotalNetSalary { get; set; }
        public decimal IncomeTaxPercent { get; set; }
        /// <summary>Daromad solig'i qo'llanadimi (HRM-B18: soatbay va ishbay — ikkalasiga ham).</summary>
        public bool ApplyIncomeTax { get; set; }
        public decimal TotalIncomeTax { get; set; }
        
        public HrPayrollHeaderViewModel()
        {
            Lines = new List<HrPayrollLineViewModel>();
        }
    }
    
    public class HrPayrollLineViewModel
    {
        public int ID { get; set; }
        public int PayrollID { get; set; }
        public int EmployeeID { get; set; }
        public string EmployeeTabNo { get; set; }
        public string EmployeeFullName { get; set; }
        public string PositionName { get; set; }
        public string OrgUnitName { get; set; }
        
        public decimal BaseSalary { get; set; }
        public decimal HoursWorked { get; set; }
        public decimal PieceQty { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal GrossSalary { get; set; }
        public decimal IncomeTaxAmount { get; set; }
        public decimal Bonuses { get; set; }
        public decimal Penalties { get; set; }
        public decimal NetSalary { get; set; }
        public int? CurrencyID { get; set; }
        public string CurrencyCode { get; set; }
        public string Notes { get; set; }
        
        public bool IsFixedSalary { get; set; }
        public bool IsHourly { get; set; }
        public bool IsPieceRate { get; set; }
    }
}

