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

        // Olish so'rovi: HR/Timesheets
        public async Task<ActionResult> Index(int? year, int? month, int? orgUnitId, string status)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            year = year ?? DateTime.Now.Year;
            // Don't set standart month - allow null uchun "barcha months" filtrlash

            // Olish faqat hourly employees (non-piece-rate)
            var hourlyPositionIds = await db.HR_STAFFING_PLAN
                .Where(sp => sp.IsActive && sp.IsPieceRate == false)
                .Select(sp => sp.PositionID)
                .Distinct()
                .ToListAsync();

            var hourlyEmployeeIds = await db.HR_EMPLOYEES
                .Where(e => e.IsActive && (e.PositionID == null || hourlyPositionIds.Contains(e.PositionID ?? 0)))
                .Select(e => e.ID)
                .ToListAsync();

            IQueryable<HR_TIMESHEET_HEADERS> query = db.HR_TIMESHEET_HEADERS
                .Where(t => (t.IsDeleted == false || t.IsDeleted == null) && t.HR_TIMESHEET_LINES.Any(l => hourlyEmployeeIds.Contains(l.EmployeeID)))
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

        // Olish so'rovi: HR/Timesheets/yaratish
        public async Task<ActionResult> Create(int? year, int? month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            year = year ?? DateTime.Now.Year;
            month = month ?? DateTime.Now.Month;

            // Tekshirish agar timesheet already mavjud
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

        // POST so'rovi: HR/Timesheets/yaratish
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
                // Tekshirish: Cannot yaratish timesheet uchun future months
                var currentDate = DateTime.Now;
                var timesheetMonth = new DateTime(viewModel.Year, viewModel.Month, 1);
                var currentMonth = new DateTime(currentDate.Year, currentDate.Month, 1);

                if (timesheetMonth > currentMonth)
                {
                    ModelState.AddModelError("", "Kelajakka tabel kirita olmaysiz");
                    await PopulateViewBags(viewModel);
                    return View(viewModel);
                }

                // Tekshirish agar timesheet already mavjud
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

                // Yaratish lines uchun barcha faol employees in the org unit
                await CreateTimesheetLines(header.ID, viewModel.OrgUnitID);

            var orgUnitName = "Butun kompaniya";
            if (header.OrgUnitID.HasValue)
            {
                var orgUnit = await db.HR_ORG_UNITS.FindAsync(header.OrgUnitID.Value);
                orgUnitName = orgUnit?.Name ?? orgUnitName;
            }
            logger.Log("HrTimesheets", "Create", $"Tabel: {header.Year}-{header.Month:D2}, Bo'lim: {orgUnitName}");
                return RedirectToAction("Edit", new { id = header.ID });
            }

            await PopulateViewBags(viewModel);
            return View(viewModel);
        }

        // Olish so'rovi: HR/Timesheets/Edit/5
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
                .Include(t => t.HR_TIMESHEET_LINES)
                .Include(t => t.HR_TIMESHEET_LINES.Select(l => l.HR_EMPLOYEES))
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

            // Olish staffing plans ga tekshirish IsPieceRate
            var staffingPlans = await db.HR_STAFFING_PLAN
                .Where(sp => sp.IsActive && sp.OrgUnitID == header.OrgUnitID)
                .ToListAsync();

            // Filtrlash out piece-rate employees
            var hourlyPositionIds = await db.HR_STAFFING_PLAN
                .Where(sp => sp.IsActive && sp.IsPieceRate == false)
                .Select(sp => sp.PositionID)
                .Distinct()
                .ToListAsync();

            viewModel.Lines = header.HR_TIMESHEET_LINES
                .Where(l => l.HR_EMPLOYEES.PositionID != null && hourlyPositionIds.Contains(l.HR_EMPLOYEES.PositionID.Value))
                .Select(l =>
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

            // Olish finished goods uchun piece-rate selection
            ViewBag.FinishedGoods = new SelectList(
                await db.PRODUCTS.Where(p => p.IsFinishedGood == true && p.IsDeleted == false)
                    .OrderBy(p => p.PNo).ToListAsync(),
                "ID", "PNo");

            logger.Log("HrTimesheets", "Edit", $"Tabel: {header.Year}-{header.Month:D2}, Bolim: {header.HR_ORG_UNITS?.Name ?? "Butun kompaniya"}");
            return View(viewModel);
        }

        // POST so'rovi: HR/Timesheets/UpdateLine
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

                // Yangilash the specific day
                var propertyName = $"Day{day:D2}Hours";
                var property = typeof(HR_TIMESHEET_LINES).GetProperty(propertyName);
                if (property != null)
                {
                    property.SetValue(line, hours);
                }

                // Recalculate totals
                await RecalculateLineTotals(line);

                await db.SaveChangesAsync();

                var employeeLabel = line.HR_EMPLOYEES == null
                    ? "-"
                    : $"{line.HR_EMPLOYEES.FullName} ({line.HR_EMPLOYEES.TabNo ?? "-"})";
                logger.Log("HrTimesheets", "UpdateLine", $"Xodim: {employeeLabel}, Kun: {day}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST so'rovi: HR/Timesheets/UpdateQuantity
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

                // Yangilash product ID agar provided
                if (productId.HasValue)
                {
                    line.ProductID = productId.Value;
                }

                // Yangilash the specific day quantity
                var propertyName = $"Day{day:D2}Quantity";
                var property = typeof(HR_TIMESHEET_LINES).GetProperty(propertyName);
                if (property != null)
                {
                    property.SetValue(line, quantity);
                }

                // Recalculate totals
                await RecalculateLineTotals(line);

                await db.SaveChangesAsync();

                var employeeLabel = line.HR_EMPLOYEES == null
                    ? "-"
                    : $"{line.HR_EMPLOYEES.FullName} ({line.HR_EMPLOYEES.TabNo ?? "-"})";
                var product = line.ProductID.HasValue ? await db.PRODUCTS.FindAsync(line.ProductID.Value) : null;
                var productLabel = product?.Name ?? product?.PNo ?? "-";
                logger.Log("HrTimesheets", "UpdateQuantity", $"Xodim: {employeeLabel}, Kun: {day}, Miqdor: {quantity}, Mahsulot: {productLabel}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST so'rovi: HR/Timesheets/ImportFromProduction
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

                // Olish production ma'lumot uchun finished goods in this month
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
                    .FirstOrDefaultAsync(t => t.ID == timesheetId);
                var employee = await db.HR_EMPLOYEES.FindAsync(employeeId);
                var orgUnitName = header?.HR_ORG_UNITS?.Name ?? "Butun kompaniya";
                var employeeLabel = employee == null
                    ? "-"
                    : $"{employee.FullName} ({employee.TabNo ?? "-"})";
                logger.Log("HrTimesheets", "ImportFromProduction", $"Tabel: {header?.Year}-{header?.Month:D2}, Bolim: {orgUnitName}, Xodim: {employeeLabel}");
                return Json(new { success = true, message = "Ishbay ma'lumotlari import qilindi" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST so'rovi: HR/Timesheets/BulkUpdate
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

                var header = await db.HR_TIMESHEET_HEADERS
                    .Include(t => t.HR_ORG_UNITS)
                    .FirstOrDefaultAsync(t => t.ID == timesheetId);
                var orgUnitName = header?.HR_ORG_UNITS?.Name ?? "Butun kompaniya";
                logger.Log("HrTimesheets", "BulkUpdate", $"Tabel: {header?.Year}-{header?.Month:D2}, Bolim: {orgUnitName}, XodimlarSoni: {employeeIds.Count}, Kun: {day}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Olish so'rovi: HR/Timesheets/Approve/5
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

                var orgUnitName = header.HR_ORG_UNITS?.Name ?? "Butun kompaniya";
                logger.Log("HrTimesheets", "Approve", $"Tabel: {header.Year}-{header.Month:D2}, Bolim: {orgUnitName}");
            return RedirectToAction("Index");
        }

        // Olish so'rovi: HR/Timesheets/Close/5
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

            var orgUnitName = header.HR_ORG_UNITS?.Name ?? "Butun kompaniya";
            logger.Log("HrTimesheets", "Close", $"Tabel: {header.Year}-{header.Month:D2}, Bolim: {orgUnitName}");
            return RedirectToAction("Index");
        }

        // Olish so'rovi: HR/Timesheets/Report/5
        public async Task<ActionResult> Report(int id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var header = await db.HR_TIMESHEET_HEADERS
                .Include(t => t.HR_ORG_UNITS)
                .Include(t => t.HR_TIMESHEET_LINES)
                .Include(t => t.HR_TIMESHEET_LINES.Select(l => l.HR_EMPLOYEES))
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

            // Filtrlash out piece-rate employees
            var hourlyPositionIds = await db.HR_STAFFING_PLAN
                .Where(sp => sp.IsActive && sp.IsPieceRate == false)
                .Select(sp => sp.PositionID)
                .Distinct()
                .ToListAsync();

            viewModel.Lines = header.HR_TIMESHEET_LINES
                .Where(l => l.HR_EMPLOYEES.PositionID == null || hourlyPositionIds.Contains(l.HR_EMPLOYEES.PositionID ?? 0))
                .Select(l => new HrTimesheetLineViewModel
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

            return View("~/Views/HrReports/TimesheetReport.cshtml", viewModel);
        }

        // Olish so'rovi: HR/Timesheets/Export/5
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

                // ma'lumot
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

                // Auto-fit ustunlar
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                var fileName = $"Табель_{header.Year}_{header.Month:D2}.xlsx";
                var orgUnitName = header.HR_ORG_UNITS?.Name ?? "Butun kompaniya";
                logger.Log("HrTimesheets", "Export", $"Tabel: {header.Year}-{header.Month:D2}, Bolim: {orgUnitName}");
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }

        // Olish so'rovi: HR/Timesheets/Import
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

        // POST so'rovi: HR/Timesheets/UploadExcel (birinchi step - get varaq list)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UploadExcel(HttpPostedFileBase file, int year, int? month, int? orgUnitId)
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
                return View("Import");
            }

            try
            {
                // Saqlash fayl in session birinchi (before reading bilan ExcelPackage)
                byte[] fileBytes;
                using (var ms = new System.IO.MemoryStream())
                {
                    file.InputStream.Position = 0;
                    file.InputStream.CopyTo(ms);
                    fileBytes = ms.ToArray();
                }
                Session[$"ExcelFile_{User.Identity.Name}"] = fileBytes;
                Session[$"ExcelFileName_{User.Identity.Name}"] = file.FileName;

                // Now read varaq names foydalanib the byte array
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                var sheetNames = new List<string>();
                using (var ms = new System.IO.MemoryStream(fileBytes))
                using (var package = new ExcelPackage(ms))
                {
                    foreach (var worksheet in package.Workbook.Worksheets)
                    {
                        sheetNames.Add(worksheet.Name);
                    }
                }

                ViewBag.Year = year;
                ViewBag.OrgUnitId = orgUnitId;
                ViewBag.SheetNames = new SelectList(sheetNames);
                ViewBag.OrgUnitID = new SelectList(
                    await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                    "ID", "Name", orgUnitId);

                return View("ImportSheets");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Faylni o'qishda xatolik: {ex.Message}";
                
                // Urinish ga re-read varaq names dan session agar fayl mavjud
                byte[] currentFileBytes = Session[$"ExcelFile_{User.Identity.Name}"] as byte[];
                if (currentFileBytes != null)
                {
                    try
                    {
                        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                        using (var ms = new System.IO.MemoryStream(currentFileBytes))
                        using (var package = new ExcelPackage(ms))
                        {
                            ViewBag.SheetNames = new SelectList(package.Workbook.Worksheets.Select(w => w.Name));
                        }
                    }
                    catch
                    {
                        ViewBag.SheetNames = null;
                    }
                }
                else
                {
                    ViewBag.SheetNames = null;
                }
                
                ViewBag.Year = year;
                ViewBag.OrgUnitId = orgUnitId;
                ViewBag.OrgUnitID = new SelectList(
                    await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                    "ID", "Name", orgUnitId);
                return View("ImportSheets");
            }
        }

        // POST so'rovi: HR/Timesheets/ImportFromSheet (ikkinchi step - import selected varaq)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ImportFromSheet(string sheetName, int year, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            byte[] fileBytes = Session[$"ExcelFile_{User.Identity.Name}"] as byte[];
            if (fileBytes == null)
            {
                TempData["Error"] = "Fayl topilmadi. Qayta yuklang.";
                ViewBag.OrgUnitID = new SelectList(
                    await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                    "ID", "Name", orgUnitId);
                return View("Import");
            }

            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using (var package = new ExcelPackage(new System.IO.MemoryStream(fileBytes)))
                {
                    var worksheet = package.Workbook.Worksheets[sheetName];
                    if (worksheet == null)
                    {
                        TempData["Error"] = $"List '{sheetName}' topilmadi.";
                        ViewBag.Year = year;
                        ViewBag.OrgUnitId = orgUnitId;
                        ViewBag.SheetNames = new SelectList(package.Workbook.Worksheets.Select(w => w.Name));
                        ViewBag.OrgUnitID = new SelectList(
                            await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                            "ID", "Name", orgUnitId);
                        return View("ImportSheets");
                    }

                    // Tahlil qilish month va year dan varaq name (e.g., "Dekabr-2025" yoki "Декабрь-2025")
                    int month = ParseMonthFromSheetName(sheetName);
                    int sheetYear = ParseYearFromSheetName(sheetName);
                    if (month == 0 || sheetYear == 0)
                    {
                        // Use provided year va try ga tahlil month, yoki use current month
                        month = ParseMonthFromSheetName(sheetName);
                        sheetYear = year;
                    }

                    if (month == 0)
                    {
                        TempData["Error"] = $"List nomidan oy aniqlanmadi. List nomi 'Oy-Yil' formatida bo'lishi kerak (masalan: 'Dekabr-2025').";
                        ViewBag.Year = year;
                        ViewBag.OrgUnitId = orgUnitId;
                        ViewBag.SheetNames = new SelectList(package.Workbook.Worksheets.Select(w => w.Name));
                        ViewBag.OrgUnitID = new SelectList(
                            await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                            "ID", "Name", orgUnitId);
                        return View("ImportSheets");
                    }

                    var rowCount = worksheet.Dimension?.Rows ?? 0;

                    // Find yoki yaratish timesheet header
                    var header = await db.HR_TIMESHEET_HEADERS
                        .FirstOrDefaultAsync(t => t.Year == sheetYear && t.Month == month &&
                            (orgUnitId == null ? t.OrgUnitID == null : t.OrgUnitID == orgUnitId.Value));

                    if (header == null)
                    {
                        header = new HR_TIMESHEET_HEADERS
                        {
                            Year = sheetYear,
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

                    // Olish hourly position IDs uchun validation
                    var hourlyPositionIds = await db.HR_STAFFING_PLAN
                        .Where(sp => sp.IsActive && sp.IsPieceRate == false)
                        .Select(sp => sp.PositionID)
                        .Distinct()
                        .ToListAsync();

                    // Olish barcha existing lines uchun this header ga prevent duplicates
                    var existingLineEmployeeIds = await db.HR_TIMESHEET_LINES
                        .Where(l => l.TimesheetID == header.ID)
                        .Select(l => l.EmployeeID)
                        .ToListAsync();

                    var errorMessages = new List<string>();

                    // Tahlil qilish ma'lumot starting dan qator 3 (A3 range - ma'lumot starts here)
                    // Track processed employees ga avoid duplicates
                    var processedEmployees = new HashSet<int>();
                    
                    // Olish actual qator count dan worksheet
                    var actualRowCount = worksheet.Dimension?.End.Row ?? 0;
                    
                    // Boshlash dan qator 3 (A3 range where ma'lumot begins)
                    for (int row = 3; row <= actualRowCount; row++)
                    {
                        try
                        {
                            // TabNo is in ustun 2 (B ustun)
                            var tabNoCell = worksheet.Cells[row, 2];
                            var tabNoRaw = tabNoCell?.Value;
                            string tabNo = null;
                            
                            // Handle different ma'lumot types (string, double, int)
                            if (tabNoRaw != null)
                            {
                                if (tabNoRaw is string str)
                                {
                                    tabNo = str.Trim();
                                }
                                else if (tabNoRaw is double d)
                                {
                                    // Aylantirish double ga string bilan leading zeros agar needed (e.g., 1111 -> "1111", 11 -> "0011")
                                    tabNo = ((int)d).ToString();
                                }
                                else if (tabNoRaw is int i)
                                {
                                    tabNo = i.ToString();
                                }
                                else
                                {
                                    tabNo = tabNoRaw.ToString()?.Trim();
                                }
                            }
                            
                            if (string.IsNullOrWhiteSpace(tabNo))
                            {
                                // Skip empty qatorlar
                                continue;
                            }

                            // IMPORTANT: Remove barcha IsActive checks - import barcha employees regardless of holat
                            // They might have worked during that period even agar nofaol now
                            // Normalize TabNo in memory (PadLeft/TrimStart cannot be translated ga SQL)
                            string normalizedTabNo = tabNo.PadLeft(4, '0');
                            string trimmedTabNo = tabNo.TrimStart('0');
                            if (string.IsNullOrWhiteSpace(trimmedTabNo))
                                trimmedTabNo = "0";
                            
                            // Urinish exact match birinchi
                            var employee = await db.HR_EMPLOYEES
                                .Include(e => e.HR_POSITIONS)
                                .FirstOrDefaultAsync(e => e.TabNo == tabNo || e.TabNo == normalizedTabNo || e.TabNo == trimmedTabNo);
                            
                            // agar still not found, try loading candidates va matching in memory
                            if (employee == null)
                            {
                                var candidates = await db.HR_EMPLOYEES
                                    .Include(e => e.HR_POSITIONS)
                                    .Where(e => e.TabNo.Length == tabNo.Length || 
                                                e.TabNo.Length == normalizedTabNo.Length ||
                                                e.TabNo.Length == trimmedTabNo.Length)
                                    .ToListAsync();
                                
                                employee = candidates.FirstOrDefault(e => 
                                    e.TabNo == tabNo || 
                                    e.TabNo == normalizedTabNo || 
                                    e.TabNo == trimmedTabNo ||
                                    e.TabNo.PadLeft(4, '0') == normalizedTabNo ||
                                    e.TabNo.TrimStart('0') == trimmedTabNo ||
                                    (e.TabNo.TrimStart('0') == "0" && tabNo.TrimStart('0') == "0"));
                            }

                            // agar employee not found, log va skip this qator
                            if (employee == null)
                            {
                                // Log missing employee uchun debugging
                                errorMessages.Add($"Tab.№ {tabNo} - bu tabel raqamli xodim topilmadi (Qator: {row}).");
                                continue; // Skip va continue ga next employee
                            }

                            // Tekshirish agar employee was faol during this period foydalanib EmployeeHistory
                            bool isActiveInPeriod = await IsEmployeeActiveInPeriod(employee.ID, sheetYear, month);
                            if (!isActiveInPeriod)
                            {
                                errorMessages.Add($"Tab.№ {tabNo} ({employee.FullName}) - bu xodim ushbu muddatda ({sheetYear}-{month:D2}) ishda bo'lmagan (Qator: {row}).");
                                continue;
                            }

                            // Skip agar this employee was already processed in this import
                            if (processedEmployees.Contains(employee.ID))
                            {
                                errorMessages.Add($"Tab.№ {tabNo} ({employee.FullName}) - bu xodim allaqachon qayta ishlangan (Qator: {row}).");
                                continue;
                            }

                            // Tekshirish agar employee is piece-rate
                            if (employee.PositionID.HasValue && !hourlyPositionIds.Contains(employee.PositionID.Value))
                            {
                                errorMessages.Add($"Tab.№ {tabNo} ({employee.FullName}) - bu xodimning to'lov turi ishbay. Unga soatbay tabel kirita olmaymiz (Qator: {row}).");
                                continue;
                            }

                            // CRITICAL: tekshirish agar timesheet line already mavjud uchun this employee in this month/year
                            // This prevents duplicate inserts
                            
                            // Tekshirish agar employee already has a line in database (dan CreateTimesheetLines yoki previous import)
                            if (existingLineEmployeeIds.Contains(employee.ID))
                            {
                                // Employee already has timesheet ma'lumot uchun this month/year, skip
                                continue;
                            }

                            // Also tekshirish local context (entities added but not yet saved ga database in this import session)
                            var existingLineInLocal = db.HR_TIMESHEET_LINES
                                .Local
                                .FirstOrDefault(l => l.TimesheetID == header.ID && l.EmployeeID == employee.ID);
                            
                            if (existingLineInLocal != null)
                            {
                                // Already being added in this import session, skip
                                continue;
                            }

                            // faqat yaratish line agar it doesn't exist in database yoki local context
                            var line = new HR_TIMESHEET_LINES
                            {
                                TimesheetID = header.ID,
                                EmployeeID = employee.ID
                            };
                            db.HR_TIMESHEET_LINES.Add(line);

                            // Excel struktura:
                            // ustun A: №
                            // ustun B: Tab.№
                            // ustun C: F.I.O.
                            // ustun D: Smena (Shift) - tashlab ketiladi
                            // ustun E: (bo'sh yoki boshqa ma'lumot) - tashlab ketiladi
                            // ustun F: Day 1 (kunlar bu yerdan boshlanadi)
                            
                            // O'qish daily hours starting dan ustun 6 (F ustun = day 1)
                            int daysInMonth = DateTime.DaysInMonth(sheetYear, month);
                            int startCol = 6; // ustun F (day 1 starts here)

                            for (int day = 1; day <= daysInMonth; day++)
                            {
                                int col = startCol + (day - 1); // Calculate ustun uchun har bir day
                                var hoursCell = worksheet.Cells[row, col];
                                decimal? hours = null;
                                
                                if (hoursCell?.Value != null)
                                {
                                    var hoursValue = hoursCell.Value;
                                    
                                    // Handle different ma'lumot types directly (more reliable)
                                    if (hoursValue is double d)
                                    {
                                        hours = (decimal)d;
                                    }
                                    else if (hoursValue is int i)
                                    {
                                        hours = (decimal)i;
                                    }
                                    else if (hoursValue is decimal dec)
                                    {
                                        hours = dec;
                                    }
                                    else
                                    {
                                        var hoursStr = hoursValue.ToString()?.Trim();
                                        
                                        // Skip agar value is 'r', 'R', yoki other non-numeric values that indicate absence
                                        if (hoursStr != null && (hoursStr.Equals("r", StringComparison.OrdinalIgnoreCase) || 
                                                                 hoursStr.Equals("R", StringComparison.OrdinalIgnoreCase)))
                                        {
                                            hours = null; // Mark as absent
                                        }
                                        else if (!string.IsNullOrWhiteSpace(hoursStr))
                                        {
                                            // Urinish ga tahlil as decimal
                                            if (decimal.TryParse(hoursStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal h))
                                            {
                                                hours = h;
                                            }
                                        }
                                    }
                                }

                                // Belgilash hours uchun this day
                                var propertyName = $"Day{day:D2}Hours";
                                var property = typeof(HR_TIMESHEET_LINES).GetProperty(propertyName);
                                if (property != null)
                                {
                                    property.SetValue(line, hours);
                                }
                            }

                            // Recalculate totals uchun this line
                            await RecalculateLineTotals(line);
                            
                            // Belgilash employee as processed
                            processedEmployees.Add(employee.ID);
                        }
                        catch (Exception rowEx)
                        {
                            errorMessages.Add($"Qator {row} ni o'qishda xatolik: {rowEx.Message}");
                            continue; // Continue processing next qator
                        }
                    }

                    if (errorMessages.Any())
                    {
                        TempData["Warning"] = string.Join("<br/>", errorMessages);
                    }

                    await db.SaveChangesAsync();

                    // Clear session
                    Session.Remove($"ExcelFile_{User.Identity.Name}");
                    Session.Remove($"ExcelFileName_{User.Identity.Name}");

                logger.Log("HrTimesheets", "ImportFromSheet", $"Yil: {sheetYear}, Oy: {month}, Varaq: {sheetName}");
                    TempData["Success"] = $"Tabel muvaffaqiyatli import qilindi. Oy: {month}, Yil: {sheetYear}";
                    return RedirectToAction("Edit", new { id = header.ID });
                }
            }
            catch (Exception ex)
            {
                // Saqlash detailed xatolik message bilan inner exception va stack trace
                var errorMsg = $"Import xatosi: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMsg += $"<br/>Ichki xatolik: {ex.InnerException.Message}";
                }
                errorMsg += $"<br/><small>Stack Trace: {ex.StackTrace?.Replace("\r\n", "<br/>")}</small>";
                TempData["Error"] = errorMsg;
                
                // Reload varaq names dan session fayl
                byte[] fileBytesForError = Session[$"ExcelFile_{User.Identity.Name}"] as byte[];
                if (fileBytesForError != null)
                {
                    try
                    {
                        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                        using (var package = new ExcelPackage(new System.IO.MemoryStream(fileBytesForError)))
                        {
                            ViewBag.Year = year;
                            ViewBag.OrgUnitId = orgUnitId;
                            ViewBag.SheetNames = new SelectList(package.Workbook.Worksheets.Select(w => w.Name));
                            ViewBag.OrgUnitID = new SelectList(
                                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                                "ID", "Name", orgUnitId);
                            return View("ImportSheets");
                        }
                    }
                    catch (Exception reloadEx)
                    {
                        TempData["Error"] = errorMsg + $"<br/>Faylni qayta yuklashda xatolik: {reloadEx.Message}";
                        ViewBag.Year = year;
                        ViewBag.OrgUnitId = orgUnitId;
                        ViewBag.SheetNames = null;
                        ViewBag.OrgUnitID = new SelectList(
                            await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                            "ID", "Name", orgUnitId);
                        return View("ImportSheets");
                    }
                }
                else
                {
                    ViewBag.Year = year;
                    ViewBag.OrgUnitId = orgUnitId;
                    ViewBag.SheetNames = null;
                    ViewBag.OrgUnitID = new SelectList(
                        await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                        "ID", "Name", orgUnitId);
                    return View("ImportSheets");
                }
            }
        }

        private int ParseMonthFromSheetName(string sheetName)
        {
            // Use dictionary bilan indexer syntax ga avoid duplicate key exception
            // Note: "may" appears in both Uzbek va English, but we'll use indexer ga handle it
            var monthNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            
            // Uzbek months
            monthNames["yanvar"] = 1;
            monthNames["fevral"] = 2;
            monthNames["mart"] = 3;
            monthNames["aprel"] = 4;
            monthNames["may"] = 5; // Uzbek "may"
            monthNames["iyun"] = 6;
            monthNames["iyul"] = 7;
            monthNames["avgust"] = 8;
            monthNames["sentyabr"] = 9;
            monthNames["sentabr"] = 9; // Alternative spelling
            monthNames["oktyabr"] = 10;
            monthNames["noyabr"] = 11;
            monthNames["dekabr"] = 12;
            
            // Russian months
            monthNames["январь"] = 1;
            monthNames["февраль"] = 2;
            monthNames["март"] = 3;
            monthNames["апрель"] = 4;
            monthNames["май"] = 5;
            monthNames["июнь"] = 6;
            monthNames["июль"] = 7;
            monthNames["август"] = 8;
            monthNames["сентябрь"] = 9;
            monthNames["октябрь"] = 10;
            monthNames["ноябрь"] = 11;
            monthNames["декабрь"] = 12;
            
            // English months
            monthNames["january"] = 1;
            monthNames["february"] = 2;
            monthNames["march"] = 3;
            monthNames["april"] = 4;
            // "may" already added above (Uzbek), but indexer will just overwrite bilan same value
            monthNames["june"] = 6;
            monthNames["july"] = 7;
            monthNames["august"] = 8;
            monthNames["september"] = 9;
            monthNames["october"] = 10;
            monthNames["november"] = 11;
            monthNames["december"] = 12;

            try
            {
                var parts = sheetName.Split('-');
                if (parts.Length < 1) return 0;

                var monthPart = parts[0].Trim().ToLower();
                if (monthNames.ContainsKey(monthPart))
                {
                    return monthNames[monthPart];
                }

                return 0;
            }
            catch (Exception ex)
            {
                // Log xatolik va return 0
                System.Diagnostics.Debug.WriteLine($"ParseMonthFromSheetName error for '{sheetName}': {ex.Message}");
                throw; // Re-throw ga be caught by ImportFromSheet
            }
        }

        private int ParseYearFromSheetName(string sheetName)
        {
            var parts = sheetName.Split('-');
            if (parts.Length < 2) return 0;

            if (int.TryParse(parts[1].Trim(), out int year) && year >= 2000 && year <= 2100)
            {
                return year;
            }

            return 0;
        }

        // POST so'rovi: HR/Timesheets/Import
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

                    // Find yoki yaratish timesheet header
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

                    // Olish hourly position IDs uchun validation
                    var hourlyPositionIds = await db.HR_STAFFING_PLAN
                        .Where(sp => sp.IsActive && sp.IsPieceRate == false)
                        .Select(sp => sp.PositionID)
                        .Distinct()
                        .ToListAsync();

                    var errorMessages = new List<string>();

                    // Tahlil qilish ma'lumot starting dan qator 2 (skip header)
                    for (int row = 2; row <= rowCount; row++)
                    {
                        var tabNoCell = worksheet.Cells[row, 2];
                        var tabNoRaw = tabNoCell?.Value;
                        string tabNo = null;
                        
                        // Handle different ma'lumot types (string, double, int)
                        if (tabNoRaw != null)
                        {
                            if (tabNoRaw is string str)
                            {
                                tabNo = str.Trim();
                            }
                            else if (tabNoRaw is double d)
                            {
                                // Aylantirish double ga string bilan leading zeros agar needed
                                tabNo = ((int)d).ToString();
                            }
                            else if (tabNoRaw is int i)
                            {
                                tabNo = i.ToString();
                            }
                            else
                            {
                                tabNo = tabNoRaw.ToString()?.Trim();
                            }
                        }
                        
                        if (string.IsNullOrWhiteSpace(tabNo))
                            continue;

                        // IMPORTANT: Remove barcha IsActive checks - import barcha employees regardless of holat
                        // They might have worked during that period even agar nofaol now
                        // Normalize TabNo in memory (PadLeft/TrimStart cannot be translated ga SQL)
                        string normalizedTabNo = tabNo.PadLeft(4, '0');
                        string trimmedTabNo = tabNo.TrimStart('0');
                        if (string.IsNullOrWhiteSpace(trimmedTabNo))
                            trimmedTabNo = "0";
                        
                        // Urinish exact match birinchi
                        var employee = await db.HR_EMPLOYEES
                            .Include(e => e.HR_POSITIONS)
                            .FirstOrDefaultAsync(e => e.TabNo == tabNo || e.TabNo == normalizedTabNo || e.TabNo == trimmedTabNo);
                        
                        // agar still not found, try loading candidates va matching in memory
                        if (employee == null)
                        {
                            var candidates = await db.HR_EMPLOYEES
                                .Include(e => e.HR_POSITIONS)
                                .Where(e => e.TabNo.Length == tabNo.Length || 
                                            e.TabNo.Length == normalizedTabNo.Length ||
                                            e.TabNo.Length == trimmedTabNo.Length)
                                .ToListAsync();
                            
                            employee = candidates.FirstOrDefault(e => 
                                e.TabNo == tabNo || 
                                e.TabNo == normalizedTabNo || 
                                e.TabNo == trimmedTabNo ||
                                e.TabNo.PadLeft(4, '0') == normalizedTabNo ||
                                e.TabNo.TrimStart('0') == trimmedTabNo ||
                                (e.TabNo.TrimStart('0') == "0" && tabNo.TrimStart('0') == "0"));
                        }

                        if (employee == null)
                        {
                            errorMessages.Add($"Tab.№ {tabNo} - bu tabel raqamli xodim topilmadi (Qator: {row}).");
                            continue;
                        }

                        // Tekshirish agar employee was faol during this period foydalanib EmployeeHistory
                        bool isActiveInPeriod = await IsEmployeeActiveInPeriod(employee.ID, year, month);
                        if (!isActiveInPeriod)
                        {
                            errorMessages.Add($"Tab.№ {tabNo} ({employee.FullName}) - bu xodim ushbu muddatda ({year}-{month:D2}) ishda bo'lmagan (Qator: {row}).");
                            continue;
                        }

                        // Tekshirish agar employee is piece-rate
                        if (employee.PositionID.HasValue && !hourlyPositionIds.Contains(employee.PositionID.Value))
                        {
                            errorMessages.Add($"Tab.№ {tabNo} ({employee.FullName}) - bu xodimning to'lov turi ishbay. Unga soatbay tabel kirita olmaymiz (Qator: {row}).");
                            continue;
                        }

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
                        int col = 5; // Start dan day 1 ustun

                        for (int day = 1; day <= daysInMonth; day++)
                        {
                            var hoursCell = worksheet.Cells[row, col];
                            decimal? hours = null;
                            
                            if (hoursCell?.Value != null)
                            {
                                var hoursValue = hoursCell.Value;
                                
                                // Handle different ma'lumot types directly (more reliable)
                                if (hoursValue is double d)
                                {
                                    hours = (decimal)d;
                                }
                                else if (hoursValue is int i)
                                {
                                    hours = (decimal)i;
                                }
                                else if (hoursValue is decimal dec)
                                {
                                    hours = dec;
                                }
                                else
                                {
                                    var hoursStr = hoursValue.ToString()?.Trim();
                                    if (!string.IsNullOrWhiteSpace(hoursStr))
                                    {
                                        if (decimal.TryParse(hoursStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal h))
                                        {
                                            hours = h;
                                        }
                                    }
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

                    logger.Log("HrTimesheets", "Import", $"Yil: {year}, Oy: {month}");
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

        // yordamchi method ga tekshirish agar employee was faol during the specified period
        private async Task<bool> IsEmployeeActiveInPeriod(int employeeId, int year, int month)
        {
            var timesheetDate = new DateTime(year, month, 1);
            var timesheetEndDate = timesheetDate.AddMonths(1).AddDays(-1);

            // Olish employee history records ordered bo'yicha date
            var histories = await db.HR_EMPLOYEE_HISTORY
                .Where(h => h.EmployeeID == employeeId)
                .OrderBy(h => h.EffectiveDateFrom)
                .ToListAsync();

            // agar no history, tekshirish current IsActive holat
            if (histories.Count == 0)
            {
                var employee = await db.HR_EMPLOYEES.FindAsync(employeeId);
                return employee?.IsActive ?? false;
            }

            // Find the last action that was effective before yoki during the timesheet period
            var lastActionBeforePeriod = histories
                .Where(h => h.EffectiveDateFrom.Date <= timesheetEndDate)
                .OrderByDescending(h => h.EffectiveDateFrom)
                .FirstOrDefault();

            if (lastActionBeforePeriod == null)
            {
                // No history before this period, tekshirish current IsActive holat
                var employee = await db.HR_EMPLOYEES.FindAsync(employeeId);
                return employee?.IsActive ?? false;
            }

            // Tekshirish ActionType
            var actionType = lastActionBeforePeriod.ActionType?.ToLower().Trim() ?? "";
            
            // Tekshirish uchun dismiss action
            if (actionType.Contains("dismiss") || actionType.Contains("увольнение") || 
                actionType.Contains("ishdan bo'shatish") || actionType.Contains("ishdan bo'shatilgan"))
            {
                // agar dismissed, tekshirish agar dismissal date is after timesheet period
                // agar dismissed on yoki before timesheet period, employee was not faol
                return lastActionBeforePeriod.EffectiveDateFrom.Date > timesheetEndDate;
            }
            
            // Tekshirish uchun hire yoki transfer action
            if (actionType.Contains("hire") || actionType.Contains("прием") || 
                actionType.Contains("ishga qabul") || actionType.Contains("qabul qilish") ||
                actionType.Contains("transfer") || actionType.Contains("перевод") || 
                actionType.Contains("ko'chirish") || actionType.Contains("ko'chirilgan"))
            {
                // agar hired yoki transferred, tekshirish agar timesheet period is after the action date
                var actionDate = lastActionBeforePeriod.EffectiveDateFrom.Date;
                
                // Tekshirish agar there's a dismiss action after this hire/transfer
                var dismissAfter = histories
                    .Where(h => h.EffectiveDateFrom.Date > actionDate && 
                               h.EffectiveDateFrom.Date <= timesheetEndDate &&
                               (h.ActionType.ToLower().Contains("dismiss") || 
                                h.ActionType.ToLower().Contains("увольнение") ||
                                h.ActionType.ToLower().Contains("ishdan bo'shatish")))
                    .OrderBy(h => h.EffectiveDateFrom)
                    .FirstOrDefault();

                if (dismissAfter != null)
                {
                    // Employee was dismissed before yoki during timesheet period
                    return dismissAfter.EffectiveDateFrom.Date > timesheetEndDate;
                }

                // Tekshirish agar timesheet period is within the employment period
                var toDate = lastActionBeforePeriod.EffectiveDateTo?.Date ?? DateTime.MaxValue;
                return timesheetDate >= actionDate && timesheetDate <= toDate;
            }

            // standart: agar we have history but unclear action type, tekshirish agar period overlaps
            var fromDate = lastActionBeforePeriod.EffectiveDateFrom.Date;
            var endDate = lastActionBeforePeriod.EffectiveDateTo?.Date ?? DateTime.MaxValue;
            
            if (timesheetDate >= fromDate && timesheetDate <= endDate)
            {
                // Period overlaps, tekshirish current IsActive as fallback
                var employee = await db.HR_EMPLOYEES.FindAsync(employeeId);
                return employee?.IsActive ?? false;
            }

            // No overlap, tekshirish current IsActive holat
            var emp = await db.HR_EMPLOYEES.FindAsync(employeeId);
            return emp?.IsActive ?? false;
        }

        private async Task CreateTimesheetLines(int timesheetId, int? orgUnitId)
        {
            var header = await db.HR_TIMESHEET_HEADERS.FindAsync(timesheetId);
            if (header == null) return;

            int createdCount = 0;

            // Olish faqat hourly employees (non-piece-rate)
            var hourlyPositionIds = await db.HR_STAFFING_PLAN
                .Where(sp => sp.IsActive && sp.IsPieceRate == false)
                .Select(sp => sp.PositionID)
                .Distinct()
                .ToListAsync();

            IQueryable<HR_EMPLOYEES> employeesQuery = db.HR_EMPLOYEES
                .Where(e => (e.PositionID == null || hourlyPositionIds.Contains(e.PositionID ?? 0)));

            if (orgUnitId.HasValue)
            {
                employeesQuery = employeesQuery.Where(e => e.OrgUnitID == orgUnitId.Value);
            }

            var employees = await employeesQuery.ToListAsync();

            foreach (var employee in employees)
            {
                // Tekshirish agar employee was faol during this period foydalanib EmployeeHistory
                bool isActive = await IsEmployeeActiveInPeriod(employee.ID, header.Year, header.Month);
                
                if (!isActive)
                {
                    continue; // Skip employees who were not faol during this period
                }

                // Tekshirish agar line already mavjud
                var existingLine = await db.HR_TIMESHEET_LINES
                    .FirstOrDefaultAsync(l => l.TimesheetID == timesheetId && l.EmployeeID == employee.ID);

                if (existingLine == null)
                {
                    var line = new HR_TIMESHEET_LINES
                    {
                        TimesheetID = timesheetId,
                        EmployeeID = employee.ID
                    };

                    db.HR_TIMESHEET_LINES.Add(line);
                    createdCount++;
                }
            }

            await db.SaveChangesAsync();
            var orgUnitName = header.HR_ORG_UNITS?.Name ?? "Butun kompaniya";
            logger.Log("HrTimesheets", "CreateLines", $"Tabel: {header.Year}-{header.Month:D2}, Bolim: {orgUnitName}, YangiSatrlar: {createdCount}");
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

        // Olish so'rovi: HR/Timesheets/CreateSimple
        public async Task<ActionResult> CreateSimple()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Olish faol employees
            ViewBag.EmployeeID = new SelectList(
                await db.HR_EMPLOYEES.Where(e => e.IsActive).OrderBy(e => e.FullName).ToListAsync(),
                "ID", "FullName");

            // Olish finished goods
            ViewBag.ProductID = new SelectList(
                await db.PRODUCTS.Where(p => p.IsFinishedGood == true && p.IsDeleted == false)
                    .OrderBy(p => p.PNo).ToListAsync(),
                "ID", "PNo");

            // Olish operations
            ViewBag.OperationID = new SelectList(
                await db.HR_PIECE_RATE_OPERATIONS.Where(o => o.IsActive).OrderBy(o => o.SortOrder).ToListAsync(),
                "ID", "Name");

            return View();
        }

        // POST so'rovi: HR/Timesheets/CreateSimple
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateSimple(int EmployeeID, int ProductID, int OperationID, DateTime WorkDate, decimal Quantity, string Notes)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            try
            {
                // Tekshirish: Cannot yaratish timesheet uchun future months
                var currentDate = DateTime.Now;
                var workDateMonth = new DateTime(WorkDate.Year, WorkDate.Month, 1);
                var currentMonth = new DateTime(currentDate.Year, currentDate.Month, 1);

                if (workDateMonth > currentMonth)
                {
                    ModelState.AddModelError("", "Kelajakka tabel kirita olmaysiz");
                    
                    // Qayta ga'ldirish ViewBags
                    ViewBag.EmployeeID = new SelectList(
                        await db.HR_EMPLOYEES.Where(e => e.IsActive).OrderBy(e => e.FullName).ToListAsync(),
                        "ID", "FullName", EmployeeID);

                    ViewBag.ProductID = new SelectList(
                        await db.PRODUCTS.Where(p => p.IsFinishedGood == true && p.IsDeleted == false)
                        .OrderBy(p => p.PNo).ToListAsync(),
                    "ID", "PNo", ProductID);

                    ViewBag.OperationID = new SelectList(
                        await db.HR_PIECE_RATE_OPERATIONS.Where(o => o.IsActive).OrderBy(o => o.SortOrder).ToListAsync(),
                        "ID", "Name", OperationID);

                    return View();
                }

                var workEntry = new HR_PIECE_RATE_WORK_ENTRIES
                {
                    EmployeeID = EmployeeID,
                    ProductID = ProductID,
                    OperationID = OperationID,
                    WorkDate = WorkDate,
                    Quantity = Quantity,
                    Notes = Notes,
                    CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                    CreatedDT = DateTime.Now
                };

                db.HR_PIECE_RATE_WORK_ENTRIES.Add(workEntry);
                await db.SaveChangesAsync();

                var employee = await db.HR_EMPLOYEES.FindAsync(EmployeeID);
                var product = await db.PRODUCTS.FindAsync(ProductID);
                var operation = await db.HR_PIECE_RATE_OPERATIONS.FindAsync(OperationID);
                var employeeLabel = employee == null
                    ? "-"
                    : $"{employee.FullName} ({employee.TabNo ?? "-"})";
                var productLabel = product?.Name ?? product?.PNo ?? "-";
                var operationLabel = operation?.Name ?? "-";
                logger.Log("HrTimesheets", "CreateSimple", $"Xodim: {employeeLabel}, Mahsulot: {productLabel}, Operatsiya: {operationLabel}, Sana: {WorkDate:dd.MM.yyyy}, Miqdor: {Quantity}");

                TempData["Success"] = "Ishbay ma'lumotlari muvaffaqiyatli kiritildi.";
                return RedirectToAction("CreateSimple");
            }
            catch (Exception ex)
            {
                var errorMessage = $"Xatolik: {ex.Message}";
                var innerEx = ex.InnerException;
                while (innerEx != null)
                {
                    errorMessage += $" | Ichki xatolik: {innerEx.Message}";
                    innerEx = innerEx.InnerException;
                }
                ModelState.AddModelError("", errorMessage);

                // Qayta ga'ldirish ViewBags
                ViewBag.EmployeeID = new SelectList(
                    await db.HR_EMPLOYEES.Where(e => e.IsActive).OrderBy(e => e.FullName).ToListAsync(),
                    "ID", "FullName", EmployeeID);

                ViewBag.ProductID = new SelectList(
                    await db.PRODUCTS.Where(p => p.IsFinishedGood == true && p.IsDeleted == false)
                    .OrderBy(p => p.PNo).ToListAsync(),
                "ID", "PNo", ProductID);

                ViewBag.OperationID = new SelectList(
                    await db.HR_PIECE_RATE_OPERATIONS.Where(o => o.IsActive).OrderBy(o => o.SortOrder).ToListAsync(),
                    "ID", "Name", OperationID);

                return View();
            }
        }

        // Olish so'rovi: HR/Timesheets/o'chirish/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
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

        // POST so'rovi: HR/Timesheets/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var timesheet = await db.HR_TIMESHEET_HEADERS.FindAsync(id);
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

            // Soft o'chirish timesheet lines birinchi
            var lines = await db.HR_TIMESHEET_LINES.Where(l => l.TimesheetID == id).ToListAsync();
            foreach (var line in lines)
            {
                line.IsDeleted = true;
                db.Entry(line).State = System.Data.Entity.EntityState.Modified;
            }

            timesheet.IsDeleted = true;
            db.Entry(timesheet).State = System.Data.Entity.EntityState.Modified;
            await db.SaveChangesAsync();

            logger.Log("HrTimesheets", "Delete", $"Tabel: {timesheet.Year}-{timesheet.Month:D2}, Bolim: {timesheet.HR_ORG_UNITS?.Name ?? "Butun kompaniya"}");
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

        // POST so'rovi: HR/Timesheets/DeleteLine (single line)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteLine(int lineId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
            {
                TempData["Error"] = "Ruxsat yo'q";
                return RedirectToAction("Index");
            }

            try
            {
                var line = await db.HR_TIMESHEET_LINES.FindAsync(lineId);

                if (line == null)
                {
                    TempData["Error"] = "Qator topilmadi";
                    return RedirectToAction("Index");
                }

                var timesheetId = line.TimesheetID;
                var header = await db.HR_TIMESHEET_HEADERS
                    .Include(t => t.HR_ORG_UNITS)
                    .FirstOrDefaultAsync(t => t.ID == timesheetId);
                
                if (header == null)
                {
                    TempData["Error"] = "Tabel topilmadi";
                    return RedirectToAction("Index");
                }

                if (header.Status != "Draft")
                {
                    TempData["Error"] = "Faqat qoralama holatidagi tabeldan qatorlarni o'chirish mumkin";
                    return RedirectToAction("Edit", new { id = timesheetId });
                }

                // O'chirish the line dan database
                db.HR_TIMESHEET_LINES.Remove(line);
                await db.SaveChangesAsync();

                var employee = await db.HR_EMPLOYEES.FindAsync(line.EmployeeID);
                var employeeLabel = employee == null
                    ? "-"
                    : $"{employee.FullName} ({employee.TabNo ?? "-"})";
                var orgUnitName = header?.HR_ORG_UNITS?.Name ?? "Butun kompaniya";
                logger.Log("HrTimesheets", "DeleteLine", $"Tabel: {header?.Year}-{header?.Month:D2}, Bolim: {orgUnitName}, Xodim: {employeeLabel}");
                TempData["Success"] = "Qator muvaffaqiyatli o'chirildi";
                return RedirectToAction("Edit", new { id = timesheetId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Xatolik: {ex.Message}";
                
                // Urinish ga get timesheetId ga redirect back
                try
                {
                    var line = await db.HR_TIMESHEET_LINES.FindAsync(lineId);
                    if (line != null)
                    {
                        return RedirectToAction("Edit", new { id = line.TimesheetID });
                    }
                }
                catch { }
                
                return RedirectToAction("Index");
            }
        }
    }
}

