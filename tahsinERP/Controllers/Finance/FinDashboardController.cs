using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.Models;
using tahsinERP.Services.Finance;
using tahsinERP.ViewModels.Finance;

namespace tahsinERP.Controllers.Finance
{
    /// <summary>
    /// Moliya hisobotlari. Hisob-kitob Services/Finance'da: FinAnalyticsService (pul oqimi, analitika, P&amp;L),
    /// BalanceSheetService (yagona balans engine), CounterpartyBalanceService (AR/AP), LiabilityReportsService,
    /// ValuationService. URL'lar (/FinDashboard/...) o'zgarmagan — menyu shularga bog'langan.
    /// </summary>
    [Permission("FinDashboard")]
    public class FinDashboardController : BaseController
    {
        private static SelectList ActiveAccountsList(DBTHSNEntities db, int? selected)
        {
            return new SelectList(db.FIN_ACCOUNTS.Where(a => a.IsActive).OrderBy(a => a.Name).ToList(), "ID", "Name", selected);
        }

        // GET: FinDashboard/Cashflow
        public ActionResult Cashflow(DateTime? fromDate, DateTime? toDate, int? accountID)
        {
            fromDate = fromDate ?? DateTime.Now.AddMonths(-1);
            toDate = toDate ?? DateTime.Now;

            using (var db = new DBTHSNEntities())
            {
                var r = FinAnalyticsService.Cashflow(db, fromDate.Value, toDate.Value, accountID);

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.AccountID = accountID;
                ViewBag.AccountList = ActiveAccountsList(db, accountID);
                ViewBag.Inflow = r.Inflow;
                ViewBag.Outflow = r.Outflow;
                ViewBag.NetCashflow = r.Inflow - r.Outflow;
                ViewBag.CurrentBalance = r.CurrentBalance;
                ViewBag.CurrencyShortname = r.CurrencyShortname;
                ViewBag.DailyData = r.DailyData;

                return View();
            }
        }

        // GET: FinDashboard/PL
        public ActionResult PL(int? year, int? month)
        {
            year = year ?? DateTime.Now.Year;
            month = month ?? DateTime.Now.Month;

            using (var db = new DBTHSNEntities())
            {
                ViewBag.Year = year;
                ViewBag.Month = month;
                ViewBag.YearList = FinSelectLists.Years(year);
                ViewBag.MonthList = FinSelectLists.Months(month);
                ViewBag.PLData = FinAnalyticsService.ProfitAndLoss(db, year.Value, month.Value);

                return View();
            }
        }

        // GET: FinDashboard/BalanceSheet
        public ActionResult BalanceSheet(DateTime? asOfDate)
        {
            asOfDate = asOfDate ?? DateTime.Now;

            using (var db = new DBTHSNEntities())
            {
                var service = new BalanceSheetService(db);
                ViewBag.AsOfDate = asOfDate;
                ViewBag.BalanceData = service.BalanceSheet(asOfDate.Value);
                ViewBag.Totals = service.Totals(asOfDate.Value);
                return View();
            }
        }

        /// <summary>Aktivlar va passivlar tafsiloti — bitta view (FIN-D19).</summary>
        private ActionResult DetailsView(List<BalanceCategoryDetailData> details, string heading, string totalLabel, bool isLiability)
        {
            ViewBag.Details = details;
            ViewBag.Heading = heading;
            ViewBag.TotalLabel = totalLabel;
            ViewBag.IsLiability = isLiability;
            return View("BalanceSheetDetails");
        }

        // GET: FinDashboard/BalanceSheetLiabilityDetails
        public ActionResult BalanceSheetLiabilityDetails(DateTime? asOfDate)
        {
            asOfDate = asOfDate ?? DateTime.Now;

            using (var db = new DBTHSNEntities())
            {
                var service = new BalanceSheetService(db);
                var details = service.LiabilityDetails(asOfDate.Value);
                ViewBag.Totals = service.Totals(asOfDate.Value);
                ViewBag.AsOfDate = asOfDate;
                ViewBag.LiabilityDetails = details;
                ViewBag.TotalAmount = details.Sum(d => d.Amount);
                return DetailsView(details, "Passivlar va Kapital Tafsiloti", "Jami passivlar va kapital:", true);
            }
        }

        // GET: FinDashboard/BalanceSheetAssetDetails
        public ActionResult BalanceSheetAssetDetails(DateTime? asOfDate)
        {
            asOfDate = asOfDate ?? DateTime.Now;

            using (var db = new DBTHSNEntities())
            {
                var details = new BalanceSheetService(db).AssetDetails(asOfDate.Value);
                ViewBag.AsOfDate = asOfDate;
                ViewBag.AssetDetails = details;
                ViewBag.TotalAmount = details.Sum(d => d.Amount);
                return DetailsView(details, "Aktivlar Tafsiloti", "Jami aktivlar:", false);
            }
        }

