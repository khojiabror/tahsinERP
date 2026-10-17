using System;
using System.Collections.Generic;
using tahsinERP.Services.DlocFifo;

namespace tahsinERP.ViewModels
{
    // InventoryReports/Dashboard modellari (SRM-M02: avval controller faylida e'lon qilingan edi).
    /// <summary>
    /// Inventory Dashboard ViewModel
    /// </summary>
    public class InventoryDashboardViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<InventoryDashboardItem> HighDOHItems { get; set; }
        public List<InventoryDashboardItem> LowDOHItems { get; set; }
        public List<InventoryDashboardItem> StockItems { get; set; }
        public List<InventoryDashboardItem> HighRunnerItems { get; set; }
        public List<InventoryDashboardItem> LowRunnerItems { get; set; }
        public List<InventoryFifoViolationItem> FifoJournal { get; set; }
        public List<InventoryFifoViolationItem> FifoHeuristicAlerts { get; set; }
        public int DlocBelowMinCount { get; set; }
        public int DlocNearMaxCount { get; set; }
        public List<DlocCapacityAlertRow> DlocAlerts { get; set; }
        public List<CategoryStockSummary> StockByCategory { get; set; }
        public List<TrendData> InboundTrends { get; set; }
        public List<TrendData> OutboundTrends { get; set; }
    }

    public class InventoryDashboardItem
    {
        public int ProductID { get; set; }
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        public string CategoryName { get; set; }
        public string WarehouseName { get; set; }
        public double CurrentStock { get; set; }
        public string UnitName { get; set; }
        public double AverageDailyConsumption { get; set; }
        public double PlannedAverageDailyConsumption { get; set; }
        public double? PlannedDOH { get; set; }
        public double? ActualDOH { get; set; }
    }

    public class InventoryFifoViolationItem
    {
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        public string WarehouseName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime OldestIncomeDate { get; set; }
        public DateTime LatestExpenseDate { get; set; }
        public double RemainingFromOldest { get; set; }
        public string ReasonCode { get; set; }
        public string ExpenseDocNo { get; set; }
        public string ExpectedLotNo { get; set; }
        public string SelectedLotNo { get; set; }
        public string CreatedBy { get; set; }
    }

    internal static class InventoryReportsFifoMapper
    {
        public static InventoryFifoViolationItem MapFifoJournalRow(FifoViolationReportRow v)
        {
            return new InventoryFifoViolationItem
            {
                ProductPNo = v.ProductPNo ?? "N/A",
                ProductName = v.ProductName ?? "N/A",
                WarehouseName = v.WarehouseName ?? "N/A",
                CreatedAt = v.CreatedAt,
                OldestIncomeDate = v.ExpectedReceivedAt ?? v.CreatedAt,
                LatestExpenseDate = v.SelectedReceivedAt ?? v.CreatedAt,
                RemainingFromOldest = 0,
                ReasonCode = v.ReasonCode,
                ExpenseDocNo = v.ExpenseDocNo,
                ExpectedLotNo = v.ExpectedLotNo,
                SelectedLotNo = v.SelectedLotNo,
                CreatedBy = v.CreatedBy
            };
        }
    }

    public class CategoryStockSummary
    {
        public string CategoryName { get; set; }
        public double TotalStock { get; set; }
    }

    public class TrendData
    {
        public DateTime Date { get; set; }
        public double Amount { get; set; }
    }
}
