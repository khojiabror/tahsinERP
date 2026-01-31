using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.HR;
using tahsinERP.ViewModels.HR;

namespace tahsinERP.Controllers.HR
{
    // Helper class for raw SQL queries
    public class DateInfo
    {
        public DateTime EffectiveDateFrom { get; set; }
        public DateTime? EffectiveDateTo { get; set; }
    }

    [Authorize]
    public class HrEmployeeHistoryController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();
        private IUserActionLogger logger;
        private EmployeeTenureService tenureService;

        public HrEmployeeHistoryController()
        {
            logger = new UserActionLogger(User?.Identity?.Name ?? "System");
            tenureService = new EmployeeTenureService(db);
        }

        // GET: HR/EmployeeHistory/Index (Xodimlar staji)
        public async Task<ActionResult> Index(int? orgUnitId, int? positionId, DateTime? fromDate, DateTime? toDate, string status = null)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Get all employees (for filtering by status and including employees without history)
            IQueryable<HR_EMPLOYEES> employeesQuery = db.HR_EMPLOYEES
                .Include(e => e.HR_ORG_UNITS)
                .Include(e => e.HR_POSITIONS);

            // Apply status filter at query level
            if (status == "active")
            {
                employeesQuery = employeesQuery.Where(e => e.IsActive);
            }
            else if (status == "inactive")
            {
                employeesQuery = employeesQuery.Where(e => !e.IsActive);
            }

            var allEmployees = await employeesQuery.ToListAsync();
            var employeeIds = allEmployees.Select(e => e.ID).ToList();

            // Get all employee history records - using Include for navigation properties
            var allHistory = await db.HR_EMPLOYEE_HISTORY
                .Include(h => h.HR_EMPLOYEES)
                .Include(h => h.HR_ORG_UNITS)
                .Include(h => h.HR_POSITIONS)
                .Where(h => employeeIds.Contains(h.EmployeeID))
                .OrderBy(h => h.HR_EMPLOYEES.FullName)
                .ThenBy(h => h.EffectiveDateFrom)
                .ToListAsync();

            // Group by EmployeeID - include employees even if they have no history
            var historyEmployeeIds = allHistory.Select(h => h.EmployeeID).Distinct().ToList();
            
            // Add employees without history records
            var employeesWithoutHistory = allEmployees
                .Where(e => !historyEmployeeIds.Contains(e.ID))
                .Select(e => new
                {
                    EmployeeID = e.ID,
                    Employee = e,
                    HistoryRecords = new List<HR_EMPLOYEE_HISTORY>()
                })
                .ToList();

            // Group by EmployeeID
            var groupedByEmployee = allHistory
                .GroupBy(h => h.EmployeeID)
                .Select(g => new
                {
                    EmployeeID = g.Key,
                    Employee = g.First().HR_EMPLOYEES,
                    HistoryRecords = g.ToList()
                })
                .Union(employeesWithoutHistory)
                .ToList();

            var groupedViewModels = new List<HrEmployeeHistoryGroupedViewModel>();

