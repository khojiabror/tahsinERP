using System.Collections.Generic;

namespace tahsinERP.ViewModels.HR
{
    public class HrOrgUnitStaffingPlanViewModel
    {
        public int PositionID { get; set; }
        public string PositionName { get; set; }
        public int PlannedHeadcount { get; set; }
        public List<string> AssignedEmployees { get; set; }
        public int AssignedCount { get; set; }
    }
}

