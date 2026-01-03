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
    public class HrEmployeeHistoryController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();
        private IUserActionLogger logger;

        public HrEmployeeHistoryController()
        {
            logger = new UserActionLogger(User?.Identity?.Name ?? "System");
        }

        // GET: HR/EmployeeHistory/Index (Xodimlar staji)
        public async Task<ActionResult> Index(int? orgUnitId, int? positionId, DateTime? fromDate, DateTime? toDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Get dismissed employees from history
            var historyDismissals = await db.HR_EMPLOYEE_HISTORY
                .Include(h => h.HR_EMPLOYEES)
                .Include(h => h.HR_ORG_UNITS) // FromOrgUnitID navigation
                .Include(h => h.HR_POSITIONS) // FromPositionID navigation
                .Where(h => h.ActionType == "Dismissal")
                .ToListAsync();

            // Also get employees with DismissalDate set (even if not in history)
            var employeesWithDismissal = await db.HR_EMPLOYEES
                .Include(e => e.HR_ORG_UNITS)
                .Include(e => e.HR_POSITIONS)
                .Where(e => e.DismissalDate != null)
                .ToListAsync();

            // Combine both sources
            var allDismissals = new List<HrEmployeeHistoryViewModel>();

            // Add from history
            foreach (var h in historyDismissals)
            {
                // Apply filters
                if (orgUnitId.HasValue && h.FromOrgUnitID != orgUnitId.Value)
                    continue;
                if (positionId.HasValue && h.FromPositionID != positionId.Value)
                    continue;
                if (fromDate.HasValue && h.EffectiveDate < fromDate.Value)
                    continue;
                if (toDate.HasValue && h.EffectiveDate > toDate.Value)
                    continue;

                allDismissals.Add(new HrEmployeeHistoryViewModel
                {
                    ID = h.ID,
                    EmployeeID = h.EmployeeID,
                    EmployeeName = h.HR_EMPLOYEES?.FullName,
                    TabNo = h.HR_EMPLOYEES?.TabNo,
                    ActionType = h.ActionType,
                    FromOrgUnitName = h.HR_ORG_UNITS?.Name,
                    FromPositionName = h.HR_POSITIONS?.Name,
                    EffectiveDate = h.EffectiveDate,
                    Reason = h.Reason,
                    EmploymentDate = h.HR_EMPLOYEES?.EmploymentDate
                });
            }

            // Add from employees table (if not already in history)
            var historyEmployeeIds = historyDismissals.Select(h => h.EmployeeID).ToList();
            foreach (var emp in employeesWithDismissal)
            {
                // Skip if already in history
                if (historyEmployeeIds.Contains(emp.ID))
                    continue;

                // Apply filters
                if (orgUnitId.HasValue && emp.OrgUnitID != orgUnitId.Value)
                    continue;
                if (positionId.HasValue && emp.PositionID != positionId.Value)
                    continue;
                if (fromDate.HasValue && emp.DismissalDate < fromDate.Value)
                    continue;
                if (toDate.HasValue && emp.DismissalDate > toDate.Value)
                    continue;

                allDismissals.Add(new HrEmployeeHistoryViewModel
                {
                    ID = 0, // Not from history
                    EmployeeID = emp.ID,
                    EmployeeName = emp.FullName,
                    TabNo = emp.TabNo,
                    ActionType = "Dismissal",
                    FromOrgUnitName = emp.HR_ORG_UNITS?.Name,
                    FromPositionName = emp.HR_POSITIONS?.Name,
                    EffectiveDate = emp.DismissalDate,
                    Reason = null,
                    EmploymentDate = emp.EmploymentDate
                });
            }

            var viewModels = allDismissals.OrderByDescending(d => d.EffectiveDate).ToList();

            // Debug: Log counts
            System.Diagnostics.Debug.WriteLine($"History dismissals: {historyDismissals.Count}, Employees with dismissal: {employeesWithDismissal.Count}, Total viewModels: {viewModels.Count}");

            // Calculate tenure for each employee
            foreach (var vm in viewModels)
            {
                if (vm.EmploymentDate.HasValue && vm.EffectiveDate != null)
                {
                    var tenure = vm.EffectiveDate.Value - vm.EmploymentDate.Value;
                    vm.TenureYears = tenure.Days / 365;
                    vm.TenureMonths = (tenure.Days % 365) / 30;
                    vm.TenureDays = (tenure.Days % 365) % 30;
                }
            }

            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnitId);

            ViewBag.PositionID = new SelectList(
                await db.HR_POSITIONS.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name", positionId);

            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            logger.Log("HrEmployeeHistory", "Index");
            return View(viewModels);
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

