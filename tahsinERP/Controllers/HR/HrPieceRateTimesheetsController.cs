using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels.HR;
using tahsinERP.Controllers;
using OfficeOpenXml;
using System.IO;
using System.Globalization;

namespace tahsinERP.Controllers.HR
{
    [Authorize]
    public class HrPieceRateTimesheetsController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();
        private IUserActionLogger logger;

        public HrPieceRateTimesheetsController()
        {
            logger = new UserActionLogger(User?.Identity?.Name ?? "System");
        }

        // GET: HR/PieceRateTimesheets
        public async Task<ActionResult> Index(int? year, int? month, int? orgUnitId, string status)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Don't set default values if not provided - allow showing all months
            // year = year ?? DateTime.Now.Year;
            // month = month ?? DateTime.Now.Month;

            // Get work entries from HR_PIECE_RATE_WORK_ENTRIES
            IQueryable<HR_PIECE_RATE_WORK_ENTRIES> query = db.HR_PIECE_RATE_WORK_ENTRIES
                .Include(e => e.HR_EMPLOYEES)
                .Include(e => e.PRODUCT)
                .Include(e => e.HR_PIECE_RATE_OPERATIONS);

            // Apply filters only if provided
            if (year.HasValue)
            {
                query = query.Where(e => e.WorkDate.Year == year.Value);
            }

            if (month.HasValue && month.Value > 0)
            {
                query = query.Where(e => e.WorkDate.Month == month.Value);
            }

            if (orgUnitId.HasValue)
            {
                query = query.Where(e => e.HR_EMPLOYEES.OrgUnitID == orgUnitId.Value);
            }

            var workEntries = await query.OrderByDescending(e => e.WorkDate).ToListAsync();

            // Get approval statuses
            var approvals = await db.HR_PIECE_RATE_TIMESHEET_APPROVALS.ToListAsync();

