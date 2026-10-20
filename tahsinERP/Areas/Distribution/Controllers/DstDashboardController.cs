using System;
using System.Collections.Generic;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Reports;
using tahsinERP.Services.Common;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>
    /// Rahbar paneli: kunlik holat bir ekranda (<see cref="DashboardService"/>). Kassa/bank qoldiqlari — faqat cheklovsiz
    /// (scope'siz) foydalanuvchiga, tannarx, foyda va qoldiq qiymati — <see cref="DstPermissions.SeeCost"/> bilan.
    /// </summary>
    public class DstDashboardController : DstControllerBase
    {
        private DashboardService.Model Load(DateTime? date)
        {
            var day = (date ?? DateTime.Today).Date;
            if (day > DateTime.Today) day = DateTime.Today;
            using (var uow = NewUnitOfWork())
            {
                var scope = ReportScope.For(uow.Dst, CurrentUserName);
                return DashboardService.Build(uow, day, scope, CanSeeCost, scope.IsUnrestricted);
            }
        }

        public ActionResult Index(DateTime? date)
        {
            return View(Load(date));
        }

        private sealed class Indicator
        {
            public string Group { get; set; }
            public string Name { get; set; }
            public object Value { get; set; }
        }

        public ActionResult Export(DateTime? date)
        {
            var m = Load(date);
            var rows = new List<Indicator>();
            void Add(string group, string name, object value) => rows.Add(new Indicator { Group = group, Name = name, Value = value });

            string today = DstLocalization.T("Dash_Today"), month = DstLocalization.T("Dash_Month");
            Add(today, DstLocalization.T("Report_Revenue"), m.Day.Net);
            Add(month, DstLocalization.T("Report_Revenue"), m.Month.Net);
            if (CanSeeCost)
            {
                Add(today, DstLocalization.T("Report_GrossProfit"), m.Day.Profit);
                Add(month, DstLocalization.T("Report_GrossProfit"), m.Month.Profit);
            }
            Add(today, DstLocalization.T("Returns"), m.Day.ReturnAmount);
            Add(month, DstLocalization.T("Returns"), m.Month.ReturnAmount);
            Add(today, DstLocalization.T("Dash_Collected"), m.CollectedToday);
            Add(month, DstLocalization.T("Dash_Collected"), m.CollectedMonth);
            Add(today, DstLocalization.T("Dash_NewOrders"), m.OrdersTodayTotal);
            Add(DstLocalization.T("Orders"), DstLocalization.T("Dash_AwaitingConfirm"), m.AwaitingConfirmTotal);
            Add(DstLocalization.T("Orders"), DstLocalization.T("Dash_AwaitingShipment"), m.AwaitingShipmentTotal);

            string debt = DstLocalization.T("Debt");
            Add(debt, DstLocalization.T("Debt"), m.Debt.Total);
            Add(debt, DstLocalization.T("Aging_NotDue"), m.Debt.NotDue);
            Add(debt, "1–7", m.Debt.Days1To7);
            Add(debt, "8–30", m.Debt.Days8To30);
            Add(debt, "31–60", m.Debt.Days31To60);
            Add(debt, "60+", m.Debt.Over60);
            Add(debt, DstLocalization.T("Overdue"), m.Debt.Overdue);
            Add(debt, DstLocalization.T("Aging_Advance"), m.Debt.Advance);

            string money = DstLocalization.T("Dash_Money");
            if (m.Accounts != null)
                foreach (var a in m.Accounts) Add(money, a.Name + " (" + a.Currency + ")", a.Balance);
            Add(money, DstLocalization.T("Dash_AgentsOnHand"), m.AgentsOnHand);
            Add(money, DstLocalization.T("Dash_PendingHandovers"), m.PendingHandovers);

            if (m.Stock != null)
            {
                string stock = DstLocalization.T("Dash_StockValue");
                foreach (var w in m.Stock) Add(stock, w.Warehouse, w.Value);
            }

            var bytes = ExcelExport.Create(DstLocalization.T("Dashboard"), rows,
                ExcelExport.Col<Indicator>(DstLocalization.T("Dash_Section"), r => r.Group),
                ExcelExport.Col<Indicator>(DstLocalization.T("Dash_Indicator"), r => r.Name),
                ExcelExport.Col<Indicator>(m.Date.ToString("dd.MM.yyyy"), r => r.Value, "#,##0"));
            return File(bytes, ExcelExport.ContentType, string.Format("dashboard_{0:yyyyMMdd}.xlsx", m.Date));
        }
    }
}
