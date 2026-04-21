using System;
using System.Collections.Generic;

namespace tahsinERP.ViewModels.HR
{
    public class HrDashboardViewModel
    {
        public int TotalHeadcount { get; set; }
        public int ActiveHeadcount { get; set; }
        public int PlannedHeadcount { get; set; }
        public decimal TotalMonthlyPayroll { get; set; }
        public decimal TotalOvertimeHours { get; set; }
        public decimal TotalWorkedHours { get; set; }
        public decimal TotalPieceQuantity { get; set; }
        public decimal TotalHourlyCalculated { get; set; }
        public decimal TotalPieceCalculated { get; set; }
        public decimal TotalSalaryPaidFromFin { get; set; }
        public decimal TotalSalaryBalance { get; set; }
        
        public List<HrHeadcountByOrgUnitViewModel> HeadcountByOrgUnit { get; set; }
        public List<HrPayrollTrendViewModel> PayrollTrend { get; set; }
        public List<HrHeadcountTrendViewModel> HeadcountTrend { get; set; }
        
        public HrDashboardViewModel()
        {
            HeadcountByOrgUnit = new List<HrHeadcountByOrgUnitViewModel>();
            PayrollTrend = new List<HrPayrollTrendViewModel>();
            HeadcountTrend = new List<HrHeadcountTrendViewModel>();
        }
    }
    
    public class HrHeadcountByOrgUnitViewModel
    {
        public int OrgUnitID { get; set; }
        public string OrgUnitName { get; set; }
        public int PlannedHeadcount { get; set; }
        public int ActualHeadcount { get; set; }
    }
    
    public class HrPayrollTrendViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal TotalPayroll { get; set; }
    }
    
    public class HrHeadcountTrendViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int Headcount { get; set; }
    }
}

