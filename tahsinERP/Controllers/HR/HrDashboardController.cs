using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Finance;
using tahsinERP.Services.HR;
using tahsinERP.ViewModels.HR;

namespace tahsinERP.Controllers.HR
{
    [Authorize]
    public class HrDashboardController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();
        private IUserActionLogger logger;

        public HrDashboardController()
        {
            logger = new UserActionLogger(User?.Identity?.Name ?? "System");
        }

        // Olish so'rovi: HR/Dashboard
        public async Task<ActionResult> Index(int? year, int? month, int? orgUnitId)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrDashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            year = year ?? DateTime.Now.Year;
            month = month ?? DateTime.Now.Month;
            var periodStart = new DateTime(year.Value, month.Value, 1);
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);

            var viewModel = new HrDashboardViewModel();
            // Oylik ochilish qoldig'i — Finance qoidasi bilan (avval xom SQL + xatoni yutuvchi try/catch, HRM-D15)
            var balances = new CounterpartyBalanceService(db);
            var latestOpeningDate = balances.LatestOpeningDate(periodEnd, CounterpartyBalanceService.BalanceTypePayroll);
            var openingPayrollRows = latestOpeningDate.HasValue
                ? balances.OpeningContractBalances(latestOpeningDate.Value, CounterpartyBalanceService.BalanceTypePayroll)
                : new List<OpeningContractBalance>();
            var openingPayrollTotal = openingPayrollRows.Sum(x => x.TotalAmount);
            var openingPayrollPaid = openingPayrollRows.Sum(x => x.PaidAmount);

            // Jami xodimlar (o'chirilganlar hisobga olinmaydi)
            viewModel.TotalHeadcount = await db.HR_EMPLOYEES.CountAsync(e => e.IsActive && e.IsDeleted != true);
            viewModel.ActiveHeadcount = viewModel.TotalHeadcount;

            viewModel.PlannedHeadcount = (int)(await db.HR_STAFFING_PLAN
                .Where(sp => sp.IsActive && sp.IsDeleted != true)
                .SumAsync(sp => (decimal?)sp.PlannedHeadcount) ?? 0);

            // Oylik ish haqi — SQL'da yig'iladi (avval qatorlar xotiraga yuklanardi, HRM-P09)
            var payrollSums = await db.HR_PAYROLL_LINES.AsNoTracking()
                .Where(l => (l.IsDeleted == null || l.IsDeleted == false) &&
                            (l.HR_PAYROLL_HEADERS.IsDeleted == null || l.HR_PAYROLL_HEADERS.IsDeleted == false) &&
                            l.HR_PAYROLL_HEADERS.Year == year.Value && l.HR_PAYROLL_HEADERS.Month == month.Value &&
                            (orgUnitId == null ? l.HR_PAYROLL_HEADERS.OrgUnitID == null : l.HR_PAYROLL_HEADERS.OrgUnitID == orgUnitId.Value))
                .GroupBy(l => l.HR_PAYROLL_HEADERS.PayrollType)
                .Select(g => new { PayrollType = g.Key, Net = g.Sum(l => l.NetSalary) })
                .ToListAsync();

            viewModel.TotalHourlyCalculated = payrollSums.Where(x => x.PayrollType == "Hourly").Sum(x => x.Net ?? 0);
            viewModel.TotalPieceCalculated = payrollSums.Where(x => x.PayrollType == "PieceRate").Sum(x => x.Net ?? 0);
            viewModel.TotalMonthlyPayroll = openingPayrollTotal + payrollSums.Sum(x => x.Net ?? 0);

            // Qo'shimcha va ishlangan soatlar
            var timesheetTotals = await db.HR_TIMESHEET_LINES.AsNoTracking()
                .Where(l => (l.IsDeleted == null || l.IsDeleted == false) &&
                            l.HR_TIMESHEET_HEADERS.Year == year.Value && l.HR_TIMESHEET_HEADERS.Month == month.Value &&
                            (l.HR_TIMESHEET_HEADERS.IsDeleted == null || l.HR_TIMESHEET_HEADERS.IsDeleted == false) &&
                            (orgUnitId == null ? l.HR_TIMESHEET_HEADERS.OrgUnitID == null : l.HR_TIMESHEET_HEADERS.OrgUnitID == orgUnitId.Value))
                .GroupBy(l => l.TimesheetID)
                .Select(g => new { TimesheetID = g.Key, Overtime = g.Sum(l => l.OvertimeHours), Worked = g.Sum(l => l.TotalWorkedHours) })
                .OrderBy(x => x.TimesheetID)
                .FirstOrDefaultAsync();
            if (timesheetTotals != null)
            {
                viewModel.TotalOvertimeHours = timesheetTotals.Overtime ?? 0;
                viewModel.TotalWorkedHours = timesheetTotals.Worked ?? 0;
            }

            viewModel.TotalPieceQuantity = await db.HR_PIECE_RATE_WORK_ENTRIES
                .Where(e => (e.IsDeleted == null || e.IsDeleted == false) &&
                            e.WorkDate >= periodStart &&
                            e.WorkDate <= periodEnd &&
                            (orgUnitId == null || e.HR_EMPLOYEES.OrgUnitID == orgUnitId.Value))
                .SumAsync(e => (decimal?)e.Quantity) ?? 0m;

            // To'langan oylik: Finance'ning yagona qoidasi ("oylik" toifasi, xodim kontragenti yoki HR_PAYROLL chiqimi, HRM-D16)
            var directSalaryPaid = await FinQueries.PayrollPayments(db, FinQueries.SalaryCategoryIds(db), periodStart, periodEnd)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            viewModel.TotalSalaryPaidFromFin = openingPayrollPaid + directSalaryPaid;
            viewModel.TotalSalaryBalance = viewModel.TotalMonthlyPayroll - viewModel.TotalSalaryPaidFromFin;

            // Bo'limlar kesimida: ikki guruhlangan so'rov (avval har bo'limga 2 ta so'rov, HRM-P09)
            var plannedByOrg = await db.HR_STAFFING_PLAN.AsNoTracking()
                .Where(sp => sp.IsActive && sp.IsDeleted != true)
                .GroupBy(sp => sp.OrgUnitID)
                .Select(g => new { OrgUnitID = g.Key, Planned = g.Sum(sp => sp.PlannedHeadcount) })
                .ToDictionaryAsync(x => x.OrgUnitID, x => x.Planned);
            var actualByOrg = await db.HR_EMPLOYEES.AsNoTracking()
                .Where(e => e.IsActive && e.IsDeleted != true && e.OrgUnitID != null)
                .GroupBy(e => e.OrgUnitID.Value)
                .Select(g => new { OrgUnitID = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.OrgUnitID, x => x.Count);

            foreach (var orgUnit in HrLookups.Get(HrLookups.OrgUnits))
            {
                decimal planned;
                int actual;
                plannedByOrg.TryGetValue(orgUnit.ID, out planned);
                actualByOrg.TryGetValue(orgUnit.ID, out actual);
                viewModel.HeadcountByOrgUnit.Add(new HrHeadcountByOrgUnitViewModel
                {
                    OrgUnitID = orgUnit.ID,
                    OrgUnitName = orgUnit.Name,
                    PlannedHeadcount = (int)planned,
                    ActualHeadcount = actual
                });
            }

            // Ish haqi trendi (oxirgi 6 oy): o'chirilganlar hisobga olinmaydi, oy bo'yicha bitta nuqta (HRM-B11)
            var startDate = new DateTime(year.Value, month.Value, 1).AddMonths(-5);
            int fromKey = startDate.Year * 100 + startDate.Month;
            int toKey = year.Value * 100 + month.Value;
            var trend = await db.HR_PAYROLL_LINES.AsNoTracking()
                .Where(l => (l.IsDeleted == null || l.IsDeleted == false) &&
                            (l.HR_PAYROLL_HEADERS.IsDeleted == null || l.HR_PAYROLL_HEADERS.IsDeleted == false) &&
                            l.HR_PAYROLL_HEADERS.Year * 100 + l.HR_PAYROLL_HEADERS.Month >= fromKey &&
                            l.HR_PAYROLL_HEADERS.Year * 100 + l.HR_PAYROLL_HEADERS.Month <= toKey &&
                            (orgUnitId == null ? l.HR_PAYROLL_HEADERS.OrgUnitID == null : l.HR_PAYROLL_HEADERS.OrgUnitID == orgUnitId.Value))
                .GroupBy(l => new { l.HR_PAYROLL_HEADERS.Year, l.HR_PAYROLL_HEADERS.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(l => l.NetSalary) })
                .ToListAsync();

            viewModel.PayrollTrend.AddRange(trend
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .Select(x => new HrPayrollTrendViewModel { Year = x.Year, Month = x.Month, TotalPayroll = x.Total ?? 0 }));

            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.OrgUnitID = HrLookups.SelectList(HrLookups.OrgUnits, orgUnitId);

            logger.Log("HrDashboard", "Index");
            return View(viewModel);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