            foreach (var group in groupedByEmployee)
            {
                // Apply filters at employee level
                bool shouldInclude = true;
                if (orgUnitId.HasValue && !group.HistoryRecords.Any(h => h.FromOrgUnitID == orgUnitId.Value))
                    shouldInclude = false;
                if (positionId.HasValue && !group.HistoryRecords.Any(h => h.FromPositionID == positionId.Value))
                    shouldInclude = false;
                if (fromDate.HasValue && !group.HistoryRecords.Any(h => h.EffectiveDateFrom >= fromDate.Value))
                    shouldInclude = false;
                if (toDate.HasValue && !group.HistoryRecords.Any(h => h.EffectiveDateFrom <= toDate.Value))
                    shouldInclude = false;

                if (!shouldInclude)
                    continue;

                // Calculate total tenure for this employee
                var totalTenure = await tenureService.CalculateTotalTenureAsync(group.EmployeeID);

                // Build position history list
                var positions = new List<HrEmployeeHistoryViewModel>();
                
                // If employee has no history records but is active, add current position
                if (!group.HistoryRecords.Any() && group.Employee != null && group.Employee.IsActive && group.Employee.EmploymentDate.HasValue)
                {
                    var currentTenure = tenureService.CalculateTenure(
                        group.Employee.EmploymentDate.Value,
                        DateTime.Now);

                    positions.Add(new HrEmployeeHistoryViewModel
                    {
                        ID = 0, // No history record ID
                        EmployeeID = group.EmployeeID,
                        EmployeeName = group.Employee.FullName,
                        TabNo = group.Employee.TabNo,
                        FromOrgUnitName = group.Employee.HR_ORG_UNITS?.Name,
                        FromPositionName = group.Employee.HR_POSITIONS?.Name,
                        EffectiveDate = group.Employee.EmploymentDate.Value,
                        EffectiveDateTo = null, // Still active
                        Reason = "Hozirgi lavozim",
                        TenureYears = currentTenure.Years,
                        TenureMonths = currentTenure.Months,
                        TenureDays = currentTenure.Days
                    });
                }

                foreach (var h in group.HistoryRecords.OrderBy(h => h.EffectiveDateFrom))
                {
                    // Apply filters at record level
                    if (orgUnitId.HasValue && h.FromOrgUnitID != orgUnitId.Value)
                        continue;
                    if (positionId.HasValue && h.FromPositionID != positionId.Value)
                        continue;
                    if (fromDate.HasValue && h.EffectiveDateFrom < fromDate.Value)
                        continue;
                    if (toDate.HasValue && h.EffectiveDateFrom > toDate.Value)
                        continue;

                    DateTime? effectiveDateTo = h.EffectiveDateTo;
                    
                    // If EffectiveDateTo is null and employee is active, use Today
                    if (!effectiveDateTo.HasValue)
                    {
                        var emp = await db.HR_EMPLOYEES.FindAsync(h.EmployeeID);
                        if (emp != null && emp.IsActive)
                        {
                            effectiveDateTo = DateTime.Now;
                        }
                        else
                        {
                            var nextRecord = group.HistoryRecords
                                .Where(h2 => h2.EffectiveDateFrom > h.EffectiveDateFrom)
                                .OrderBy(h2 => h2.EffectiveDateFrom)
                                .FirstOrDefault();
                            
                            if (nextRecord != null)
                            {
                                effectiveDateTo = nextRecord.EffectiveDateFrom;
                            }
                            else if (emp != null && emp.DismissalDate.HasValue)
                            {
                                effectiveDateTo = emp.DismissalDate;
                            }
                        }
                    }

                    // Calculate tenure for this position
                    var tenureResult = tenureService.CalculateTenure(
                        h.EffectiveDateFrom,
                        effectiveDateTo);

                    positions.Add(new HrEmployeeHistoryViewModel
                    {
                        ID = h.ID,
                        EmployeeID = h.EmployeeID,
                        EmployeeName = h.HR_EMPLOYEES?.FullName,
                        TabNo = h.HR_EMPLOYEES?.TabNo,
                        FromOrgUnitName = h.HR_ORG_UNITS?.Name,
                        FromPositionName = h.HR_POSITIONS?.Name,
                        EffectiveDate = h.EffectiveDateFrom,
                        EffectiveDateTo = effectiveDateTo,
                        Reason = h.Reason,
                        TenureYears = tenureResult.Years,
                        TenureMonths = tenureResult.Months,
                        TenureDays = tenureResult.Days
                    });
                }

                if (positions.Any())
                {
                    groupedViewModels.Add(new HrEmployeeHistoryGroupedViewModel
                    {
                        EmployeeID = group.EmployeeID,
                        EmployeeName = group.Employee?.FullName,
                        TabNo = group.Employee?.TabNo,
                        Positions = positions,
                        TotalTenureYears = totalTenure.Years,
                        TotalTenureMonths = totalTenure.Months,
                        TotalTenureDays = totalTenure.Days
                    });
                }
            }

