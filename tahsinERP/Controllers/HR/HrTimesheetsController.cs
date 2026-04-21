using Newtonsoft.Json;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels.HR;

namespace tahsinERP.Controllers.HR
{
    [Authorize]
    public class HrTimesheetsController : Controller
    {
        private class EmployeePickItem
        {
            public int ID { get; set; }
            public string DisplayName { get; set; }
        }
        private class AddEmployeesResult
        {
            public int AddedCount { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
        }

        private class TimesheetSavePayload
        {
            public List<TimesheetHourCell> Hours { get; set; }
            public List<TimesheetQtyCell> Quantities { get; set; }
            public List<TimesheetLineProduct> Products { get; set; }
        }

        private class TimesheetHourCell
        {
            public int LineId { get; set; }
            public int Day { get; set; }
            public decimal? Hours { get; set; }
        }

        private class TimesheetQtyCell
        {
            public int LineId { get; set; }
            public int Day { get; set; }
            public decimal? Quantity { get; set; }
        }

        private class TimesheetLineProduct
        {
            public int LineId { get; set; }
            public int ProductId { get; set; }
        }

        private const string Draft = "Draft";
        private const string Approved = "Approved";
        private const string Closed = "Closed";

        private readonly DBTHSNEntities db = new DBTHSNEntities();
        private readonly IUserActionLogger logger;
        private readonly HrTimesheetSettingsService timesheetSettingsService;

        public HrTimesheetsController()
        {
            logger = new UserActionLogger(User?.Identity?.Name ?? "System");
            timesheetSettingsService = new HrTimesheetSettingsService(db);
        }

        public async Task<ActionResult> Index(int? year, int? month, int? orgUnitId, string status)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            year = year ?? DateTime.Now.Year;
            IQueryable<HR_TIMESHEET_HEADERS> query = db.HR_TIMESHEET_HEADERS
                .Where(IsNotDeletedHeaderExpr())
                .Where(h => h.HR_TIMESHEET_LINES.Any(l =>
                    (l.IsDeleted == null || l.IsDeleted == false) &&
                    (l.HR_EMPLOYEES.IsDeleted == null || l.HR_EMPLOYEES.IsDeleted == false)))
                .Include(h => h.HR_ORG_UNITS);

            if (year.HasValue) query = query.Where(h => h.Year == year.Value);
            if (month.HasValue) query = query.Where(h => h.Month == month.Value);
            if (orgUnitId.HasValue) query = query.Where(h => h.OrgUnitID == orgUnitId.Value);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(h => h.Status == status);

            var model = await query.OrderByDescending(h => h.Year).ThenByDescending(h => h.Month).ToListAsync();

            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.Status = status;
            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnitId);

            return View(model);
        }

