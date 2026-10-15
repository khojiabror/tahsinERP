using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace tahsinERP.ViewModels.Reports
{
    public class SRMCoverageViewModel
    {
        public int? SelectedWarehouseId { get; set; }
        public IEnumerable<SelectListItem> Warehouses { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int? CategoryId { get; set; }
        public IEnumerable<SelectListItem> Categories { get; set; }
        public string ProductType { get; set; }
        /// <summary>
        /// Ko'rinish rejimi: "daily", "weekly", "monthly"
        /// </summary>
        public string ViewMode { get; set; }
        public List<DateTime> Dates { get; set; } = new List<DateTime>();
        public List<SRMCoverageRowViewModel> Rows { get; set; } = new List<SRMCoverageRowViewModel>();
    }

    public class SRMCoverageRowViewModel
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public string UnitName { get; set; }
        public double CurrentStock { get; set; }
        public double PlannedTotal { get; set; }
        public double ActualTotal { get; set; }
        public string ABCClass { get; set; }
        public string XYZClass { get; set; }
        public double? SafetyDOH { get; set; }
        public List<SRMCoverageDayCellViewModel> DayCells { get; set; } = new List<SRMCoverageDayCellViewModel>();
    }

    public class SRMCoverageDayCellViewModel
    {
        public DateTime Date { get; set; }
        public double? CoverageInDays { get; set; }
    }
}