            var viewModels = groupedViewModels.SelectMany(g => g.Positions).ToList();

            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnitId);

            ViewBag.PositionID = new SelectList(
                await db.HR_POSITIONS.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name", positionId);

            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.Status = status; // Pass status filter to view
            ViewBag.GroupedViewModels = groupedViewModels; // Pass grouped data to view

            logger.Log("HrEmployeeHistory", "Index");
            return View(viewModels);
        }

        // GET: HR/EmployeeHistory/Create
        public async Task<ActionResult> Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            ViewBag.EmployeeID = new SelectList(
                await db.HR_EMPLOYEES
                    .OrderBy(e => e.FullName)
                    .ToListAsync(),
                "ID", "FullName");

            ViewBag.FromOrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name");

            ViewBag.FromPositionID = new SelectList(
                await db.HR_POSITIONS.OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name");

            logger.Log("HrEmployeeHistory", "Create");
            return View();
        }

        // POST: HR/EmployeeHistory/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(HR_EMPLOYEE_HISTORY history)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                // Get EffectiveDateTo from form
                var effectiveDateToStr = Request.Form["EffectiveDateTo"];
                DateTime? effectiveDateTo = null;
                if (!string.IsNullOrEmpty(effectiveDateToStr))
                {
                    DateTime parsedDate;
                    if (DateTime.TryParse(effectiveDateToStr, out parsedDate))
                    {
                        effectiveDateTo = parsedDate;
                    }
                }

                // Validate dates
                if (effectiveDateTo.HasValue && effectiveDateTo.Value < history.EffectiveDateFrom)
                {
                    ModelState.AddModelError("", "Tugash sanasi boshlanish sanasidan kichik bo'lishi mumkin emas.");
                    await PopulateViewBags(history);
                    return View(history);
                }

                history.CreatedByUserID = UserHelper.GetUserId(User.Identity.Name);
                history.CreatedDT = DateTime.Now;
                history.ActionType = "History"; // Simple history record

                // Use raw SQL to insert with EffectiveDateFrom and EffectiveDateTo
                await db.Database.ExecuteSqlCommandAsync(
                    @"INSERT INTO HR_EMPLOYEE_HISTORY 
                      (EmployeeID, ActionType, FromOrgUnitID, FromPositionID, ToOrgUnitID, ToPositionID, 
                       EffectiveDateFrom, EffectiveDateTo, Reason, CreatedByUserID, CreatedDT)
                      VALUES 
                      ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10})",
                    history.EmployeeID,
                    "History",
                    history.FromOrgUnitID ?? (object)DBNull.Value,
                    history.FromPositionID ?? (object)DBNull.Value,
                    history.ToOrgUnitID ?? (object)DBNull.Value,
                    history.ToPositionID ?? (object)DBNull.Value,
                    history.EffectiveDateFrom, // EffectiveDateFrom - from form
                    effectiveDateTo ?? (object)DBNull.Value, // EffectiveDateTo - from form
                    history.Reason ?? (object)DBNull.Value,
                    history.CreatedByUserID,
                    history.CreatedDT);

                // Update HrEmployee based on EffectiveDateFrom and EffectiveDateTo
                var employee = await db.HR_EMPLOYEES.FindAsync(history.EmployeeID);
                if (employee != null)
                {
                    bool employeeUpdated = false;
                    
                    // If EffectiveDateFrom > employee.EmploymentDate, update PositionID
                    if (employee.EmploymentDate.HasValue && history.EffectiveDateFrom > employee.EmploymentDate.Value)
                    {
                        if (history.ToPositionID.HasValue)
                        {
                            employee.PositionID = history.ToPositionID;
                            employeeUpdated = true;
                        }
                    }
                    
                    // If EffectiveDateTo > employee.DismissalDate, update DismissalDate
                    if (effectiveDateTo.HasValue)
                    {
                        if (!employee.DismissalDate.HasValue || effectiveDateTo.Value > employee.DismissalDate.Value)
                        {
                            employee.DismissalDate = effectiveDateTo.Value;
                            employee.IsActive = false;
                            employeeUpdated = true;
                        }
                    }
                    
                    if (employeeUpdated)
                    {
                        employee.UpdatedDT = DateTime.Now;
                        await db.SaveChangesAsync();
                    }
                }

                logger.Log("HrEmployeeHistory", "Create", $"EmployeeID: {history.EmployeeID}");
                TempData["Success"] = "Xodim tarixi muvaffaqiyatli qo'shildi.";
                return RedirectToAction("Index");
            }

            await PopulateViewBags(history);
            return View(history);
        }

        // GET: HR/EmployeeHistory/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var history = await db.HR_EMPLOYEE_HISTORY.FindAsync(id);
            if (history == null)
            {
                return HttpNotFound();
            }

            // Get EffectiveDateTo from database (if column exists) or calculate
            DateTime? effectiveDateTo = null;
            
            // Try to get from EffectiveDateTo column using raw SQL
            try
            {
                var dateInfo = db.Database.SqlQuery<DateInfo>(
                    @"SELECT EffectiveDateFrom, EffectiveDateTo 
                      FROM HR_EMPLOYEE_HISTORY 
                      WHERE ID = {0}",
                    id.Value)
                    .FirstOrDefault();
                
                if (dateInfo != null)
                {
                    effectiveDateTo = dateInfo.EffectiveDateTo;
                    ViewBag.EffectiveDateFrom = dateInfo.EffectiveDateFrom.ToString("yyyy-MM-dd");
                }
            }
            catch
            {
                // Fallback: calculate from next record or employee DismissalDate
                var nextRecord = await db.HR_EMPLOYEE_HISTORY
                    .Where(h => h.EmployeeID == history.EmployeeID && h.EffectiveDateFrom > history.EffectiveDateFrom)
                    .OrderBy(h => h.EffectiveDateFrom)
                    .FirstOrDefaultAsync();
                
                if (nextRecord != null)
                {
                    effectiveDateTo = nextRecord.EffectiveDateFrom;
                }
                else
                {
                    var emp = await db.HR_EMPLOYEES.FindAsync(history.EmployeeID);
                    if (emp != null && emp.DismissalDate.HasValue)
                    {
                        effectiveDateTo = emp.DismissalDate;
                    }
                }
                ViewBag.EffectiveDateFrom = history.EffectiveDateFrom.ToString("yyyy-MM-dd");
            }

            ViewBag.EffectiveDateTo = effectiveDateTo?.ToString("yyyy-MM-dd");
            ViewBag.EffectiveDateFrom = history.EffectiveDateFrom.ToString("yyyy-MM-dd");

            // Populate ViewBags with selected values
            await PopulateViewBags(history);
            
            logger.Log("HrEmployeeHistory", "Edit", $"ID: {id}");
            return View(history);
        }

        // POST: HR/EmployeeHistory/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(HR_EMPLOYEE_HISTORY history)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                var existingHistory = await db.HR_EMPLOYEE_HISTORY.FindAsync(history.ID);
                if (existingHistory == null)
                {
                    return HttpNotFound();
                }

                // Get EffectiveDateTo from form
                var effectiveDateToStr = Request.Form["EffectiveDateTo"];
                DateTime? effectiveDateTo = null;
                if (!string.IsNullOrEmpty(effectiveDateToStr))
                {
                    DateTime parsedDate;
                    if (DateTime.TryParse(effectiveDateToStr, out parsedDate))
                    {
                        effectiveDateTo = parsedDate;
                    }
                }

                // Validate dates
                if (effectiveDateTo.HasValue && effectiveDateTo.Value < history.EffectiveDateFrom)
                {
                    ModelState.AddModelError("", "Tugash sanasi boshlanish sanasidan kichik bo'lishi mumkin emas.");
                    await PopulateViewBags(history);
                    ViewBag.EffectiveDateTo = effectiveDateTo?.ToString("yyyy-MM-dd");
                    ViewBag.EffectiveDateFrom = history.EffectiveDateFrom.ToString("yyyy-MM-dd");
                    return View(history);
                }

                // Update existing history record using raw SQL to update EffectiveDateFrom and EffectiveDateTo
                await db.Database.ExecuteSqlCommandAsync(
                    @"UPDATE HR_EMPLOYEE_HISTORY 
                      SET EmployeeID = {0}, FromOrgUnitID = {1}, FromPositionID = {2}, 
                          EffectiveDateFrom = {3}, EffectiveDateTo = {4}, Reason = {5}
                      WHERE ID = {6}",
                    history.EmployeeID,
                    history.FromOrgUnitID ?? (object)DBNull.Value,
                    history.FromPositionID ?? (object)DBNull.Value,
                    history.EffectiveDateFrom, // EffectiveDateFrom
                    effectiveDateTo ?? (object)DBNull.Value, // EffectiveDateTo
                    history.Reason ?? (object)DBNull.Value,
                    history.ID);

                logger.Log("HrEmployeeHistory", "Edit", $"ID: {history.ID}");
                TempData["Success"] = "Xodim tarixi muvaffaqiyatli yangilandi.";
                return RedirectToAction("Index");
            }

            await PopulateViewBags(history);
            return View(history);
        }

        private async Task PopulateViewBags(HR_EMPLOYEE_HISTORY history = null)
        {
            ViewBag.EmployeeID = new SelectList(
                await db.HR_EMPLOYEES
                    .OrderBy(e => e.FullName)
                    .ToListAsync(),
                "ID", "FullName", history?.EmployeeID);

            ViewBag.FromOrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", history?.FromOrgUnitID);

            ViewBag.FromPositionID = new SelectList(
                await db.HR_POSITIONS.OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name", history?.FromPositionID);

            ViewBag.ToOrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", history?.ToOrgUnitID);

            ViewBag.ToPositionID = new SelectList(
                await db.HR_POSITIONS.OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name", history?.ToPositionID);
        }
    }
}