        public async Task<ActionResult> Create(int? year, int? month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            year = year ?? DateTime.Now.Year;
            month = month ?? DateTime.Now.Month;

            var existing = await FindActiveHeader(year.Value, month.Value, orgUnitId);
            if (existing != null)
            {
                TempData["Info"] = "Bu oy uchun tabel allaqachon mavjud";
                ViewBag.ExistingTimesheetId = existing.ID;
            }

            ViewBag.Year = year;
            ViewBag.Month = month;
            await PopulateOrgUnits(orgUnitId);
            await PopulateCreateEmployees();
            return View(new HrTimesheetHeaderViewModel
            {
                Year = year.Value,
                Month = month.Value,
                OrgUnitID = orgUnitId,
                FundHours = 160m
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(HrTimesheetHeaderViewModel model, List<int> selectedEmployeeIds)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (!ModelState.IsValid)
            {
                await PopulateOrgUnits(model.OrgUnitID);
                await PopulateCreateEmployees();
                return View(model);
            }

            if (new DateTime(model.Year, model.Month, 1) > new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1))
            {
                ModelState.AddModelError("", "Kelajak oyiga tabel yaratib bo'lmaydi");
                await PopulateOrgUnits(model.OrgUnitID);
                await PopulateCreateEmployees();
                return View(model);
            }

            var existing = await FindActiveHeader(model.Year, model.Month, model.OrgUnitID);
                if (existing != null)
                {
                ModelState.AddModelError("", "Ushbu davr uchun tabel allaqachon mavjud");
                await PopulateOrgUnits(model.OrgUnitID);
                await PopulateCreateEmployees();
                return View(model);
                }

                var header = new HR_TIMESHEET_HEADERS
                {
                Year = model.Year,
                Month = model.Month,
                OrgUnitID = model.OrgUnitID,
                Status = Draft,
                    CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                CreatedDT = DateTime.Now,
                IsDeleted = false
                };
                db.HR_TIMESHEET_HEADERS.Add(header);
                await db.SaveChangesAsync();
                timesheetSettingsService.UpsertFundHours(header.ID, model.FundHours);

            await CreateTimesheetLines(header.ID, header.OrgUnitID);
            var addResult = await AddEmployeesToTimesheet(header.ID, selectedEmployeeIds);
            if (addResult.Errors.Any())
            {
                TempData["Error"] = string.Join("<br/>", addResult.Errors);
            }
            if (addResult.AddedCount > 0)
            {
                TempData["Success"] = $"{addResult.AddedCount} ta xodim qo'shildi";
            }
            logger.Log("HrTimesheets", "Create", $"ID={header.ID}");
                return RedirectToAction("Edit", new { id = header.ID });
            }

        public async Task<ActionResult> Edit(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
            if (!id.HasValue) return HttpNotFound();

            var header = await db.HR_TIMESHEET_HEADERS
                .Include(h => h.HR_ORG_UNITS)
                .Include(h => h.HR_TIMESHEET_LINES.Select(l => l.HR_EMPLOYEES.HR_POSITIONS))
                .Include(h => h.HR_TIMESHEET_LINES.Select(l => l.PRODUCT))
                .FirstOrDefaultAsync(h => h.ID == id.Value && (h.IsDeleted == null || h.IsDeleted == false));
            if (header == null) return HttpNotFound();

            var daysInMonth = DateTime.DaysInMonth(header.Year, header.Month);
            var vm = new HrTimesheetHeaderViewModel
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
                ApprovedDT = header.ApprovedDT,
                FundHours = timesheetSettingsService.GetFundHoursOrDefault(header.ID),
                Lines = header.HR_TIMESHEET_LINES
                    .Where(l => l.IsDeleted == null || l.IsDeleted == false)
                    .Where(l => l.HR_EMPLOYEES != null)
                    .OrderBy(l => l.HR_EMPLOYEES.TabNo)
                    .Select(l => ToLineVm(l, daysInMonth))
                    .ToList()
            };

            ViewBag.FinishedGoods = new SelectList(
                await db.PRODUCTS.Where(p => p.IsFinishedGood == true && p.IsDeleted == false).OrderBy(p => p.PNo).ToListAsync(),
                "ID", "PNo");
            ViewBag.AvailableEmployees = new MultiSelectList(
                await GetEmployeesNotInTimesheet(header.ID),
                "ID",
                "DisplayName");

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddEmployees(int timesheetId, List<int> employeeIds)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
            {
                TempData["Error"] = "Ruxsat yo'q";
                return RedirectToAction("Index");
            }

            var header = await db.HR_TIMESHEET_HEADERS.FirstOrDefaultAsync(h => h.ID == timesheetId && (h.IsDeleted == null || h.IsDeleted == false));
            if (header == null) return HttpNotFound();
            if (header.Status != Draft)
            {
                TempData["Error"] = "Faqat qoralama tabelga xodim qo'shish mumkin";
                return RedirectToAction("Edit", new { id = timesheetId });
            }

            var result = await AddEmployeesToTimesheet(timesheetId, employeeIds);
            if (result.AddedCount > 0)
            {
                TempData["Success"] = $"{result.AddedCount} ta xodim qo'shildi";
            }
            if (result.Errors.Any())
            {
                TempData["Error"] = string.Join("<br/>", result.Errors);
            }
            if (result.AddedCount == 0 && !result.Errors.Any())
            {
                TempData["Info"] = "Yangi xodim qo'shilmadi: tanlanganlar allaqachon tabelda bor";
            }
            return RedirectToAction("Edit", new { id = timesheetId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SaveTimesheet(int timesheetId, string gridJson, decimal? fundHours)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (string.IsNullOrWhiteSpace(gridJson))
            {
                TempData["Error"] = "Saqlash uchun ma'lumot yuborilmadi";
                return RedirectToAction("Edit", new { id = timesheetId });
            }

            TimesheetSavePayload payload;
            try
            {
                payload = JsonConvert.DeserializeObject<TimesheetSavePayload>(gridJson);
            }
            catch
            {
                TempData["Error"] = "Ma'lumot formati noto'g'ri";
                return RedirectToAction("Edit", new { id = timesheetId });
            }

            var header = await db.HR_TIMESHEET_HEADERS.FirstOrDefaultAsync(h => h.ID == timesheetId && (h.IsDeleted == null || h.IsDeleted == false));
            if (header == null) return HttpNotFound();
            if (header.Status != Draft)
            {
                TempData["Error"] = "Faqat qoralama holatidagi tabelni saqlash mumkin";
                return RedirectToAction("Edit", new { id = timesheetId });
            }

            var daysInMonth = DateTime.DaysInMonth(header.Year, header.Month);
            var linesList = await db.HR_TIMESHEET_LINES
                .Where(l => l.TimesheetID == timesheetId && (l.IsDeleted == null || l.IsDeleted == false))
                .ToListAsync();
            var linesDict = linesList.ToDictionary(l => l.ID);

            var touchedLineIds = new HashSet<int>();

            if (payload.Hours != null)
            {
                foreach (var cell in payload.Hours)
                {
                    if (cell.Day < 1 || cell.Day > daysInMonth) continue;
                    if (!linesDict.TryGetValue(cell.LineId, out var line)) continue;
                    typeof(HR_TIMESHEET_LINES).GetProperty($"Day{cell.Day:D2}Hours")?.SetValue(line, cell.Hours);
                    touchedLineIds.Add(line.ID);
                }
            }

            if (payload.Quantities != null)
            {
                foreach (var cell in payload.Quantities)
                {
                    if (cell.Day < 1 || cell.Day > daysInMonth) continue;
                    if (!linesDict.TryGetValue(cell.LineId, out var line)) continue;
                    typeof(HR_TIMESHEET_LINES).GetProperty($"Day{cell.Day:D2}Quantity")?.SetValue(line, cell.Quantity);
                    touchedLineIds.Add(line.ID);
                }
            }

            if (payload.Products != null)
            {
                foreach (var p in payload.Products)
                {
                    if (!linesDict.TryGetValue(p.LineId, out var line)) continue;
                    line.ProductID = p.ProductId;
                    touchedLineIds.Add(line.ID);
                }
            }

            foreach (var lineId in touchedLineIds)
            {
                if (linesDict.TryGetValue(lineId, out var line))
                {
                    await RecalculateLineTotals(line);
                }
            }

            if (fundHours.HasValue)
            {
                timesheetSettingsService.UpsertFundHours(timesheetId, fundHours.Value);
            }

            await db.SaveChangesAsync();
            TempData["Success"] = "Tabel ma'lumotlari bazaga saqlandi";
            logger.Log("HrTimesheets", "SaveTimesheet", $"TabelID={timesheetId}");
            return RedirectToAction("Edit", new { id = timesheetId });
        }

        [HttpPost]
        public async Task<JsonResult> UpdateLine(int lineId, int day, decimal? hours)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
                return Json(new { success = false, message = "Ruxsat yo'q" });
            if (day < 1 || day > 31) return Json(new { success = false, message = "Kun noto'g'ri" });

            var line = await db.HR_TIMESHEET_LINES.FirstOrDefaultAsync(l => l.ID == lineId && (l.IsDeleted == null || l.IsDeleted == false));
            if (line == null) return Json(new { success = false, message = "Qator topilmadi" });

            typeof(HR_TIMESHEET_LINES).GetProperty($"Day{day:D2}Hours")?.SetValue(line, hours);
                await RecalculateLineTotals(line);
                await db.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<JsonResult> UpdateQuantity(int lineId, int day, decimal? quantity, int? productId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
                return Json(new { success = false, message = "Ruxsat yo'q" });
            if (day < 1 || day > 31) return Json(new { success = false, message = "Kun noto'g'ri" });

            var line = await db.HR_TIMESHEET_LINES.FirstOrDefaultAsync(l => l.ID == lineId && (l.IsDeleted == null || l.IsDeleted == false));
            if (line == null) return Json(new { success = false, message = "Qator topilmadi" });

            if (productId.HasValue) line.ProductID = productId;
            typeof(HR_TIMESHEET_LINES).GetProperty($"Day{day:D2}Quantity")?.SetValue(line, quantity);
            await RecalculateLineTotals(line);
            await db.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<JsonResult> BulkUpdate(int timesheetId, List<int> employeeIds, int day, decimal? hours)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
                return Json(new { success = false, message = "Ruxsat yo'q" });
            if (employeeIds == null || !employeeIds.Any())
                return Json(new { success = false, message = "Xodimlar tanlanmagan" });

                var lines = await db.HR_TIMESHEET_LINES
                .Where(l => l.TimesheetID == timesheetId && employeeIds.Contains(l.EmployeeID) && (l.IsDeleted == null || l.IsDeleted == false))
                    .ToListAsync();

            var prop = typeof(HR_TIMESHEET_LINES).GetProperty($"Day{day:D2}Hours");
                foreach (var line in lines)
                {
                prop?.SetValue(line, hours);
                    await RecalculateLineTotals(line);
                }
                await db.SaveChangesAsync();
                return Json(new { success = true });
            }

        [HttpPost]
        public async Task<JsonResult> ImportFromProduction(int timesheetId, int employeeId, int year, int month)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
                return Json(new { success = false, message = "Ruxsat yo'q" });

            var line = await db.HR_TIMESHEET_LINES
                .FirstOrDefaultAsync(l => l.TimesheetID == timesheetId && l.EmployeeID == employeeId && (l.IsDeleted == null || l.IsDeleted == false));
            if (line == null) return Json(new { success = false, message = "Qator topilmadi" });

            var from = new DateTime(year, month, 1);
            var to = from.AddMonths(1).AddDays(-1);
            var grouped = await db.PRODUCT_PRODUCTION
                .Where(p => p.Date >= from && p.Date <= to && p.PRODUCT.IsFinishedGood == true)
                .GroupBy(p => new { p.Date, p.ProductID })
                .Select(g => new { g.Key.Date, g.Key.ProductID, Total = g.Sum(x => x.Total) })
                .ToListAsync();

            foreach (var g in grouped)
            {
                var day = g.Date.Day;
                if (day < 1 || day > 31) continue;
                typeof(HR_TIMESHEET_LINES).GetProperty($"Day{day:D2}Quantity")?.SetValue(line, (decimal?)g.Total);
                if (!line.ProductID.HasValue) line.ProductID = g.ProductID;
            }

            await RecalculateLineTotals(line);
            await db.SaveChangesAsync();
            return Json(new { success = true, message = "Import bajarildi" });
        }

        public async Task<ActionResult> Approve(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets", "Approve"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            var header = await db.HR_TIMESHEET_HEADERS.FirstOrDefaultAsync(h => h.ID == id && (h.IsDeleted == null || h.IsDeleted == false));
            if (header == null) return HttpNotFound();
            if (header.Status != Draft)
            {
                TempData["Error"] = "Faqat qoralama tabel tasdiqlanadi";
                return RedirectToAction("Edit", new { id });
            }
            header.Status = Approved;
            header.ApprovedByUserID = UserHelper.GetUserId(User.Identity.Name);
            header.ApprovedDT = DateTime.Now;
            await db.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        public async Task<ActionResult> Close(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            var header = await db.HR_TIMESHEET_HEADERS.FirstOrDefaultAsync(h => h.ID == id && (h.IsDeleted == null || h.IsDeleted == false));
            if (header == null) return HttpNotFound();
            if (header.Status != Approved)
            {
                TempData["Error"] = "Faqat tasdiqlangan tabel yopiladi";
                return RedirectToAction("Edit", new { id });
            }
            header.Status = Closed;
            await db.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        public async Task<ActionResult> Delete(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
                return RedirectToAction("Index", "ViewNotAuthorized");
            if (!id.HasValue) return HttpNotFound();

            var header = await db.HR_TIMESHEET_HEADERS
                .Include(h => h.HR_ORG_UNITS)
                .FirstOrDefaultAsync(h => h.ID == id.Value && (h.IsDeleted == null || h.IsDeleted == false));
            if (header == null) return HttpNotFound();

            return View(header);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            var header = await db.HR_TIMESHEET_HEADERS.FirstOrDefaultAsync(h => h.ID == id && (h.IsDeleted == null || h.IsDeleted == false));
            if (header == null) return HttpNotFound();
            if (header.Status != Draft)
            {
                TempData["Error"] = "Faqat qoralama tabel o'chiriladi";
                return RedirectToAction("Index");
            }

            var hasPayroll = await db.HR_PAYROLL_HEADERS.AnyAsync(p => p.TimesheetID == id);
            if (hasPayroll)
            {
                TempData["Error"] = "Bu tabelga bog'langan ish haqi bor";
                return RedirectToAction("Index");
            }

            var lines = await db.HR_TIMESHEET_LINES.Where(l => l.TimesheetID == id && (l.IsDeleted == null || l.IsDeleted == false)).ToListAsync();
            foreach (var line in lines) line.IsDeleted = true;
            header.IsDeleted = true;
            await db.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteLine(int lineId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
            {
                TempData["Error"] = "Ruxsat yo'q";
                return RedirectToAction("Index");
            }

            var line = await db.HR_TIMESHEET_LINES.FirstOrDefaultAsync(l => l.ID == lineId && (l.IsDeleted == null || l.IsDeleted == false));
            if (line == null)
            {
                TempData["Error"] = "Qator topilmadi";
                return RedirectToAction("Index");
            }

            var header = await db.HR_TIMESHEET_HEADERS.FirstOrDefaultAsync(h => h.ID == line.TimesheetID && (h.IsDeleted == null || h.IsDeleted == false));
            if (header == null) return RedirectToAction("Index");
            if (header.Status != Draft)
            {
                TempData["Error"] = "Faqat qoralama tabel satri o'chiriladi";
                return RedirectToAction("Edit", new { id = header.ID });
            }

            line.IsDeleted = true;
            await db.SaveChangesAsync();
            TempData["Success"] = "Qator o'chirildi";
            return RedirectToAction("Edit", new { id = header.ID });
        }

        public async Task<ActionResult> Report(int id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrTimesheets"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            var header = await db.HR_TIMESHEET_HEADERS
                .Include(h => h.HR_ORG_UNITS)
                .Include(h => h.HR_TIMESHEET_LINES.Select(l => l.HR_EMPLOYEES.HR_POSITIONS))
                .FirstOrDefaultAsync(h => h.ID == id && (h.IsDeleted == null || h.IsDeleted == false));
            if (header == null) return HttpNotFound();

            var daysInMonth = DateTime.DaysInMonth(header.Year, header.Month);
            var vm = new HrTimesheetHeaderViewModel
            {
                ID = header.ID,
                Year = header.Year,
                Month = header.Month,
                OrgUnitID = header.OrgUnitID,
                OrgUnitName = header.HR_ORG_UNITS?.Name,
                Status = header.Status,
                FundHours = timesheetSettingsService.GetFundHoursOrDefault(header.ID),
                Lines = header.HR_TIMESHEET_LINES
                    .Where(l => l.IsDeleted == null || l.IsDeleted == false)
                    .OrderBy(l => l.HR_EMPLOYEES.TabNo)
                    .Select(l => ToLineVm(l, daysInMonth))
                    .ToList()
            };
            return View("~/Views/HrReports/TimesheetReport.cshtml", vm);
        }

        public async Task<ActionResult> Preview(int id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrTimesheets"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            var header = await db.HR_TIMESHEET_HEADERS
                .Include(h => h.HR_ORG_UNITS)
                .Include(h => h.HR_TIMESHEET_LINES.Select(l => l.HR_EMPLOYEES.HR_POSITIONS))
                .FirstOrDefaultAsync(h => h.ID == id && (h.IsDeleted == null || h.IsDeleted == false));
            if (header == null) return HttpNotFound();

            var daysInMonth = DateTime.DaysInMonth(header.Year, header.Month);
            var vm = new HrTimesheetHeaderViewModel
            {
                ID = header.ID,
                Year = header.Year,
                Month = header.Month,
                OrgUnitID = header.OrgUnitID,
                OrgUnitName = header.HR_ORG_UNITS?.Name,
                Status = header.Status,
                FundHours = timesheetSettingsService.GetFundHoursOrDefault(header.ID),
                Lines = header.HR_TIMESHEET_LINES
                    .Where(l => l.IsDeleted == null || l.IsDeleted == false)
                    .OrderBy(l => l.HR_EMPLOYEES.TabNo)
                    .Select(l => ToLineVm(l, daysInMonth))
                    .ToList()
            };

            return View("~/Views/HrTimesheets/Details.cshtml", vm);
        }

        public async Task<ActionResult> Export(int id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrTimesheets"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            var header = await db.HR_TIMESHEET_HEADERS
                .Include(h => h.HR_TIMESHEET_LINES.Select(l => l.HR_EMPLOYEES.HR_POSITIONS))
                .FirstOrDefaultAsync(h => h.ID == id && (h.IsDeleted == null || h.IsDeleted == false));
            if (header == null) return HttpNotFound();

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add($"Tabel {header.Year}-{header.Month:D2}");
                var fundHours = timesheetSettingsService.GetFundHoursOrDefault(header.ID);
                ws.Cells[1, 1].Value = "№";
                ws.Cells[1, 2].Value = "Tab.№";
                ws.Cells[1, 3].Value = "F.I.Sh.";
                ws.Cells[1, 4].Value = "Lavozim";

                var daysInMonth = DateTime.DaysInMonth(header.Year, header.Month);
                int col = 5;
                for (int d = 1; d <= daysInMonth; d++) ws.Cells[1, col++].Value = d;
                ws.Cells[1, col++].Value = "Ishlangan kun";
                ws.Cells[1, col++].Value = "Ishlangan soat";
                ws.Cells[1, col++].Value = "Qo'shimcha soat";
                ws.Cells[1, col++].Value = "Kelmagan kun";

                int row = 2; int n = 1;
                foreach (var line in header.HR_TIMESHEET_LINES.Where(l => l.IsDeleted == null || l.IsDeleted == false).OrderBy(l => l.HR_EMPLOYEES.TabNo))
                {
                    ws.Cells[row, 1].Value = n++;
                    ws.Cells[row, 2].Value = line.HR_EMPLOYEES.TabNo;
                    ws.Cells[row, 3].Value = line.HR_EMPLOYEES.FullName;
                    ws.Cells[row, 4].Value = line.HR_EMPLOYEES.HR_POSITIONS?.Name;
                    col = 5;
                    for (int d = 1; d <= daysInMonth; d++)
                    {
                        var h = typeof(HR_TIMESHEET_LINES).GetProperty($"Day{d:D2}Hours")?.GetValue(line) as decimal?;
                        ws.Cells[row, col++].Value = h ?? 0;
                    }
                    ws.Cells[row, col++].Value = line.TotalWorkedDays ?? 0;
                    ws.Cells[row, col++].Value = line.TotalWorkedHours ?? 0;
                    ws.Cells[row, col++].Value = line.OvertimeHours ?? 0;
                    ws.Cells[row, col++].Value = line.AbsentDays ?? 0;
                    row++;
                }

                ws.Cells[row + 1, 1].Value = "Oylik fond soati";
                ws.Cells[row + 1, 2].Value = fundHours;
                ws.Cells[row + 2, 1].Value = "Jami qo'shimcha soat";
                ws.Cells[row + 2, 2].Value = header.HR_TIMESHEET_LINES
                    .Where(l => l.IsDeleted == null || l.IsDeleted == false)
                    .Sum(l => l.OvertimeHours ?? 0);

                ws.Cells[ws.Dimension.Address].AutoFitColumns();
                return File(package.GetAsByteArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"Tabel_{header.Year}_{header.Month:D2}.xlsx");
            }
        }

        public async Task<ActionResult> Import()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
                return RedirectToAction("Index", "ViewNotAuthorized");
            await PopulateOrgUnits(null);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UploadExcel(HttpPostedFileBase file, int year, int? month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
                return RedirectToAction("Index", "ViewNotAuthorized");
            if (file == null || file.ContentLength == 0)
            {
                TempData["Error"] = "Fayl tanlanmagan";
                await PopulateOrgUnits(orgUnitId);
                return View("Import");
            }

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                    file.InputStream.CopyTo(ms);
                bytes = ms.ToArray();
                }
            Session[$"ExcelFile_{User.Identity.Name}"] = bytes;

                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage(new MemoryStream(bytes)))
            {
                ViewBag.Year = year;
                ViewBag.OrgUnitId = orgUnitId;
                            ViewBag.SheetNames = new SelectList(package.Workbook.Worksheets.Select(w => w.Name));
                await PopulateOrgUnits(orgUnitId);
                return View("ImportSheets");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ImportFromSheet(string sheetName, int year, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            var bytes = Session[$"ExcelFile_{User.Identity.Name}"] as byte[];
            if (bytes == null)
            {
                TempData["Error"] = "Fayl topilmadi, qayta yuklang";
                return RedirectToAction("Import");
            }

                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage(new MemoryStream(bytes)))
            {
                var ws = package.Workbook.Worksheets[sheetName];
                if (ws == null)
                {
                    TempData["Error"] = "Tanlangan varaq topilmadi";
                    return RedirectToAction("Import");
                }

                var month = ParseMonthFromSheetName(sheetName);
                var sheetYear = ParseYearFromSheetName(sheetName);
                if (month == 0) month = DateTime.Now.Month;
                if (sheetYear == 0) sheetYear = year;

                var header = await FindActiveHeader(sheetYear, month, orgUnitId);
                    if (header == null)
                    {
                        header = new HR_TIMESHEET_HEADERS
                        {
                            Year = sheetYear,
                            Month = month,
                            OrgUnitID = orgUnitId,
                        Status = Draft,
                            CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                        CreatedDT = DateTime.Now,
                        IsDeleted = false
                        };
                        db.HR_TIMESHEET_HEADERS.Add(header);
                        await db.SaveChangesAsync();
                }

                var daysInMonth = DateTime.DaysInMonth(sheetYear, month);
                var lastRow = ws.Dimension?.End.Row ?? 0;
                for (int row = 3; row <= lastRow; row++)
                {
                    var tabNo = (ws.Cells[row, 2].Value ?? "").ToString().Trim();
                    if (string.IsNullOrWhiteSpace(tabNo)) continue;

                    var employee = await FindEmployeeByTabNo(tabNo);
                    if (employee == null) continue;
                    if (!await IsEmployeeActiveInPeriod(employee.ID, sheetYear, month)) continue;

                    var line = await db.HR_TIMESHEET_LINES.FirstOrDefaultAsync(l => l.TimesheetID == header.ID && l.EmployeeID == employee.ID);
                    if (line == null)
                    {
                        line = new HR_TIMESHEET_LINES { TimesheetID = header.ID, EmployeeID = employee.ID, IsDeleted = false };
                            db.HR_TIMESHEET_LINES.Add(line);
                    }
                    else if (line.IsDeleted == true)
                    {
                        line.IsDeleted = false;
                    }

                    for (int d = 1; d <= daysInMonth; d++)
                    {
                        var val = (ws.Cells[row, 5 + d]?.Value ?? "").ToString().Trim();
                        decimal parsed;
                        decimal? h = decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed) ? (decimal?)parsed : null;
                        typeof(HR_TIMESHEET_LINES).GetProperty($"Day{d:D2}Hours")?.SetValue(line, h);
                    }
                            await RecalculateLineTotals(line);
                    }

                    await db.SaveChangesAsync();
                TempData["Success"] = "Import yakunlandi";
                    return RedirectToAction("Edit", new { id = header.ID });
                }
            }

        private async Task CreateTimesheetLines(int timesheetId, int? orgUnitId)
        {
            var header = await db.HR_TIMESHEET_HEADERS.FindAsync(timesheetId);
            if (header == null) return;

            var periodStart = new DateTime(header.Year, header.Month, 1);
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);
            var employees = db.HR_EMPLOYEES
                .Where(e => (e.IsDeleted == null || e.IsDeleted == false));

            if (orgUnitId.HasValue)
            {
                var historyIds = await db.HR_EMPLOYEE_HISTORY
                    .Where(h => (h.ToOrgUnitID == orgUnitId.Value || h.FromOrgUnitID == orgUnitId.Value)
                        && h.EffectiveDateFrom <= periodEnd
                        && (h.EffectiveDateTo == null || h.EffectiveDateTo >= periodStart))
                    .Select(h => h.EmployeeID)
                    .Distinct()
                    .ToListAsync();

                employees = employees.Where(e => e.OrgUnitID == orgUnitId.Value || historyIds.Contains(e.ID));
            }

            var employeeList = await employees.ToListAsync();
            foreach (var employee in employeeList)
            {
                if (!await IsEmployeeActiveInPeriod(employee.ID, header.Year, header.Month)) continue;
                var line = await db.HR_TIMESHEET_LINES.FirstOrDefaultAsync(l => l.TimesheetID == timesheetId && l.EmployeeID == employee.ID);
                if (line == null)
                {
                    db.HR_TIMESHEET_LINES.Add(new HR_TIMESHEET_LINES
                    {
                        TimesheetID = timesheetId,
                        EmployeeID = employee.ID,
                        IsDeleted = false
                    });
                }
                else if (line.IsDeleted == true)
                {
                    line.IsDeleted = false;
                }
            }
                        await db.SaveChangesAsync();
        }

