using Newtonsoft.Json;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.HR;
using tahsinERP.ViewModels.HR;
using tahsinERP.Filters;

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
        }

        private class TimesheetHourCell
        {
            public int LineId { get; set; }
            public int Day { get; set; }
            public decimal? Hours { get; set; }
        }

        private const string Draft = "Draft";
        private const string Approved = "Approved";
        private const string Closed = "Closed";
        private const decimal MaxDayHours = 24m;

        /// <summary>Yuklangan Excel fayli Import va ImportFromSheet so'rovlari orasida (avval Session'da tozalanmay qolardi, HRM-M03).</summary>
        private static readonly MemoryCache UploadCache = new MemoryCache("tahsinERP.HrTimesheetUploads");
        private static readonly TimeSpan UploadTtl = TimeSpan.FromMinutes(20);

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
            IQueryable<HR_TIMESHEET_HEADERS> query = db.HR_TIMESHEET_HEADERS.AsNoTracking()
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
            PopulateOrgUnits(orgUnitId);

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
            PopulateOrgUnits(orgUnitId);
            await PopulateCreateEmployees();
            return View(new HrTimesheetHeaderViewModel
            {
                Year = year.Value,
                Month = month.Value,
                OrgUnitID = orgUnitId,
                // Standart fond soati — HR ish kalendaridan: ish kunlari × kunlik me'yor (HRM-B15)
                FundHours = HrSettingsService.Current.FundHoursForMonth(year.Value, month.Value)
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

            if (ModelState.IsValid && new DateTime(model.Year, model.Month, 1) > new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1))
            {
                ModelState.AddModelError("", "Kelajak oyiga tabel yaratib bo'lmaydi");
            }

            if (ModelState.IsValid && await FindActiveHeader(model.Year, model.Month, model.OrgUnitID) != null)
            {
                ModelState.AddModelError("", "Ushbu davr uchun tabel allaqachon mavjud");
            }

            if (!ModelState.IsValid)
            {
                PopulateOrgUnits(model.OrgUnitID);
                await PopulateCreateEmployees();
                return View(model);
            }

            HR_TIMESHEET_HEADERS header;
            AddEmployeesResult addResult;
            // Sarlavha, sozlama va qatorlar — bitta tranzaksiyada (HRM-M02)
            using (var tx = db.Database.BeginTransaction())
            {
                header = new HR_TIMESHEET_HEADERS
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

                await CreateTimesheetLines(header);
                addResult = await AddEmployeesToTimesheet(header, selectedEmployeeIds);
                tx.Commit();
            }

            if (addResult.Errors.Any())
            {
                TempData["Error"] = string.Join("; ", addResult.Errors);
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

            var vm = await BuildTimesheetVmAsync(id.Value);
            if (vm == null) return HttpNotFound();

            ViewBag.AvailableEmployees = new MultiSelectList(
                await GetEmployeesNotInTimesheet(vm.ID),
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

            var header = await FindHeaderById(timesheetId);
            if (header == null) return HttpNotFound();
            if (header.Status != Draft)
            {
                TempData["Error"] = "Faqat qoralama tabelga xodim qo'shish mumkin";
                return RedirectToAction("Edit", new { id = timesheetId });
            }

            var result = await AddEmployeesToTimesheet(header, employeeIds);
            if (result.AddedCount > 0)
            {
                TempData["Success"] = $"{result.AddedCount} ta xodim qo'shildi";
            }
            if (result.Errors.Any())
            {
                TempData["Error"] = string.Join("; ", result.Errors);
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

            var header = await FindHeaderById(timesheetId);
            if (header == null) return HttpNotFound();
            if (header.Status != Draft)
            {
                TempData["Error"] = "Faqat qoralama holatidagi tabelni saqlash mumkin";
                return RedirectToAction("Edit", new { id = timesheetId });
            }

            var daysInMonth = DateTime.DaysInMonth(header.Year, header.Month);
            var linesDict = await db.HR_TIMESHEET_LINES
                .Where(l => l.TimesheetID == timesheetId && (l.IsDeleted == null || l.IsDeleted == false))
                .ToDictionaryAsync(l => l.ID);

            var touchedLineIds = new HashSet<int>();
            if (payload?.Hours != null)
            {
                foreach (var cell in payload.Hours)
                {
                    if (cell.Day < 1 || cell.Day > daysInMonth) continue;
                    if (!linesDict.TryGetValue(cell.LineId, out var line)) continue;
                    TimesheetLineAccessor.SetHours(line, cell.Day, ClampHours(cell.Hours));
                    touchedLineIds.Add(line.ID);
                }
            }

            foreach (var lineId in touchedLineIds)
            {
                TimesheetLineAccessor.RecalculateTotals(linesDict[lineId], header.Year, header.Month);
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
        [ValidateAntiForgeryHeader]
        public async Task<JsonResult> UpdateLine(int lineId, int day, decimal? hours)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
                return Json(new { success = false, message = "Ruxsat yo'q" });

            var line = await db.HR_TIMESHEET_LINES
                .Include(l => l.HR_TIMESHEET_HEADERS)
                .FirstOrDefaultAsync(l => l.ID == lineId && (l.IsDeleted == null || l.IsDeleted == false));
            if (line == null) return Json(new { success = false, message = "Qator topilmadi" });

            // Tasdiqlangan/yopilgan (ish haqi hisoblangan bo'lishi mumkin) tabel o'zgarmaydi (HRM-B05)
            var header = line.HR_TIMESHEET_HEADERS;
            if (header == null || header.IsDeleted == true || header.Status != Draft)
                return Json(new { success = false, message = "Faqat qoralama holatidagi tabelni tahrirlash mumkin" });

            var daysInMonth = DateTime.DaysInMonth(header.Year, header.Month);
            if (day < 1 || day > daysInMonth) return Json(new { success = false, message = "Kun noto'g'ri" });

            TimesheetLineAccessor.SetHours(line, day, ClampHours(hours));
            TimesheetLineAccessor.RecalculateTotals(line, header.Year, header.Month);
            await db.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Approve(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets", "Approve"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            var header = await FindHeaderById(id);
            if (header == null) return HttpNotFound();
            if (header.Status != Draft)
            {
                TempData["Error"] = "Faqat qoralama tabel tasdiqlanadi";
                return RedirectToAction("Index");
            }
            header.Status = Approved;
            header.ApprovedByUserID = UserHelper.GetUserId(User.Identity.Name);
            header.ApprovedDT = DateTime.Now;
            await db.SaveChangesAsync();
            logger.Log("HrTimesheets", "Approve", $"TabelID={id}");
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Close(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            var header = await FindHeaderById(id);
            if (header == null) return HttpNotFound();
            if (header.Status != Approved)
            {
                TempData["Error"] = "Faqat tasdiqlangan tabel yopiladi";
                return RedirectToAction("Index");
            }
            header.Status = Closed;
            await db.SaveChangesAsync();
            logger.Log("HrTimesheets", "Close", $"TabelID={id}");
            return RedirectToAction("Index");
        }

        public async Task<ActionResult> Delete(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
                return RedirectToAction("Index", "ViewNotAuthorized");
            if (!id.HasValue) return HttpNotFound();

            var header = await db.HR_TIMESHEET_HEADERS.AsNoTracking()
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

            var header = await FindHeaderById(id);
            if (header == null) return HttpNotFound();
            if (header.Status != Draft)
            {
                TempData["Error"] = "Faqat qoralama tabel o'chiriladi";
                return RedirectToAction("Index");
            }

            // O'chirilgan ish haqi hisobi bloklamasligi kerak (HRM-B08)
            var hasPayroll = await db.HR_PAYROLL_HEADERS.AnyAsync(p => p.TimesheetID == id && p.IsDeleted != true);
            if (hasPayroll)
            {
                TempData["Error"] = "Bu tabelga bog'langan ish haqi bor";
                return RedirectToAction("Index");
            }

            var lines = await db.HR_TIMESHEET_LINES.Where(l => l.TimesheetID == id && (l.IsDeleted == null || l.IsDeleted == false)).ToListAsync();
            foreach (var line in lines) line.IsDeleted = true;
            header.IsDeleted = true;
            await db.SaveChangesAsync();
            logger.Log("HrTimesheets", "Delete", $"TabelID={id}");
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

            var line = await db.HR_TIMESHEET_LINES
                .Include(l => l.HR_TIMESHEET_HEADERS)
                .FirstOrDefaultAsync(l => l.ID == lineId && (l.IsDeleted == null || l.IsDeleted == false));
            if (line == null)
            {
                TempData["Error"] = "Qator topilmadi";
                return RedirectToAction("Index");
            }

            var header = line.HR_TIMESHEET_HEADERS;
            if (header == null || header.IsDeleted == true) return RedirectToAction("Index");
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

            var vm = await BuildTimesheetVmAsync(id);
            if (vm == null) return HttpNotFound();
            return View("~/Views/HrReports/TimesheetReport.cshtml", vm);
        }

        public async Task<ActionResult> Preview(int id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrTimesheets"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            var vm = await BuildTimesheetVmAsync(id);
            if (vm == null) return HttpNotFound();
            return View("~/Views/HrTimesheets/Details.cshtml", vm);
        }

        public async Task<ActionResult> Export(int id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrTimesheets"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            var vm = await BuildTimesheetVmAsync(id);
            if (vm == null) return HttpNotFound();

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add($"Tabel {vm.Year}-{vm.Month:D2}");
                ws.Cell(1, 1).Value = "№";
                ws.Cell(1, 2).Value = "Tab.№";
                ws.Cell(1, 3).Value = "F.I.Sh.";
                ws.Cell(1, 4).Value = "Lavozim";

                var daysInMonth = DateTime.DaysInMonth(vm.Year, vm.Month);
                int col = 5;
                for (int d = 1; d <= daysInMonth; d++) ws.Cell(1, col++).Value = d;
                ws.Cell(1, col++).Value = "Ishlangan kun";
                ws.Cell(1, col++).Value = "Ishlangan soat";
                ws.Cell(1, col++).Value = "Qo'shimcha soat";
                ws.Cell(1, col++).Value = "Kelmagan kun";

                int row = 2; int n = 1;
                foreach (var line in vm.Lines)
                {
                    ws.Cell(row, 1).Value = n++;
                    ws.Cell(row, 2).Value = line.EmployeeTabNo;
                    ws.Cell(row, 3).Value = line.EmployeeFullName;
                    ws.Cell(row, 4).Value = line.PositionName;
                    col = 5;
                    for (int d = 1; d <= daysInMonth; d++)
                    {
                        ws.Cell(row, col++).Value = TimesheetLineAccessor.GetHours(line, d) ?? 0;
                    }
                    ws.Cell(row, col++).Value = line.TotalWorkedDays;
                    ws.Cell(row, col++).Value = line.TotalWorkedHours;
                    ws.Cell(row, col++).Value = line.OvertimeHours;
                    ws.Cell(row, col++).Value = line.AbsentDays;
                    row++;
                }

                ws.Cell(row + 1, 1).Value = "Oylik fond soati";
                ws.Cell(row + 1, 2).Value = vm.FundHours;
                ws.Cell(row + 2, 1).Value = "Jami qo'shimcha soat";
                ws.Cell(row + 2, 2).Value = vm.Lines.Sum(l => l.OvertimeHours);

                ws.Columns().AdjustToContents();
                return File(workbook.ToByteArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"Tabel_{vm.Year}_{vm.Month:D2}.xlsx");
            }
        }

        public ActionResult Import()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
                return RedirectToAction("Index", "ViewNotAuthorized");
            PopulateOrgUnits(null);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UploadExcel(HttpPostedFileBase file, int year, int? month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
                return RedirectToAction("Index", "ViewNotAuthorized");
            if (file == null || file.ContentLength == 0)
            {
                TempData["Error"] = "Fayl tanlanmagan";
                PopulateOrgUnits(orgUnitId);
                return View("Import");
            }

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                file.InputStream.CopyTo(ms);
                bytes = ms.ToArray();
            }

            var fileToken = Guid.NewGuid().ToString("N");
            UploadCache.Set(UploadKey(fileToken), bytes, DateTimeOffset.Now.Add(UploadTtl));

            using (var workbook = new XLWorkbook(new MemoryStream(bytes)))
            {
                ViewBag.Year = year;
                ViewBag.OrgUnitId = orgUnitId;
                ViewBag.FileToken = fileToken;
                ViewBag.SheetNames = new SelectList(workbook.Worksheets.Select(w => w.Name));
                PopulateOrgUnits(orgUnitId);
                return View("ImportSheets");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ImportFromSheet(string sheetName, int year, int? orgUnitId, string fileToken)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrTimesheets"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            var bytes = string.IsNullOrEmpty(fileToken) ? null : UploadCache.Get(UploadKey(fileToken)) as byte[];
            if (bytes == null)
            {
                TempData["Error"] = "Fayl topilmadi, qayta yuklang";
                return RedirectToAction("Import");
            }

            using (var workbook = new XLWorkbook(new MemoryStream(bytes)))
            {
                IXLWorksheet ws = null;
                if (!string.IsNullOrEmpty(sheetName))
                    workbook.Worksheets.TryGetWorksheet(sheetName, out ws);
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
                // Tasdiqlangan/yopilgan tabelga import qilib bo'lmaydi — ish haqi bilan farq paydo bo'lardi (HRM-B05)
                if (header != null && header.Status != Draft)
                {
                    TempData["Error"] = $"{sheetYear}-{month:D2} uchun tabel qoralama holatida emas — import qilib bo'lmaydi";
                    return RedirectToAction("Import");
                }

                var daysInMonth = DateTime.DaysInMonth(sheetYear, month);
                var lastRow = ws.LastRowNumber();

                using (var tx = db.Database.BeginTransaction())
                {
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

                    // Oldindan yuklash: TabNo → xodim, faol xodimlar to'plami, mavjud qatorlar (avval har Excel qatoriga ~4 so'rov, HRM-P04)
                    var employeeIdByTabNo = await LoadEmployeeIdsByTabNoAsync();
                    var activeIds = await GetActiveEmployeeIdsAsync(sheetYear, month, null);
                    var existingLines = await LoadLinesByEmployeeAsync(header.ID);

                    for (int row = 3; row <= lastRow; row++)
                    {
                        var tabNo = (ws.Cell(row, 2).GetRawValue() ?? "").ToString().Trim();
                        if (string.IsNullOrWhiteSpace(tabNo)) continue;

                        var employeeId = FindEmployeeIdByTabNo(employeeIdByTabNo, tabNo);
                        if (!employeeId.HasValue || !activeIds.Contains(employeeId.Value)) continue;

                        if (!existingLines.TryGetValue(employeeId.Value, out var line))
                        {
                            line = new HR_TIMESHEET_LINES { TimesheetID = header.ID, EmployeeID = employeeId.Value, IsDeleted = false };
                            db.HR_TIMESHEET_LINES.Add(line);
                            existingLines[employeeId.Value] = line;
                        }
                        else if (line.IsDeleted == true)
                        {
                            line.IsDeleted = false;
                        }

                        for (int d = 1; d <= daysInMonth; d++)
                        {
                            var val = (ws.Cell(row, 5 + d).GetRawValue() ?? "").ToString().Trim();
                            decimal parsed;
                            decimal? h = decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed) ? (decimal?)parsed : null;
                            TimesheetLineAccessor.SetHours(line, d, ClampHours(h));
                        }
                        TimesheetLineAccessor.RecalculateTotals(line, sheetYear, month);
                    }

                    await db.SaveChangesAsync();
                    tx.Commit();
                }

                UploadCache.Remove(UploadKey(fileToken));
                TempData["Success"] = "Import yakunlandi";
                logger.Log("HrTimesheets", "ImportFromSheet", $"TabelID={header.ID}, Varaq={sheetName}");
                return RedirectToAction("Edit", new { id = header.ID });
            }
        }

        private string UploadKey(string token) => (User?.Identity?.Name ?? "") + "|" + token;

        private static decimal? ClampHours(decimal? hours)
        {
            if (!hours.HasValue) return null;
            if (hours.Value < 0) return 0m;
            return hours.Value > MaxDayHours ? MaxDayHours : hours.Value;
        }

        private async Task<HrTimesheetHeaderViewModel> BuildTimesheetVmAsync(int id)
        {
            var header = await db.HR_TIMESHEET_HEADERS.AsNoTracking()
                .Include(h => h.HR_ORG_UNITS)
                .FirstOrDefaultAsync(h => h.ID == id && (h.IsDeleted == null || h.IsDeleted == false));
            if (header == null) return null;

            var lines = await db.HR_TIMESHEET_LINES.AsNoTracking()
                .Include(l => l.HR_EMPLOYEES.HR_POSITIONS)
                .Where(l => l.TimesheetID == id && (l.IsDeleted == null || l.IsDeleted == false) && l.HR_EMPLOYEES != null)
                .ToListAsync();

            var daysInMonth = DateTime.DaysInMonth(header.Year, header.Month);
            return new HrTimesheetHeaderViewModel
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
                Lines = lines
                    .OrderBy(l => l.HR_EMPLOYEES.TabNo)
                    .Select(l => TimesheetLineAccessor.ToLineVm(l, daysInMonth))
                    .ToList()
            };
        }

        private async Task CreateTimesheetLines(HR_TIMESHEET_HEADERS header)
        {
            var periodStart = new DateTime(header.Year, header.Month, 1);
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);

            IQueryable<int> candidates = db.HR_EMPLOYEES
                .Where(e => e.IsDeleted == null || e.IsDeleted == false)
                .Select(e => e.ID);

            if (header.OrgUnitID.HasValue)
            {
                var orgUnitId = header.OrgUnitID.Value;
                var historyIds = db.HR_EMPLOYEE_HISTORY
                    .Where(h => (h.ToOrgUnitID == orgUnitId || h.FromOrgUnitID == orgUnitId)
                        && h.EffectiveDateFrom <= periodEnd
                        && (h.EffectiveDateTo == null || h.EffectiveDateTo >= periodStart))
                    .Select(h => h.EmployeeID);

                candidates = db.HR_EMPLOYEES
                    .Where(e => (e.IsDeleted == null || e.IsDeleted == false) &&
                                (e.OrgUnitID == orgUnitId || historyIds.Contains(e.ID)))
                    .Select(e => e.ID);
            }

            var activeIds = await GetActiveEmployeeIdsAsync(header.Year, header.Month, candidates);
            var existingLines = await LoadLinesByEmployeeAsync(header.ID);

            foreach (var employeeId in activeIds)
            {
                if (existingLines.TryGetValue(employeeId, out var line))
                {
                    if (line.IsDeleted == true) line.IsDeleted = false;
                    continue;
                }
                db.HR_TIMESHEET_LINES.Add(new HR_TIMESHEET_LINES
                {
                    TimesheetID = header.ID,
                    EmployeeID = employeeId,
                    IsDeleted = false
                });
            }
            await db.SaveChangesAsync();
        }

        private async Task<AddEmployeesResult> AddEmployeesToTimesheet(HR_TIMESHEET_HEADERS header, List<int> employeeIds)
        {
            var result = new AddEmployeesResult();
            if (employeeIds == null || !employeeIds.Any()) return result;

            var validIds = employeeIds.Distinct().ToList();
            var employees = await db.HR_EMPLOYEES.AsNoTracking()
                .Where(e => validIds.Contains(e.ID) && (e.IsDeleted == null || e.IsDeleted == false))
                .Select(e => new { e.ID, e.TabNo, e.FullName })
                .ToListAsync();

            var activeIds = await GetActiveEmployeeIdsAsync(header.Year, header.Month, null, validIds);
            var existingLines = await LoadLinesByEmployeeAsync(header.ID);

            foreach (var emp in employees)
            {
                var label = $"{(emp.TabNo ?? "-")} - {emp.FullName}";
                if (!activeIds.Contains(emp.ID))
                {
                    result.Errors.Add($"{label}: tanlangan oyda ish davri topilmadi");
                    continue;
                }

                if (!existingLines.TryGetValue(emp.ID, out var line))
                {
                    db.HR_TIMESHEET_LINES.Add(new HR_TIMESHEET_LINES
                    {
                        TimesheetID = header.ID,
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

        /// <summary>
        /// Tanlangan oyda ishlagan xodimlar (bitta so'rov; avval har xodimga 1-2 so'rov, HRM-P04).
        /// Faol: davr bilan kesishgan tarix yozuvi bor yoki ishga qabul/bo'shash oralig'i davr bilan kesishadi.
        /// Davr boshlanishidan oldin bo'shatilgan xodim tarixidagi ochiq qatordan qat'i nazar faol emas (HRM-B06).
        /// </summary>
        private async Task<HashSet<int>> GetActiveEmployeeIdsAsync(int year, int month, IQueryable<int> candidateIds, List<int> candidateList = null)
        {
            var periodStart = new DateTime(year, month, 1);
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);

            var historyIds = db.HR_EMPLOYEE_HISTORY
                .Where(h => h.EffectiveDateFrom <= periodEnd &&
                            (h.EffectiveDateTo == null || h.EffectiveDateTo >= periodStart))
                .Select(h => h.EmployeeID);

            var query = db.HR_EMPLOYEES
                .Where(e => e.DismissalDate == null || e.DismissalDate >= periodStart)
                .Where(e => historyIds.Contains(e.ID) ||
                            e.EmploymentDate == null ||
                            e.EmploymentDate <= periodEnd);

            if (candidateIds != null)
            {
                query = query.Where(e => candidateIds.Contains(e.ID));
            }
            if (candidateList != null)
            {
                query = query.Where(e => candidateList.Contains(e.ID));
            }

            return new HashSet<int>(await query.Select(e => e.ID).ToListAsync());
        }

        private async Task<Dictionary<int, HR_TIMESHEET_LINES>> LoadLinesByEmployeeAsync(int timesheetId)
        {
            var lines = await db.HR_TIMESHEET_LINES.Where(l => l.TimesheetID == timesheetId).ToListAsync();
            // Bir xodimga bir nechta qator bo'lsa — faol qator ustun
            return lines
                .GroupBy(l => l.EmployeeID)
                .ToDictionary(g => g.Key, g => g.OrderBy(l => l.IsDeleted == true ? 1 : 0).ThenBy(l => l.ID).First());
        }

        private async Task<Dictionary<string, int>> LoadEmployeeIdsByTabNoAsync()
        {
            var rows = await db.HR_EMPLOYEES.AsNoTracking()
                .Where(e => (e.IsDeleted == null || e.IsDeleted == false) && e.TabNo != null)
                .OrderBy(e => e.ID)
                .Select(e => new { e.ID, e.TabNo })
                .ToListAsync();

            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var r in rows)
            {
                if (!map.ContainsKey(r.TabNo)) map[r.TabNo] = r.ID;
            }
            return map;
        }

        /// <summary>TabNo aynan, 4 xonagacha 0 bilan to'ldirilgan yoki boshidagi 0'larsiz ko'rinishda qidiriladi.</summary>
        private static int? FindEmployeeIdByTabNo(Dictionary<string, int> map, string tabNo)
        {
            var trimmed = tabNo.TrimStart('0');
            if (string.IsNullOrWhiteSpace(trimmed)) trimmed = "0";
            foreach (var key in new[] { tabNo, tabNo.PadLeft(4, '0'), trimmed })
            {
                if (map.TryGetValue(key, out var id)) return id;
            }
            return null;
        }

        private Task<HR_TIMESHEET_HEADERS> FindHeaderById(int id)
        {
            return db.HR_TIMESHEET_HEADERS.FirstOrDefaultAsync(h => h.ID == id && (h.IsDeleted == null || h.IsDeleted == false));
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

        private void PopulateOrgUnits(int? orgUnitId)
        {
            ViewBag.OrgUnitID = HrLookups.SelectList(HrLookups.OrgUnits, orgUnitId);
        }

        private async Task PopulateCreateEmployees()
        {
            var employees = await db.HR_EMPLOYEES.AsNoTracking()
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
            return await db.HR_EMPLOYEES.AsNoTracking()
                .Where(e => (e.IsDeleted == null || e.IsDeleted == false)
                            && !db.HR_TIMESHEET_LINES.Any(l => l.TimesheetID == timesheetId &&
                                                               l.EmployeeID == e.ID &&
                                                               (l.IsDeleted == null || l.IsDeleted == false)))
                .OrderBy(e => e.TabNo)
                .Select(e => new EmployeePickItem
                {
                    ID = e.ID,
                    DisplayName = (e.TabNo ?? "-") + " - " + e.FullName
                })
                .ToListAsync();
        }

        private static readonly Dictionary<string, int> SheetMonths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["yanvar"] = 1, ["fevral"] = 2, ["mart"] = 3, ["aprel"] = 4, ["may"] = 5, ["iyun"] = 6, ["iyul"] = 7, ["avgust"] = 8, ["sentyabr"] = 9, ["oktyabr"] = 10, ["noyabr"] = 11, ["dekabr"] = 12
        };

        private static int ParseMonthFromSheetName(string name)
        {
            var m = name.Split('-').FirstOrDefault()?.Trim();
            return m != null && SheetMonths.ContainsKey(m) ? SheetMonths[m] : 0;
        }

        private static int ParseYearFromSheetName(string name)
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
