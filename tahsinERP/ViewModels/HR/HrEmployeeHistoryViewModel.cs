using System;

namespace tahsinERP.ViewModels.HR
{
    public class HrEmployeeHistoryViewModel
    {
        public int ID { get; set; }
        public int EmployeeID { get; set; }
        public string EmployeeName { get; set; }
        public string TabNo { get; set; }
        public string ActionType { get; set; }
        public string FromOrgUnitName { get; set; }
        public string FromPositionName { get; set; }
        public DateTime? EffectiveDate { get; set; }
        public string Reason { get; set; }
        public DateTime? EmploymentDate { get; set; }
        public int TenureYears { get; set; }
        public int TenureMonths { get; set; }
        public int TenureDays { get; set; }
    }
}