        private async Task<AddEmployeesResult> AddEmployeesToTimesheet(int timesheetId, List<int> employeeIds)
        {
            var result = new AddEmployeesResult();
            if (employeeIds == null || !employeeIds.Any()) return result;

            var header = await db.HR_TIMESHEET_HEADERS.FirstOrDefaultAsync(h => h.ID == timesheetId && (h.IsDeleted == null || h.IsDeleted == false));
            if (header == null)
            {
                result.Errors.Add("Tabel topilmadi");
                return result;
            }

            var validIds = employeeIds.Distinct().ToList();
            var employees = await db.HR_EMPLOYEES
                .Where(e => validIds.Contains(e.ID) && (e.IsDeleted == null || e.IsDeleted == false))
                                .ToListAsync();
                            
            foreach (var emp in employees)
            {
                var label = $"{(emp.TabNo ?? "-")} - {emp.FullName}";
                var activeInPeriod = await IsEmployeeActiveInPeriod(emp.ID, header.Year, header.Month);
                if (!activeInPeriod)
                {
                    result.Errors.Add($"{label}: tanlangan oyda ish davri topilmadi");
                            continue;
                        }

                var line = await db.HR_TIMESHEET_LINES.FirstOrDefaultAsync(l => l.TimesheetID == timesheetId && l.EmployeeID == emp.ID);
                        if (line == null)
                        {
                    db.HR_TIMESHEET_LINES.Add(new HR_TIMESHEET_LINES
                    {
                        TimesheetID = timesheetId,
                        EmployeeID = emp.ID,
                        IsDeleted = false
                    });
                    result.AddedCount++;
                }
                else if (line.IsDeleted == true)
                {
                    line.IsDeleted = false;
                    result.AddedCount++;
                                }
                                else
                                {
                    result.Errors.Add($"{label}: allaqachon tabelda mavjud");
                }
            }

            if (result.AddedCount > 0) await db.SaveChangesAsync();
            return result;
        }

