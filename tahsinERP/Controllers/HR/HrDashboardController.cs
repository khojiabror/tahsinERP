using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
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
            var latestOpeningDate = db.FIN_BALANCE_SNAPSHOTS
                .Where(s => s.SourceType == "OpeningBalance" && s.SnapshotDate <= periodEnd)
                .OrderByDescending(s => s.SnapshotDate)
                .Select(s => (DateTime?)s.SnapshotDate)
                .FirstOrDefault();
            var openingPayrollTotal = 0m;
            var openingPayrollPaid = 0m;
            if (latestOpeningDate.HasValue)
            {
                try
                {
                    var openingPayrollRows = db.Database.SqlQuery<OpeningPayrollRow>(
                        "SELECT TotalAmount, PaidAmount FROM FIN_OPENING_CONTRACT_BALANCES WHERE SnapshotDate = @p0 AND BalanceType = 3",
                        new SqlParameter("@p0", latestOpeningDate.Value.Date)).ToList();
                    openingPayrollTotal = openingPayrollRows.Sum(x => x.TotalAmount);
                    openingPayrollPaid = openingPayrollRows.Sum(x => x.PaidAmount);
                }
                catch
                {
                }
            }

            // Total headcount
            viewModel.TotalHeadcount = await db.HR_EMPLOYEES.CountAsync(e => e.IsActive);
            viewModel.ActiveHeadcount = viewModel.TotalHeadcount;

            // Planned headcount
            viewModel.PlannedHeadcount = (int)(await db.HR_STAFFING_PLAN
                .Where(sp => sp.IsActive)
                .SumAsync(sp => (decimal?)sp.PlannedHeadcount) ?? 0);

            // Total monthly payroll
            var payrollHeaders = await db.HR_PAYROLL_HEADERS
                .Include(p => p.HR_PAYROLL_LINES)
                .Where(p => p.Year == year.Value && p.Month == month.Value &&
                    (p.IsDeleted == null || p.IsDeleted == false) &&
                    (orgUnitId == null ? p.OrgUnitID == null : p.OrgUnitID == orgUnitId.Value))
                .ToListAsync();
            var activePayrollLines = payrollHeaders
                .SelectMany(p => p.HR_PAYROLL_LINES)
                .Where(l => l.IsDeleted == null || l.IsDeleted == false)
                .ToList();

            viewModel.TotalMonthlyPayroll = openingPayrollTotal + activePayrollLines.Sum(l => l.NetSalary ?? 0);
            viewModel.TotalHourlyCalculated = payrollHeaders
                .Where(p => p.PayrollType == "Hourly")
                .SelectMany(p => p.HR_PAYROLL_LINES.Where(l => l.IsDeleted == null || l.IsDeleted == false))
                .Sum(l => l.NetSalary ?? 0);
            viewModel.TotalPieceCalculated = payrollHeaders
                .Where(p => p.PayrollType == "PieceRate")
                .SelectMany(p => p.HR_PAYROLL_LINES.Where(l => l.IsDeleted == null || l.IsDeleted == false))
                .Sum(l => l.NetSalary ?? 0);

            // Total overtime hours
            var timesheet = await db.HR_TIMESHEET_HEADERS
                .Include(t => t.HR_TIMESHEET_LINES)
                .FirstOrDefaultAsync(t => t.Year == year.Value && t.Month == month.Value &&
                    (t.IsDeleted == null || t.IsDeleted == false) &&
                    (orgUnitId == null ? t.OrgUnitID == null : t.OrgUnitID == orgUnitId.Value));

            if (timesheet != null)
            {
                var activeTimesheetLines = timesheet.HR_TIMESHEET_LINES.Where(l => l.IsDeleted == null || l.IsDeleted == false).ToList();
                viewModel.TotalOvertimeHours = activeTimesheetLines.Sum(l => l.OvertimeHours ?? 0);
                viewModel.TotalWorkedHours = activeTimesheetLines.Sum(l => l.TotalWorkedHours ?? 0);
            }

            viewModel.TotalPieceQuantity = await db.HR_PIECE_RATE_WORK_ENTRIES
                .Where(e => (e.IsDeleted == null || e.IsDeleted == false) &&
                            e.WorkDate >= periodStart &&
                            e.WorkDate <= periodEnd &&
                            (orgUnitId == null || e.HR_EMPLOYEES.OrgUnitID == orgUnitId.Value))
                .SumAsync(e => (decimal?)e.Quantity) ?? 0m;

            // To'langan oylik: FinTransactions (xodimga bog'langan) + HR_PAYROLL postlari
            var salaryCategoryIds = await db.FIN_CATEGORIES
                .Where(c => c.IsActive &&
                            (c.IsDeleted == null || c.IsDeleted == false) &&
                            c.Name != null &&
                            c.Name.ToLower().Contains("oylik"))
                .Select(c => c.ID)
                .ToListAsync();

            var directSalaryPaid = await db.FIN_TRANSACTIONS
                .Where(t => (t.IsDeleted == null || t.IsDeleted == false) &&
                            t.Direction == 2 &&
                            t.TxnDate >= periodStart && t.TxnDate <= periodEnd &&
                            ((salaryCategoryIds.Contains(t.CategoryID)) ||
                             (t.CounterpartyType.HasValue && t.CounterpartyType.Value == 6) ||
                             t.SourceDocType == "HR_PAYROLL"))
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            viewModel.TotalSalaryPaidFromFin = openingPayrollPaid + directSalaryPaid;
            viewModel.TotalSalaryBalance = viewModel.TotalMonthlyPayroll - viewModel.TotalSalaryPaidFromFin;

            // Headcount by OrgUnit
            var orgUnits = await db.HR_ORG_UNITS
                .Where(o => o.IsActive)
                .OrderBy(o => o.Name)
                .ToListAsync();

            foreach (var orgUnit in orgUnits)
            {
                var planned = (int)(await db.HR_STAFFING_PLAN
                    .Where(sp => sp.OrgUnitID == orgUnit.ID && sp.IsActive)
                    .SumAsync(sp => (decimal?)sp.PlannedHeadcount) ?? 0);

                var actual = await db.HR_EMPLOYEES
                    .CountAsync(e => e.OrgUnitID == orgUnit.ID && e.IsActive);

                viewModel.HeadcountByOrgUnit.Add(new HrHeadcountByOrgUnitViewModel
                {
                    OrgUnitID = orgUnit.ID,
                    OrgUnitName = orgUnit.Name,
                    PlannedHeadcount = planned,
                    ActualHeadcount = actual
                });
            }

            // Payroll trend (last 6 months)
            var startDate = new DateTime(year.Value, month.Value, 1).AddMonths(-5);
            var payrolls = await db.HR_PAYROLL_HEADERS
                .Include(p => p.HR_PAYROLL_LINES)
                .Where(p => p.Year * 100 + p.Month >= startDate.Year * 100 + startDate.Month &&
                    p.Year * 100 + p.Month <= year.Value * 100 + month.Value &&
                    (orgUnitId == null ? p.OrgUnitID == null : p.OrgUnitID == orgUnitId.Value))
                .OrderBy(p => p.Year).ThenBy(p => p.Month)
                .ToListAsync();

            foreach (var p in payrolls)
            {
                viewModel.PayrollTrend.Add(new HrPayrollTrendViewModel
                {
                    Year = p.Year,
                    Month = p.Month,
                    TotalPayroll = p.HR_PAYROLL_LINES.Sum(l => l.NetSalary ?? 0)
                });
            }

            // Headcount trend (last 6 months)
            // uchun simplicity, foydalanib current headcount uchun barcha months
            // In a real scenario, you'd track historical headcount
            for (int i = 5; i >= 0; i--)
            {
                var trendDate = new DateTime(year.Value, month.Value, 1).AddMonths(-i);
                viewModel.HeadcountTrend.Add(new HrHeadcountTrendViewModel
                {
                    Year = trendDate.Year,
                    Month = trendDate.Month,
                    Headcount = viewModel.TotalHeadcount
                });
            }

            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnitId);

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

    internal class OpeningPayrollRow
    {
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
    }
}

