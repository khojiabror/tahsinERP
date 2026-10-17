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
using tahsinERP.Filters;

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

        // Olish so'rovi: HR/PieceRateTimesheets
        public async Task<ActionResult> Index(int? year, int? month, int? orgUnitId, string status)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Don't set standart values agar not provided - allow showing barcha months
            // year = year ?? DateTime.Now.Year;
            // month = month ?? DateTime.Now.Month;

            // Olish work entries dan HR_PIECE_RATE_WORK_ENTRIES
            IQueryable<HR_PIECE_RATE_WORK_ENTRIES> query = db.HR_PIECE_RATE_WORK_ENTRIES
                .Include(e => e.HR_EMPLOYEES)
                .Include(e => e.PRODUCT)
                .Include(e => e.HR_PIECE_RATE_OPERATIONS)
                .Where(e => e.IsDeleted != true);

            // Apply filters faqat agar provided
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

            // Olish tasdiqlash statuses
            var approvals = await db.HR_PIECE_RATE_TIMESHEET_APPROVALS
                .Where(a => a.IsDeleted != true)
                .ToListAsync();

            // Group by year, month, va org unit
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
            ViewBag.Month = month; // Keep null agar not provided ga show barcha months
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

            // Olish barcha work entries uchun this month
            var workEntries = db.HR_PIECE_RATE_WORK_ENTRIES
                .Where(e => e.WorkDate >= startDate && e.WorkDate <= endDate && e.IsDeleted != true)
                .ToList();

            return CalculatePieceRateAmountForEntries(workEntries);
        }

        private decimal CalculatePieceRateAmountForEntries(List<HR_PIECE_RATE_WORK_ENTRIES> workEntries)
        {
            decimal totalAmount = 0;

            foreach (var entry in workEntries)
            {
                // Olish tariff uchun this product va operation
                int? positionId = entry.HR_EMPLOYEES?.PositionID;
                
                var tariff = db.HR_PIECE_RATE_TARIFFS
                    .Where(t => t.ProductID == entry.ProductID &&
                               t.OperationID == entry.OperationID &&
                               t.IsActive &&
                               t.IsDeleted != true &&
                               t.EffectiveFrom <= entry.WorkDate &&
                               (t.EffectiveTo == null || t.EffectiveTo >= entry.WorkDate) &&
                               (positionId.HasValue ? t.PositionID == positionId.Value : t.PositionID == null))
                    .OrderByDescending(t => t.PositionID) // Prefer position-specific
                    .FirstOrDefault();

                // agar not found, try general tariff (PositionID == null)
                if (tariff == null)
                {
                    tariff = db.HR_PIECE_RATE_TARIFFS
                        .Where(t => t.ProductID == entry.ProductID &&
                                   t.OperationID == entry.OperationID &&
                                   t.PositionID == null &&
                                   t.IsActive &&
                                   t.IsDeleted != true &&
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

        // Olish so'rovi: HR/PieceRateTimesheets/yaratish
        public ActionResult Create()
        {
            // Redirect ga CreateSimple uchun simple entry
            return RedirectToAction("CreateSimple");
        }

        // Olish so'rovi: HR/PieceRateTimesheets/Details
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
                .Where(e => e.WorkDate >= startDate && e.WorkDate <= endDate && e.IsDeleted != true);

            if (orgUnitId.HasValue)
            {
                query = query.Where(e => e.HR_EMPLOYEES.OrgUnitID == orgUnitId.Value);
            }

            var workEntries = await query.OrderBy(e => e.WorkDate).ThenBy(e => e.HR_EMPLOYEES.FullName).ToListAsync();

            // Calculate tariffs uchun har bir entry
            var viewModel = new List<HrPieceRateTimesheetDetailsViewModel>();
            foreach (var entry in workEntries)
            {
                int? positionId = entry.HR_EMPLOYEES?.PositionID;
                
                var tariff = db.HR_PIECE_RATE_TARIFFS
                    .Include(t => t.CURRENCy)
                    .Where(t => t.ProductID == entry.ProductID &&
                               t.OperationID == entry.OperationID &&
                               t.IsActive &&
                               t.IsDeleted != true &&
                               t.EffectiveFrom <= entry.WorkDate &&
                               (t.EffectiveTo == null || t.EffectiveTo >= entry.WorkDate) &&
                               (positionId.HasValue ? t.PositionID == positionId.Value : t.PositionID == null))
                    .OrderByDescending(t => t.PositionID)
                    .FirstOrDefault();

                // agar not found, try general tariff (PositionID == null)
                if (tariff == null)
                {
                    tariff = db.HR_PIECE_RATE_TARIFFS
                        .Include(t => t.CURRENCy)
                        .Where(t => t.ProductID == entry.ProductID &&
                                   t.OperationID == entry.OperationID &&
                                   t.PositionID == null &&
                                   t.IsActive &&
                                   t.IsDeleted != true &&
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

        // Olish so'rovi: HR/PieceRateTimesheets/Edit/5
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
                .FirstOrDefaultAsync(t => t.ID == id && t.IsDeleted != true);

            if (header == null)
            {
                return HttpNotFound();
            }

            // Olish faqat piece-rate employees
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
                .Where(l => l.IsDeleted != true && pieceRatePositionIds.Contains(l.HR_EMPLOYEES.PositionID ?? 0))
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

            // Olish finished goods uchun piece-rate selection
            ViewBag.FinishedGoods = new SelectList(
                await db.PRODUCTS.Where(p => p.IsFinishedGood == true && p.IsDeleted != true)
                    .OrderBy(p => p.PNo).ToListAsync(),
                "ID", "PNo");

            logger.Log("HrPieceRateTimesheets", "Edit", $"Tabel: {header.Year}-{header.Month:D2}, Bo'lim: {header.HR_ORG_UNITS?.Name ?? "Butun kompaniya"}");
            return View(viewModel);
        }

        // POST so'rovi: HR/PieceRateTimesheets/UpdateQuantity
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public async Task<JsonResult> UpdateQuantity(int lineId, int day, decimal? quantity, int? productId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return Json(new { success = false, message = "Ruxsat yo'q" });
            }

            try
            {
                var line = await db.HR_TIMESHEET_LINES
                    .FirstOrDefaultAsync(l => l.ID == lineId && l.IsDeleted != true);
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

                var employee = await db.HR_EMPLOYEES.FindAsync(line.EmployeeID);
                var employeeLabel = employee == null
                    ? "-"
                    : $"{employee.FullName} ({employee.TabNo ?? "-"})";
                var product = line.ProductID.HasValue ? await db.PRODUCTS.FindAsync(line.ProductID.Value) : null;
                var productLabel = product?.Name ?? product?.PNo ?? "-";
                logger.Log("HrPieceRateTimesheets", "UpdateQuantity", $"Xodim: {employeeLabel}, Kun: {day}, Miqdor: {quantity}, Mahsulot: {productLabel}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST so'rovi: HR/PieceRateTimesheets/ImportFromProduction
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public async Task<JsonResult> ImportFromProduction(int timesheetId, int employeeId, int year, int month)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return Json(new { success = false, message = "Ruxsat yo'q" });
            }

            try
            {
                var line = await db.HR_TIMESHEET_LINES
                    .FirstOrDefaultAsync(l => l.TimesheetID == timesheetId && l.EmployeeID == employeeId && l.IsDeleted != true);

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

                var header = await db.HR_TIMESHEET_HEADERS
                    .Include(t => t.HR_ORG_UNITS)
                    .FirstOrDefaultAsync(t => t.ID == timesheetId && t.IsDeleted != true);
                var employee = await db.HR_EMPLOYEES.FindAsync(employeeId);
                var orgUnitName = header?.HR_ORG_UNITS?.Name ?? "Butun kompaniya";
                var employeeLabel = employee == null
                    ? "-"
                    : $"{employee.FullName} ({employee.TabNo ?? "-"})";
                logger.Log("HrPieceRateTimesheets", "ImportFromProduction", $"Tabel: {header?.Year}-{header?.Month:D2}, Bo'lim: {orgUnitName}, Xodim: {employeeLabel}");
                return Json(new { success = true, message = "Ishbay ma'lumotlari import qilindi" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST so'rovi: HR/PieceRateTimesheets/Approve
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Approve(int year, int? month, int? orgUnitId)
        {
            // Tekshirish permission uchun Approve action specifically
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets", "Approve"))
            {
                TempData["Error"] = "Sizda tasdiqlash huquqi yo'q";
                return RedirectToAction("Index", new { year, month, orgUnitId });
            }

            // Tekshirish month parameter
            if (!month.HasValue || month.Value < 1 || month.Value > 12)
            {
                TempData["Error"] = "Noto'g'ri oy parametri";
                return RedirectToAction("Index", new { year, month, orgUnitId });
            }

            // Tekshirish agar there are any work entries uchun this period
            var startDate = new DateTime(year, month.Value, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            IQueryable<HR_PIECE_RATE_WORK_ENTRIES> query = db.HR_PIECE_RATE_WORK_ENTRIES
                .Where(e => e.WorkDate >= startDate && e.WorkDate <= endDate && e.IsDeleted != true);

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

            // Olish yoki yaratish tasdiqlash record
            var approval = await db.HR_PIECE_RATE_TIMESHEET_APPROVALS
                .FirstOrDefaultAsync(a => a.Year == year && a.Month == month.Value && a.OrgUnitID == orgUnitId && a.IsDeleted != true);

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

            var orgUnitName = orgUnitId.HasValue
                ? (await db.HR_ORG_UNITS.FindAsync(orgUnitId.Value))?.Name ?? "Butun kompaniya"
                : "Butun kompaniya";
            logger.Log("HrPieceRateTimesheets", "Approve", $"Tabel: {year}-{month.Value:D2}, Bo'lim: {orgUnitName}");
            TempData["Success"] = "Tabel muvaffaqiyatli tasdiqlandi";
            return RedirectToAction("Index", new { year, month = month.Value, orgUnitId });
        }

        private async Task CreatePieceRateTimesheetLines(int timesheetId, int? orgUnitId)
        {
            // Olish piece-rate positions
            var pieceRatePositionIds = await db.HR_STAFFING_PLAN
                .Where(sp => sp.IsActive && sp.IsPieceRate == true && 
                    (orgUnitId == null || sp.OrgUnitID == orgUnitId.Value))
                .Select(sp => sp.PositionID)
                .Distinct()
                .ToListAsync();

            // Olish employees bilan piece-rate positions
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
            var header = await db.HR_TIMESHEET_HEADERS
                .FirstOrDefaultAsync(t => t.ID == line.TimesheetID && t.IsDeleted != true);
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

        // Qo'shimcha grid-kiritish (CreateSimple o'rniga emas, qo'shimcha imkoniyat)
        public async Task<ActionResult> GridCreate(int? year, int? month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var vm = new HrPieceRateGridEntryViewModel
            {
                Year = year ?? DateTime.Now.Year,
                Month = month ?? DateTime.Now.Month,
                OrgUnitID = orgUnitId
            };

            vm.Rows.Add(new HrPieceRateGridRowViewModel { WorkDate = new DateTime(vm.Year, vm.Month, 1) });

            await PopulatePieceRateGridViewBags(orgUnitId);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> GridCreate(HrPieceRateGridEntryViewModel model)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            try
            {
                if (model.Rows == null || !model.Rows.Any())
                {
                    TempData["Error"] = "Saqlash uchun qator yo'q";
                    await PopulatePieceRateGridViewBags(model.OrgUnitID);
                    return View(model);
                }

                var candidateEmpIds = model.Rows
                    .Where(r => r.EmployeeID.HasValue && r.ProductID.HasValue && r.OperationID.HasValue && r.WorkDate.HasValue && r.Quantity.HasValue && r.Quantity.Value > 0)
                    .Select(r => r.EmployeeID.Value).Distinct().ToList();
                var blockMsg = await GetPieceRateEditBlockedMessageAsync(model.Year, model.Month, candidateEmpIds);
                if (blockMsg != null)
                {
                    TempData["Error"] = blockMsg;
                    await PopulatePieceRateGridViewBags(model.OrgUnitID);
                    return View(model);
                }

                int created = 0;
                var errors = new List<string>();
                var userId = UserHelper.GetUserId(User.Identity.Name);
                var touchedEmployeeIds = new List<int>();

                foreach (var row in model.Rows)
                {
                    if (!row.EmployeeID.HasValue || !row.ProductID.HasValue || !row.OperationID.HasValue || !row.WorkDate.HasValue || !row.Quantity.HasValue)
                    {
                        continue;
                    }
                    if (row.Quantity.Value <= 0)
                    {
                        continue;
                    }

                    var exists = await db.HR_PIECE_RATE_WORK_ENTRIES.AnyAsync(e =>
                        e.EmployeeID == row.EmployeeID.Value &&
                        e.ProductID == row.ProductID.Value &&
                        e.OperationID == row.OperationID.Value &&
                        e.WorkDate == row.WorkDate.Value &&
                        e.IsDeleted != true);
                    if (exists)
                    {
                        errors.Add($"Qator o'tkazildi: xodim/mahsulot/operatsiya/sana bo'yicha dublikat mavjud");
                        continue;
                    }

                    var deletedDuplicate = await db.HR_PIECE_RATE_WORK_ENTRIES
                        .FirstOrDefaultAsync(e =>
                            e.EmployeeID == row.EmployeeID.Value &&
                            e.ProductID == row.ProductID.Value &&
                            e.OperationID == row.OperationID.Value &&
                            e.WorkDate == row.WorkDate.Value &&
                            e.IsDeleted == true);

                    if (deletedDuplicate != null)
                    {
                        deletedDuplicate.Quantity = row.Quantity.Value;
                        deletedDuplicate.IsDeleted = false;
                        deletedDuplicate.ModifiedByUserID = userId;
                        deletedDuplicate.ModifiedDT = DateTime.Now;
                        db.Entry(deletedDuplicate).State = EntityState.Modified;
                    }
                    else
                    {
                        db.HR_PIECE_RATE_WORK_ENTRIES.Add(new HR_PIECE_RATE_WORK_ENTRIES
                        {
                            EmployeeID = row.EmployeeID.Value,
                            ProductID = row.ProductID.Value,
                            OperationID = row.OperationID.Value,
                            WorkDate = row.WorkDate.Value,
                            Quantity = row.Quantity.Value,
                            CreatedByUserID = userId,
                            CreatedDT = DateTime.Now,
                            IsDeleted = false
                        });
                    }
                    touchedEmployeeIds.Add(row.EmployeeID.Value);
                    created++;
                }

                await db.SaveChangesAsync();
                if (created > 0)
                    await EnsurePieceRateDraftApprovalsForEmployeesAsync(model.Year, model.Month, touchedEmployeeIds);
                TempData["Success"] = $"{created} ta yozuv saqlandi";
                if (errors.Any()) TempData["Error"] = string.Join("<br/>", errors);
                return RedirectToAction("GridCreate", new { year = model.Year, month = model.Month, orgUnitId = model.OrgUnitID });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Saqlashda xatolik: " + ex.Message;
                await PopulatePieceRateGridViewBags(model.OrgUnitID, model.Rows);
                return View(model);
            }
        }

        public async Task<ActionResult> Grids(int? year, int? month, int? orgUnitId)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            IQueryable<HR_PIECE_RATE_WORK_ENTRIES> query = db.HR_PIECE_RATE_WORK_ENTRIES
                .Include(e => e.HR_EMPLOYEES.HR_ORG_UNITS)
                .Where(e => e.IsDeleted != true);
            if (year.HasValue) query = query.Where(e => e.WorkDate.Year == year.Value);
            if (month.HasValue) query = query.Where(e => e.WorkDate.Month == month.Value);
            if (orgUnitId.HasValue) query = query.Where(e => e.HR_EMPLOYEES.OrgUnitID == orgUnitId.Value);

            var entries = await query.ToListAsync();
            var model = entries
                .GroupBy(e => new { Year = e.WorkDate.Year, Month = e.WorkDate.Month, OrgUnitID = e.HR_EMPLOYEES.OrgUnitID })
                .Select(g => new HrPieceRateTimesheetIndexViewModel
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month),
                    OrgUnitID = g.Key.OrgUnitID,
                    OrgUnitName = g.First().HR_EMPLOYEES.HR_ORG_UNITS?.Name ?? "Butun kompaniya",
                    EntryCount = g.Count(),
                    CalculatedAmount = CalculatePieceRateAmountForEntries(g.ToList()),
                    CreatedDT = g.Min(x => x.CreatedDT),
                    Status = "Draft"
                })
                .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).ToList();

            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.OrgUnitID = new SelectList(await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(), "ID", "Name", orgUnitId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> BulkDeleteGrid(int year, int month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);
            IQueryable<HR_PIECE_RATE_WORK_ENTRIES> query = db.HR_PIECE_RATE_WORK_ENTRIES
                .Where(e => e.WorkDate >= startDate && e.WorkDate <= endDate && e.IsDeleted != true);
            if (orgUnitId.HasValue) query = query.Where(e => e.HR_EMPLOYEES.OrgUnitID == orgUnitId.Value);

            var rows = await query.ToListAsync();
            if (rows.Count > 0)
            {
                var blockDel = await GetPieceRateEditBlockedMessageAsync(year, month, rows.Select(r => r.EmployeeID).Distinct());
                if (blockDel != null)
                {
                    TempData["Error"] = blockDel;
                    return RedirectToAction("Index", new { year, month, orgUnitId });
                }
            }
            foreach (var r in rows)
            {
                r.IsDeleted = true;
                db.Entry(r).State = EntityState.Modified;
            }
            await db.SaveChangesAsync();

            TempData["Success"] = $"{rows.Count} ta yozuv o'chirildi";
            return RedirectToAction("Index", new { year, month, orgUnitId });
        }

        // Qo'shimcha grid-tahrirlash (mavjud yozuvlarni jadvalda tahrirlash)
        public async Task<ActionResult> GridEdit(int? year, int? month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var y = year ?? DateTime.Now.Year;
            var m = month ?? DateTime.Now.Month;
            var startDate = new DateTime(y, m, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            IQueryable<HR_PIECE_RATE_WORK_ENTRIES> query = db.HR_PIECE_RATE_WORK_ENTRIES
                .Where(e => e.WorkDate >= startDate && e.WorkDate <= endDate && e.IsDeleted != true);
            if (orgUnitId.HasValue)
            {
                query = query.Where(e => e.HR_EMPLOYEES.OrgUnitID == orgUnitId.Value);
            }

            var entries = await query.OrderBy(e => e.WorkDate).ToListAsync();
            var vm = new HrPieceRateGridEntryViewModel
            {
                Year = y,
                Month = m,
                OrgUnitID = orgUnitId,
                Rows = entries.Select(e => new HrPieceRateGridRowViewModel
                {
                    EntryID = e.ID,
                    EmployeeID = e.EmployeeID,
                    ProductID = e.ProductID,
                    OperationID = e.OperationID,
                    WorkDate = e.WorkDate,
                    Quantity = e.Quantity
                }).ToList()
            };

            await PopulatePieceRateGridViewBags(orgUnitId, vm.Rows);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> GridEdit(HrPieceRateGridEntryViewModel model)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            try
            {
                if (model.Rows == null)
                {
                    TempData["Error"] = "Saqlash uchun ma'lumot yo'q";
                    return RedirectToAction("GridEdit", new { year = model.Year, month = model.Month, orgUnitId = model.OrgUnitID });
                }

                var editCandidates = model.Rows
                    .Where(r => r.EmployeeID.HasValue && r.ProductID.HasValue && r.OperationID.HasValue && r.WorkDate.HasValue && r.Quantity.HasValue && r.Quantity.Value > 0)
                    .Select(r => r.EmployeeID.Value).Distinct().ToList();
                var blockEdit = await GetPieceRateEditBlockedMessageAsync(model.Year, model.Month, editCandidates);
                if (blockEdit != null)
                {
                    TempData["Error"] = blockEdit;
                    return RedirectToAction("GridEdit", new { year = model.Year, month = model.Month, orgUnitId = model.OrgUnitID });
                }

                int updated = 0;
                int inserted = 0;
                var userId = UserHelper.GetUserId(User.Identity.Name);
                var touchedEmployeeIds = new List<int>();

                foreach (var row in model.Rows)
                {
                    if (!row.EmployeeID.HasValue || !row.ProductID.HasValue || !row.OperationID.HasValue || !row.WorkDate.HasValue || !row.Quantity.HasValue)
                    {
                        continue;
                    }
                    if (row.Quantity.Value <= 0) continue;

                    if (row.EntryID.HasValue)
                    {
                        var entry = await db.HR_PIECE_RATE_WORK_ENTRIES
                            .FirstOrDefaultAsync(e => e.ID == row.EntryID.Value && e.IsDeleted != true);
                        if (entry == null) continue;

                        entry.EmployeeID = row.EmployeeID.Value;
                        entry.ProductID = row.ProductID.Value;
                        entry.OperationID = row.OperationID.Value;
                        entry.WorkDate = row.WorkDate.Value;
                        entry.Quantity = row.Quantity.Value;
                        entry.CreatedByUserID = userId;
                        entry.CreatedDT = DateTime.Now;
                        touchedEmployeeIds.Add(row.EmployeeID.Value);
                        updated++;
                    }
                    else
                    {
                        var deletedDuplicate = await db.HR_PIECE_RATE_WORK_ENTRIES
                            .FirstOrDefaultAsync(e =>
                                e.EmployeeID == row.EmployeeID.Value &&
                                e.ProductID == row.ProductID.Value &&
                                e.OperationID == row.OperationID.Value &&
                                e.WorkDate == row.WorkDate.Value &&
                                e.IsDeleted == true);

                        if (deletedDuplicate != null)
                        {
                            deletedDuplicate.Quantity = row.Quantity.Value;
                            deletedDuplicate.IsDeleted = false;
                            deletedDuplicate.ModifiedByUserID = userId;
                            deletedDuplicate.ModifiedDT = DateTime.Now;
                            db.Entry(deletedDuplicate).State = EntityState.Modified;
                        }
                        else
                        {
                            db.HR_PIECE_RATE_WORK_ENTRIES.Add(new HR_PIECE_RATE_WORK_ENTRIES
                            {
                                EmployeeID = row.EmployeeID.Value,
                                ProductID = row.ProductID.Value,
                                OperationID = row.OperationID.Value,
                                WorkDate = row.WorkDate.Value,
                                Quantity = row.Quantity.Value,
                                CreatedByUserID = userId,
                                CreatedDT = DateTime.Now,
                                IsDeleted = false
                            });
                        }
                        touchedEmployeeIds.Add(row.EmployeeID.Value);
                        inserted++;
                    }
                }

                await db.SaveChangesAsync();
                if (updated + inserted > 0)
                    await EnsurePieceRateDraftApprovalsForEmployeesAsync(model.Year, model.Month, touchedEmployeeIds);
                TempData["Success"] = $"Yangilandi: {updated}, qo'shildi: {inserted}";
                return RedirectToAction("GridEdit", new { year = model.Year, month = model.Month, orgUnitId = model.OrgUnitID });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Saqlashda xatolik: " + ex.Message;
                await PopulatePieceRateGridViewBags(model.OrgUnitID, model.Rows);
                return View(model);
            }
        }

        /// <summary>
        /// Tasdiqlangan oy/bo'lim uchun ma'lumot kiritishni bloklaydi (avtomatik tasdiqni bekor qilmaymiz).
        /// </summary>
        private async Task<string> GetPieceRateEditBlockedMessageAsync(int year, int month, IEnumerable<int> employeeIds)
        {
            var ids = employeeIds?.Where(x => x > 0).Distinct().ToList();
            if (ids == null || !ids.Any()) return null;

            var companyWideApproved = await db.HR_PIECE_RATE_TIMESHEET_APPROVALS
                .AnyAsync(a => a.Year == year && a.Month == month && a.OrgUnitID == null &&
                    a.IsDeleted != true && a.Status == "Approved");
            if (companyWideApproved)
                return "Bu oy uchun tabel (butun kompaniya) allaqachon tasdiqlangan. O'zgartirish mumkin emas.";

            var orgIds = await db.HR_EMPLOYEES.Where(e => ids.Contains(e.ID)).Select(e => e.OrgUnitID).Distinct().ToListAsync();
            foreach (var ou in orgIds)
            {
                var orgApproved = await db.HR_PIECE_RATE_TIMESHEET_APPROVALS
                    .AnyAsync(a => a.Year == year && a.Month == month && a.OrgUnitID == ou &&
                        a.IsDeleted != true && a.Status == "Approved");
                if (orgApproved)
                    return "Bu oy uchun tegishli bo'lim tabeli allaqachon tasdiqlangan. O'zgartirish mumkin emas.";
            }
            return null;
        }

        /// <summary>
        /// Yangi yoki o'zgargan yozuvlar uchun HR_PIECE_RATE_TIMESHEET_APPROVALS da Draft yozuv bor-yo'qligini ta'minlaydi (Index: Draft ko'rinishi).
        /// </summary>
        private async Task EnsurePieceRateDraftApprovalsForEmployeesAsync(int year, int month, IEnumerable<int> employeeIds)
        {
            var ids = employeeIds?.Where(x => x > 0).Distinct().ToList();
            if (ids == null || !ids.Any()) return;

            var orgIds = await db.HR_EMPLOYEES.Where(e => ids.Contains(e.ID)).Select(e => e.OrgUnitID).Distinct().ToListAsync();
            var uid = UserHelper.GetUserId(User.Identity.Name);
            var now = DateTime.Now;

            foreach (var ou in orgIds)
            {
                var existing = await db.HR_PIECE_RATE_TIMESHEET_APPROVALS
                    .FirstOrDefaultAsync(a => a.Year == year && a.Month == month && a.OrgUnitID == ou);
                if (existing == null)
                {
                    db.HR_PIECE_RATE_TIMESHEET_APPROVALS.Add(new HR_PIECE_RATE_TIMESHEET_APPROVALS
                    {
                        Year = year,
                        Month = month,
                        OrgUnitID = ou,
                        Status = "Draft",
                        CreatedByUserID = uid,
                        CreatedDT = now
                    });
                }
                else if (existing.IsDeleted == true)
                {
                    existing.IsDeleted = false;
                    existing.Status = "Draft";
                    existing.ModifiedByUserID = uid;
                    existing.ModifiedDT = now;
                    db.Entry(existing).State = EntityState.Modified;
                }
            }
            await db.SaveChangesAsync();
        }

        private async Task PopulatePieceRateGridViewBags(int? orgUnitId, List<HrPieceRateGridRowViewModel> existingRows = null)
        {
            IQueryable<HR_EMPLOYEES> employeeQuery = db.HR_EMPLOYEES.Where(e => e.IsDeleted != true);
            if (orgUnitId.HasValue) employeeQuery = employeeQuery.Where(e => e.OrgUnitID == orgUnitId.Value);
            var employees = await employeeQuery.OrderBy(e => e.FullName).ToListAsync();
            var products = await db.PRODUCTS.Where(p => p.IsFinishedGood == true && p.IsDeleted != true).OrderBy(p => p.PNo).ToListAsync();
            var operations = await db.HR_PIECE_RATE_OPERATIONS.Where(o => o.IsActive).OrderBy(o => o.SortOrder).ToListAsync();

            if (existingRows != null && existingRows.Any())
            {
                var rowEmployeeIds = existingRows.Where(r => r.EmployeeID.HasValue).Select(r => r.EmployeeID.Value).Distinct().ToList();
                var rowProductIds = existingRows.Where(r => r.ProductID.HasValue).Select(r => r.ProductID.Value).Distinct().ToList();
                var rowOperationIds = existingRows.Where(r => r.OperationID.HasValue).Select(r => r.OperationID.Value).Distinct().ToList();

                var missingEmployeeIds = rowEmployeeIds.Except(employees.Select(e => e.ID)).ToList();
                if (missingEmployeeIds.Any())
                {
                    var missingEmployees = await db.HR_EMPLOYEES
                        .Where(e => missingEmployeeIds.Contains(e.ID))
                        .ToListAsync();
                    employees.AddRange(missingEmployees);
                    employees = employees.OrderBy(e => e.FullName).ToList();
                }

                var missingProductIds = rowProductIds.Except(products.Select(p => p.ID)).ToList();
                if (missingProductIds.Any())
                {
                    var missingProducts = await db.PRODUCTS
                        .Where(p => missingProductIds.Contains(p.ID))
                        .ToListAsync();
                    products.AddRange(missingProducts);
                    products = products.OrderBy(p => p.PNo).ToList();
                }

                var missingOperationIds = rowOperationIds.Except(operations.Select(o => o.ID)).ToList();
                if (missingOperationIds.Any())
                {
                    var missingOperations = await db.HR_PIECE_RATE_OPERATIONS
                        .Where(o => missingOperationIds.Contains(o.ID))
                        .ToListAsync();
                    operations.AddRange(missingOperations);
                    operations = operations.OrderBy(o => o.SortOrder).ThenBy(o => o.Name).ToList();
                }
            }

            ViewBag.EmployeeID = new SelectList(employees, "ID", "FullName");
            ViewBag.ProductID = new SelectList(products, "ID", "PNo");
            ViewBag.OperationID = new SelectList(operations, "ID", "Name");
            ViewBag.OrgUnitID = new SelectList(await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(), "ID", "Name", orgUnitId);
        }

        // Olish so'rovi: HR/PieceRateTimesheets/CreateSimple
        public async Task<ActionResult> CreateSimple()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            await PopulateCreateSimpleViewBags();
            return View();
        }

        // Olish so'rovi: HR/PieceRateTimesheets/EditWorkEntry/5 (grid yoki ro'yxatdan bitta yozuv)
        public async Task<ActionResult> EditWorkEntry(int? id, int? year, int? month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var entry = await db.HR_PIECE_RATE_WORK_ENTRIES
                .FirstOrDefaultAsync(e => e.ID == id.Value && e.IsDeleted != true);
            if (entry == null)
            {
                return HttpNotFound();
            }

            var blockEdit = await GetPieceRateEditBlockedMessageAsync(entry.WorkDate.Year, entry.WorkDate.Month, new List<int> { entry.EmployeeID });
            if (blockEdit != null)
            {
                TempData["Error"] = blockEdit;
                return RedirectToAction("GridEdit", new { year = year ?? entry.WorkDate.Year, month = month ?? entry.WorkDate.Month, orgUnitId });
            }

            await PopulateCreateSimpleViewBags(entry.EmployeeID, entry.ProductID, entry.OperationID);
            ViewBag.EntryID = entry.ID;
            ViewBag.PrefillWorkDate = entry.WorkDate.ToString("yyyy-MM-dd");
            ViewBag.PrefillQuantity = entry.Quantity;
            ViewBag.ReturnUrl = Url.Action("GridEdit", "HrPieceRateTimesheets", new { year = year ?? entry.WorkDate.Year, month = month ?? entry.WorkDate.Month, orgUnitId });
            ViewBag.PageTitle = "Ishbay yozuvni tahrirlash";
            return View("CreateSimple");
        }

        // POST so'rovi: HR/PieceRateTimesheets/CreateSimple
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateSimple(int? entryId, int EmployeeID, int ProductID, int OperationID, DateTime WorkDate, decimal Quantity, string returnUrl = null)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            try
            {
                if (entryId.HasValue)
                {
                    var entry = await db.HR_PIECE_RATE_WORK_ENTRIES
                        .FirstOrDefaultAsync(e => e.ID == entryId.Value && e.IsDeleted != true);
                    if (entry == null)
                    {
                        TempData["Error"] = "Yozuv topilmadi";
                        await PopulateCreateSimpleViewBags();
                        return View();
                    }

                    var workDateMonth = new DateTime(WorkDate.Year, WorkDate.Month, 1);
                    var currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                    if (workDateMonth > currentMonth)
                    {
                        TempData["Error"] = "Kelajakka tabel kirita olmaysiz";
                        await PopulateCreateSimpleViewBags(EmployeeID, ProductID, OperationID);
                        ViewBag.EntryID = entry.ID;
                        ViewBag.PrefillWorkDate = WorkDate.ToString("yyyy-MM-dd");
                        ViewBag.PrefillQuantity = Quantity;
                        ViewBag.ReturnUrl = returnUrl;
                        ViewBag.PageTitle = "Ishbay yozuvni tahrirlash";
                        return View();
                    }

                    var editBlockedMessage = await GetPieceRateEditBlockedMessageAsync(WorkDate.Year, WorkDate.Month, new List<int> { EmployeeID });
                    if (editBlockedMessage != null)
                    {
                        TempData["Error"] = editBlockedMessage;
                        await PopulateCreateSimpleViewBags(EmployeeID, ProductID, OperationID);
                        ViewBag.EntryID = entry.ID;
                        ViewBag.PrefillWorkDate = WorkDate.ToString("yyyy-MM-dd");
                        ViewBag.PrefillQuantity = Quantity;
                        ViewBag.ReturnUrl = returnUrl;
                        ViewBag.PageTitle = "Ishbay yozuvni tahrirlash";
                        return View();
                    }

                    entry.EmployeeID = EmployeeID;
                    entry.ProductID = ProductID;
                    entry.OperationID = OperationID;
                    entry.WorkDate = WorkDate;
                    entry.Quantity = Quantity;
                    entry.CreatedByUserID = UserHelper.GetUserId(User.Identity.Name);
                    entry.CreatedDT = DateTime.Now;
                    await db.SaveChangesAsync();
                    await EnsurePieceRateDraftApprovalsForEmployeesAsync(WorkDate.Year, WorkDate.Month, new List<int> { EmployeeID });
                    TempData["Success"] = "Yozuv yangilandi";
                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    return RedirectToAction("CreateSimple");
                }

                // Tekshirish: Cannot yaratish timesheet uchun future months
                var currentDate = DateTime.Now;
                var workDateMonthNew = new DateTime(WorkDate.Year, WorkDate.Month, 1);
                var currentMonthNew = new DateTime(currentDate.Year, currentDate.Month, 1);

                if (workDateMonthNew > currentMonthNew)
                {
                    TempData["Error"] = "Kelajakka tabel kirita olmaysiz";
                    await PopulateCreateSimpleViewBags();
                    return View();
                }

                var blockSimple = await GetPieceRateEditBlockedMessageAsync(WorkDate.Year, WorkDate.Month, new List<int> { EmployeeID });
                if (blockSimple != null)
                {
                    TempData["Error"] = blockSimple;
                    await PopulateCreateSimpleViewBags();
                    return View();
                }

                // Tekshirish agar work entry already mavjud uchun this employee, product, operation, va date
                var existing = await db.HR_PIECE_RATE_WORK_ENTRIES
                    .FirstOrDefaultAsync(e => e.EmployeeID == EmployeeID &&
                                             e.ProductID == ProductID &&
                                             e.OperationID == OperationID &&
                                             e.WorkDate == WorkDate &&
                                             e.IsDeleted != true);

                if (existing != null)
                {
                    // Yangilash existing entry
                    existing.Quantity = Quantity;
                    existing.CreatedByUserID = UserHelper.GetUserId(User.Identity.Name);
                    existing.CreatedDT = DateTime.Now;
                }
                else
                {
                    // Yaratish new entry
                    var workEntry = new HR_PIECE_RATE_WORK_ENTRIES
                    {
                        EmployeeID = EmployeeID,
                        ProductID = ProductID,
                        OperationID = OperationID,
                        WorkDate = WorkDate,
                        Quantity = Quantity,
                        CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                        CreatedDT = DateTime.Now,
                        IsDeleted = false
                    };

                    db.HR_PIECE_RATE_WORK_ENTRIES.Add(workEntry);
                }

                await db.SaveChangesAsync();

                await EnsurePieceRateDraftApprovalsForEmployeesAsync(WorkDate.Year, WorkDate.Month, new List<int> { EmployeeID });

                var employee = await db.HR_EMPLOYEES.FindAsync(EmployeeID);
                var product = await db.PRODUCTS.FindAsync(ProductID);
                var operation = await db.HR_PIECE_RATE_OPERATIONS.FindAsync(OperationID);
                var employeeLabel = employee == null
                    ? "-"
                    : $"{employee.FullName} ({employee.TabNo ?? "-"})";
                var productLabel = product?.Name ?? product?.PNo ?? "-";
                var operationLabel = operation?.Name ?? "-";
            logger.Log("HrPieceRateTimesheets", "CreateSimple", $"Xodim: {employeeLabel}, Mahsulot: {productLabel}, Operatsiya: {operationLabel}, Sana: {WorkDate:dd.MM.yyyy}, Miqdor: {Quantity}");
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

        /// <summary>
        /// GridEdit jadvalidan bitta yozuvni o'chirish (soft delete).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteGridEntry(int id, int year, int month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var entry = await db.HR_PIECE_RATE_WORK_ENTRIES.FirstOrDefaultAsync(e => e.ID == id && e.IsDeleted != true);
            if (entry == null)
            {
                TempData["Error"] = "Yozuv topilmadi";
                return RedirectToAction("GridEdit", new { year, month, orgUnitId });
            }

            var blockEdit = await GetPieceRateEditBlockedMessageAsync(entry.WorkDate.Year, entry.WorkDate.Month, new List<int> { entry.EmployeeID });
            if (blockEdit != null)
            {
                TempData["Error"] = blockEdit;
                return RedirectToAction("GridEdit", new { year, month, orgUnitId });
            }

            entry.IsDeleted = true;
            db.Entry(entry).State = EntityState.Modified;
            await db.SaveChangesAsync();
            TempData["Success"] = "Yozuv o'chirildi";
            return RedirectToAction("GridEdit", new { year, month, orgUnitId });
        }

        // Olish so'rovi: HR/PieceRateTimesheets/GetTariff
        public async Task<JsonResult> GetTariff(int productId, int operationId, int? employeeId = null, DateTime? workDate = null)
        {
            try
            {
                var tariffDate = workDate ?? DateTime.Now;
                int? positionId = null;
                if (employeeId.HasValue)
                {
                    var employee = await db.HR_EMPLOYEES.FindAsync(employeeId.Value);
                    if (employee != null)
                    {
                        positionId = employee.PositionID;
                    }
                }

                // Avval try ga find position-specific tariff
                var tariff = await db.HR_PIECE_RATE_TARIFFS
                    .Include(t => t.CURRENCy)
                    .Where(t => t.ProductID == productId && 
                               t.OperationID == operationId && 
                               t.IsActive &&
                               t.IsDeleted != true &&
                               t.EffectiveFrom <= tariffDate &&
                               (t.EffectiveTo == null || t.EffectiveTo >= tariffDate) &&
                               (positionId.HasValue ? t.PositionID == positionId.Value : t.PositionID == null))
                    .OrderByDescending(t => t.PositionID) // Prefer position-specific
                    .FirstOrDefaultAsync();

                // agar not found, try general tariff (PositionID == null)
                if (tariff == null)
                {
                    tariff = await db.HR_PIECE_RATE_TARIFFS
                        .Include(t => t.CURRENCy)
                        .Where(t => t.ProductID == productId && 
                                   t.OperationID == operationId && 
                                   t.PositionID == null &&
                                   t.IsActive &&
                                   t.IsDeleted != true &&
                                   t.EffectiveFrom <= tariffDate &&
                                   (t.EffectiveTo == null || t.EffectiveTo >= tariffDate))
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

        private async Task PopulateCreateSimpleViewBags(int? selectedEmployeeId = null, int? selectedProductId = null, int? selectedOperationId = null)
        {
            // Olish piece-rate employees
            var pieceRatePositionIds = await db.HR_STAFFING_PLAN
                .Where(sp => sp.IsActive && sp.IsPieceRate == true)
                .Select(sp => sp.PositionID)
                .Distinct()
                .ToListAsync();

            // agar no piece-rate positions found, get barcha faol employees
            // Otherwise, get employees bilan piece-rate positions
            List<HR_EMPLOYEES> employees;
            if (pieceRatePositionIds == null || !pieceRatePositionIds.Any())
            {
                // Fallback: get barcha faol employees agar no piece-rate positions configured
                employees = await db.HR_EMPLOYEES
                    .Where(e => e.IsActive)
                    .OrderBy(e => e.FullName)
                    .ToListAsync();
            }
            else
            {
                // Olish employees where PositionID matches piece-rate positions
                employees = await db.HR_EMPLOYEES
                    .Where(e => e.IsActive && e.PositionID.HasValue && pieceRatePositionIds.Contains(e.PositionID.Value))
                    .OrderBy(e => e.FullName)
                    .ToListAsync();
            }

            var products = await db.PRODUCTS.Where(p => p.IsFinishedGood == true && p.IsDeleted != true)
                .OrderBy(p => p.PNo).ToListAsync();
            var operations = await db.HR_PIECE_RATE_OPERATIONS.Where(o => o.IsActive).OrderBy(o => o.SortOrder).ToListAsync();

            ViewBag.EmployeeID = new SelectList(employees, "ID", "FullName", selectedEmployeeId);
            ViewBag.ProductID = new SelectList(products, "ID", "PNo", selectedProductId);
            ViewBag.OperationID = new SelectList(operations, "ID", "Name", selectedOperationId);
        }

        // Olish so'rovi: HR/PieceRateTimesheets/o'chirish/5
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
                .FirstOrDefaultAsync(t => t.ID == id && t.IsDeleted != true);

            if (timesheet == null)
            {
                return HttpNotFound();
            }

            return View(timesheet);
        }

        // POST so'rovi: HR/PieceRateTimesheets/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var timesheet = await db.HR_TIMESHEET_HEADERS
                .FirstOrDefaultAsync(t => t.ID == id && t.IsDeleted != true);
            if (timesheet == null)
            {
                return HttpNotFound();
            }

            // Tekshirish agar timesheet is approved yoki closed - cannot o'chirish
            if (timesheet.Status == "Approved" || timesheet.Status == "Closed")
            {
                TempData["Error"] = "Tasdiqlangan yoki yopilgan tabelni o'chirib bo'lmaydi";
                return RedirectToAction("Index");
            }

            // Tekshirish agar there are related payroll records
            var hasPayrolls = await db.HR_PAYROLL_HEADERS.AnyAsync(p => p.TimesheetID == id);
            if (hasPayrolls)
            {
                TempData["Error"] = "Bu tabelga bog'liq ish haqi hisobotlari mavjud. Avval ish haqi hisobotlarini o'chiring.";
                return RedirectToAction("Index");
            }

            // O'chirish timesheet lines birinchi
            var lines = await db.HR_TIMESHEET_LINES
                .Where(l => l.TimesheetID == id && l.IsDeleted != true)
                .ToListAsync();
            foreach (var line in lines)
            {
                line.IsDeleted = true;
                db.Entry(line).State = System.Data.Entity.EntityState.Modified;
            }

            timesheet.IsDeleted = true;
            db.Entry(timesheet).State = System.Data.Entity.EntityState.Modified;
            await db.SaveChangesAsync();

            logger.Log("HrPieceRateTimesheets", "Delete", $"Tabel: {timesheet.Year}-{timesheet.Month:D2}");
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

        // Olish so'rovi: HR/PieceRateTimesheets/DeleteEntries
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
                .Where(e => e.WorkDate >= startDate && e.WorkDate <= endDate && e.IsDeleted != true);

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

        // POST so'rovi: HR/PieceRateTimesheets/DeleteEntries
        [HttpPost, ActionName("DeleteEntries")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteEntriesConfirmed(int year, int month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Tekshirish agar there are related payroll records
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Olish work entries uchun this period
            IQueryable<HR_PIECE_RATE_WORK_ENTRIES> query = db.HR_PIECE_RATE_WORK_ENTRIES
                .Where(e => e.WorkDate >= startDate && e.WorkDate <= endDate && e.IsDeleted != true);

            if (orgUnitId.HasValue)
            {
                query = query.Where(e => e.HR_EMPLOYEES.OrgUnitID == orgUnitId.Value);
            }

            var workEntries = await query.ToListAsync();

            // Tekshirish agar any payroll mavjud uchun this period
            var hasPayrolls = await db.HR_PAYROLL_HEADERS
                .AnyAsync(p => p.Year == year && p.Month == month && 
                    p.PayrollType == "PieceRate" &&
                    (orgUnitId == null ? p.OrgUnitID == null : p.OrgUnitID == orgUnitId.Value));

            if (hasPayrolls)
            {
                TempData["Error"] = "Bu oy uchun ish haqi hisobotlari mavjud. Avval ish haqi hisobotlarini o'chiring.";
                return RedirectToAction("Index", new { year, month, orgUnitId });
            }

            // Tekshirish agar timesheet is approved
            var approval = await db.HR_PIECE_RATE_TIMESHEET_APPROVALS
                .FirstOrDefaultAsync(a => a.Year == year && a.Month == month && a.OrgUnitID == orgUnitId && a.IsDeleted != true);

            if (approval != null && approval.Status == "Approved")
            {
                TempData["Error"] = "Tasdiqlangan tabelni o'chirib bo'lmaydi. Avval tasdiqlashni bekor qiling.";
                return RedirectToAction("Index", new { year, month, orgUnitId });
            }

            // Soft o'chirish work entries
            foreach (var entry in workEntries)
            {
                entry.IsDeleted = true;
                db.Entry(entry).State = System.Data.Entity.EntityState.Modified;
            }

            // Soft o'chirish tasdiqlash record agar mavjud
            if (approval != null)
            {
                approval.IsDeleted = true;
                db.Entry(approval).State = System.Data.Entity.EntityState.Modified;
            }

            await db.SaveChangesAsync();

            var orgUnitName = orgUnitId.HasValue
                ? (await db.HR_ORG_UNITS.FindAsync(orgUnitId.Value))?.Name ?? "Butun kompaniya"
                : "Butun kompaniya";
            logger.Log("HrPieceRateTimesheets", "DeleteEntries", $"Tabel: {year}-{month:D2}, Bo'lim: {orgUnitName}, YozuvlarSoni: {workEntries.Count}");
            TempData["Success"] = $"{workEntries.Count} ta yozuv muvaffaqiyatli o'chirildi";
            return RedirectToAction("Index", new { year, month, orgUnitId });
        }

    }
}

