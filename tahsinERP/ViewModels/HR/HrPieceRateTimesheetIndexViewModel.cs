using System;
using System.Collections.Generic;

namespace tahsinERP.ViewModels.HR
{
    public class HrPieceRateTimesheetIndexViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; }
        public int? OrgUnitID { get; set; }
        public string OrgUnitName { get; set; }
        public decimal CalculatedAmount { get; set; }
        public DateTime CreatedDT { get; set; }
        public int EntryCount { get; set; }
        public string Status { get; set; }
        public DateTime? ApprovedDT { get; set; }
        public int? ApprovedByUserID { get; set; }
    }
}

