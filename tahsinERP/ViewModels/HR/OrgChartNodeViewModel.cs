using System.Collections.Generic;

namespace tahsinERP.ViewModels.HR
{
    public class OrgChartNodeViewModel
    {
        public int ID { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public int LevelNo { get; set; }
        public List<OrgChartEmployeeViewModel> Employees { get; set; } = new List<OrgChartEmployeeViewModel>();
        public List<OrgChartPositionViewModel> Positions { get; set; } = new List<OrgChartPositionViewModel>();
        public List<OrgChartNodeViewModel> Children { get; set; } = new List<OrgChartNodeViewModel>();
    }

    public class OrgChartEmployeeViewModel
    {
        public string FullName { get; set; }
        public string PositionName { get; set; }
        public string TabNo { get; set; }
    }

    public class OrgChartPositionViewModel
    {
        public string PositionName { get; set; }
        public int PlannedHeadcount { get; set; }
        public int ActualHeadcount { get; set; }
    }
}
