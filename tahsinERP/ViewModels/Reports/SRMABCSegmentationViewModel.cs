using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace tahsinERP.ViewModels.Reports
{
    public class SRMABCSegmentationViewModel
    {
        public int? SelectedWarehouseId { get; set; }
        public IEnumerable<SelectListItem> Warehouses { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<SRMABCItemViewModel> Items { get; set; } = new List<SRMABCItemViewModel>();
    }

    public class SRMABCItemViewModel
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public string UnitName { get; set; }
        public double TotalDemand { get; set; }
        public double SharePercent { get; set; }
        public double CumSharePercent { get; set; }
        public string ABCClass { get; set; }
        public double CoefficientOfVariation { get; set; }
        public string XYZClass { get; set; }
    }
}

