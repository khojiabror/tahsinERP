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

            year = year ?? DateTime.Now.Year;
            month = month ?? DateTime.Now.Month;

            // Get only piece-rate employees
            var pieceRatePositionIds = await db.HR_STAFFING_PLAN
                .Where(sp => sp.IsActive && sp.IsPieceRate == true)
                .Select(sp => sp.PositionID)
                .Distinct()
                .ToListAsync();

            var pieceRateEmployeeIds = await db.HR_EMPLOYEES
                .Where(e => e.IsActive && pieceRatePositionIds.Contains(e.PositionID ?? 0))
                .Select(e => e.ID)
                .ToListAsync();

            IQueryable<HR_TIMESHEET_HEADERS> query = db.HR_TIMESHEET_HEADERS
                .Include(t => t.HR_ORG_UNITS)
                .Where(t => t.HR_TIMESHEET_LINES.Any(l => pieceRateEmployeeIds.Contains(l.EmployeeID)));

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

            // Calculate piece-rate payroll amounts for each timesheet
            var viewModel = timesheets.Select(t => new
            {
                Timesheet = t,
                MonthName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(t.Month),
                CalculatedAmount = CalculatePieceRateAmount(t.Year, t.Month)
            }).ToList();

            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnitId);

            logger.Log("HrPieceRateTimesheets", "Index");
            return View(viewModel);
        }

        private decimal CalculatePieceRateAmount(int year, int month)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Get all work entries for this month
            var workEntries = db.HR_PIECE_RATE_WORK_ENTRIES
                .Where(e => e.WorkDate >= startDate && e.WorkDate <= endDate)
                .ToList();

            decimal totalAmount = 0;

            foreach (var entry in workEntries)
            {
                // Get tariff for this product and operation
                var tariff = db.HR_PIECE_RATE_TARIFFS
                    .Where(t => t.ProductID == entry.ProductID &&
                               (t.PositionID == entry.HR_EMPLOYEES.PositionID || t.PositionID == null) &&
                               t.IsActive &&
                               t.EffectiveFrom <= entry.WorkDate &&
                               (t.EffectiveTo == null || t.EffectiveTo >= entry.WorkDate))
                    .OrderByDescending(t => t.PositionID) // Prefer position-specific
                    .FirstOrDefault();

                if (tariff != null)
                {
                    totalAmount += entry.Quantity * tariff.RatePerUnit;
                }
            }

            return totalAmount;
        }

        // GET: HR/PieceRateTimesheets/Create
        public async Task<ActionResult> Create(int? year, int? month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
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

        // POST: HR/PieceRateTimesheets/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(HrTimesheetHeaderViewModel viewModel)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
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
                    ModelState.AddModelError("", "Bu oy uchun tabel allaqachon mavjud");
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

                // Create lines only for piece-rate employees
                await CreatePieceRateTimesheetLines(header.ID, viewModel.OrgUnitID);

                logger.Log("HrPieceRateTimesheets", "Create", $"ID: {header.ID}, Year: {viewModel.Year}, Month: {viewModel.Month}");
                return RedirectToAction("Edit", new { id = header.ID });
            }

            await PopulateViewBags(viewModel);
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

        // GET: HR/PieceRateTimesheets/Approve/5
        public async Task<ActionResult> Approve(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTimesheets"))
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

            logger.Log("HrPieceRateTimesheets", "Approve", $"ID: {id}");
            return RedirectToAction("Index");
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

            // Delete timesheet lines first
            var lines = await db.HR_TIMESHEET_LINES.Where(l => l.TimesheetID == id).ToListAsync();
            foreach (var line in lines)
            {
                db.HR_TIMESHEET_LINES.Remove(line);
            }

            db.HR_TIMESHEET_HEADERS.Remove(timesheet);
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
    }
}

