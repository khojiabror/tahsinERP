using System;
using System.Collections.Generic;
using tahsinERP.I18n;

namespace tahsinERP.ViewModels
{
    /// <summary>
    /// Ombor oylik hisoboti (MRP topshirig'i) — kirim/chiqim va qoldiqlar.
    /// Barcha omborlar uchun: xomashyo, ishlab chiqarish, savdo. Dynamic — ma'lumotlar INCOME/EXPENSE dan hisoblanadi.
    /// </summary>
    public class WarehouseMonthlyReportViewModel
    {
        public int? SelectedWarehouseId { get; set; }
        public string SelectedWarehouseName { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public string PeriodTitle => new DateTime(Year, Month, 1).ToString("yyyy-MM");
        /// <summary>Joriy til bo'yicha oy nomi va yil (nomi tarixiy).</summary>
        public string PeriodTitleUz => L.T("Month_" + Month) + " " + Year;
        public DateTime PeriodStart => new DateTime(Year, Month, 1);
        public DateTime PeriodEnd => PeriodStart.AddMonths(1).AddDays(-1);
        public string ReportDate { get; set; }
        public string CompanyName { get; set; }
        /// <summary> "quantity" — miqdor bo'yicha, "amount" — summa bo'yicha </summary>
        public string ViewMode { get; set; } = "quantity";
        public List<WarehouseMonthlyReportRowViewModel> Rows { get; set; } = new List<WarehouseMonthlyReportRowViewModel>();
        public WarehouseMonthlyReportTotalsViewModel Totals { get; set; }
        public List<System.Web.Mvc.SelectListItem> Warehouses { get; set; } = new List<System.Web.Mvc.SelectListItem>();
    }

    /// <summary>
    /// Hisobot satri — bitta mahsulot bo'yicha oldingi qoldiq, kirim, chiqim, oy oxiridagi qoldiq.
    /// </summary>
    public class WarehouseMonthlyReportRowViewModel
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public string UnitName { get; set; }
        public double OpeningBalance { get; set; }
        public double AdjustmentQty { get; set; }
        public double IncomeQty { get; set; }
        public double ExpenseQty { get; set; }
        public double ClosingBalance { get; set; }
        /// <summary> Summa bo'yicha: oldingi qoldiq (jami narx) </summary>
        public decimal OpeningBalanceSum { get; set; }
        public decimal AdjustmentSum { get; set; }
        public decimal IncomeSum { get; set; }
        public decimal ExpenseSum { get; set; }
        public decimal ClosingBalanceSum { get; set; }
    }

    /// <summary>
    /// Jami qator (subtotal/total).
    /// </summary>
    public class WarehouseMonthlyReportTotalsViewModel
    {
        public double TotalOpening { get; set; }
        public double TotalAdjustment { get; set; }
        public double TotalIncome { get; set; }
        public double TotalExpense { get; set; }
        public double TotalClosing { get; set; }
        public decimal TotalOpeningSum { get; set; }
        public decimal TotalAdjustmentSum { get; set; }
        public decimal TotalIncomeSum { get; set; }
        public decimal TotalExpenseSum { get; set; }
        public decimal TotalClosingSum { get; set; }
    }
}
