using System;
using System.Collections.Generic;

namespace tahsinERP.ViewModels.HR
{
    public class HrOrgUnitViewModel
    {
        public int ID { get; set; }
        public int? ParentID { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public int LevelNo { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDT { get; set; }
        public string ParentName { get; set; }
        public int PlannedHeadcount { get; set; }
        public int ActualHeadcount { get; set; }
        public List<HrOrgUnitViewModel> Children { get; set; }
        public List<HrOrgUnitStaffingPlanViewModel> StaffingPlans { get; set; }
        
        public HrOrgUnitViewModel()
        {
            Children = new List<HrOrgUnitViewModel>();
            StaffingPlans = new List<HrOrgUnitStaffingPlanViewModel>();
        }
    }
}

