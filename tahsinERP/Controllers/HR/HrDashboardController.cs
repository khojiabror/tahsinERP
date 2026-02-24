using System;
using System.Collections.Generic;
using System.Data.Entity;
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

            var viewModel = new HrDashboardViewModel();

            // Total headcount
            viewModel.TotalHeadcount = await db.HR_EMPLOYEES.CountAsync(e => e.IsActive);
            viewModel.ActiveHeadcount = viewModel.TotalHeadcount;

            // Planned headcount
            viewModel.PlannedHeadcount = (int)(await db.HR_STAFFING_PLAN
                .Where(sp => sp.IsActive)
                .SumAsync(sp => (decimal?)sp.PlannedHeadcount) ?? 0);

            // Total monthly payroll
            var payroll = await db.HR_PAYROLL_HEADERS
                .Include(p => p.HR_PAYROLL_LINES)
                .FirstOrDefaultAsync(p => p.Year == year.Value && p.Month == month.Value &&
                    (orgUnitId == null ? p.OrgUnitID == null : p.OrgUnitID == orgUnitId.Value));

            if (payroll != null)
            {
                viewModel.TotalMonthlyPayroll = payroll.HR_PAYROLL_LINES.Sum(l => l.NetSalary ?? 0);
            }

            // Total overtime hours
            var timesheet = await db.HR_TIMESHEET_HEADERS
                .Include(t => t.HR_TIMESHEET_LINES)
                .FirstOrDefaultAsync(t => t.Year == year.Value && t.Month == month.Value &&
                    (orgUnitId == null ? t.OrgUnitID == null : t.OrgUnitID == orgUnitId.Value));

            if (timesheet != null)
            {
                viewModel.TotalOvertimeHours = timesheet.HR_TIMESHEET_LINES.Sum(l => l.OvertimeHours ?? 0);
            }

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
}