        // GET: FinDashboard/BalanceCategoryMovements (popup)
        // Ruxsat yo'q bo'lsa ham popup JSON kutadi — shu sabab tekshiruv action ichida.
        [HttpGet]
        [SkipPermission]
        public JsonResult BalanceCategoryMovements(int categoryId, DateTime asOfDate)
        {
            if (!CanView("FinDashboard"))
            {
                return Json(new { success = false, message = "Ruxsat yo'q" }, JsonRequestBehavior.AllowGet);
            }

            using (var db = new DBTHSNEntities())
            {
                return Json(new BalanceSheetService(db).Movements(categoryId, asOfDate), JsonRequestBehavior.AllowGet);
            }
        }

        // GET: FinDashboard/ShareholderValue
        // method: "ebitda" (EBITDA × multiplikator) yoki "dcf" (sof aktivlar + kelajak foydasi)
        [Permission("Shareholder")]
        public ActionResult ShareholderValue(DateTime? asOfDate, string method, decimal? ebitdaMultiple, int? dcfYears)
        {
            asOfDate = asOfDate ?? DateTime.Now;
            method = (method ?? "ebitda").ToLower() == "dcf" ? "dcf" : "ebitda";

            // EBITDA multiplikatori (ishlab chiqarish: 3–6x), default 3, chegaralar 1–10
            var multiple = Math.Min(10m, Math.Max(1m, ebitdaMultiple ?? 3m));
            // DCF kelajak foydasi yillari, default 3, chegaralar 1–10
            var years = Math.Min(10, Math.Max(1, dcfYears ?? 3));

            using (var db = new DBTHSNEntities())
            {
                var valuation = new ValuationService(db);
                var breakdown = method == "dcf"
                    ? valuation.DcfValuation(asOfDate.Value, years)
                    : valuation.EbitdaValuation(asOfDate.Value, multiple);

                ViewBag.AsOfDate = asOfDate;
                ViewBag.Method = method;
                ViewBag.EbitdaMultiple = multiple;
                ViewBag.DcfYears = years;
                ViewBag.CompanyValue = breakdown.CompanyValue;
                ViewBag.Breakdown = breakdown;
                ViewBag.ShareholderValues = valuation.ShareholderValues(asOfDate.Value, breakdown.CompanyValue);

                return View();
            }
        }

        // GET: FinDashboard/EBITDA (default — oxirgi 12 oy)
        public ActionResult EBITDA(DateTime? fromDate, DateTime? toDate)
        {
            toDate = toDate ?? DateTime.Now.Date;
            fromDate = fromDate ?? toDate.Value.AddYears(-1).AddDays(1);

            using (var db = new DBTHSNEntities())
            {
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.Ebitda = new ValuationService(db).Ebitda(fromDate.Value, toDate.Value);
                return View();
            }
        }

        // GET: FinDashboard/FinancialOverview
        public ActionResult FinancialOverview(DateTime? fromDate, DateTime? toDate)
        {
            fromDate = fromDate ?? DateTime.Now.AddMonths(-1);
            toDate = toDate ?? DateTime.Now;

            using (var db = new DBTHSNEntities())
            {
                var r = FinAnalyticsService.FinancialOverview(db, fromDate.Value, toDate.Value);

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.TotalIncome = r.TotalIncome;
                ViewBag.TotalExpense = r.TotalExpense;
                ViewBag.NetCashflow = r.TotalIncome - r.TotalExpense;
                ViewBag.CashBalance = r.CashBalance;
                ViewBag.BankBalance = r.BankBalance;
                ViewBag.TopExpenseCategories = r.TopExpenseCategories;
                ViewBag.TopIncomeCategories = r.TopIncomeCategories;
                ViewBag.MonthlyData = r.MonthlyData;
                ViewBag.CurrencyShortname = r.CurrencyShortname;

                return View();
            }
        }

        // GET: FinDashboard/CategoryAnalytics
        public ActionResult CategoryAnalytics(DateTime? fromDate, DateTime? toDate, int? categoryID)
        {
            fromDate = fromDate ?? DateTime.Now.AddMonths(-1);
            toDate = toDate ?? DateTime.Now;

            using (var db = new DBTHSNEntities())
            {
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.CategoryID = categoryID;
                ViewBag.CategoryList = new SelectList(
                    db.FIN_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name).ToList(),
                    "ID", "Name", categoryID);
                ViewBag.ExpenseByCategory = FinAnalyticsService.CategoryBreakdown(db, fromDate.Value, toDate.Value, categoryID, 2);
                ViewBag.IncomeByCategory = FinAnalyticsService.CategoryBreakdown(db, fromDate.Value, toDate.Value, categoryID, 1);

                return View();
            }
        }

