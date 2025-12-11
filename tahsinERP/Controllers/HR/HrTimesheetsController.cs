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
using OfficeOpenXml;
using System.IO;
using System.Globalization;

namespace tahsinERP.Controllers.HR
{
    [Authorize]
    public class HrTimesheetsController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();
        private IUserActionLogger logger;

        public HrTimesheetsController()
        {
            logger = new UserActionLogger(User?.Identity?.Name ?? "System");
        }

        // GET: HR/Timesheets
        public async Task<ActionResult> Index(int? year, int? month, int? orgUnitId, string status)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            year = year ?? DateTime.Now.Year;
            month = month ?? DateTime.Now.Month;

            IQueryable<HR_TIMESHEET_HEADERS> query = db.HR_TIMESHEET_HEADERS
                .Include(t => t.HR_ORG_UNITS);

            if (year.HasValue)
            {
                query = query.Where(t => t.Year == year.Value);
            }

            if (month.HasValue)
            {
                query = query.Where(t => t.Month == month.Value);
            }

            if (orgUnitId.HasValue)
            {
                query = query.Where(t => t.OrgUnitID == orgUnitId.Value);
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(t => t.Status == status);
            }

            var timesheets = await query.OrderByDescending(t => t.Year).ThenByDescending(t => t.Month).ToListAsync();

            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnitId);

            logger.Log("HrTimesheets", "Index");
            return View(timesheets);
        }

        // GET: HR/Timesheets/Create
        public async Task<ActionResult> Create(int? year, int? month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            year = year ?? DateTime.Now.Year;
            month = month ?? DateTime.Now.Month;

            // Check if timesheet already exists
            var existing = await db.HR_TIMESHEET_HEADERS
                .FirstOrDefaultAsync(t => t.Year == year.Value && t.Month == month.Value && 
                    (orgUnitId == null ? t.OrgUnitID == null : t.OrgUnitID == orgUnitId.Value));

            if (existing != null)
            {
                TempData["Info"] = $"Bu oy uchun tabel allaqachon mavjud. Mavjud tabelni ochish yoki yangi yaratishni tanlang.";
                ViewBag.ExistingTimesheetId = existing.ID;
            }

            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnitId);

            return View();
        }

        // POST: HR/Timesheets/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(HrTimesheetHeaderViewModel viewModel)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                // Check if timesheet already exists
                var existing = await db.HR_TIMESHEET_HEADERS
                    .FirstOrDefaultAsync(t => t.Year == viewModel.Year && t.Month == viewModel.Month && 
                        (viewModel.OrgUnitID == null ? t.OrgUnitID == null : t.OrgUnitID == viewModel.OrgUnitID.Value));

                if (existing != null)
                {
                    ModelState.AddModelError("", "Табель за этот период уже существует");
                    await PopulateViewBags(viewModel);
                    return View(viewModel);
                }

                var header = new HR_TIMESHEET_HEADERS
                {
                    Year = viewModel.Year,
                    Month = viewModel.Month,
                    OrgUnitID = viewModel.OrgUnitID,
                    Status = "Draft",
                    CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                    CreatedDT = DateTime.Now
                };

                db.HR_TIMESHEET_HEADERS.Add(header);
                await db.SaveChangesAsync();

                // Create lines for all active employees in the org unit
                await CreateTimesheetLines(header.ID, viewModel.OrgUnitID);

                logger.Log("HrTimesheets", "Create", $"ID: {header.ID}, Year: {viewModel.Year}, Month: {viewModel.Month}");
                return RedirectToAction("Edit", new { id = header.ID });
            }

            await PopulateViewBags(viewModel);
            return View(viewModel);
        }

        // GET: HR/Timesheets/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
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

            // Get staffing plans to check IsPieceRate
            var staffingPlans = await db.HR_STAFFING_PLAN
                .Where(sp => sp.IsActive && sp.OrgUnitID == header.OrgUnitID)
                .ToListAsync();

            viewModel.Lines = header.HR_TIMESHEET_LINES.Select(l =>
            {
                var employee = l.HR_EMPLOYEES;
                var staffingPlan = staffingPlans.FirstOrDefault(sp => sp.PositionID == employee.PositionID);
                var isPieceRate = staffingPlan?.IsPieceRate ?? false;

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
                    // Piece-rate fields
                    IsPieceRate = isPieceRate,
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

            logger.Log("HrTimesheets", "Edit", $"ID: {id}");
            return View(viewModel);
        }

        // POST: HR/Timesheets/UpdateLine
        [HttpPost]
        public async Task<JsonResult> UpdateLine(int lineId, int day, decimal? hours)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
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

                // Update the specific day
                var propertyName = $"Day{day:D2}Hours";
                var property = typeof(HR_TIMESHEET_LINES).GetProperty(propertyName);
                if (property != null)
                {
                    property.SetValue(line, hours);
                }

                // Recalculate totals
                await RecalculateLineTotals(line);

                await db.SaveChangesAsync();

                logger.Log("HrTimesheets", "UpdateLine", $"LineID: {lineId}, Day: {day}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: HR/Timesheets/UpdateQuantity
        [HttpPost]
        public async Task<JsonResult> UpdateQuantity(int lineId, int day, decimal? quantity, int? productId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
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

                // Update product ID if provided
                if (productId.HasValue)
                {
                    line.ProductID = productId.Value;
                }

                // Update the specific day quantity
                var propertyName = $"Day{day:D2}Quantity";
                var property = typeof(HR_TIMESHEET_LINES).GetProperty(propertyName);
                if (property != null)
                {
                    property.SetValue(line, quantity);
                }

                // Recalculate totals
                await RecalculateLineTotals(line);

                await db.SaveChangesAsync();

                logger.Log("HrTimesheets", "UpdateQuantity", $"LineID: {lineId}, Day: {day}, Quantity: {quantity}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: HR/Timesheets/ImportFromProduction
        [HttpPost]
        public async Task<JsonResult> ImportFromProduction(int timesheetId, int employeeId, int year, int month)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
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

                // Get production data for finished goods in this month
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

                logger.Log("HrTimesheets", "ImportFromProduction", $"TimesheetID: {timesheetId}, EmployeeID: {employeeId}");
                return Json(new { success = true, message = "Ishbay ma'lumotlari import qilindi" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: HR/Timesheets/BulkUpdate
        [HttpPost]
        public async Task<JsonResult> BulkUpdate(int timesheetId, List<int> employeeIds, int day, decimal? hours)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
            {
                return Json(new { success = false, message = "Нет прав доступа" });
            }

            try
            {
                var lines = await db.HR_TIMESHEET_LINES
                    .Where(l => l.TimesheetID == timesheetId && employeeIds.Contains(l.EmployeeID))
                    .ToListAsync();

                var propertyName = $"Day{day:D2}Hours";
                var property = typeof(HR_TIMESHEET_LINES).GetProperty(propertyName);

                foreach (var line in lines)
                {
                    if (property != null)
                    {
                        property.SetValue(line, hours);
                    }
                    await RecalculateLineTotals(line);
                }

                await db.SaveChangesAsync();

                logger.Log("HrTimesheets", "BulkUpdate", $"TimesheetID: {timesheetId}, Employees: {employeeIds.Count}, Day: {day}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: HR/Timesheets/Approve/5
        public async Task<ActionResult> Approve(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var header = await db.HR_TIMESHEET_HEADERS.FindAsync(id);
            if (header == null)
            {
                return HttpNotFound();
            }

            if (header.Status != "Draft")
            {
                TempData["Error"] = "Faqat 'Qoralama' holatidagi tabelni tasdiqlash mumkin";
                return RedirectToAction("Edit", new { id });
            }

            header.Status = "Approved";
            header.ApprovedByUserID = UserHelper.GetUserId(User.Identity.Name);
            header.ApprovedDT = DateTime.Now;

            db.Entry(header).State = EntityState.Modified;
            await db.SaveChangesAsync();

            logger.Log("HrTimesheets", "Approve", $"ID: {id}");
            return RedirectToAction("Index");
        }

        // GET: HR/Timesheets/Close/5
        public async Task<ActionResult> Close(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var header = await db.HR_TIMESHEET_HEADERS.FindAsync(id);
            if (header == null)
            {
                return HttpNotFound();
            }

            if (header.Status != "Approved")
            {
                TempData["Error"] = "Faqat 'Tasdiqlangan' holatidagi tabelni yopish mumkin";
                return RedirectToAction("Edit", new { id });
            }

            header.Status = "Closed";
            db.Entry(header).State = EntityState.Modified;
            await db.SaveChangesAsync();

            logger.Log("HrTimesheets", "Close", $"ID: {id}");
            return RedirectToAction("Index");
        }

        // GET: HR/Timesheets/Report/5
        public async Task<ActionResult> Report(int id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var header = await db.HR_TIMESHEET_HEADERS
                .Include(t => t.HR_ORG_UNITS)
                .Include(t => t.HR_TIMESHEET_LINES.Select(l => l.HR_EMPLOYEES.HR_POSITIONS))
                .FirstOrDefaultAsync(t => t.ID == id);

            if (header == null)
            {
                return HttpNotFound();
            }

            var viewModel = new HrTimesheetHeaderViewModel
            {
                ID = header.ID,
                Year = header.Year,
                Month = header.Month,
                OrgUnitID = header.OrgUnitID,
                OrgUnitName = header.HR_ORG_UNITS?.Name,
                Status = header.Status
            };

            var daysInMonth = DateTime.DaysInMonth(header.Year, header.Month);

            viewModel.Lines = header.HR_TIMESHEET_LINES.Select(l => new HrTimesheetLineViewModel
            {
                ID = l.ID,
                EmployeeTabNo = l.HR_EMPLOYEES.TabNo,
                EmployeeFullName = l.HR_EMPLOYEES.FullName,
                PositionName = l.HR_EMPLOYEES.HR_POSITIONS?.Name,
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
                DaysInMonth = daysInMonth
            }).OrderBy(l => l.EmployeeTabNo).ToList();

            return View("~/Views/HR/HrReports/TimesheetReport.cshtml", viewModel);
        }

        // GET: HR/Timesheets/Export/5
        public async Task<ActionResult> Export(int id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var header = await db.HR_TIMESHEET_HEADERS
                .Include(t => t.HR_ORG_UNITS)
                .Include(t => t.HR_TIMESHEET_LINES.Select(l => l.HR_EMPLOYEES.HR_POSITIONS))
                .FirstOrDefaultAsync(t => t.ID == id);

            if (header == null)
            {
                return HttpNotFound();
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add($"Табель {header.Year}-{header.Month:D2}");

                // Headers
                worksheet.Cells[1, 1].Value = "№";
                worksheet.Cells[1, 2].Value = "Таб.№";
                worksheet.Cells[1, 3].Value = "Ф.И.О.";
                worksheet.Cells[1, 4].Value = "Должность";

                int col = 5;
                int daysInMonth = DateTime.DaysInMonth(header.Year, header.Month);
                for (int day = 1; day <= daysInMonth; day++)
                {
                    worksheet.Cells[1, col++].Value = day.ToString();
                }

                worksheet.Cells[1, col++].Value = "Факт.раб.дней";
                worksheet.Cells[1, col++].Value = "Факт.раб.часов";
                worksheet.Cells[1, col++].Value = "Сверхурочные";
                worksheet.Cells[1, col++].Value = "Дни неявок";

                // Data
                int row = 2;
                int lineNum = 1;
                foreach (var line in header.HR_TIMESHEET_LINES.OrderBy(l => l.HR_EMPLOYEES.TabNo))
                {
                    worksheet.Cells[row, 1].Value = lineNum++;
                    worksheet.Cells[row, 2].Value = line.HR_EMPLOYEES.TabNo;
                    worksheet.Cells[row, 3].Value = line.HR_EMPLOYEES.FullName;
                    worksheet.Cells[row, 4].Value = line.HR_EMPLOYEES.HR_POSITIONS?.Name;

                    col = 5;
                    for (int day = 1; day <= daysInMonth; day++)
                    {
                        var propertyName = $"Day{day:D2}Hours";
                        var property = typeof(HR_TIMESHEET_LINES).GetProperty(propertyName);
                        var hours = property?.GetValue(line) as decimal?;
                        worksheet.Cells[row, col++].Value = hours ?? 0;
                    }

                    worksheet.Cells[row, col++].Value = line.TotalWorkedDays ?? 0;
                    worksheet.Cells[row, col++].Value = line.TotalWorkedHours ?? 0;
                    worksheet.Cells[row, col++].Value = line.OvertimeHours ?? 0;
                    worksheet.Cells[row, col++].Value = line.AbsentDays ?? 0;

                    row++;
                }

                // Auto-fit columns
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                var fileName = $"Табель_{header.Year}_{header.Month:D2}.xlsx";
                logger.Log("HrTimesheets", "Export", $"ID: {id}");
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }

        // GET: HR/Timesheets/Import
        public async Task<ActionResult> Import()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name");

            return View();
        }

        // POST: HR/Timesheets/Import
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Import(HttpPostedFileBase file, int year, int month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (file == null || file.ContentLength == 0)
            {
                TempData["Error"] = "Fayl tanlanmagan";
                ViewBag.OrgUnitID = new SelectList(
                    await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                    "ID", "Name", orgUnitId);
                return View();
            }

            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using (var package = new ExcelPackage(file.InputStream))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    var rowCount = worksheet.Dimension?.Rows ?? 0;

                    // Find or create timesheet header
                    var header = await db.HR_TIMESHEET_HEADERS
                        .FirstOrDefaultAsync(t => t.Year == year && t.Month == month && 
                            (orgUnitId == null ? t.OrgUnitID == null : t.OrgUnitID == orgUnitId.Value));

                    if (header == null)
                    {
                        header = new HR_TIMESHEET_HEADERS
                        {
                            Year = year,
                            Month = month,
                            OrgUnitID = orgUnitId,
                            Status = "Draft",
                            CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                            CreatedDT = DateTime.Now
                        };
                        db.HR_TIMESHEET_HEADERS.Add(header);
                        await db.SaveChangesAsync();
                        await CreateTimesheetLines(header.ID, orgUnitId);
                    }

                    // Parse data starting from row 2 (skip header)
                    for (int row = 2; row <= rowCount; row++)
                    {
                        var tabNo = worksheet.Cells[row, 2]?.Value?.ToString();
                        if (string.IsNullOrWhiteSpace(tabNo))
                            continue;

                        var employee = await db.HR_EMPLOYEES
                            .FirstOrDefaultAsync(e => e.TabNo == tabNo && e.IsActive);

                        if (employee == null)
                            continue;

                        var line = await db.HR_TIMESHEET_LINES
                            .FirstOrDefaultAsync(l => l.TimesheetID == header.ID && l.EmployeeID == employee.ID);

                        if (line == null)
                        {
                            line = new HR_TIMESHEET_LINES
                            {
                                TimesheetID = header.ID,
                                EmployeeID = employee.ID
                            };
                            db.HR_TIMESHEET_LINES.Add(line);
                        }

                        int daysInMonth = DateTime.DaysInMonth(year, month);
                        int col = 5; // Start from day 1 column

                        for (int day = 1; day <= daysInMonth; day++)
                        {
                            var hoursValue = worksheet.Cells[row, col]?.Value;
                            decimal? hours = null;
                            if (hoursValue != null)
                            {
                                if (decimal.TryParse(hoursValue.ToString(), out decimal h))
                                {
                                    hours = h;
                                }
                            }

                            var propertyName = $"Day{day:D2}Hours";
                            var property = typeof(HR_TIMESHEET_LINES).GetProperty(propertyName);
                            property?.SetValue(line, hours);

                            col++;
                        }

                        await RecalculateLineTotals(line);
                    }

                    await db.SaveChangesAsync();

                    logger.Log("HrTimesheets", "Import", $"Year: {year}, Month: {month}");
                    return RedirectToAction("Edit", new { id = header.ID });
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Import xatosi: {ex.Message}";
                ViewBag.OrgUnitID = new SelectList(
                    await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                    "ID", "Name", orgUnitId);
                return View();
            }
        }

        private async Task CreateTimesheetLines(int timesheetId, int? orgUnitId)
        {
            IQueryable<HR_EMPLOYEES> employeesQuery = db.HR_EMPLOYEES.Where(e => e.IsActive);

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
                    if (hours > 8) // Assuming 8 hours is standard
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

