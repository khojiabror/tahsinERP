using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace tahsinERP.ViewModels.Reports
{
    public class SRMSafetyStockViewModel
    {
        public int? SelectedWarehouseId { get; set; }
        public IEnumerable<SelectListItem> Warehouses { get; set; }
        public int? CategoryId { get; set; }
        public IEnumerable<SelectListItem> Categories { get; set; }
        public string ProductType { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int ServiceLevel { get; set; }
        public int LeadTimeDays { get; set; }
        public List<SRMSafetyStockItemViewModel> Items { get; set; } = new List<SRMSafetyStockItemViewModel>();
    }

    public class SRMSafetyStockItemViewModel
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public string UnitName { get; set; }

        public double AverageDailyDemand { get; set; }
        public double StdDevDailyDemand { get; set; }
        public double SafetyStock { get; set; }
        public double ReorderPoint { get; set; }
        public double? SafetyDOH { get; set; }
    }
}