        // GET: FinDashboard/AccountAnalytics
        public ActionResult AccountAnalytics(DateTime? fromDate, DateTime? toDate, int? accountID)
        {
            fromDate = fromDate ?? DateTime.Now.AddMonths(-1);
            toDate = toDate ?? DateTime.Now;

            using (var db = new DBTHSNEntities())
            {
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.AccountID = accountID;
                ViewBag.AccountList = ActiveAccountsList(db, accountID);
                ViewBag.AccountUsage = FinAnalyticsService.AccountUsage(db, fromDate.Value, toDate.Value, accountID, out var currency);
                ViewBag.CurrencyShortname = currency;

                return View();
            }
        }

        // GET: FinDashboard/ExpenseAnalysis
        public ActionResult ExpenseAnalysis(DateTime? fromDate, DateTime? toDate)
        {
            fromDate = fromDate ?? DateTime.Now.AddMonths(-1);
            toDate = toDate ?? DateTime.Now;

            using (var db = new DBTHSNEntities())
            {
                var r = FinAnalyticsService.DirectionAnalysis(db, fromDate.Value, toDate.Value, 2);
                var byCategory = r.ByCategory.Select(x => new ExpenseByCategoryData { CategoryName = x.Name, Amount = x.Amount }).ToList();
                if (r.Total > 0)
                {
                    byCategory.ForEach(item => item.Percentage = (item.Amount / r.Total) * 100);
                }

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.ExpenseByCategory = byCategory;
                ViewBag.ExpenseByAccount = r.ByAccount.Select(x => new ExpenseByAccountData { AccountName = x.Name, Amount = x.Amount }).ToList();
                ViewBag.MonthlyExpense = r.Monthly;
                ViewBag.TotalExpense = r.Total;
                ViewBag.CurrencyShortname = r.CurrencyShortname;

                return View();
            }
        }

        // GET: FinDashboard/IncomeAnalysis
        public ActionResult IncomeAnalysis(DateTime? fromDate, DateTime? toDate)
        {
            fromDate = fromDate ?? DateTime.Now.AddMonths(-1);
            toDate = toDate ?? DateTime.Now;

            using (var db = new DBTHSNEntities())
            {
                var r = FinAnalyticsService.DirectionAnalysis(db, fromDate.Value, toDate.Value, 1);
                var byCategory = r.ByCategory.Select(x => new IncomeByCategoryData { CategoryName = x.Name, Amount = x.Amount }).ToList();
                if (r.Total > 0)
                {
                    byCategory.ForEach(item => item.Percentage = (item.Amount / r.Total) * 100);
                }

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.IncomeByCategory = byCategory;
                ViewBag.IncomeByAccount = r.ByAccount.Select(x => new IncomeByAccountData { AccountName = x.Name, Amount = x.Amount }).ToList();
                ViewBag.MonthlyIncome = r.Monthly;
                ViewBag.TotalIncome = r.Total;
                ViewBag.CurrencyShortname = r.CurrencyShortname;

                return View();
            }
        }

        // GET: FinDashboard/AccountBalances
        public ActionResult AccountBalances(DateTime? fromDate, DateTime? toDate, int? accountType)
        {
            fromDate = fromDate ?? DateTime.Now.AddMonths(-1);
            toDate = toDate ?? DateTime.Now;

            using (var db = new DBTHSNEntities())
            {
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.AccountBalances = FinAnalyticsService.AccountBalances(db, fromDate.Value, toDate.Value, accountType);
                ViewBag.AccountType = accountType;

                return View();
            }
        }

        // GET: FinDashboard/FinanceReportsHub
        public ActionResult FinanceReportsHub(DateTime? asOfDate)
        {
            var asOf = (asOfDate ?? DateTime.Now).Date;
            using (var db = new DBTHSNEntities())
            {
                return View(new LiabilityReportsService(db).ReportsHub(asOf));
            }
        }

        // GET: FinDashboard/LiabilitiesDashboard
        public ActionResult LiabilitiesDashboard(DateTime? fromDate, DateTime? toDate)
        {
            fromDate = fromDate ?? DateTime.Now.AddMonths(-1);
            toDate = toDate ?? DateTime.Now;

            using (var db = new DBTHSNEntities())
            {
                var r = new LiabilityReportsService(db).Liabilities(fromDate.Value, toDate.Value);

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.PayablesBySupplier = r.PayablesBySupplier;
                ViewBag.ReceivablesByCustomer = r.ReceivablesByCustomer;
                ViewBag.MonthlyPayables = r.MonthlyPayables;
                ViewBag.MonthlyReceivables = r.MonthlyReceivables;
                ViewBag.MonthlyPayrollLiability = r.MonthlyPayrollLiability;
                ViewBag.PayablesAccountBalances = r.PayablesAccountBalances;
                ViewBag.ReceivablesAccountBalances = r.ReceivablesAccountBalances;
                ViewBag.CurrencyShortname = r.CurrencyShortname;
                ViewBag.PassiveBalanceStats = r.PassiveBalanceStats;

                return View();
            }
        }
    }
}