        private async Task<bool> IsEmployeeActiveInPeriod(int employeeId, int year, int month)
        {
            var periodStart = new DateTime(year, month, 1);
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);

            var hasHistoryOverlap = await db.HR_EMPLOYEE_HISTORY.AnyAsync(h =>
                h.EmployeeID == employeeId &&
                h.EffectiveDateFrom <= periodEnd &&
                (h.EffectiveDateTo == null || h.EffectiveDateTo >= periodStart));
            if (hasHistoryOverlap) return true;

            var emp = await db.HR_EMPLOYEES.FindAsync(employeeId);
            if (emp == null) return false;
            var from = emp.EmploymentDate ?? DateTime.MinValue;
            var to = emp.DismissalDate ?? DateTime.MaxValue;
            return from <= periodEnd && to >= periodStart;
        }

        private async Task RecalculateLineTotals(HR_TIMESHEET_LINES line)
        {
            var header = await db.HR_TIMESHEET_HEADERS.FindAsync(line.TimesheetID);
            if (header == null) return;
            var days = DateTime.DaysInMonth(header.Year, header.Month);

            decimal totalH = 0, workD = 0, absent = 0, overtime = 0, totalQ = 0;
            for (int d = 1; d <= days; d++)
            {
                var h = typeof(HR_TIMESHEET_LINES).GetProperty($"Day{d:D2}Hours")?.GetValue(line) as decimal? ?? 0;
                var q = typeof(HR_TIMESHEET_LINES).GetProperty($"Day{d:D2}Quantity")?.GetValue(line) as decimal? ?? 0;
                if (h > 0)
                {
                    totalH += h;
                    workD += 1;
                    if (h > 8) overtime += (h - 8);
                }
                else absent += 1;
                if (q > 0) totalQ += q;
            }
            line.TotalWorkedHours = totalH;
            line.TotalWorkedDays = workD;
            line.AbsentDays = absent;
            line.OvertimeHours = overtime;
            line.TotalQuantity = totalQ;
        }

        private HrTimesheetLineViewModel ToLineVm(HR_TIMESHEET_LINES l, int daysInMonth)
        {
            return new HrTimesheetLineViewModel
            {
                ID = l.ID,
                TimesheetID = l.TimesheetID,
                EmployeeID = l.EmployeeID,
                EmployeeTabNo = l.HR_EMPLOYEES?.TabNo,
                EmployeeFullName = l.HR_EMPLOYEES?.FullName,
                PositionName = l.HR_EMPLOYEES?.HR_POSITIONS?.Name,
                Day01Hours = l.Day01Hours, Day02Hours = l.Day02Hours, Day03Hours = l.Day03Hours, Day04Hours = l.Day04Hours, Day05Hours = l.Day05Hours,
                Day06Hours = l.Day06Hours, Day07Hours = l.Day07Hours, Day08Hours = l.Day08Hours, Day09Hours = l.Day09Hours, Day10Hours = l.Day10Hours,
                Day11Hours = l.Day11Hours, Day12Hours = l.Day12Hours, Day13Hours = l.Day13Hours, Day14Hours = l.Day14Hours, Day15Hours = l.Day15Hours,
                Day16Hours = l.Day16Hours, Day17Hours = l.Day17Hours, Day18Hours = l.Day18Hours, Day19Hours = l.Day19Hours, Day20Hours = l.Day20Hours,
                Day21Hours = l.Day21Hours, Day22Hours = l.Day22Hours, Day23Hours = l.Day23Hours, Day24Hours = l.Day24Hours, Day25Hours = l.Day25Hours,
                Day26Hours = l.Day26Hours, Day27Hours = l.Day27Hours, Day28Hours = l.Day28Hours, Day29Hours = l.Day29Hours, Day30Hours = l.Day30Hours, Day31Hours = l.Day31Hours,
                Day01Quantity = l.Day01Quantity, Day02Quantity = l.Day02Quantity, Day03Quantity = l.Day03Quantity, Day04Quantity = l.Day04Quantity, Day05Quantity = l.Day05Quantity,
                Day06Quantity = l.Day06Quantity, Day07Quantity = l.Day07Quantity, Day08Quantity = l.Day08Quantity, Day09Quantity = l.Day09Quantity, Day10Quantity = l.Day10Quantity,
                Day11Quantity = l.Day11Quantity, Day12Quantity = l.Day12Quantity, Day13Quantity = l.Day13Quantity, Day14Quantity = l.Day14Quantity, Day15Quantity = l.Day15Quantity,
                Day16Quantity = l.Day16Quantity, Day17Quantity = l.Day17Quantity, Day18Quantity = l.Day18Quantity, Day19Quantity = l.Day19Quantity, Day20Quantity = l.Day20Quantity,
                Day21Quantity = l.Day21Quantity, Day22Quantity = l.Day22Quantity, Day23Quantity = l.Day23Quantity, Day24Quantity = l.Day24Quantity, Day25Quantity = l.Day25Quantity,
                Day26Quantity = l.Day26Quantity, Day27Quantity = l.Day27Quantity, Day28Quantity = l.Day28Quantity, Day29Quantity = l.Day29Quantity, Day30Quantity = l.Day30Quantity, Day31Quantity = l.Day31Quantity,
                ProductID = l.ProductID,
                ProductName = l.PRODUCT?.PNo,
                TotalWorkedDays = l.TotalWorkedDays ?? 0,
                TotalWorkedHours = l.TotalWorkedHours ?? 0,
                OvertimeHours = l.OvertimeHours ?? 0,
                AbsentDays = l.AbsentDays ?? 0,
                TotalQuantity = l.TotalQuantity ?? 0,
                IsPieceRate = false,
                DaysInMonth = daysInMonth
            };
        }

        private async Task<HR_EMPLOYEES> FindEmployeeByTabNo(string tabNo)
        {
            var norm = tabNo.PadLeft(4, '0');
            var trimmed = tabNo.TrimStart('0');
            if (string.IsNullOrWhiteSpace(trimmed)) trimmed = "0";
            return await db.HR_EMPLOYEES.FirstOrDefaultAsync(e =>
                (e.IsDeleted == null || e.IsDeleted == false) &&
                (e.TabNo == tabNo || e.TabNo == norm || e.TabNo == trimmed));
        }

        private async Task<HR_TIMESHEET_HEADERS> FindActiveHeader(int year, int month, int? orgUnitId)
        {
            return await db.HR_TIMESHEET_HEADERS.FirstOrDefaultAsync(h =>
                h.Year == year &&
                h.Month == month &&
                (orgUnitId == null ? h.OrgUnitID == null : h.OrgUnitID == orgUnitId.Value) &&
                (h.IsDeleted == null || h.IsDeleted == false));
        }

        private static System.Linq.Expressions.Expression<Func<HR_TIMESHEET_HEADERS, bool>> IsNotDeletedHeaderExpr()
        {
            return h => h.IsDeleted == null || h.IsDeleted == false;
        }

        private async Task<List<int>> GetHourlyPositionIds()
        {
            return await db.HR_STAFFING_PLAN
                .Where(s => s.IsActive && s.IsPieceRate == false)
                .Select(s => s.PositionID)
                .Distinct()
                .ToListAsync();
        }

        private async Task PopulateOrgUnits(int? orgUnitId)
        {
            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnitId);
        }

        private async Task PopulateCreateEmployees()
        {
            var employees = await db.HR_EMPLOYEES
                .Where(e => (e.IsDeleted == null || e.IsDeleted == false))
                .OrderBy(e => e.TabNo)
                .Select(e => new EmployeePickItem
                {
                    ID = e.ID,
                    DisplayName = (e.TabNo ?? "-") + " - " + e.FullName
                })
                .ToListAsync();

            ViewBag.EmployeeOptions = new MultiSelectList(employees, "ID", "DisplayName");
        }

        private async Task<List<EmployeePickItem>> GetEmployeesNotInTimesheet(int timesheetId)
        {
            var usedEmployeeIds = await db.HR_TIMESHEET_LINES
                .Where(l => l.TimesheetID == timesheetId && (l.IsDeleted == null || l.IsDeleted == false))
                .Select(l => l.EmployeeID)
                .ToListAsync();

            var employees = await db.HR_EMPLOYEES
                .Where(e => (e.IsDeleted == null || e.IsDeleted == false)
                            && !usedEmployeeIds.Contains(e.ID))
                .OrderBy(e => e.TabNo)
                .Select(e => new EmployeePickItem
                {
                    ID = e.ID,
                    DisplayName = (e.TabNo ?? "-") + " - " + e.FullName
                })
                .ToListAsync();

            return employees;
        }

        private int ParseMonthFromSheetName(string name)
        {
            var months = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["yanvar"] = 1, ["fevral"] = 2, ["mart"] = 3, ["aprel"] = 4, ["may"] = 5, ["iyun"] = 6, ["iyul"] = 7, ["avgust"] = 8, ["sentyabr"] = 9, ["oktyabr"] = 10, ["noyabr"] = 11, ["dekabr"] = 12
            };
            var m = name.Split('-').FirstOrDefault()?.Trim();
            return m != null && months.ContainsKey(m) ? months[m] : 0;
        }

        private int ParseYearFromSheetName(string name)
        {
            var parts = name.Split('-');
            if (parts.Length < 2) return 0;
            int y;
            return int.TryParse(parts[1].Trim(), out y) ? y : 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
