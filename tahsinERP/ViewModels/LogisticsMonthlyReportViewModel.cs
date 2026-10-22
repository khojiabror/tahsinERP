using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Mvc;

using tahsinERP.I18n;

namespace tahsinERP.ViewModels
{
    public class LogisticsMonthlyReportViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int? ForwarderId { get; set; }
        public string PeriodTitle => new DateTime(Year, Month, 1).ToString("yyyy-MM");
        public string PeriodTitleUz => L.T("Month_" + Month) + " " + Year;
        public string ReportDate { get; set; }
        public string CompanyName { get; set; }
        public List<LogisticsMonthlyReportRowViewModel> Rows { get; set; } = new List<LogisticsMonthlyReportRowViewModel>();
        public LogisticsMonthlyReportTotalsViewModel Totals { get; set; } = new LogisticsMonthlyReportTotalsViewModel();
        public List<SelectListItem> Forwarders { get; set; } = new List<SelectListItem>();
    }

    public class LogisticsMonthlyReportRowViewModel
    {
        public int? ForwarderId { get; set; }
        public string ForwarderName { get; set; }
        public int TripsInMonth { get; set; }
        public decimal OpeningDebt { get; set; }
        public decimal FreightInMonth { get; set; }
        public decimal PaymentInMonth { get; set; }
        public decimal ClosingDebt { get; set; }
    }

    public class LogisticsMonthlyReportTotalsViewModel
    {
        public int TotalTrips { get; set; }
        public decimal TotalOpeningDebt { get; set; }
        public decimal TotalFreight { get; set; }
        public decimal TotalPayment { get; set; }
        public decimal TotalClosingDebt { get; set; }
    }
}