            // Group by year, month, and org unit
            var groupedData = workEntries
                .GroupBy(e => new { 
                    Year = e.WorkDate.Year, 
                    Month = e.WorkDate.Month,
                    OrgUnitID = e.HR_EMPLOYEES.OrgUnitID
                })
                .Select(g => {
                    var approval = approvals.FirstOrDefault(a => 
                        a.Year == g.Key.Year && 
                        a.Month == g.Key.Month && 
                        a.OrgUnitID == g.Key.OrgUnitID);
                    
                    return new HrPieceRateTimesheetIndexViewModel
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        MonthName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month),
                        OrgUnitID = g.Key.OrgUnitID,
                        OrgUnitName = g.First().HR_EMPLOYEES.HR_ORG_UNITS?.Name ?? "Butun kompaniya",
                        CalculatedAmount = CalculatePieceRateAmountForEntries(g.ToList()),
                        CreatedDT = g.Min(e => e.CreatedDT),
                        EntryCount = g.Count(),
                        Status = approval?.Status ?? "Draft",
                        ApprovedDT = approval?.ApprovedDT,
                        ApprovedByUserID = approval?.ApprovedByUserID
                    };
                })
                .OrderByDescending(x => x.Year)
                .ThenByDescending(x => x.Month)
                .ToList();

            ViewBag.Year = year ?? DateTime.Now.Year;
            ViewBag.Month = month; // Keep null if not provided to show all months
            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnitId);

            logger.Log("HrPieceRateTimesheets", "Index");
            return View(groupedData);
        }

        private decimal CalculatePieceRateAmount(int year, int month)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Get all work entries for this month
            var workEntries = db.HR_PIECE_RATE_WORK_ENTRIES
                .Where(e => e.WorkDate >= startDate && e.WorkDate <= endDate)
                .ToList();

            return CalculatePieceRateAmountForEntries(workEntries);
        }

        private decimal CalculatePieceRateAmountForEntries(List<HR_PIECE_RATE_WORK_ENTRIES> workEntries)
        {
            decimal totalAmount = 0;

            foreach (var entry in workEntries)
            {
                // Get tariff for this product and operation
                int? positionId = entry.HR_EMPLOYEES?.PositionID;
                
                var tariff = db.HR_PIECE_RATE_TARIFFS
                    .Where(t => t.ProductID == entry.ProductID &&
                               t.OperationID == entry.OperationID &&
                               t.IsActive &&
                               t.EffectiveFrom <= entry.WorkDate &&
                               (t.EffectiveTo == null || t.EffectiveTo >= entry.WorkDate) &&
                               (positionId.HasValue ? t.PositionID == positionId.Value : t.PositionID == null))
                    .OrderByDescending(t => t.PositionID) // Prefer position-specific
                    .FirstOrDefault();

                // If not found, try general tariff (PositionID == null)
                if (tariff == null)
                {
                    tariff = db.HR_PIECE_RATE_TARIFFS
                        .Where(t => t.ProductID == entry.ProductID &&
                                   t.OperationID == entry.OperationID &&
                                   t.PositionID == null &&
                                   t.IsActive &&
                                   t.EffectiveFrom <= entry.WorkDate &&
                                   (t.EffectiveTo == null || t.EffectiveTo >= entry.WorkDate))
                        .FirstOrDefault();
                }

                if (tariff != null)
                {
                    totalAmount += entry.Quantity * tariff.RatePerUnit;
                }
            }

            return totalAmount;
        }

        // GET: HR/PieceRateTimesheets/Create
        public ActionResult Create()
        {
            // Redirect to CreateSimple for simple entry
            return RedirectToAction("CreateSimple");
        }

        // GET: HR/PieceRateTimesheets/Details
        public async Task<ActionResult> Details(int year, int month, int? orgUnitId)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            IQueryable<HR_PIECE_RATE_WORK_ENTRIES> query = db.HR_PIECE_RATE_WORK_ENTRIES
                .Include(e => e.HR_EMPLOYEES)
                .Include(e => e.HR_EMPLOYEES.HR_ORG_UNITS)
                .Include(e => e.HR_EMPLOYEES.HR_POSITIONS)
                .Include(e => e.PRODUCT)
                .Include(e => e.HR_PIECE_RATE_OPERATIONS)
                .Where(e => e.WorkDate >= startDate && e.WorkDate <= endDate);

            if (orgUnitId.HasValue)
            {
                query = query.Where(e => e.HR_EMPLOYEES.OrgUnitID == orgUnitId.Value);
            }

            var workEntries = await query.OrderBy(e => e.WorkDate).ThenBy(e => e.HR_EMPLOYEES.FullName).ToListAsync();

            // Calculate tariffs for each entry
            var viewModel = new List<HrPieceRateTimesheetDetailsViewModel>();
            foreach (var entry in workEntries)
            {
                int? positionId = entry.HR_EMPLOYEES?.PositionID;
                
                var tariff = db.HR_PIECE_RATE_TARIFFS
                    .Include(t => t.CURRENCy)
                    .Where(t => t.ProductID == entry.ProductID &&
                               t.OperationID == entry.OperationID &&
                               t.IsActive &&
                               t.EffectiveFrom <= entry.WorkDate &&
                               (t.EffectiveTo == null || t.EffectiveTo >= entry.WorkDate) &&
                               (positionId.HasValue ? t.PositionID == positionId.Value : t.PositionID == null))
                    .OrderByDescending(t => t.PositionID)
                    .FirstOrDefault();

                // If not found, try general tariff (PositionID == null)
                if (tariff == null)
                {
                    tariff = db.HR_PIECE_RATE_TARIFFS
                        .Include(t => t.CURRENCy)
                        .Where(t => t.ProductID == entry.ProductID &&
                                   t.OperationID == entry.OperationID &&
                                   t.PositionID == null &&
                                   t.IsActive &&
                                   t.EffectiveFrom <= entry.WorkDate &&
                                   (t.EffectiveTo == null || t.EffectiveTo >= entry.WorkDate))
                        .FirstOrDefault();
                }

                decimal entryAmount = 0;
                if (tariff != null)
                {
                    entryAmount = entry.Quantity * tariff.RatePerUnit;
                }

                viewModel.Add(new HrPieceRateTimesheetDetailsViewModel
                {
                    Entry = entry,
                    Tariff = tariff,
                    EntryAmount = entryAmount
                });
            }

            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.MonthName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);
            ViewBag.OrgUnitName = orgUnitId.HasValue ? 
                (await db.HR_ORG_UNITS.FindAsync(orgUnitId.Value))?.Name : "Butun kompaniya";

            return View(viewModel);
        }

        // GET: HR/PieceRateTimesheets/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var header = await db.HR_TIMESHEET_HEADERS
                .Include(t => t.HR_ORG_UNITS)
                .Include(t => t.HR_TIMESHEET_LINES.Select(l => l.HR_EMPLOYEES.HR_POSITIONS))
                .FirstOrDefaultAsync(t => t.ID == id);

            if (header == null)
            {
                return HttpNotFound();
            }

            // Get only piece-rate employees
            var pieceRatePositionIds = await db.HR_STAFFING_PLAN
                .Where(sp => sp.IsActive && sp.IsPieceRate == true && sp.OrgUnitID == header.OrgUnitID)
                .Select(sp => sp.PositionID)
                .Distinct()
                .ToListAsync();

            var viewModel = new HrTimesheetHeaderViewModel
            {
                ID = header.ID,
                Year = header.Year,
                Month = header.Month,
                OrgUnitID = header.OrgUnitID,
                OrgUnitName = header.HR_ORG_UNITS?.Name,
                Status = header.Status,
                CreatedByUserID = header.CreatedByUserID,
                CreatedDT = header.CreatedDT,
                ApprovedByUserID = header.ApprovedByUserID,
                ApprovedDT = header.ApprovedDT
            };

            var daysInMonth = DateTime.DaysInMonth(header.Year, header.Month);

            viewModel.Lines = header.HR_TIMESHEET_LINES
                .Where(l => pieceRatePositionIds.Contains(l.HR_EMPLOYEES.PositionID ?? 0))
                .Select(l =>
                {
                    var employee = l.HR_EMPLOYEES;
                    return new HrTimesheetLineViewModel
                    {
                        ID = l.ID,
                        TimesheetID = l.TimesheetID,
                        EmployeeID = l.EmployeeID,
                        EmployeeTabNo = employee.TabNo,
                        EmployeeFullName = employee.FullName,
                        PositionName = employee.HR_POSITIONS?.Name,
                        Day01Hours = l.Day01Hours,
                        Day02Hours = l.Day02Hours,
                        Day03Hours = l.Day03Hours,
                        Day04Hours = l.Day04Hours,
                        Day05Hours = l.Day05Hours,
                        Day06Hours = l.Day06Hours,
                        Day07Hours = l.Day07Hours,
                        Day08Hours = l.Day08Hours,
                        Day09Hours = l.Day09Hours,
                        Day10Hours = l.Day10Hours,
                        Day11Hours = l.Day11Hours,
                        Day12Hours = l.Day12Hours,
                        Day13Hours = l.Day13Hours,
                        Day14Hours = l.Day14Hours,
                        Day15Hours = l.Day15Hours,
                        Day16Hours = l.Day16Hours,
                        Day17Hours = l.Day17Hours,
                        Day18Hours = l.Day18Hours,
                        Day19Hours = l.Day19Hours,
                        Day20Hours = l.Day20Hours,
                        Day21Hours = l.Day21Hours,
                        Day22Hours = l.Day22Hours,
                        Day23Hours = l.Day23Hours,
                        Day24Hours = l.Day24Hours,
                        Day25Hours = l.Day25Hours,
                        Day26Hours = l.Day26Hours,
                        Day27Hours = l.Day27Hours,
                        Day28Hours = l.Day28Hours,
                        Day29Hours = l.Day29Hours,
                        Day30Hours = l.Day30Hours,
                        Day31Hours = l.Day31Hours,
                        TotalWorkedDays = l.TotalWorkedDays ?? 0,
                        TotalWorkedHours = l.TotalWorkedHours ?? 0,
                        OvertimeHours = l.OvertimeHours ?? 0,
                        AbsentDays = l.AbsentDays ?? 0,
                        Notes = l.Notes,
                        DaysInMonth = daysInMonth,
                        IsPieceRate = true,
                        ProductID = l.ProductID,
                        ProductName = l.PRODUCT?.PNo,
                        Day01Quantity = l.Day01Quantity,
                        Day02Quantity = l.Day02Quantity,
                        Day03Quantity = l.Day03Quantity,
                        Day04Quantity = l.Day04Quantity,
                        Day05Quantity = l.Day05Quantity,
                        Day06Quantity = l.Day06Quantity,
                        Day07Quantity = l.Day07Quantity,
                        Day08Quantity = l.Day08Quantity,
                        Day09Quantity = l.Day09Quantity,
                        Day10Quantity = l.Day10Quantity,
                        Day11Quantity = l.Day11Quantity,
                        Day12Quantity = l.Day12Quantity,
                        Day13Quantity = l.Day13Quantity,
                        Day14Quantity = l.Day14Quantity,
                        Day15Quantity = l.Day15Quantity,
                        Day16Quantity = l.Day16Quantity,
                        Day17Quantity = l.Day17Quantity,
                        Day18Quantity = l.Day18Quantity,
                        Day19Quantity = l.Day19Quantity,
                        Day20Quantity = l.Day20Quantity,
                        Day21Quantity = l.Day21Quantity,
                        Day22Quantity = l.Day22Quantity,
                        Day23Quantity = l.Day23Quantity,
                        Day24Quantity = l.Day24Quantity,
                        Day25Quantity = l.Day25Quantity,
                        Day26Quantity = l.Day26Quantity,
                        Day27Quantity = l.Day27Quantity,
                        Day28Quantity = l.Day28Quantity,
                        Day29Quantity = l.Day29Quantity,
                        Day30Quantity = l.Day30Quantity,
                        Day31Quantity = l.Day31Quantity,
                        TotalQuantity = l.TotalQuantity
                    };
                }).OrderBy(l => l.EmployeeTabNo).ToList();

            // Get finished goods for piece-rate selection
            ViewBag.FinishedGoods = new SelectList(
                await db.PRODUCTS.Where(p => p.IsFinishedGood == true && p.IsDeleted == false)
                    .OrderBy(p => p.PNo).ToListAsync(),
                "ID", "PNo");

            logger.Log("HrPieceRateTimesheets", "Edit", $"ID: {id}");
            return View(viewModel);
        }

        // POST: HR/PieceRateTimesheets/UpdateQuantity
        [HttpPost]
        public async Task<JsonResult> UpdateQuantity(int lineId, int day, decimal? quantity, int? productId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return Json(new { success = false, message = "Ruxsat yo'q" });
            }

            try
            {
                var line = await db.HR_TIMESHEET_LINES.FindAsync(lineId);
                if (line == null)
                {
                    return Json(new { success = false, message = "Qator topilmadi" });
                }

                if (productId.HasValue)
                {
                    line.ProductID = productId.Value;
                }

                var propertyName = $"Day{day:D2}Quantity";
                var property = typeof(HR_TIMESHEET_LINES).GetProperty(propertyName);
                if (property != null)
                {
                    property.SetValue(line, quantity);
                }

                await RecalculateLineTotals(line);
                await db.SaveChangesAsync();

                logger.Log("HrPieceRateTimesheets", "UpdateQuantity", $"LineID: {lineId}, Day: {day}, Quantity: {quantity}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: HR/PieceRateTimesheets/ImportFromProduction
        [HttpPost]
        public async Task<JsonResult> ImportFromProduction(int timesheetId, int employeeId, int year, int month)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return Json(new { success = false, message = "Ruxsat yo'q" });
            }

            try
            {
                var line = await db.HR_TIMESHEET_LINES
                    .FirstOrDefaultAsync(l => l.TimesheetID == timesheetId && l.EmployeeID == employeeId);

                if (line == null)
                {
                    return Json(new { success = false, message = "Qator topilmadi" });
                }

                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                var productions = await db.PRODUCT_PRODUCTION
                    .Where(p => p.Date >= startDate && p.Date <= endDate && 
                                p.PRODUCT.IsFinishedGood == true)
                    .GroupBy(p => new { p.Date, p.ProductID })
                    .Select(g => new { g.Key.Date, g.Key.ProductID, Total = g.Sum(p => p.Total) })
                    .ToListAsync();

                int daysInMonth = DateTime.DaysInMonth(year, month);
                foreach (var prod in productions)
                {
                    int day = prod.Date.Day;
                    if (day <= daysInMonth)
                    {
                        var quantityProperty = typeof(HR_TIMESHEET_LINES).GetProperty($"Day{day:D2}Quantity");
                        quantityProperty?.SetValue(line, (decimal?)prod.Total);

                        if (line.ProductID == null)
                        {
                            line.ProductID = prod.ProductID;
                        }
                    }
                }

                await RecalculateLineTotals(line);
                await db.SaveChangesAsync();

                logger.Log("HrPieceRateTimesheets", "ImportFromProduction", $"TimesheetID: {timesheetId}, EmployeeID: {employeeId}");
                return Json(new { success = true, message = "Ishbay ma'lumotlari import qilindi" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: HR/PieceRateTimesheets/Approve
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Approve(int year, int? month, int? orgUnitId)
        {
            // Check permission for Approve action specifically
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                TempData["Error"] = "Sizda tasdiqlash huquqi yo'q";
                return RedirectToAction("Index", new { year, month, orgUnitId });
            }

            // Validate month parameter
            if (!month.HasValue || month.Value < 1 || month.Value > 12)
            {
                TempData["Error"] = "Noto'g'ri oy parametri";
                return RedirectToAction("Index", new { year, month, orgUnitId });
            }

            // Check if there are any work entries for this period
            var startDate = new DateTime(year, month.Value, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            IQueryable<HR_PIECE_RATE_WORK_ENTRIES> query = db.HR_PIECE_RATE_WORK_ENTRIES
                .Where(e => e.WorkDate >= startDate && e.WorkDate <= endDate);

            if (orgUnitId.HasValue)
            {
                query = query.Where(e => e.HR_EMPLOYEES.OrgUnitID == orgUnitId.Value);
            }

            var hasEntries = await query.AnyAsync();
            if (!hasEntries)
            {
                TempData["Error"] = "Bu oy uchun ishbay ma'lumotlari topilmadi";
                return RedirectToAction("Index", new { year, month, orgUnitId });
            }

            // Get or create approval record
            var approval = await db.HR_PIECE_RATE_TIMESHEET_APPROVALS
                .FirstOrDefaultAsync(a => a.Year == year && a.Month == month.Value && a.OrgUnitID == orgUnitId);

            if (approval == null)
            {
                approval = new HR_PIECE_RATE_TIMESHEET_APPROVALS
                {
                    Year = year,
                    Month = month?? 0,
                    OrgUnitID = orgUnitId,
                    Status = "Draft",
                    CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                    CreatedDT = DateTime.Now
                };
                db.HR_PIECE_RATE_TIMESHEET_APPROVALS.Add(approval);
            }
            else
            {
                if (approval.Status == "Approved")
                {
                    TempData["Error"] = "Bu tabel allaqachon tasdiqlangan";
                    return RedirectToAction("Index", new { year, month, orgUnitId });
                }
            }

            approval.Status = "Approved";
            approval.ApprovedByUserID = UserHelper.GetUserId(User.Identity.Name);
            approval.ApprovedDT = DateTime.Now;
            approval.ModifiedByUserID = UserHelper.GetUserId(User.Identity.Name);
            approval.ModifiedDT = DateTime.Now;

            await db.SaveChangesAsync();

            logger.Log("HrPieceRateTimesheets", "Approve", $"Year: {year}, Month: {month.Value}, OrgUnitID: {orgUnitId}");
            TempData["Success"] = "Tabel muvaffaqiyatli tasdiqlandi";
            return RedirectToAction("Index", new { year, month = month.Value, orgUnitId });
        }

        private async Task CreatePieceRateTimesheetLines(int timesheetId, int? orgUnitId)
        {
            // Get piece-rate positions
            var pieceRatePositionIds = await db.HR_STAFFING_PLAN
                .Where(sp => sp.IsActive && sp.IsPieceRate == true && 
                    (orgUnitId == null || sp.OrgUnitID == orgUnitId.Value))
                .Select(sp => sp.PositionID)
                .Distinct()
                .ToListAsync();

            // Get employees with piece-rate positions
            IQueryable<HR_EMPLOYEES> employeesQuery = db.HR_EMPLOYEES
                .Where(e => e.IsActive && pieceRatePositionIds.Contains(e.PositionID ?? 0));

            if (orgUnitId.HasValue)
            {
                employeesQuery = employeesQuery.Where(e => e.OrgUnitID == orgUnitId.Value);
            }

            var employees = await employeesQuery.ToListAsync();

            foreach (var employee in employees)
            {
                var line = new HR_TIMESHEET_LINES
                {
                    TimesheetID = timesheetId,
                    EmployeeID = employee.ID
                };

                db.HR_TIMESHEET_LINES.Add(line);
            }

            await db.SaveChangesAsync();
        }

        private async Task RecalculateLineTotals(HR_TIMESHEET_LINES line)
        {
            var header = await db.HR_TIMESHEET_HEADERS.FindAsync(line.TimesheetID);
            if (header == null) return;

            int daysInMonth = DateTime.DaysInMonth(header.Year, header.Month);
            decimal totalHours = 0;
            decimal workedDays = 0;
            decimal absentDays = 0;
            decimal overtimeHours = 0;
            decimal totalQuantity = 0;

            for (int day = 1; day <= daysInMonth; day++)
            {
                var hoursPropertyName = $"Day{day:D2}Hours";
                var hoursProperty = typeof(HR_TIMESHEET_LINES).GetProperty(hoursPropertyName);
                var hours = hoursProperty?.GetValue(line) as decimal? ?? 0;

                var quantityPropertyName = $"Day{day:D2}Quantity";
                var quantityProperty = typeof(HR_TIMESHEET_LINES).GetProperty(quantityPropertyName);
                var quantity = quantityProperty?.GetValue(line) as decimal? ?? 0;

                if (hours > 0)
                {
                    totalHours += hours;
                    workedDays += 1;
                    if (hours > 8)
                    {
                        overtimeHours += (hours - 8);
                    }
                }
                else
                {
                    absentDays += 1;
                }

                if (quantity > 0)
                {
                    totalQuantity += quantity;
                }
            }

            line.TotalWorkedHours = totalHours;
            line.TotalWorkedDays = workedDays;
            line.AbsentDays = absentDays;
            line.OvertimeHours = overtimeHours;
            line.TotalQuantity = totalQuantity;
        }

        private async Task PopulateViewBags(HrTimesheetHeaderViewModel viewModel)
        {
            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", viewModel.OrgUnitID);
        }

        // GET: HR/PieceRateTimesheets/CreateSimple
        public async Task<ActionResult> CreateSimple()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            await PopulateCreateSimpleViewBags();
            return View();
        }

        // POST: HR/PieceRateTimesheets/CreateSimple
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateSimple(int EmployeeID, int ProductID, int OperationID, DateTime WorkDate, decimal Quantity)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            try
            {
                // Check if work entry already exists for this employee, product, operation, and date
                var existing = await db.HR_PIECE_RATE_WORK_ENTRIES
                    .FirstOrDefaultAsync(e => e.EmployeeID == EmployeeID && 
                                             e.ProductID == ProductID && 
                                             e.OperationID == OperationID && 
                                             e.WorkDate == WorkDate);

                if (existing != null)
                {
                    // Update existing entry
                    existing.Quantity = Quantity;
                    existing.CreatedByUserID = UserHelper.GetUserId(User.Identity.Name);
                    existing.CreatedDT = DateTime.Now;
                }
                else
                {
                    // Create new entry
                    var workEntry = new HR_PIECE_RATE_WORK_ENTRIES
                    {
                        EmployeeID = EmployeeID,
                        ProductID = ProductID,
                        OperationID = OperationID,
                        WorkDate = WorkDate,
                        Quantity = Quantity,
                        CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                        CreatedDT = DateTime.Now
                    };

                    db.HR_PIECE_RATE_WORK_ENTRIES.Add(workEntry);
                }

                await db.SaveChangesAsync();

                logger.Log("HrPieceRateTimesheets", "CreateSimple", $"EmployeeID: {EmployeeID}, ProductID: {ProductID}, OperationID: {OperationID}, Date: {WorkDate}, Quantity: {Quantity}");
                TempData["Success"] = "Ishbay ma'lumot muvaffaqiyatli saqlandi";
                return RedirectToAction("CreateSimple");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Xatolik: " + ex.Message;
                await PopulateCreateSimpleViewBags();
                return View();
            }
        }

        // GET: HR/PieceRateTimesheets/GetTariff
        public async Task<JsonResult> GetTariff(int productId, int operationId, int? employeeId = null)
        {
            try
            {
                int? positionId = null;
                if (employeeId.HasValue)
                {
                    var employee = await db.HR_EMPLOYEES.FindAsync(employeeId.Value);
                    if (employee != null)
                    {
                        positionId = employee.PositionID;
                    }
                }

                // First try to find position-specific tariff
                var tariff = await db.HR_PIECE_RATE_TARIFFS
                    .Include(t => t.CURRENCy)
                    .Where(t => t.ProductID == productId && 
                               t.OperationID == operationId && 
                               t.IsActive &&
                               t.EffectiveFrom <= DateTime.Now &&
                               (t.EffectiveTo == null || t.EffectiveTo >= DateTime.Now) &&
                               (positionId.HasValue ? t.PositionID == positionId.Value : t.PositionID == null))
                    .OrderByDescending(t => t.PositionID) // Prefer position-specific
                    .FirstOrDefaultAsync();

                // If not found, try general tariff (PositionID == null)
                if (tariff == null)
                {
                    tariff = await db.HR_PIECE_RATE_TARIFFS
                        .Include(t => t.CURRENCy)
                        .Where(t => t.ProductID == productId && 
                                   t.OperationID == operationId && 
                                   t.PositionID == null &&
                                   t.IsActive &&
                                   t.EffectiveFrom <= DateTime.Now &&
                                   (t.EffectiveTo == null || t.EffectiveTo >= DateTime.Now))
                        .FirstOrDefaultAsync();
                }

                if (tariff != null)
                {
                    return Json(new { 
                        success = true, 
                        tariff = new { 
                            ratePerUnit = tariff.RatePerUnit,
                            currencyShortname = tariff.CURRENCy?.Shortname ?? ""
                        } 
                    }, JsonRequestBehavior.AllowGet);
                }

                return Json(new { success = false, message = "Tarif topilmadi" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        private async Task PopulateCreateSimpleViewBags()
        {
            // Get piece-rate employees
            var pieceRatePositionIds = await db.HR_STAFFING_PLAN
                .Where(sp => sp.IsActive && sp.IsPieceRate == true)
                .Select(sp => sp.PositionID)
                .Distinct()
                .ToListAsync();

            // If no piece-rate positions found, get all active employees
            // Otherwise, get employees with piece-rate positions
            List<HR_EMPLOYEES> employees;
            if (pieceRatePositionIds == null || !pieceRatePositionIds.Any())
            {
                // Fallback: get all active employees if no piece-rate positions configured
                employees = await db.HR_EMPLOYEES
                    .Where(e => e.IsActive)
                    .OrderBy(e => e.FullName)
                    .ToListAsync();
            }
            else
            {
                // Get employees where PositionID matches piece-rate positions
                employees = await db.HR_EMPLOYEES
                    .Where(e => e.IsActive && e.PositionID.HasValue && pieceRatePositionIds.Contains(e.PositionID.Value))
                    .OrderBy(e => e.FullName)
                    .ToListAsync();
            }

            ViewBag.EmployeeID = new SelectList(
                employees,
                "ID", "FullName");

            ViewBag.ProductID = new SelectList(
                await db.PRODUCTS.Where(p => p.IsFinishedGood == true && p.IsDeleted == false)
                    .OrderBy(p => p.PNo).ToListAsync(),
                "ID", "PNo");

            ViewBag.OperationID = new SelectList(
                await db.HR_PIECE_RATE_OPERATIONS.Where(o => o.IsActive).OrderBy(o => o.SortOrder).ToListAsync(),
                "ID", "Name");
        }

        // GET: HR/PieceRateTimesheets/Delete/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var timesheet = await db.HR_TIMESHEET_HEADERS
                .Include(t => t.HR_ORG_UNITS)
                .FirstOrDefaultAsync(t => t.ID == id);

            if (timesheet == null)
            {
                return HttpNotFound();
            }

            return View(timesheet);
        }

        // POST: HR/PieceRateTimesheets/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var timesheet = await db.HR_TIMESHEET_HEADERS.FindAsync(id);
            if (timesheet == null)
            {
                return HttpNotFound();
            }

            // Check if timesheet is approved or closed - cannot delete
            if (timesheet.Status == "Approved" || timesheet.Status == "Closed")
            {
                TempData["Error"] = "Tasdiqlangan yoki yopilgan tabelni o'chirib bo'lmaydi";
                return RedirectToAction("Index");
            }

            // Check if there are related payroll records
            var hasPayrolls = await db.HR_PAYROLL_HEADERS.AnyAsync(p => p.TimesheetID == id);
            if (hasPayrolls)
            {
                TempData["Error"] = "Bu tabelga bog'liq ish haqi hisobotlari mavjud. Avval ish haqi hisobotlarini o'chiring.";
                return RedirectToAction("Index");
            }

            // Delete timesheet lines first
            var lines = await db.HR_TIMESHEET_LINES.Where(l => l.TimesheetID == id).ToListAsync();
            foreach (var line in lines)
            {
                line.IsDeleted = true;
                db.Entry(line).State = System.Data.Entity.EntityState.Modified;
            }

            timesheet.IsDeleted = true;
            db.Entry(timesheet).State = System.Data.Entity.EntityState.Modified;
            await db.SaveChangesAsync();

            logger.Log("HrPieceRateTimesheets", "Delete", $"ID: {id}");
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

        // GET: HR/PieceRateTimesheets/DeleteEntries
        public async Task<ActionResult> DeleteEntries(int year, int month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            IQueryable<HR_PIECE_RATE_WORK_ENTRIES> query = db.HR_PIECE_RATE_WORK_ENTRIES
                .Include(e => e.HR_EMPLOYEES)
                .Include(e => e.HR_EMPLOYEES.HR_ORG_UNITS)
                .Where(e => e.WorkDate >= startDate && e.WorkDate <= endDate);

            if (orgUnitId.HasValue)
            {
                query = query.Where(e => e.HR_EMPLOYEES.OrgUnitID == orgUnitId.Value);
            }

            var workEntries = await query.ToListAsync();
            var entryCount = workEntries.Count;

            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.OrgUnitId = orgUnitId;
            ViewBag.EntryCount = entryCount;
            ViewBag.MonthName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);
            ViewBag.OrgUnitName = workEntries.FirstOrDefault()?.HR_EMPLOYEES.HR_ORG_UNITS?.Name ?? "Butun kompaniya";

            return View();
        }

        // POST: HR/PieceRateTimesheets/DeleteEntries
        [HttpPost, ActionName("DeleteEntries")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteEntriesConfirmed(int year, int month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Check if there are related payroll records
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Get work entries for this period
            IQueryable<HR_PIECE_RATE_WORK_ENTRIES> query = db.HR_PIECE_RATE_WORK_ENTRIES
                .Where(e => e.WorkDate >= startDate && e.WorkDate <= endDate);

            if (orgUnitId.HasValue)
            {
                query = query.Where(e => e.HR_EMPLOYEES.OrgUnitID == orgUnitId.Value);
            }

            var workEntries = await query.ToListAsync();

            // Check if any payroll exists for this period
            var hasPayrolls = await db.HR_PAYROLL_HEADERS
                .AnyAsync(p => p.Year == year && p.Month == month && 
                    p.PayrollType == "PieceRate" &&
                    (orgUnitId == null ? p.OrgUnitID == null : p.OrgUnitID == orgUnitId.Value));

            if (hasPayrolls)
            {
                TempData["Error"] = "Bu oy uchun ish haqi hisobotlari mavjud. Avval ish haqi hisobotlarini o'chiring.";
                return RedirectToAction("Index", new { year, month, orgUnitId });
            }

            // Check if timesheet is approved
            var approval = await db.HR_PIECE_RATE_TIMESHEET_APPROVALS
                .FirstOrDefaultAsync(a => a.Year == year && a.Month == month && a.OrgUnitID == orgUnitId);

            if (approval != null && approval.Status == "Approved")
            {
                TempData["Error"] = "Tasdiqlangan tabelni o'chirib bo'lmaydi. Avval tasdiqlashni bekor qiling.";
                return RedirectToAction("Index", new { year, month, orgUnitId });
            }

            // Soft delete work entries
            foreach (var entry in workEntries)
            {
                entry.IsDeleted = true;
                db.Entry(entry).State = System.Data.Entity.EntityState.Modified;
            }

            // Soft delete approval record if exists
            if (approval != null)
            {
                approval.IsDeleted = true;
                db.Entry(approval).State = System.Data.Entity.EntityState.Modified;
            }

            await db.SaveChangesAsync();

            logger.Log("HrPieceRateTimesheets", "DeleteEntries", $"Year: {year}, Month: {month}, OrgUnitID: {orgUnitId}");
            TempData["Success"] = $"{workEntries.Count} ta yozuv muvaffaqiyatli o'chirildi";
            return RedirectToAction("Index", new { year, month, orgUnitId });
        }

        // GET: HR/PieceRateTimesheets/ImportFromExcel
        public ActionResult ImportFromExcel()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            return View();
        }

        // POST: HR/PieceRateTimesheets/ImportFromExcel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ImportFromExcel(HttpPostedFileBase file)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (file == null || file.ContentLength == 0)
            {
                TempData["Error"] = "Fayl yuklanmadi yoki bo'sh!";
                return View();
            }

            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Faqat .xlsx formatidagi fayllarni yuklash mumkin!";
                return View();
            }

            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                var importResults = new List<string>();
                int successCount = 0;
                int skipCount = 0;
                int errorCount = 0;

                using (var package = new ExcelPackage(file.InputStream))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    if (worksheet.Dimension == null)
                    {
                        TempData["Error"] = "Excel fayl bo'sh!";
                        return View();
                    }

                    int rowCount = worksheet.Dimension.Rows;
                    int colCount = worksheet.Dimension.Columns;

                    // Find column indices
                    // Excel shablon struktura:
                    // Row 1: Header (A1:J1)
                    //   A: # (raqam - e'tiborsiz)
                    //   B: Tabel. Code
                    //   C: Ф.И.О
                    //   D: Sana
                    //   E: Maxsulot kodi
                    //   F: kesish
                    //   G: poddon
                    //   H: ramka
                    //   I: selofan /DVP /Karton
                    //   J: ichki sxema
                    // Row 2+: Data rows
                    
                    int colIndex = -1, fullNameColIndex = -1, dateColIndex = -1, productCodeColIndex = -1;
                    Dictionary<string, int> operationColumns = new Dictionary<string, int>();

                    // Read header row (row 1) to find column indices
                    for (int col = 1; col <= colCount; col++)
                    {
                        string header = worksheet.Cells[1, col].Text.Trim();
                        string headerLower = header.ToLower();
                        string headerNormalized = headerLower.Replace(" ", "").Replace("/", "").Replace("\\", "").Replace(".", "");
                        
                        // Tabel. Code ustuni (B ustuni)
                        if (headerNormalized.Contains("tabel") && headerNormalized.Contains("code"))
                            colIndex = col;
                        // Ф.И.О ustuni (C ustuni) - Cyrillic yoki Latin
                        else if (header.Contains("Ф.И.О") || header.Contains("Ф.И.О") || 
                                 headerNormalized.Contains("fio") || headerNormalized.Contains("fullname"))
                            fullNameColIndex = col;
                        // Sana ustuni (D ustuni)
                        else if (headerLower == "sana" || headerLower.Contains("date"))
                            dateColIndex = col;
                        // Maxsulot kodi ustuni (E ustuni)
                        else if ((headerLower.Contains("maxsulot") && headerLower.Contains("kodi")) ||
                                 (headerLower.Contains("product") && headerLower.Contains("code")))
                            productCodeColIndex = col;
                        // Operatsiya ustunlari
                        // kesish (F ustuni)
                        else if (headerLower == "kesish" || headerLower.Contains("cutting"))
                            operationColumns["kesish"] = col;
                        // poddon (G ustuni)
                        else if (headerLower == "poddon" || headerLower.Contains("pallet"))
                            operationColumns["poddon"] = col;
                        // ramka (H ustuni)
                        else if (headerLower == "ramka" || headerLower.Contains("frame"))
                            operationColumns["ramka"] = col;
                        // selofan /DVP /Karton (I ustuni)
                        else if (headerLower.Contains("selofan") || headerLower.Contains("dvp") || 
                                 headerLower.Contains("karton") || headerLower.Contains("cellophane") || 
                                 headerLower.Contains("carton"))
                            operationColumns["selofan"] = col;
                        // ichki sxema (J ustuni)
                        else if (headerLower.Contains("ichki") && headerLower.Contains("sxema") ||
                                 headerLower.Contains("internal") && headerLower.Contains("scheme"))
                            operationColumns["ichki sxema"] = col;
                    }

                    // Tabel. Code majburiy, Ф.И.О ixtiyoriy (faqat tekshirish uchun)
                    if (colIndex == -1 || dateColIndex == -1 || productCodeColIndex == -1)
                    {
                        TempData["Error"] = "Excel faylda kerakli ustunlar topilmadi! (Tabel. Code, Sana, Maxsulot kodi)";
                        return View();
                    }

                    if (operationColumns.Count == 0)
                    {
                        TempData["Error"] = "Excel faylda operatsiya ustunlari topilmadi!";
                        return View();
                    }

                    // Get all employees (including inactive) for lookup
                    var allEmployees = await db.HR_EMPLOYEES
                        .ToListAsync();

                    // Get all products with IsFinishedGood category
                    var validProducts = await db.PRODUCTS
                        .Include(p => p.PRODUCT_CATEGORIES)
                        .Where(p => p.IsDeleted == false && 
                            (p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.IsFinishedGood == true))
                        .ToListAsync();

                    // Get all operations
                    var allOperations = await db.HR_PIECE_RATE_OPERATIONS
                        .Where(o => o.IsActive)
                        .ToListAsync();

                    // Get all tariffs
                    var allTariffs = await db.HR_PIECE_RATE_TARIFFS
                        .Where(t => t.IsActive && t.IsDeleted != true)
                        .Include(t => t.PRODUCT)
                        .ToListAsync();

                    var userId = UserHelper.GetUserId(User.Identity.Name);
                    var currentDate = DateTime.Now;

                    // Process data rows (starting from row 2, as row 1 is header)
                    for (int row = 2; row <= rowCount; row++)
                    {
                        try
                        {
                            // Read row data
                            string tabelCode = colIndex > 0 ? worksheet.Cells[row, colIndex].Text.Trim() : "";
                            string fullName = fullNameColIndex > 0 ? worksheet.Cells[row, fullNameColIndex].Text.Trim() : "";
                            string dateStr = worksheet.Cells[row, dateColIndex].Text.Trim();
                            string productCode = worksheet.Cells[row, productCodeColIndex].Text.Trim();

                            // Skip empty rows
                            if (string.IsNullOrWhiteSpace(tabelCode) && string.IsNullOrWhiteSpace(productCode))
                                continue;

                            // Tabel. Code majburiy
                            if (string.IsNullOrWhiteSpace(tabelCode))
                            {
                                importResults.Add($"Qator {row}: Tabel. Code bo'sh bo'lishi mumkin emas");
                                errorCount++;
                                continue;
                            }

                            // Parse date
                            DateTime workDate;
                            if (!DateTime.TryParse(dateStr, out workDate))
                            {
                                importResults.Add($"Qator {row}: Sana noto'g'ri format - {dateStr}");
                                errorCount++;
                                continue;
                            }

                            // Find employee by Tabel. Code (TabNo)
                            var employee = allEmployees.FirstOrDefault(e => 
                                e.TabNo != null && e.TabNo.Trim().Equals(tabelCode.Trim(), StringComparison.OrdinalIgnoreCase));

                            if (employee == null)
                            {
                                importResults.Add($"Qator {row}: Tabel. Code bo'yicha xodim topilmadi - {tabelCode}");
                                skipCount++;
                                continue;
                            }

                            // Optional: Verify FullName matches if provided (for validation)
                            if (!string.IsNullOrWhiteSpace(fullName))
                            {
                                if (!employee.FullName.Trim().Equals(fullName.Trim(), StringComparison.OrdinalIgnoreCase))
                                {
                                    importResults.Add($"Qator {row}: Tabel. Code ({tabelCode}) va Ф.И.О ({fullName}) mos kelmaydi. Xodim: {employee.FullName}");
                                    // Continue anyway, as Tabel. Code is the primary identifier
                                }
                            }

                            // Find product by PNo
                            var product = validProducts.FirstOrDefault(p => 
                                p.PNo != null && p.PNo.Trim().Equals(productCode, StringComparison.OrdinalIgnoreCase));

                            if (product == null)
                            {
                                importResults.Add($"Qator {row}: Maxsulot topilmadi yoki IsFinishedGood emas - {productCode}");
                                skipCount++;
                                continue;
                            }

                            // Process each operation column
                            foreach (var operationCol in operationColumns)
                            {
                                string operationName = operationCol.Key;
                                int operationColIndex = operationCol.Value;

                                string quantityStr = worksheet.Cells[row, operationColIndex].Text.Trim();
                                if (string.IsNullOrWhiteSpace(quantityStr))
                                    continue;

                                // Parse quantity
                                decimal quantity;
                                if (!decimal.TryParse(quantityStr, out quantity) || quantity <= 0)
                                    continue;

                                // Find operation by name (case-insensitive partial match)
                                var operation = allOperations.FirstOrDefault(o => 
                                    o.Name != null && o.Name.Trim().ToLower().Contains(operationName.ToLower()));

                                if (operation == null)
                                {
                                    importResults.Add($"Qator {row}: Operatsiya topilmadi - {operationName}");
                                    skipCount++;
                                    continue;
                                }

                                // Check for duplicate entry
                                bool exists = await db.HR_PIECE_RATE_WORK_ENTRIES
                                    .AnyAsync(e => e.EmployeeID == employee.ID &&
                                                  e.ProductID == product.ID &&
                                                  e.OperationID == operation.ID &&
                                                  e.WorkDate == workDate.Date &&
                                                  e.Quantity == quantity &&
                                                  e.IsDeleted != true);

                                if (exists)
                                {
                                    importResults.Add($"Qator {row}: Bu yozuv allaqachon mavjud (Tabel. Code: {tabelCode}, Xodim: {employee.FullName}, Maxsulot: {productCode}, Operatsiya: {operationName}, Miqdor: {quantity}, Sana: {workDate:dd.MM.yyyy})");
                                    skipCount++;
                                    continue;
                                }

                                // Find tariff for this product and operation
                                var tariff = allTariffs
                                    .Where(t => t.ProductID == product.ID &&
                                               t.OperationID == operation.ID &&
                                               t.EffectiveFrom <= workDate &&
                                               (t.EffectiveTo == null || t.EffectiveTo >= workDate))
                                    .OrderByDescending(t => t.PositionID) // Prefer position-specific
                                    .FirstOrDefault();

                                // If not found, try general tariff (PositionID == null)
                                if (tariff == null)
                                {
                                    tariff = allTariffs
                                        .Where(t => t.ProductID == product.ID &&
                                                   t.OperationID == operation.ID &&
                                                   t.PositionID == null &&
                                                   t.EffectiveFrom <= workDate &&
                                                   (t.EffectiveTo == null || t.EffectiveTo >= workDate))
                                        .FirstOrDefault();
                                }

                                // Create work entry
                                var workEntry = new HR_PIECE_RATE_WORK_ENTRIES
                                {
                                    EmployeeID = employee.ID,
                                    ProductID = product.ID,
                                    OperationID = operation.ID,
                                    WorkDate = workDate.Date,
                                    Quantity = quantity,
                                    CreatedByUserID = userId,
                                    CreatedDT = currentDate,
                                    IsDeleted = false
                                };

                                db.HR_PIECE_RATE_WORK_ENTRIES.Add(workEntry);
                                successCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            importResults.Add($"Qator {row}: Xatolik - {ex.Message}");
                            errorCount++;
                        }
                    }

                    // Save all changes
                    await db.SaveChangesAsync();

                    // Prepare result message
                    string resultMessage = $"Muvaffaqiyatli: {successCount} ta yozuv qo'shildi. ";
                    if (skipCount > 0)
                        resultMessage += $"O'tkazib yuborildi: {skipCount} ta. ";
                    if (errorCount > 0)
                        resultMessage += $"Xatoliklar: {errorCount} ta.";

                    if (importResults.Count > 0)
                    {
                        TempData["ImportResults"] = importResults;
                    }

                    TempData["Success"] = resultMessage;
                    logger.Log("HrPieceRateTimesheets", "ImportFromExcel", $"Success: {successCount}, Skipped: {skipCount}, Errors: {errorCount}");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Xatolik: {ex.Message}";
                logger.Log("HrPieceRateTimesheets", "ImportFromExcel", $"Error: {ex.Message}");
            }

            return View();
        }
    }
}

