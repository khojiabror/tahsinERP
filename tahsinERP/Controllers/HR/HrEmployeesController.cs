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
    // Helper class for SQL query result
    public class HistoryWithDates
    {
        public int ID { get; set; }
        public DateTime EffectiveDateFrom { get; set; }
        public DateTime? EffectiveDateTo { get; set; }
        public string FromOrgUnitName { get; set; }
        public string FromPositionName { get; set; }
        public string Reason { get; set; }
    }

    [Authorize]
    public class HrEmployeesController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();
        private IUserActionLogger logger;

        public HrEmployeesController()
        {
            logger = new UserActionLogger(User?.Identity?.Name ?? "System");
        }

        // GET: HR/Employees
        public async Task<ActionResult> Index(int? orgUnitId, int? positionId, string status)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            IQueryable<HR_EMPLOYEES> query = db.HR_EMPLOYEES
                .Include(e => e.HR_ORG_UNITS)
                .Include(e => e.HR_POSITIONS)
                .Include(e => e.HR_SALARY_GRADES);

            if (orgUnitId.HasValue)
            {
                query = query.Where(e => e.OrgUnitID == orgUnitId.Value);
            }

            if (positionId.HasValue)
            {
                query = query.Where(e => e.PositionID == positionId.Value);
            }

            // Default: show only active employees (not dismissed)
            if (status == "InActive")
            {
                query = query.Where(e => !e.IsActive || e.DismissalDate != null);
            }
            else
            {
                // Show only active employees (IsActive = true and DismissalDate is null)
                query = query.Where(e => e.IsActive && e.DismissalDate == null);
            }

            var employees = await query.OrderBy(e => e.FullName).ToListAsync();

            // Get latest position for each employee from history
            var employeeIds = employees.Select(e => e.ID).ToList();
            var latestHistory = await db.HR_EMPLOYEE_HISTORY
                .Include(h => h.HR_ORG_UNITS1) // ToOrgUnitID navigation
                .Include(h => h.HR_POSITIONS1) // ToPositionID navigation
                .Where(h => employeeIds.Contains(h.EmployeeID) && h.ActionType == "Transfer")
                .GroupBy(h => h.EmployeeID)
                .Select(g => new { 
                    EmployeeID = g.Key, 
                    LatestHistory = g.OrderByDescending(x => x.EffectiveDateFrom).FirstOrDefault() 
                })
                .ToListAsync();

            var viewModels = employees.Select(e => {
                var latestTransfer = latestHistory.FirstOrDefault(h => h.EmployeeID == e.ID)?.LatestHistory;
                
                // Use current position if no transfer history, otherwise use latest transfer position
                var currentOrgUnitID = latestTransfer?.ToOrgUnitID ?? e.OrgUnitID;
                var currentPositionID = latestTransfer?.ToPositionID ?? e.PositionID;
                
                // Get names
                var currentOrgUnit = latestTransfer?.HR_ORG_UNITS1 ?? e.HR_ORG_UNITS;
                var currentPosition = latestTransfer?.HR_POSITIONS1 ?? e.HR_POSITIONS;

                return new HrEmployeeViewModel
                {
                    ID = e.ID,
                    TabNo = e.TabNo,
                    FullName = e.FullName,
                    OrgUnitID = currentOrgUnitID,
                    OrgUnitName = currentOrgUnit?.Name,
                    PositionID = currentPositionID,
                    PositionName = currentPosition?.Name,
                    BaseSalary = e.BaseSalary,
                    SalaryGradeID = e.SalaryGradeID,
                    SalaryGradeName = e.HR_SALARY_GRADES?.Name,
                    EmploymentDate = e.EmploymentDate,
                    DismissalDate = e.DismissalDate,
                    IsActive = e.IsActive
                };
            }).ToList();

            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnitId);

            ViewBag.PositionID = new SelectList(
                await db.HR_POSITIONS.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name", positionId);

            logger.Log("HrEmployees", "Index");
            return View(viewModels);
        }

        // GET: HR/Employees/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var employee = await db.HR_EMPLOYEES
                .Include(e => e.HR_ORG_UNITS)
                .Include(e => e.HR_POSITIONS)
                .Include(e => e.HR_SALARY_GRADES)
                .FirstOrDefaultAsync(e => e.ID == id);

            if (employee == null)
            {
                return HttpNotFound();
            }

            // Get employee history - simple query
            var allHistory = db.HR_EMPLOYEE_HISTORY
                .Include(h => h.HR_ORG_UNITS)
                .Include(h => h.HR_POSITIONS)
                .Where(h => h.EmployeeID == id.Value)
                .OrderBy(h => h.EffectiveDateFrom)
                .ToList();

            var viewModel = new HrEmployeeViewModel
            {
                ID = employee.ID,
                TabNo = employee.TabNo,
                FullName = employee.FullName,
                OrgUnitID = employee.OrgUnitID,
                OrgUnitName = employee.HR_ORG_UNITS?.Name,
                PositionID = employee.PositionID,
                PositionName = employee.HR_POSITIONS?.Name,
                BaseSalary = employee.BaseSalary,
                SalaryGradeID = employee.SalaryGradeID,
                SalaryGradeName = employee.HR_SALARY_GRADES?.Name,
                EmploymentDate = employee.EmploymentDate,
                DismissalDate = employee.DismissalDate,
                IsActive = employee.IsActive
            };

            // Pass history to view with EffectiveDateFrom and EffectiveDateTo from database
            var historyList = new List<HrEmployeeHistoryViewModel>();
            
            // Use Entity Framework with Include to ensure navigation properties are loaded
            var historyRecords = await db.HR_EMPLOYEE_HISTORY
                .Include(h => h.HR_ORG_UNITS)
                .Include(h => h.HR_POSITIONS)
                .Where(h => h.EmployeeID == id.Value)
                .OrderBy(h => h.EffectiveDateFrom)
                .ToListAsync();

            foreach (var h in historyRecords)
            {
                DateTime effectiveDateFrom = h.EffectiveDateFrom;
                DateTime? effectiveDateTo = h.EffectiveDateTo;
                
                // If EffectiveDateTo is null, try to get from next record or employee DismissalDate
                if (!effectiveDateTo.HasValue)
                {
                    var nextRecord = historyRecords
                        .Where(h2 => h2.EffectiveDateFrom > effectiveDateFrom)
                        .OrderBy(h2 => h2.EffectiveDateFrom)
                        .FirstOrDefault();
                    
                    if (nextRecord != null)
                    {
                        effectiveDateTo = nextRecord.EffectiveDateFrom;
                    }
                    else if (employee.DismissalDate.HasValue)
                    {
                        effectiveDateTo = employee.DismissalDate;
                    }
                }

                historyList.Add(new HrEmployeeHistoryViewModel
                {
                    FromOrgUnitName = h.HR_ORG_UNITS?.Name ?? "-",
                    FromPositionName = h.HR_POSITIONS?.Name ?? "-",
                    EffectiveDate = effectiveDateFrom,
                    EffectiveDateTo = effectiveDateTo,
                    Reason = h.Reason ?? "-"
                });
            }
            
            ViewBag.History = historyList;

            logger.Log("HrEmployees", "Details", $"ID: {id}");
            return View(viewModel);
        }

        // GET: HR/Employees/Create
        public async Task<ActionResult> Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Generate next TabNo automatically
            string nextTabNo = await GenerateNextTabNo();

            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name");

            ViewBag.PositionID = new SelectList(
                await db.HR_POSITIONS.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name");

            ViewBag.SalaryGradeID = new SelectList(
                await db.HR_SALARY_GRADES.OrderBy(sg => sg.GradeCode).ToListAsync(),
                "ID", "Name");

            ViewBag.NextTabNo = nextTabNo;

            return View();
        }

        // POST: HR/Employees/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(HrEmployeeViewModel viewModel)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                // Generate TabNo automatically if not provided or empty
                string tabNo = viewModel.TabNo;
                if (string.IsNullOrWhiteSpace(tabNo))
                {
                    tabNo = await GenerateNextTabNo();
                }

                // Check if TabNo already exists
                var existing = await db.HR_EMPLOYEES
                    .FirstOrDefaultAsync(e => e.TabNo == tabNo);
                if (existing != null)
                {
                    ModelState.AddModelError("TabNo", "Табельный номер уже существует");
                    await PopulateViewBags(viewModel);
                    return View(viewModel);
                }

                var employee = new HR_EMPLOYEES
                {
                    TabNo = tabNo,
                    FullName = viewModel.FullName,
                    OrgUnitID = viewModel.OrgUnitID,
                    PositionID = viewModel.PositionID,
                    BaseSalary = viewModel.BaseSalary,
                    SalaryGradeID = viewModel.SalaryGradeID,
                    EmploymentDate = viewModel.EmploymentDate,
                    DismissalDate = viewModel.DismissalDate,
                    IsActive = viewModel.IsActive,
                    CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                    CreatedDT = DateTime.Now
                };

                db.HR_EMPLOYEES.Add(employee);
                await db.SaveChangesAsync();

                // Create HrEmployeeHistory record for the new employee
                if (employee.OrgUnitID.HasValue && employee.PositionID.HasValue && employee.EmploymentDate.HasValue)
                {
                    var history = new HR_EMPLOYEE_HISTORY
                    {
                        EmployeeID = employee.ID,
                        ActionType = "Initial Employment",
                        FromOrgUnitID = employee.OrgUnitID,
                        FromPositionID = employee.PositionID,
                        ToOrgUnitID = null,
                        ToPositionID = null,
                        EffectiveDateFrom = employee.EmploymentDate.Value, // Employment start date
                        EffectiveDateTo = employee.DismissalDate, // Dismissal date (if any)
                        Reason = "Ishga qabul qilingan",
                        CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                        CreatedDT = DateTime.Now
                    };

                    db.HR_EMPLOYEE_HISTORY.Add(history);
                    await db.SaveChangesAsync(); // Save history record
                }

                logger.Log("HrEmployees", "Create", $"ID: {employee.ID}, TabNo: {employee.TabNo}");
                return RedirectToAction("Index");
            }

            await PopulateViewBags(viewModel);
            return View(viewModel);
        }

        // GET: HR/Employees/Edit/5
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

            var employee = await db.HR_EMPLOYEES.FindAsync(id);
            if (employee == null)
            {
                return HttpNotFound();
            }

            var viewModel = new HrEmployeeViewModel
            {
                ID = employee.ID,
                TabNo = employee.TabNo,
                FullName = employee.FullName,
                OrgUnitID = employee.OrgUnitID,
                PositionID = employee.PositionID,
                BaseSalary = employee.BaseSalary,
                SalaryGradeID = employee.SalaryGradeID,
                EmploymentDate = employee.EmploymentDate,
                DismissalDate = employee.DismissalDate,
                IsActive = employee.IsActive
            };

            await PopulateViewBags(viewModel);
            return View(viewModel);
        }

        // POST: HR/Employees/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(HrEmployeeViewModel viewModel)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                // Check if TabNo already exists for another employee
                var existing = await db.HR_EMPLOYEES
                    .FirstOrDefaultAsync(e => e.TabNo == viewModel.TabNo && e.ID != viewModel.ID && e.IsActive);
                if (existing != null)
                {
                    ModelState.AddModelError("TabNo", "Табельный номер уже существует");
                    await PopulateViewBags(viewModel);
                    return View(viewModel);
                }

                var employee = await db.HR_EMPLOYEES.FindAsync(viewModel.ID);
                if (employee == null)
                {
                    return HttpNotFound();
                }

                employee.TabNo = viewModel.TabNo;
                employee.FullName = viewModel.FullName;
                employee.OrgUnitID = viewModel.OrgUnitID;
                employee.PositionID = viewModel.PositionID;
                employee.BaseSalary = viewModel.BaseSalary;
                employee.SalaryGradeID = viewModel.SalaryGradeID;
                employee.EmploymentDate = viewModel.EmploymentDate;
                employee.DismissalDate = viewModel.DismissalDate;
                employee.IsActive = viewModel.IsActive;
                employee.UpdatedDT = DateTime.Now;

                db.Entry(employee).State = EntityState.Modified;
                await db.SaveChangesAsync();

                logger.Log("HrEmployees", "Edit", $"ID: {viewModel.ID}");
                return RedirectToAction("Index");
            }

            await PopulateViewBags(viewModel);
            return View(viewModel);
        }

        // POST: HR/Employees/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var employee = await db.HR_EMPLOYEES.FindAsync(id);
            if (employee == null)
            {
                return HttpNotFound();
            }

            // Soft delete - set IsActive to false
            employee.IsActive = false;
            employee.DismissalDate = DateTime.Now;
            employee.UpdatedDT = DateTime.Now;

            db.Entry(employee).State = EntityState.Modified;
            await db.SaveChangesAsync();

            logger.Log("HrEmployees", "Delete", $"ID: {id}");
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Generates the next TabNo by finding the highest TabNo that starts with '0' and adding 1
        /// Format: 4 digits with leading zeros (e.g., 0001, 0009, 0010, 0099, 0100)
        /// </summary>
        private async Task<string> GenerateNextTabNo()
        {
            // Get all TabNo values that start with '0' (4-digit format)
            var tabNos = await db.HR_EMPLOYEES
                .Where(e => e.TabNo != null && e.TabNo.StartsWith("0") && e.TabNo.Length == 4)
                .Select(e => e.TabNo)
                .ToListAsync();

            if (tabNos == null || !tabNos.Any())
            {
                // No TabNo starting with 0 exists, start from 0001
                return "0001";
            }

            // Parse all TabNo values to integers and find the maximum
            int maxTabNo = 0;
            foreach (var tabNo in tabNos)
            {
                if (int.TryParse(tabNo, out int tabNoInt))
                {
                    if (tabNoInt > maxTabNo)
                    {
                        maxTabNo = tabNoInt;
                    }
                }
            }

            // Increment by 1 and format as 4-digit string with leading zeros
            int nextTabNo = maxTabNo + 1;
            
            // Ensure it doesn't exceed 9999 (4 digits max)
            if (nextTabNo > 9999)
            {
                throw new InvalidOperationException("TabNo has reached the maximum value (9999). Please contact administrator.");
            }

            return nextTabNo.ToString("D4"); // Format as 4-digit string with leading zeros
        }

        private async Task PopulateViewBags(HrEmployeeViewModel viewModel)
        {
            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", viewModel.OrgUnitID);

            ViewBag.PositionID = new SelectList(
                await db.HR_POSITIONS.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name", viewModel.PositionID);

            ViewBag.SalaryGradeID = new SelectList(
                await db.HR_SALARY_GRADES.OrderBy(sg => sg.GradeCode).ToListAsync(),
                "ID", "Name", viewModel.SalaryGradeID);
        }

        // GET: HR/Employees/Dismiss/5
        public async Task<ActionResult> Dismiss(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var employee = await db.HR_EMPLOYEES
                .Include(e => e.HR_ORG_UNITS)
                .Include(e => e.HR_POSITIONS)
                .FirstOrDefaultAsync(e => e.ID == id);

            if (employee == null)
            {
                return HttpNotFound();
            }

            if (employee.DismissalDate != null)
            {
                TempData["Error"] = "Bu xodim allaqachon ishdan bo'shatilgan";
                return RedirectToAction("Index");
            }

            ViewBag.Employee = employee;
            return View();
        }

        // POST: HR/Employees/Dismiss/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Dismiss(int id, DateTime dismissalDate, string reason)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var employee = await db.HR_EMPLOYEES
                .Include(e => e.HR_ORG_UNITS)
                .Include(e => e.HR_POSITIONS)
                .FirstOrDefaultAsync(e => e.ID == id);

            if (employee == null)
            {
                return HttpNotFound();
            }

            if (employee.DismissalDate != null)
            {
                TempData["Error"] = "Bu xodim allaqachon ishdan bo'shatilgan";
                return RedirectToAction("Index");
            }

            // Create history record for the last position (from EmploymentDate to dismissalDate)
            if (employee.OrgUnitID.HasValue && employee.PositionID.HasValue && employee.EmploymentDate.HasValue)
            {
                try
                {
                    // Create HR_EMPLOYEE_HISTORY object using Entity Framework
                    var history = new HR_EMPLOYEE_HISTORY
                    {
                        EmployeeID = id,
                        ActionType = "History",
                        FromOrgUnitID = employee.OrgUnitID,
                        FromPositionID = employee.PositionID,
                        ToOrgUnitID = null,
                        ToPositionID = null,
                        EffectiveDateFrom = employee.EmploymentDate.Value, // Employment start date
                        EffectiveDateTo = dismissalDate, // Dismissal date
                        Reason = reason,
                        CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                        CreatedDT = DateTime.Now
                    };

                    db.HR_EMPLOYEE_HISTORY.Add(history);
                }
                catch (Exception ex)
                {
                    // Log error but continue with employee update
                    System.Diagnostics.Debug.WriteLine($"Error creating history record: {ex.Message}");
                }
            }

            // Update employee
            employee.DismissalDate = dismissalDate;
            employee.IsActive = false;
            employee.UpdatedDT = DateTime.Now;

            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Xatolik yuz berdi: {ex.Message}";
                return RedirectToAction("Dismiss", new { id });
            }

            logger.Log("HrEmployees", "Dismiss", $"ID: {id}, Date: {dismissalDate}");
            TempData["Success"] = "Xodim muvaffaqiyatli ishdan bo'shatildi";
            return RedirectToAction("Index");
        }

        // GET: HR/Employees/Transfer/5
        public async Task<ActionResult> Transfer(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var employee = await db.HR_EMPLOYEES
                .Include(e => e.HR_ORG_UNITS)
                .Include(e => e.HR_POSITIONS)
                .FirstOrDefaultAsync(e => e.ID == id);

            if (employee == null)
            {
                return HttpNotFound();
            }

            if (employee.DismissalDate != null)
            {
                TempData["Error"] = "Ishdan bo'shatilgan xodimni transfer qilib bo'lmaydi";
                return RedirectToAction("Index");
            }

            ViewBag.Employee = employee;
            ViewBag.FromOrgUnitID = employee.OrgUnitID;
            ViewBag.FromPositionID = employee.PositionID;
            ViewBag.ToOrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name");
            ViewBag.ToPositionID = new SelectList(
                await db.HR_POSITIONS.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name");

            return View();
        }

        // POST: HR/Employees/Transfer/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Transfer(int id, int toOrgUnitId, int toPositionId, DateTime effectiveDate, string reason)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var employee = await db.HR_EMPLOYEES
                .Include(e => e.HR_ORG_UNITS)
                .Include(e => e.HR_POSITIONS)
                .FirstOrDefaultAsync(e => e.ID == id);

            if (employee == null)
            {
                return HttpNotFound();
            }

            if (employee.DismissalDate != null)
            {
                TempData["Error"] = "Ishdan bo'shatilgan xodimni transfer qilib bo'lmaydi";
                return RedirectToAction("Index");
            }

            if (employee.OrgUnitID == toOrgUnitId && employee.PositionID == toPositionId)
            {
                TempData["Error"] = "Xodim allaqachon ushbu bo'lim va lavozimda";
                return RedirectToAction("Transfer", new { id });
            }

            // Create history record for the OLD position (from EmploymentDate to effectiveDate - new position start date)
            if (employee.OrgUnitID.HasValue && employee.PositionID.HasValue && employee.EmploymentDate.HasValue)
            {
                try
                {
                    // Create HR_EMPLOYEE_HISTORY object using Entity Framework
                    var history = new HR_EMPLOYEE_HISTORY
                    {
                        EmployeeID = id,
                        ActionType = "History",
                        FromOrgUnitID = employee.OrgUnitID,
                        FromPositionID = employee.PositionID,
                        ToOrgUnitID = toOrgUnitId,
                        ToPositionID = toPositionId,
                        EffectiveDateFrom = employee.EmploymentDate.Value, // Old position start date
                        EffectiveDateTo = effectiveDate, // Old position end date (new position start date)
                        Reason = reason ?? "Boshqa lavozimga o'tkazilgan",
                        CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                        CreatedDT = DateTime.Now
                    };

                    db.HR_EMPLOYEE_HISTORY.Add(history);
                }
                catch (Exception ex)
                {
                    // Log error but continue with employee update
                    System.Diagnostics.Debug.WriteLine($"Error creating history record: {ex.Message}");
                }
            }

            // Update employee to new position
            employee.OrgUnitID = toOrgUnitId;
            employee.PositionID = toPositionId;
            employee.EmploymentDate = effectiveDate; // New position start date
            employee.UpdatedDT = DateTime.Now;

            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Xatolik yuz berdi: {ex.Message}";
                return RedirectToAction("Transfer", new { id });
            }

            logger.Log("HrEmployees", "Transfer", $"ID: {id}, ToOrgUnit: {toOrgUnitId}, ToPosition: {toPositionId}");
            TempData["Success"] = "Xodim muvaffaqiyatli transfer qilindi";
            return RedirectToAction("Index");
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

