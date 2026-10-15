using System;
using System.Collections.Generic;

namespace tahsinERP.ViewModels.HR
{
    public class HrEmployeeHistoryGroupedViewModel
    {
        public int EmployeeID { get; set; }
        public string EmployeeName { get; set; }
        public string TabNo { get; set; }
        public int TotalTenureYears { get; set; }
        public int TotalTenureMonths { get; set; }
        public int TotalTenureDays { get; set; }
        public List<HrEmployeeHistoryViewModel> Positions { get; set; }
    }
}

