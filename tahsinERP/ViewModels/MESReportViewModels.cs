using System;
using System.Collections.Generic;

namespace tahsinERP.ViewModels
{
    public class ProductionMonthlyReportViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<ProductionMonthlyReportRow> Rows { get; set; }
        public double TotalReja { get; set; }
        public double TotalFakt { get; set; }
        public double TotalFarq { get; set; }
    }

    public class ProductionMonthlyReportRow
    {
        public int ProductID { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public double Reja { get; set; }
        public double Fakt { get; set; }
        public double Farq { get; set; }
        public double Qoldiq { get; set; }
    }

    public class QualityMonthlyReportViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<QualityMonthlyReportRow> Rows { get; set; }
        public List<TopDefectRow> TopDefects { get; set; }
        public double TotalTekshirilgan { get; set; }
        public double TotalQabul { get; set; }
        public double TotalRad { get; set; }
        public double? FTQPercent { get; set; }
    }

    public class QualityMonthlyReportRow
    {
        public int ProductID { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public double IshlabChiqarilgan { get; set; }
        public int TekshiruvlarSoni { get; set; }
        public double TekshirilganMiqdor { get; set; }
        public double QabulQilingan { get; set; }
        public double RadEtilgan { get; set; }
        public double? FTQPercent { get; set; }
    }

    public class TopDefectRow
    {
        public int? DefectTypeID { get; set; }
        public string DefectName { get; set; }
        public double RejectedQty { get; set; }
        public int Count { get; set; }
    }
}
