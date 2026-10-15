using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Mvc;

namespace tahsinERP.ViewModels
{
    public class LogisticsMonthlyReportViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int? ForwarderId { get; set; }
        public string PeriodTitle => new DateTime(Year, Month, 1).ToString("yyyy-MM");
        public string PeriodTitleUz => new DateTime(Year, Month, 1).ToString("MMMM yyyy", new CultureInfo("uz-Latn-UZ"));
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
        public double OpeningDebt { get; set; }
        public double FreightInMonth { get; set; }
        public double PaymentInMonth { get; set; }
        public double ClosingDebt { get; set; }
    }

    public class LogisticsMonthlyReportTotalsViewModel
    {
        public int TotalTrips { get; set; }
        public double TotalOpeningDebt { get; set; }
        public double TotalFreight { get; set; }
        public double TotalPayment { get; set; }
        public double TotalClosingDebt { get; set; }
    }
}
