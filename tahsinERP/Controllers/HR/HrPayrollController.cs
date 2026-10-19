using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Common;
using tahsinERP.Services.HR;
using tahsinERP.ViewModels.HR;
using ClosedXML.Excel;
using tahsinERP.Filters;

namespace tahsinERP.Controllers.HR
{
    [Authorize]
    public class HrPayrollController : Controller
    {
        private const string Draft = "Draft";
        private const string Approved = "Approved";
        private const string Posted = "Posted";
        private const string Hourly = "Hourly";
        private const string PieceRate = "PieceRate";

        /// <summary>FIN_TRANSACTIONS.Direction: 1 = kirim, 2 = chiqim. Ish haqi — chiqim.</summary>
        private const byte FinDirectionOutflow = 2;

        private DBTHSNEntities db = new DBTHSNEntities();
        private IUserActionLogger logger;
        private readonly HrEmployeeAllowanceService allowanceService;
        private readonly HrTimesheetSettingsService timesheetSettingsService;

        public HrPayrollController()
        {
            logger = new UserActionLogger(User?.Identity?.Name ?? "System");
            allowanceService = new HrEmployeeAllowanceService(db);
            timesheetSettingsService = new HrTimesheetSettingsService(db);
        }

        /// <summary>O'chirilgan (IsDeleted == true) sarlavha yoki qator hisobga olinmaydi.</summary>
        private static bool IsPayrollHeaderActive(HR_PAYROLL_HEADERS p) =>
            p != null && (p.IsDeleted == null || p.IsDeleted == false);

        private static bool IsPayrollLineActive(HR_PAYROLL_LINES l) =>
            l != null && (l.IsDeleted == null || l.IsDeleted == false);

        private static string TypeLabel(string payrollType) => payrollType == PieceRate ? "Ishbay" : "Soatbay";

        /// <summary>Generatsiya natijasi (foydalanuvchiga xabar uchun).</summary>
        private sealed class GenerationResult
        {
            public int Created { get; set; }
            public List<string> SkippedAlreadyPaid { get; } = new List<string>();
            public int EntriesWithoutTariff { get; set; }
            public int UnapprovedEntries { get; set; }

            public string Warning()
            {
                var parts = new List<string>();
                if (SkippedAlreadyPaid.Count > 0)
                {
                    var names = string.Join(", ", SkippedAlreadyPaid.Take(10));
                    if (SkippedAlreadyPaid.Count > 10) names += $" va yana {SkippedAlreadyPaid.Count - 10} ta";
                    parts.Add($"{SkippedAlreadyPaid.Count} ta xodim shu oy uchun boshqa (bo'lim yoki butun kompaniya) hisobida allaqachon bor va qo'shilmadi: {names}");
                }
                if (UnapprovedEntries > 0)
                    parts.Add($"{UnapprovedEntries} ta ishbay yozuv tasdiqlanmagan bo'lim tabeliga tegishli va hisobga olinmadi");
                if (EntriesWithoutTariff > 0)
                    parts.Add($"{EntriesWithoutTariff} ta ishbay yozuv uchun amaldagi tarif topilmadi (0 deb olindi)");
                return parts.Count == 0 ? null : string.Join(". ", parts) + ".";
            }
        }

        // Olish so'rovi: HR/Payroll
        public async Task<ActionResult> Index(int? year, int? month, int? orgUnitId, string status, string payrollType)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            year = year ?? DateTime.Now.Year;

            IQueryable<HR_PAYROLL_HEADERS> query = db.HR_PAYROLL_HEADERS.AsNoTracking()
                .Where(p => (p.IsDeleted == false || p.IsDeleted == null) && p.Year == year.Value);

            if (month.HasValue && month.Value > 0)
            {
                query = query.Where(p => p.Month == month.Value);
            }

            if (orgUnitId.HasValue)
            {
                query = query.Where(p => p.OrgUnitID == orgUnitId.Value);
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(p => p.Status == status);
            }

            if (!string.IsNullOrEmpty(payrollType))
            {
                query = query.Where(p => p.PayrollType == payrollType);
            }

            // Jamilar SQL'da: avval yilning barcha qatorlari Include bilan xotiraga yuklanardi (HRM-P07)
            var rows = await query
                .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
                .Select(p => new
                {
                    p.ID,
                    p.Year,
                    p.Month,
                    p.OrgUnitID,
                    OrgUnitName = p.HR_ORG_UNITS.Name,
                    p.Status,
                    p.TimesheetID,
                    p.CreatedDT,
                    p.ApprovedDT,
                    p.PayrollType,
                    Gross = p.HR_PAYROLL_LINES.Where(l => l.IsDeleted != true).Sum(l => l.GrossSalary),
                    Net = p.HR_PAYROLL_LINES.Where(l => l.IsDeleted != true).Sum(l => l.NetSalary)
                })
                .ToListAsync();

            var viewModels = rows.Select(p => new HrPayrollHeaderViewModel
            {
                ID = p.ID,
                Year = p.Year,
                Month = p.Month,
                OrgUnitID = p.OrgUnitID,
                OrgUnitName = p.OrgUnitName,
                Status = p.Status,
                TimesheetID = p.TimesheetID,
                CreatedDT = p.CreatedDT,
                ApprovedDT = p.ApprovedDT,
                PayrollType = p.PayrollType ?? Hourly,
                TotalGrossSalary = p.Gross ?? 0,
                TotalNetSalary = p.Net ?? 0
            }).ToList();

            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.PayrollType = payrollType;
            ViewBag.OrgUnitID = HrLookups.SelectList(HrLookups.OrgUnits, orgUnitId);

            logger.Log("HrPayroll", "Index");
            return View(viewModels);
        }

        // Olish so'rovi: HR/Payroll/GenerateHourly
        public async Task<ActionResult> GenerateHourly(int? year, int? month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            year = year ?? DateTime.Now.Year;
            month = month ?? DateTime.Now.Month;

            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.PayrollType = Hourly;
            ViewBag.OrgUnitID = HrLookups.SelectList(HrLookups.OrgUnits, orgUnitId);
            ViewBag.TimesheetID = new SelectList(await BuildHourlyTimesheetDropdownItemsAsync(year.Value, month.Value, orgUnitId), "ID", "Name");

            return View("Generate");
        }

        // POST so'rovi: HR/Payroll/GenerateHourly
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> GenerateHourly(int year, int month, int? orgUnitId, int timesheetId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Avval tabel tekshiriladi — avval sarlavha oldin saqlanib, xato bo'lsa bo'sh qoralama qolardi (HRM-B09)
            var timesheet = await db.HR_TIMESHEET_HEADERS.AsNoTracking()
                .Include(t => t.HR_ORG_UNITS)
                .FirstOrDefaultAsync(t => t.ID == timesheetId && (t.IsDeleted == null || t.IsDeleted == false));

            if (timesheet == null || timesheet.Status != Approved)
            {
                TempData["Error"] = "Tabel topilmadi yoki tasdiqlanmagan";
                return RedirectToAction("GenerateHourly", new { year, month, orgUnitId });
            }

            if (timesheet.Year != year || timesheet.Month != month ||
                (orgUnitId.HasValue && timesheet.OrgUnitID != orgUnitId.Value))
            {
                TempData["Error"] = "Tabel ma'lumotlari tanlangan davr yoki bo'limga mos kelmaydi";
                return RedirectToAction("GenerateHourly", new { year, month, orgUnitId });
            }

            // Hisob tabel bo'limi bo'yicha (ishbay bilan bir xil qoida)
            int? payrollOrgUnitId = timesheet.OrgUnitID;

            var existing = await FindPayrollHeadersAsync(year, month, payrollOrgUnitId, Hourly);
            var activeExisting = existing.FirstOrDefault(IsPayrollHeaderActive);
            if (activeExisting != null && activeExisting.Status != Draft)
            {
                TempData["Error"] = "Mavjud soatbay hisob tasdiqlangan yoki o'tkazilgan. Qayta hisoblash uchun uni qoralama holatda yarating.";
                return RedirectToAction("Edit", new { id = activeExisting.ID });
            }

            HR_PAYROLL_HEADERS payroll;
            GenerationResult result;
            using (var tx = db.Database.BeginTransaction())
            {
                // HRM-B09: qoralama ishbay oqimidagidek qayta quriladi, qo'lda kiritilgan mukofot/jarima/soliq saqlanadi
                Dictionary<int, ManualAdjustment> adjustments = null;
                if (activeExisting != null)
                {
                    payroll = activeExisting;
                    payroll.TimesheetID = timesheet.ID;
                    adjustments = await SoftDeleteLinesKeepingAdjustmentsAsync(payroll.ID);
                }
                else
                {
                    payroll = await ReviveOrCreateDraftHeaderAsync(existing, year, month, payrollOrgUnitId, Hourly, timesheet.ID);
                }

                result = await GenerateHourlyPayrollLines(payroll, timesheet);
                ApplyAdjustments(payroll.ID, adjustments);
                await db.SaveChangesAsync();
                tx.Commit();
            }

            var orgUnitName = timesheet.HR_ORG_UNITS?.Name ?? "Butun kompaniya";
            logger.Log("HrPayroll", "GenerateHourly", $"Tabel: {timesheet.Year}-{timesheet.Month:D2}, Bo'lim: {orgUnitName}, Tur: Soatbay, Satrlar: {result.Created}");
            SetGenerationMessages(result, null);
            return RedirectToAction("Edit", new { id = payroll.ID });
        }

        // Olish so'rovi: HR/Payroll/GetTimesheets (AJAX endpoint)
        [HttpGet]
        public async Task<JsonResult> GetTimesheets(int year, int month, int? orgUnitId, string payrollType)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPayroll"))
            {
                return Json(new { success = false, message = "Ruxsat yo'q" }, JsonRequestBehavior.AllowGet);
            }

            try
            {
                var items = payrollType == PieceRate
                    ? await BuildPieceRateTimesheetDropdownItemsAsync(year, month, orgUnitId)
                    : await BuildHourlyTimesheetDropdownItemsAsync(year, month, orgUnitId);

                return Json(new { success = true, data = items }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[HrPayroll.GetTimesheets] " + ex);
                return Json(new { success = false, message = "Tabellarni yuklashda xatolik" }, JsonRequestBehavior.AllowGet);
            }
        }

        // Olish so'rovi: HR/Payroll/GeneratePieceRate
        public async Task<ActionResult> GeneratePieceRate(int? year, int? month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            year = year ?? DateTime.Now.Year;
            month = month ?? DateTime.Now.Month;

            var pieceDropItems = await BuildPieceRateTimesheetDropdownItemsAsync(year.Value, month.Value, orgUnitId);

            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.PayrollType = PieceRate;
            ViewBag.OrgUnitID = HrLookups.SelectList(HrLookups.OrgUnits, orgUnitId);
            ViewBag.TimesheetID = new SelectList(pieceDropItems, "ID", "Name");

            return View("Generate");
        }

        // POST so'rovi: HR/Payroll/GeneratePieceRate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> GeneratePieceRate(int year, int month, int? orgUnitId, string timesheetId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // timesheetId formati: "Yil-Oy-OrgUnitID" yoki "ALL"
            if (string.IsNullOrEmpty(timesheetId))
            {
                TempData["Error"] = "Tabel tanlanmagan";
                return RedirectToAction("GeneratePieceRate", new { year, month, orgUnitId });
            }

            int? payrollOrgUnitId;
            bool allMode = timesheetId == "ALL";
            if (allMode)
            {
                // "Barcha tabellar": forma bo'limi (null — butun kompaniya)
                payrollOrgUnitId = orgUnitId;
            }
            else
            {
                var parts = timesheetId.Split('-');
                int parsedYear, parsedMonth, parsedOrg = 0;
                if (parts.Length != 3 || !int.TryParse(parts[0], out parsedYear) || !int.TryParse(parts[1], out parsedMonth) ||
                    (!string.IsNullOrEmpty(parts[2]) && !int.TryParse(parts[2], out parsedOrg)))
                {
                    TempData["Error"] = "Noto'g'ri tabel formati";
                    return RedirectToAction("GeneratePieceRate", new { year, month, orgUnitId });
                }
                int? parsedOrgUnitId = string.IsNullOrEmpty(parts[2]) ? (int?)null : parsedOrg;

                // Forma bo'lim filtri: bo'lim tanlangan bo'lsa, tabel ham shu bo'limga tegishli bo'lishi kerak
                if (parsedYear != year || parsedMonth != month || (orgUnitId.HasValue && parsedOrgUnitId != orgUnitId.Value))
                {
                    TempData["Error"] = "Tabel ma'lumotlari mos kelmaydi";
                    return RedirectToAction("GeneratePieceRate", new { year, month, orgUnitId });
                }
                payrollOrgUnitId = parsedOrgUnitId;
            }

            var approvals = await LoadApprovedPieceApprovalsAsync(year, month);
            bool anyApplicable = payrollOrgUnitId == null
                ? approvals.Count > 0
                : approvals.Any(a => a.OrgUnitID == null || a.OrgUnitID == payrollOrgUnitId);
            if (!anyApplicable)
            {
                TempData["Error"] = "Ushbu oy (bo'lim) uchun tasdiqlangan ishbay tabel topilmadi";
                return RedirectToAction("GeneratePieceRate", new { year, month, orgUnitId });
            }

            var existing = await FindPayrollHeadersAsync(year, month, payrollOrgUnitId, PieceRate);
            var activeExisting = existing.FirstOrDefault(IsPayrollHeaderActive);
            if (activeExisting != null && activeExisting.Status != Draft)
            {
                TempData["Error"] = "Mavjud ishbay hisob tasdiqlangan yoki o'tkazilgan. Qayta hisoblash uchun uni qoralama holatda yarating.";
                return RedirectToAction("Edit", new { id = activeExisting.ID });
            }

            HR_PAYROLL_HEADERS payroll;
            GenerationResult result;
            using (var tx = db.Database.BeginTransaction())
            {
                Dictionary<int, ManualAdjustment> adjustments = null;
                if (activeExisting != null)
                {
                    // Qoralama: tasdiqlangan tabel holatini to'liq qayta tortish uchun satrlar yangidan quriladi
                    payroll = activeExisting;
                    adjustments = await SoftDeleteLinesKeepingAdjustmentsAsync(payroll.ID);
                }
                else
                {
                    payroll = await ReviveOrCreateDraftHeaderAsync(existing, year, month, payrollOrgUnitId, PieceRate, null);
                }

                result = await GeneratePieceRatePayrollLinesFromWorkEntries(payroll, year, month, payrollOrgUnitId, approvals);
                ApplyAdjustments(payroll.ID, adjustments);
                await db.SaveChangesAsync();
                tx.Commit();
            }

            var orgUnitName = HrLookups.NameOf(HrLookups.OrgUnits, payrollOrgUnitId) ?? "Butun kompaniya";
            logger.Log("HrPayroll", "GeneratePieceRate", $"Tabel: {year}-{month:D2}, Bo'lim: {orgUnitName}, Tur: Ishbay, BarchaTabel: {(allMode ? "ha" : "yo'q")}, Satrlar: {result.Created}");
            SetGenerationMessages(result, $"Ishbay ish haqi hisobi yaratildi: {result.Created} ta xodim");
            return RedirectToAction("Edit", new { id = payroll.ID });
        }

        private void SetGenerationMessages(GenerationResult result, string success)
        {
            if (!string.IsNullOrEmpty(success)) TempData["Success"] = success;
            var warning = result.Warning();
            if (warning != null) TempData["Error"] = warning;
        }

        // Olish so'rovi: HR/Payroll/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var viewModel = await BuildPayrollViewModelAsync(id.Value);
            if (viewModel == null)
            {
                return HttpNotFound();
            }

            logger.Log("HrPayroll", "Details", $"Hisob: {viewModel.Year}-{viewModel.Month:D2}, Bo'lim: {viewModel.OrgUnitName ?? "Butun kompaniya"}, Tur: {TypeLabel(viewModel.PayrollType)}");
            return View(viewModel);
        }

        // Olish so'rovi: HR/Payroll/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var viewModel = await BuildPayrollViewModelAsync(id.Value);
            if (viewModel == null)
            {
                return HttpNotFound();
            }

            ViewBag.CurrencyItems = ReferenceDataCache.Get(ReferenceDataCache.Currencies);
            if (viewModel.Status == Draft || viewModel.Status == Approved)
            {
                await PopulateFinanceChoicesAsync();
            }

            logger.Log("HrPayroll", "Edit", $"Hisob: {viewModel.Year}-{viewModel.Month:D2}, Bo'lim: {viewModel.OrgUnitName ?? "Butun kompaniya"}, Tur: {TypeLabel(viewModel.PayrollType)}");
            return View(viewModel);
        }

        /// <summary>Edit/Details/Report/Export uchun yagona VM (HRM-D07).</summary>
        private async Task<HrPayrollHeaderViewModel> BuildPayrollViewModelAsync(int id)
        {
            var payroll = await db.HR_PAYROLL_HEADERS.AsNoTracking()
                .Include(p => p.HR_ORG_UNITS)
                .FirstOrDefaultAsync(p => p.ID == id);

            if (!IsPayrollHeaderActive(payroll))
            {
                return null;
            }

            var payrollType = payroll.PayrollType ?? Hourly;
            // Daromad solig'i soatbay va ishbay hisoblarga ham qo'llanadi (HRM-B18 — foydalanuvchi qarori)
            bool isHourly = true;

            var lineEntities = await db.HR_PAYROLL_LINES.AsNoTracking()
                .Include(l => l.HR_EMPLOYEES.HR_POSITIONS)
                .Include(l => l.HR_EMPLOYEES.HR_ORG_UNITS)
                .Include(l => l.CURRENCy)
                .Where(l => l.PayrollID == id && (l.IsDeleted == null || l.IsDeleted == false))
                .ToListAsync();

            var lines = lineEntities
                .Select(l => new HrPayrollLineViewModel
                {
                    ID = l.ID,
                    PayrollID = l.PayrollID,
                    EmployeeID = l.EmployeeID,
                    EmployeeTabNo = l.HR_EMPLOYEES.TabNo,
                    EmployeeFullName = l.HR_EMPLOYEES.FullName,
                    PositionName = l.HR_EMPLOYEES.HR_POSITIONS?.Name,
                    OrgUnitName = l.HR_EMPLOYEES.HR_ORG_UNITS?.Name,
                    BaseSalary = l.BaseSalary ?? 0,
                    HoursWorked = l.HoursWorked ?? 0,
                    PieceQty = l.PieceQty ?? 0,
                    HourlyRate = l.HourlyRate ?? 0,
                    GrossSalary = l.GrossSalary ?? 0,
                    // Soliq saqlangan qatordan: hisoblangan + mukofot − jarima − to'lovga (HRM-B04).
                    // Shu bilan har qator va jami doimo Gross − Net ga mos keladi.
                    IncomeTaxAmount = isHourly ? HrPayrollCalculator.StoredIncomeTax(l) : 0m,
                    Bonuses = l.Bonuses ?? 0,
                    Penalties = l.Penalties ?? 0,
                    NetSalary = l.NetSalary ?? 0,
                    CurrencyID = l.CurrencyID,
                    CurrencyCode = l.CURRENCy?.Shortname,
                    Notes = l.Notes
                }).OrderBy(l => l.EmployeeTabNo).ToList();

            var viewModel = new HrPayrollHeaderViewModel
            {
                ID = payroll.ID,
                Year = payroll.Year,
                Month = payroll.Month,
                OrgUnitID = payroll.OrgUnitID,
                OrgUnitName = payroll.HR_ORG_UNITS?.Name,
                Status = payroll.Status,
                PayrollType = payrollType,
                TimesheetID = payroll.TimesheetID,
                CreatedByUserID = payroll.CreatedByUserID,
                CreatedDT = payroll.CreatedDT,
                ApprovedByUserID = payroll.ApprovedByUserID,
                ApprovedDT = payroll.ApprovedDT,
                Lines = lines
            };

            // Foiz sxemada saqlanmaydi: barcha qatorlar RecalculateTax bilan bir xil foizga keltiriladi,
            // ko'rsatish uchun birinchi soliqli qatordan olinadi.
            viewModel.IncomeTaxPercent = isHourly
                ? lineEntities.Where(l => (l.GrossSalary ?? 0) > 0)
                    .OrderBy(l => l.ID)
                    .Select(l => HrPayrollCalculator.StoredIncomeTaxPercent(l))
                    .FirstOrDefault() ?? HrPayrollCalculator.DefaultIncomeTaxPercent
                : 0m;

            viewModel.ApplyIncomeTax = isHourly;
            viewModel.TotalGrossSalary = viewModel.Lines.Sum(l => l.GrossSalary);
            viewModel.TotalIncomeTax = viewModel.Lines.Sum(l => l.IncomeTaxAmount);
            viewModel.TotalNetSalary = viewModel.Lines.Sum(l => l.NetSalary);

            return viewModel;
        }

        private async Task<HR_PAYROLL_LINES> LoadEditableLineAsync(int lineId)
        {
            var line = await db.HR_PAYROLL_LINES
                .Include(l => l.HR_PAYROLL_HEADERS)
                .Include(l => l.HR_EMPLOYEES)
                .FirstOrDefaultAsync(l => l.ID == lineId);
            if (line == null || !IsPayrollLineActive(line)) return null;
            if (!IsPayrollHeaderActive(line.HR_PAYROLL_HEADERS) || line.HR_PAYROLL_HEADERS.Status != Draft) return null;
            return line;
        }

        private async Task<object> PayrollTotalsAsync(int payrollId)
        {
            var lines = await db.HR_PAYROLL_LINES.AsNoTracking()
                .Where(l => l.PayrollID == payrollId && (l.IsDeleted == null || l.IsDeleted == false))
                .ToListAsync();
            return new
            {
                gross = lines.Sum(l => l.GrossSalary ?? 0),
                bonuses = lines.Sum(l => l.Bonuses ?? 0),
                penalties = lines.Sum(l => l.Penalties ?? 0),
                tax = lines.Sum(l => HrPayrollCalculator.StoredIncomeTax(l)),
                net = lines.Sum(l => l.NetSalary ?? 0)
            };
        }

        // POST so'rovi: HR/Payroll/UpdateLine
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public async Task<JsonResult> UpdateLine(int lineId, decimal? bonuses, decimal? penalties, decimal? incomeTaxPercent, string notes, int? currencyId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPayroll"))
            {
                return Json(new { success = false, message = "Ruxsat yo'q" });
            }

            try
            {
                var line = await LoadEditableLineAsync(lineId);
                if (line == null)
                {
                    return Json(new { success = false, message = "Faqat qoralama, o'chirilmagan hisob bo'yicha satrlarni tahrirlash mumkin" });
                }

                if (bonuses.HasValue)
                {
                    line.Bonuses = Math.Max(0m, bonuses.Value);
                }

                if (penalties.HasValue)
                {
                    line.Penalties = Math.Max(0m, penalties.Value);
                }

                if (notes != null)
                {
                    line.Notes = notes;
                }

                if (currencyId.HasValue)
                {
                    if (ReferenceDataCache.NameOf(ReferenceDataCache.Currencies, currencyId) == null)
                    {
                        return Json(new { success = false, message = "Valyuta topilmadi" });
                    }
                    line.CurrencyID = currencyId.Value;
                }

                var applyIncomeTax = true; // HRM-B18: ishbayga ham
                // Foiz berilmasa — shu qatorning amaldagi foizi saqlanadi (avval birinchi qatordan teskari hisoblanardi)
                var taxPercent = applyIncomeTax
                    ? HrPayrollCalculator.NormalizeTaxPercent(incomeTaxPercent ?? HrPayrollCalculator.StoredIncomeTaxPercent(line) ?? HrPayrollCalculator.DefaultIncomeTaxPercent)
                    : 0m;

                var grossSalary = line.GrossSalary ?? 0;
                line.NetSalary = HrPayrollCalculator.NetSalary(grossSalary, line.Bonuses ?? 0, line.Penalties ?? 0, taxPercent);
                var incomeTaxAmount = HrPayrollCalculator.IncomeTax(grossSalary, taxPercent);

                await db.SaveChangesAsync();

                var employeeLabel = line.HR_EMPLOYEES == null
                    ? "-"
                    : $"{line.HR_EMPLOYEES.FullName} ({line.HR_EMPLOYEES.TabNo ?? "-"})";
                logger.Log("HrPayroll", "UpdateLine", $"Xodim: {employeeLabel}");
                return Json(new
                {
                    success = true,
                    netSalary = line.NetSalary,
                    incomeTaxAmount,
                    currencyCode = ReferenceDataCache.NameOf(ReferenceDataCache.Currencies, line.CurrencyID),
                    totals = await PayrollTotalsAsync(line.PayrollID)
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[HrPayroll.UpdateLine] " + ex);
                return Json(new { success = false, message = "Saqlashda xatolik yuz berdi" });
            }
        }

        /// <summary>
        /// Daromad solig'i foizini butun hisob bo'yicha server tomonida qayta hisoblash (HRM-B04).
        /// Avval UI faqat birinchi qatorni qayta yuborardi, qolgan qatorlarning NetSalary eski foizda qolardi.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public async Task<JsonResult> RecalculateTax(int payrollId, decimal incomeTaxPercent)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPayroll"))
            {
                return Json(new { success = false, message = "Ruxsat yo'q" });
            }

            var payroll = await db.HR_PAYROLL_HEADERS.FirstOrDefaultAsync(p => p.ID == payrollId);
            if (!IsPayrollHeaderActive(payroll) || payroll.Status != Draft)
            {
                return Json(new { success = false, message = "Faqat qoralama hisobda soliqni o'zgartirish mumkin" });
            }
            var taxPercent = HrPayrollCalculator.NormalizeTaxPercent(incomeTaxPercent);
            var lines = await db.HR_PAYROLL_LINES
                .Where(l => l.PayrollID == payrollId && (l.IsDeleted == null || l.IsDeleted == false))
                .ToListAsync();
            foreach (var line in lines)
            {
                line.NetSalary = HrPayrollCalculator.NetSalary(line.GrossSalary ?? 0, line.Bonuses ?? 0, line.Penalties ?? 0, taxPercent);
            }
            await db.SaveChangesAsync();

            logger.Log("HrPayroll", "RecalculateTax", $"Hisob: {payroll.Year}-{payroll.Month:D2}, Soliq: {taxPercent:0.##}%, Satrlar: {lines.Count}");
            return Json(new { success = true, totals = await PayrollTotalsAsync(payrollId) });
        }

        // POST so'rovi: HR/Payroll/Approve/5 (avval GET edi — HRM-S03)
        // Tasdiqlash bilan birga Finance'ga chiqim tranzaksiyalari avtomatik yoziladi (alohida "Post" qadami yo'q).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Approve(int id, int? accountId, int? categoryId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var payroll = await db.HR_PAYROLL_HEADERS.AsNoTracking().Include(p => p.HR_ORG_UNITS).FirstOrDefaultAsync(p => p.ID == id);
            if (!IsPayrollHeaderActive(payroll))
            {
                return HttpNotFound();
            }

            if (payroll.Status != Draft)
            {
                TempData["Error"] = "Faqat 'Qoralama' holatidagi hisoblarni tasdiqlash mumkin";
                return RedirectToAction("Edit", new { id });
            }

            return await PostToFinanceAsync(payroll, Draft, accountId, categoryId, "Approve",
                "Hisob tasdiqlandi va moliya moduliga chiqim sifatida yozildi");
        }

        // POST so'rovi: HR/Payroll/Post/5 — avvaldan "Tasdiqlangan" holatda qolgan (Finance'ga yozilmagan) hisoblar uchun
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Post(int id, int? accountId, int? categoryId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var payroll = await db.HR_PAYROLL_HEADERS.AsNoTracking().Include(p => p.HR_ORG_UNITS).FirstOrDefaultAsync(p => p.ID == id);
            if (!IsPayrollHeaderActive(payroll))
            {
                return HttpNotFound();
            }

            if (payroll.Status != Approved)
            {
                TempData["Error"] = "Faqat tasdiqlangan (moliyaga yozilmagan) hisoblarni o'tkazish mumkin";
                return RedirectToAction("Edit", new { id });
            }

            return await PostToFinanceAsync(payroll, Approved, accountId, categoryId, "Post",
                "Hisob moliya moduliga muvaffaqiyatli o'tkazildi");
        }

        /// <summary>
        /// Hisobni "Posted" holatiga o'tkazadi va har valyuta uchun bitta chiqim (Direction = 2) tranzaksiyasi yozadi — bitta tranzaksiyada.
        ///  - Toifa berilmasa: nomida "oylik" yoki "ish haqi" bo'lgan faol chiqim toifasi (Type = 2).
        ///  - Hisob: tanlangan hisob valyutasi mos kelsa — u, aks holda shu valyutadagi birinchi faol hisob (avval kassa).
        ///  - Optimistik qulf: holat <paramref name="expectedStatus"/> bo'lsagina o'zgaradi (ikki marta bosish ikki marta yozmaydi, HRM-B10).
        /// </summary>
        private async Task<ActionResult> PostToFinanceAsync(HR_PAYROLL_HEADERS payroll, string expectedStatus,
            int? accountId, int? categoryId, string logAction, string successMessage)
        {
            var id = payroll.ID;
            var resolvedCategoryId = categoryId ?? await FindDefaultSalaryCategoryIdAsync();
            if (!resolvedCategoryId.HasValue)
            {
                TempData["Error"] = "Ish haqi uchun moliya toifasi topilmadi. Tasdiqlashda chiqim toifasini tanlang (yoki nomida 'oylik' bo'lgan toifa yarating).";
                return RedirectToAction("Edit", new { id });
            }

            var activeLines = await db.HR_PAYROLL_LINES.AsNoTracking()
                .Where(l => l.PayrollID == id && (l.IsDeleted == null || l.IsDeleted == false))
                .ToListAsync();
            if (activeLines.Count == 0)
            {
                TempData["Error"] = "Hisobda satr yo'q";
                return RedirectToAction("Edit", new { id });
            }

            var defaultCurrencyId = await GetDefaultCurrencyIdAsync();
            var accounts = await db.FIN_ACCOUNTS.AsNoTracking()
                .Where(a => a.IsActive && a.IsDeleted != true)
                .OrderByDescending(a => a.IsCash).ThenBy(a => a.ID)
                .Select(a => new { a.ID, a.CurrencyID })
                .ToListAsync();

            // Valyutalar aralashtirilmaydi: har valyuta uchun alohida chiqim (konvertatsiyasiz)
            var groups = activeLines
                .GroupBy(l => l.CurrencyID ?? defaultCurrencyId)
                .Select(g => new { CurrencyID = g.Key, Amount = g.Sum(l => l.NetSalary ?? 0) })
                .Where(g => g.Amount != 0)
                .ToList();

            var accountByCurrency = new Dictionary<int, int>();
            foreach (var g in groups)
            {
                var account = accounts.FirstOrDefault(a => a.ID == accountId && a.CurrencyID == g.CurrencyID)
                              ?? accounts.FirstOrDefault(a => a.CurrencyID == g.CurrencyID);
                if (account == null)
                {
                    TempData["Error"] = $"{ReferenceDataCache.NameOf(ReferenceDataCache.Currencies, g.CurrencyID) ?? "Valyuta"} uchun faol moliya hisobi topilmadi";
                    return RedirectToAction("Edit", new { id });
                }
                accountByCurrency[g.CurrencyID] = account.ID;
            }

            try
            {
                var userId = UserHelper.GetUserId(User.Identity.Name);
                var transactionIds = new List<object>();
                using (var tx = db.Database.BeginTransaction())
                {
                    var approveFields = expectedStatus == Draft ? ", ApprovedByUserID = @p3, ApprovedDT = GETDATE()" : "";
                    var locked = await db.Database.ExecuteSqlCommandAsync(
                        "UPDATE dbo.HR_PAYROLL_HEADERS SET Status = @p0" + approveFields +
                        " WHERE ID = @p1 AND Status = @p2 AND (IsDeleted IS NULL OR IsDeleted = 0)",
                        Posted, id, expectedStatus, (object)userId ?? DBNull.Value);
                    if (locked != 1)
                    {
                        tx.Rollback();
                        TempData["Error"] = "Hisob holati o'zgargan (allaqachon tasdiqlangan yoki o'tkazilgan)";
                        return RedirectToAction("Edit", new { id });
                    }

                    foreach (var g in groups)
                    {
                        var newId = new System.Data.Entity.Core.Objects.ObjectParameter("newID", typeof(int));
                        db.sp_Fin_AddTransaction(
                            txnDate: DateTime.Now,
                            accountID: accountByCurrency[g.CurrencyID],
                            categoryID: resolvedCategoryId.Value,
                            direction: FinDirectionOutflow, // tizimda 1 = kirim, 2 = chiqim (HRM-B01)
                            amount: g.Amount,
                            currencyID: g.CurrencyID,
                            qty: null,
                            pricePerUnit: null,
                            counterpartyType: null,
                            counterpartyID: null,
                            counterpartyName: "Xodimlar",
                            purpose: $"Ish haqi hisobi {payroll.Year}-{payroll.Month:D2} ({TypeLabel(payroll.PayrollType)})",
                            sourceDocType: "HR_PAYROLL",
                            sourceDocID: id,
                            contractNo: null,
                            comment: $"Ish haqi hisobi tasdiqlandi, ID: {id}",
                            createdByUserID: userId,
                            newID: newId
                        );
                        transactionIds.Add(newId.Value);
                    }

                    tx.Commit();
                }

                var orgUnitName = payroll.HR_ORG_UNITS?.Name ?? "Butun kompaniya";
                logger.Log("HrPayroll", logAction, $"Hisob: {payroll.Year}-{payroll.Month:D2}, Bo'lim: {orgUnitName}, Tur: {TypeLabel(payroll.PayrollType)}, TranzaksiyaID: {string.Join(",", transactionIds)}");
                TempData["Success"] = successMessage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[HrPayroll." + logAction + "] " + ex);
                TempData["Error"] = "Moliyaga yozishda xatolik yuz berdi — hisob o'zgarmadi";
            }

            return RedirectToAction("Edit", new { id });
        }

        private async Task<int?> FindDefaultSalaryCategoryIdAsync()
        {
            return await db.FIN_CATEGORIES.AsNoTracking()
                .Where(c => c.IsActive && c.IsDeleted != true && c.SystemCode == SystemCodes.FinCategory.Payroll)
                .OrderBy(c => c.SortOrder).ThenBy(c => c.ID)
                .Select(c => (int?)c.ID)
                .FirstOrDefaultAsync();
        }

        /// <summary>Tasdiqlash formasi uchun: faol hisoblar va chiqim toifalari (standart tanlov bilan).</summary>
        private async Task PopulateFinanceChoicesAsync()
        {
            var accounts = await db.FIN_ACCOUNTS.AsNoTracking()
                .Where(a => a.IsActive && a.IsDeleted != true)
                .OrderByDescending(a => a.IsCash).ThenBy(a => a.Name)
                .Select(a => new { a.ID, a.Name, a.CurrencyID })
                .ToListAsync();
            var categories = await db.FIN_CATEGORIES.AsNoTracking()
                .Where(c => c.IsActive && c.IsDeleted != true && c.Type == 2)
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
                .Select(c => new { c.ID, c.Name })
                .ToListAsync();

            ViewBag.FinAccountID = new SelectList(
                accounts.Select(a => new { a.ID, Name = a.Name + " (" + (ReferenceDataCache.NameOf(ReferenceDataCache.Currencies, a.CurrencyID) ?? "-") + ")" }),
                "ID", "Name", accounts.FirstOrDefault()?.ID);
            ViewBag.FinCategoryID = new SelectList(categories, "ID", "Name", await FindDefaultSalaryCategoryIdAsync());
        }

        // Olish so'rovi: HR/Payroll/Report/5
        public async Task<ActionResult> Report(int id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var viewModel = await BuildPayrollViewModelAsync(id);
            if (viewModel == null)
            {
                return HttpNotFound();
            }

            return View("~/Views/HrReports/PayrollReport.cshtml", viewModel);
        }

        // Olish so'rovi: HR/Payroll/Export/5
        public async Task<ActionResult> Export(int id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var vm = await BuildPayrollViewModelAsync(id);
            if (vm == null)
            {
                return HttpNotFound();
            }

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add($"Hisob {vm.Year}-{vm.Month:D2}");

                string[] headers = { "№", "Tab.№", "F.I.O.", "Lavozim", "Asosiy maosh", "Ishlangan soatlar", "Hisoblangan", "Mukofotlar", "Jarimalar", "To'lovga", "Valyuta" };
                for (int c = 0; c < headers.Length; c++) worksheet.Cell(1, c + 1).Value = headers[c];

                int row = 2;
                int lineNum = 1;
                foreach (var line in vm.Lines)
                {
                    worksheet.Cell(row, 1).Value = lineNum++;
                    worksheet.Cell(row, 2).Value = line.EmployeeTabNo;
                    worksheet.Cell(row, 3).Value = line.EmployeeFullName;
                    worksheet.Cell(row, 4).Value = line.PositionName;
                    worksheet.Cell(row, 5).Value = line.BaseSalary;
                    worksheet.Cell(row, 6).Value = line.HoursWorked;
                    worksheet.Cell(row, 7).Value = line.GrossSalary;
                    worksheet.Cell(row, 8).Value = line.Bonuses;
                    worksheet.Cell(row, 9).Value = line.Penalties;
                    worksheet.Cell(row, 10).Value = line.NetSalary;
                    worksheet.Cell(row, 11).Value = line.CurrencyCode;
                    row++;
                }

                worksheet.Cell(row, 5).Value = "JAMI:";
                worksheet.Cell(row, 7).Value = vm.TotalGrossSalary;
                worksheet.Cell(row, 8).Value = vm.Lines.Sum(l => l.Bonuses);
                worksheet.Cell(row, 9).Value = vm.Lines.Sum(l => l.Penalties);
                worksheet.Cell(row, 10).Value = vm.TotalNetSalary;

                worksheet.Columns().AdjustToContents();

                var fileName = $"Hisob_{vm.Year}_{vm.Month:D2}.xlsx";
                logger.Log("HrPayroll", "Export", $"Hisob: {vm.Year}-{vm.Month:D2}, Bo'lim: {vm.OrgUnitName ?? "Butun kompaniya"}, Tur: {TypeLabel(vm.PayrollType)}");
                return File(workbook.ToByteArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }

        // Olish so'rovi: HR/Payroll/o'chirish/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var payroll = await db.HR_PAYROLL_HEADERS.AsNoTracking()
                .Include(p => p.HR_ORG_UNITS)
                .FirstOrDefaultAsync(p => p.ID == id);

            if (!IsPayrollHeaderActive(payroll))
            {
                return HttpNotFound();
            }

            return View(payroll);
        }

        // POST so'rovi: HR/Payroll/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var payroll = await db.HR_PAYROLL_HEADERS.Include(p => p.HR_ORG_UNITS).FirstOrDefaultAsync(p => p.ID == id);
            if (!IsPayrollHeaderActive(payroll))
            {
                TempData["Error"] = "Hisob topilmadi yoki allaqachon o'chirilgan";
                return RedirectToAction("Index");
            }

            if (payroll.Status == Approved || payroll.Status == Posted)
            {
                TempData["Error"] = "Tasdiqlangan yoki o'tkazilgan hisobni o'chirib bo'lmaydi";
                return RedirectToAction("Index");
            }

            await SoftDeleteActivePayrollLinesAsync(id);
            payroll.IsDeleted = true;
            await db.SaveChangesAsync();

            logger.Log("HrPayroll", "Delete", $"Hisob: {payroll.Year}-{payroll.Month:D2}, Bo'lim: {payroll.HR_ORG_UNITS?.Name ?? "Butun kompaniya"}, Tur: {TypeLabel(payroll.PayrollType)}");
            return RedirectToAction("Index");
        }

        #region Generatsiya yordamchilari

        /// <summary>Qatorlarni o'chirilgan deb belgilaydi (SaveChanges chaqiruvchida). Bitta o'chirish yo'li (HRM-D12).</summary>
        private Task SoftDeleteActivePayrollLinesAsync(int payrollId)
        {
            return SoftDeleteLinesKeepingAdjustmentsAsync(payrollId);
        }

        /// <summary>Qo'lda kiritiladigan qiymatlar: qayta hisoblashda xodim bo'yicha qayta qo'llanadi (HRM-B09).</summary>
        private sealed class ManualAdjustment
        {
            public decimal Bonuses { get; set; }
            public decimal Penalties { get; set; }
            public decimal? TaxPercent { get; set; }
        }

        /// <summary>Qatorlarni o'chirilgan deb belgilaydi va ulardagi mukofot/jarima/soliq foizini qaytaradi.</summary>
        private async Task<Dictionary<int, ManualAdjustment>> SoftDeleteLinesKeepingAdjustmentsAsync(int payrollId)
        {
            var activeLines = await db.HR_PAYROLL_LINES
                .Where(l => l.PayrollID == payrollId && (l.IsDeleted == null || l.IsDeleted == false))
                .ToListAsync();

            var adjustments = new Dictionary<int, ManualAdjustment>();
            foreach (var line in activeLines)
            {
                adjustments[line.EmployeeID] = new ManualAdjustment
                {
                    Bonuses = line.Bonuses ?? 0,
                    Penalties = line.Penalties ?? 0,
                    TaxPercent = HrPayrollCalculator.StoredIncomeTaxPercent(line)
                };
                line.IsDeleted = true;
            }
            return adjustments;
        }

        /// <summary>Yangi qo'shilgan (hali saqlanmagan) qatorlarga saqlab olingan qo'lda kiritilgan qiymatlarni qo'llaydi.</summary>
        private void ApplyAdjustments(int payrollId, Dictionary<int, ManualAdjustment> adjustments)
        {
            if (adjustments == null || adjustments.Count == 0) return;
            foreach (var line in db.HR_PAYROLL_LINES.Local.Where(l => l.PayrollID == payrollId && l.ID == 0))
            {
                if (!adjustments.TryGetValue(line.EmployeeID, out var adjustment)) continue;
                line.Bonuses = adjustment.Bonuses;
                line.Penalties = adjustment.Penalties;
                line.NetSalary = HrPayrollCalculator.NetSalary(line.GrossSalary ?? 0, adjustment.Bonuses, adjustment.Penalties,
                    adjustment.TaxPercent ?? HrPayrollCalculator.DefaultIncomeTaxPercent);
            }
        }

        private Task<List<HR_PAYROLL_HEADERS>> FindPayrollHeadersAsync(int year, int month, int? orgUnitId, string payrollType)
        {
            return db.HR_PAYROLL_HEADERS
                .Where(p => p.Year == year && p.Month == month &&
                            p.PayrollType == payrollType &&
                            (orgUnitId == null ? p.OrgUnitID == null : p.OrgUnitID == orgUnitId.Value))
                .OrderBy(p => p.ID)
                .ToListAsync();
        }

        /// <summary>
        /// O'chirilgan sarlavhani qoralama sifatida tiklaydi yoki yangisini yaratadi (avval 3 nusxa, HRM-D06).
        /// Faol sarlavha yo'qligi chaqiruvchi tomonidan tekshirilgan bo'lishi kerak.
        /// </summary>
        private async Task<HR_PAYROLL_HEADERS> ReviveOrCreateDraftHeaderAsync(List<HR_PAYROLL_HEADERS> existing,
            int year, int month, int? orgUnitId, string payrollType, int? timesheetId)
        {
            var userId = UserHelper.GetUserId(User.Identity.Name);
            var deleted = existing.FirstOrDefault(p => p.IsDeleted == true);
            if (deleted != null)
            {
                deleted.IsDeleted = false;
                deleted.Status = Draft;
                deleted.TimesheetID = timesheetId;
                deleted.CreatedByUserID = userId;
                deleted.CreatedDT = DateTime.Now;
                deleted.ApprovedByUserID = null;
                deleted.ApprovedDT = null;
                await SoftDeleteActivePayrollLinesAsync(deleted.ID);
                await db.SaveChangesAsync();
                return deleted;
            }

            var payroll = new HR_PAYROLL_HEADERS
            {
                Year = year,
                Month = month,
                OrgUnitID = orgUnitId,
                Status = Draft,
                TimesheetID = timesheetId,
                PayrollType = payrollType,
                CreatedByUserID = userId,
                CreatedDT = DateTime.Now,
                IsDeleted = false
            };
            db.HR_PAYROLL_HEADERS.Add(payroll);
            await db.SaveChangesAsync();
            return payroll;
        }

        /// <summary>
        /// Ikki marta to'lashdan himoya (HRM-B03): shu oy va turdagi boshqa faol hisobda (bo'lim yoki butun kompaniya)
        /// faol qatori bor xodimlar yangi hisobga qo'shilmaydi.
        /// </summary>
        private async Task<HashSet<int>> GetEmployeesInOtherPayrollsAsync(int year, int month, string payrollType, int excludePayrollId)
        {
            var ids = await db.HR_PAYROLL_LINES.AsNoTracking()
                .Where(l => (l.IsDeleted == null || l.IsDeleted == false) &&
                            l.PayrollID != excludePayrollId &&
                            l.HR_PAYROLL_HEADERS.Year == year &&
                            l.HR_PAYROLL_HEADERS.Month == month &&
                            l.HR_PAYROLL_HEADERS.PayrollType == payrollType &&
                            (l.HR_PAYROLL_HEADERS.IsDeleted == null || l.HR_PAYROLL_HEADERS.IsDeleted == false))
                .Select(l => l.EmployeeID)
                .Distinct()
                .ToListAsync();
            return new HashSet<int>(ids);
        }

        /// <summary>Oylik qatorlarining standart valyutasi: asos valyuta (so'm), u bo'lmasa — eng kichik ID'li valyuta.</summary>
        private async Task<int> GetDefaultCurrencyIdAsync()
        {
            var baseId = CurrencyRateService.BaseCurrencyId();
            if (baseId.HasValue)
            {
                return baseId.Value;
            }
            var id = await db.CURRENCIES.AsNoTracking()
                .Where(c => c.IsDeleted == null || c.IsDeleted == false)
                .OrderBy(c => c.ID)
                .Select(c => (int?)c.ID)
                .FirstOrDefaultAsync();
            if (!id.HasValue)
            {
                throw new InvalidOperationException("Valyuta jadvalida hech qanday valyuta topilmadi. Iltimos, avval valyuta qo'shing.");
            }
            return id.Value;
        }

        private static string EmployeeLabel(HR_EMPLOYEES e) => e == null ? "-" : $"{e.FullName} ({e.TabNo ?? "-"})";

        /// <summary>Soatbay qatorlar (SaveChanges chaqiruvchida). Shtat rejalari va ustamalar oldindan yuklanadi (HRM-P08).</summary>
        private async Task<GenerationResult> GenerateHourlyPayrollLines(HR_PAYROLL_HEADERS payroll, HR_TIMESHEET_HEADERS timesheet)
        {
            var result = new GenerationResult();
            var monthStart = new DateTime(payroll.Year, payroll.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var timesheetLines = await db.HR_TIMESHEET_LINES.AsNoTracking()
                .Include(l => l.HR_EMPLOYEES.HR_SALARY_GRADES)
                .Where(l => l.TimesheetID == timesheet.ID &&
                            l.IsDeleted != true &&
                            l.HR_EMPLOYEES.IsDeleted != true &&
                            (l.TotalWorkedHours ?? 0) > 0)
                .ToListAsync();

            var alreadyPaid = await GetEmployeesInOtherPayrollsAsync(payroll.Year, payroll.Month, Hourly, payroll.ID);
            var allowanceByEmployeeId = allowanceService.GetByEmployeeIds(timesheetLines.Select(x => x.EmployeeID));
            var staffingPlans = await HrPayrollCalculator.LoadStaffingPlansAsync(db, monthStart, monthEnd);
            var defaultCurrencyId = await GetDefaultCurrencyIdAsync();
            var monthFundHours = timesheetSettingsService.GetFundHoursOrDefault(timesheet.ID);
            var addedEmployees = new HashSet<int>();

            foreach (var timesheetLine in timesheetLines)
            {
                var employee = timesheetLine.HR_EMPLOYEES;
                if (alreadyPaid.Contains(employee.ID))
                {
                    result.SkippedAlreadyPaid.Add(EmployeeLabel(employee));
                    continue;
                }
                if (!addedEmployees.Add(employee.ID)) continue;

                var staffingPlan = HrPayrollCalculator.FindPlan(staffingPlans, employee.OrgUnitID, employee.PositionID);
                var monthlyBase = HrPayrollCalculator.ResolveMonthlyBase(employee, staffingPlan, monthStart);
                var allowancePercent = allowanceByEmployeeId.TryGetValue(employee.ID, out var allowanceRow)
                    ? HrPayrollCalculator.AllowancePercent(allowanceRow)
                    : 0m;
                var hourly = HrPayrollCalculator.CalculateHourly(monthlyBase, allowancePercent, monthFundHours,
                    timesheetLine.TotalWorkedHours ?? 0, timesheetLine.OvertimeHours ?? 0);

                db.HR_PAYROLL_LINES.Add(new HR_PAYROLL_LINES
                {
                    PayrollID = payroll.ID,
                    EmployeeID = employee.ID,
                    BaseSalary = monthlyBase,
                    HoursWorked = timesheetLine.TotalWorkedHours ?? 0,
                    PieceQty = 0,
                    HourlyRate = hourly.HourlyRate,
                    GrossSalary = hourly.GrossSalary,
                    Bonuses = 0,
                    Penalties = 0,
                    NetSalary = HrPayrollCalculator.NetSalary(hourly.GrossSalary, 0, 0, HrPayrollCalculator.DefaultIncomeTaxPercent),
                    CurrencyID = defaultCurrencyId,
                    Notes = allowancePercent > 0 ? $"Ustama: {allowancePercent:0.##}% (moddiy+maxsus)" : null,
                    IsDeleted = false
                });
                result.Created++;
            }

            return result;
        }

        private Task<List<HR_PIECE_RATE_TIMESHEET_APPROVALS>> LoadApprovedPieceApprovalsAsync(int year, int month)
        {
            return db.HR_PIECE_RATE_TIMESHEET_APPROVALS.AsNoTracking()
                .Where(a => a.Year == year && a.Month == month &&
                            a.Status == Approved &&
                            (a.IsDeleted == null || a.IsDeleted == false))
                .ToListAsync();
        }

        /// <summary>
        /// Ishbay qatorlar (SaveChanges chaqiruvchida).
        ///  - faqat tasdiqlangan (yil, oy, bo'lim) guruhlar yozuvlari (HRM-B02); null-bo'limli tasdiq — butun kompaniya;
        ///  - tariflar davr uchun bir marta yuklanadi (HRM-P02, PieceRateTariffResolver);
        ///  - boshqa hisobda allaqachon bor xodimlar o'tkazib yuboriladi (HRM-B03).
        /// </summary>
        private async Task<GenerationResult> GeneratePieceRatePayrollLinesFromWorkEntries(HR_PAYROLL_HEADERS payroll,
            int year, int month, int? orgUnitId, List<HR_PIECE_RATE_TIMESHEET_APPROVALS> approvals)
        {
            var result = new GenerationResult();
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            IQueryable<HR_PIECE_RATE_WORK_ENTRIES> query = db.HR_PIECE_RATE_WORK_ENTRIES.AsNoTracking()
                .Where(e => e.WorkDate >= startDate &&
                            e.WorkDate <= endDate &&
                            (e.IsDeleted == null || e.IsDeleted == false));

            if (orgUnitId.HasValue)
            {
                query = query.Where(e => e.HR_EMPLOYEES.OrgUnitID == orgUnitId.Value);
            }

            var workEntries = await query
                .Select(e => new
                {
                    e.EmployeeID,
                    e.ProductID,
                    e.OperationID,
                    e.WorkDate,
                    e.Quantity,
                    e.HR_EMPLOYEES.OrgUnitID,
                    e.HR_EMPLOYEES.PositionID,
                    e.HR_EMPLOYEES.BaseSalary,
                    e.HR_EMPLOYEES.FullName,
                    e.HR_EMPLOYEES.TabNo
                })
                .ToListAsync();

            bool companyWideApproved = approvals.Any(a => a.OrgUnitID == null);
            var approvedOrgIds = new HashSet<int>(approvals.Where(a => a.OrgUnitID.HasValue).Select(a => a.OrgUnitID.Value));
            var approvedEntries = workEntries
                .Where(e => companyWideApproved || (e.OrgUnitID.HasValue && approvedOrgIds.Contains(e.OrgUnitID.Value)))
                .ToList();
            result.UnapprovedEntries = workEntries.Count - approvedEntries.Count;

            var resolver = await PieceRateTariffResolver.LoadAsync(db, startDate, endDate,
                approvedEntries.Select(e => e.ProductID).Distinct().ToList());
            var alreadyPaid = await GetEmployeesInOtherPayrollsAsync(year, month, PieceRate, payroll.ID);
            var allowanceByEmployeeId = allowanceService.GetByEmployeeIds(approvedEntries.Select(e => e.EmployeeID));
            var defaultCurrencyId = await GetDefaultCurrencyIdAsync();

            foreach (var employeeGroup in approvedEntries.GroupBy(e => e.EmployeeID))
            {
                var first = employeeGroup.First();
                if (alreadyPaid.Contains(employeeGroup.Key))
                {
                    result.SkippedAlreadyPaid.Add($"{first.FullName} ({first.TabNo ?? "-"})");
                    continue;
                }

                decimal grossSalary = 0;
                decimal totalPieceQty = 0;
                foreach (var entry in employeeGroup)
                {
                    totalPieceQty += entry.Quantity;
                    var tariff = resolver.Resolve(entry.ProductID, entry.OperationID, entry.PositionID, entry.WorkDate);
                    if (tariff == null)
                    {
                        result.EntriesWithoutTariff++;
                        continue;
                    }
                    grossSalary += entry.Quantity * tariff.RatePerUnit;
                }

                var allowancePercent = allowanceByEmployeeId.TryGetValue(employeeGroup.Key, out var allowanceRow)
                    ? HrPayrollCalculator.AllowancePercent(allowanceRow)
                    : 0m;
                grossSalary = HrPayrollCalculator.ApplyAllowance(grossSalary, allowancePercent);

                db.HR_PAYROLL_LINES.Add(new HR_PAYROLL_LINES
                {
                    PayrollID = payroll.ID,
                    EmployeeID = employeeGroup.Key,
                    BaseSalary = first.BaseSalary ?? 0,
                    HoursWorked = 0,
                    PieceQty = totalPieceQty,
                    HourlyRate = 0,
                    GrossSalary = grossSalary,
                    Bonuses = 0,
                    Penalties = 0,
                    NetSalary = HrPayrollCalculator.NetSalary(grossSalary, 0, 0, HrPayrollCalculator.DefaultIncomeTaxPercent), // HRM-B18: ishbayga ham soliq
                    CurrencyID = defaultCurrencyId,
                    Notes = allowancePercent > 0 ? $"Ustama: {allowancePercent:0.##}% (moddiy+maxsus)" : null,
                    IsDeleted = false
                });
                result.Created++;
            }

            return result;
        }

        #endregion

        #region Dropdown

        private sealed class TimesheetDropdownItem
        {
            public string ID { get; set; }
            public string Name { get; set; }
        }

        private sealed class PieceGroupRow
        {
            public int? OrgUnitID { get; set; }
            public string OrgUnitName { get; set; }
            public int EntryCount { get; set; }
        }

        private async Task<List<TimesheetDropdownItem>> BuildHourlyTimesheetDropdownItemsAsync(int year, int month, int? orgUnitId)
        {
            var timesheets = await db.HR_TIMESHEET_HEADERS.AsNoTracking()
                .Where(t => t.Year == year && t.Month == month &&
                            t.Status == Approved &&
                            (t.IsDeleted == null || t.IsDeleted == false) &&
                            (orgUnitId == null || t.OrgUnitID == orgUnitId.Value))
                .Select(t => new { t.ID, t.Year, t.Month, OrgUnitName = t.HR_ORG_UNITS.Name })
                .ToListAsync();

            return timesheets
                .OrderBy(t => t.OrgUnitName ?? "")
                .Select(t => new TimesheetDropdownItem
                {
                    ID = t.ID.ToString(),
                    Name = $"{t.OrgUnitName ?? "Butun kompaniya"} - {t.Year}-{t.Month:D2}"
                }).ToList();
        }

        /// <summary>
        /// Ishbay dropdown: tasdiqlar + ish hajmi yozuvlari; tasdiq (null org) bo'lim yozuvlari bilan moslashadi.
        /// Yozuvlar soni SQL'da guruhlanadi (avval oyning barcha yozuvlari xodim va bo'lim bilan yuklanardi).
        /// </summary>
        private async Task<List<TimesheetDropdownItem>> BuildPieceRateTimesheetDropdownItemsAsync(int year, int month, int? orgUnitIdFilter)
        {
            var approvedTimesheets = (await LoadApprovedPieceApprovalsAsync(year, month))
                .Where(a => orgUnitIdFilter == null || a.OrgUnitID == null || a.OrgUnitID == orgUnitIdFilter.Value)
                .ToList();

            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            IQueryable<HR_PIECE_RATE_WORK_ENTRIES> query = db.HR_PIECE_RATE_WORK_ENTRIES
                .Where(e => e.WorkDate >= startDate &&
                            e.WorkDate <= endDate &&
                            (e.IsDeleted == null || e.IsDeleted == false));

            if (orgUnitIdFilter.HasValue)
            {
                query = query.Where(e => e.HR_EMPLOYEES.OrgUnitID == orgUnitIdFilter.Value);
            }

            var countsByOrg = await query
                .GroupBy(e => e.HR_EMPLOYEES.OrgUnitID)
                .Select(g => new
                {
                    OrgUnitID = g.Key,
                    Count = g.Count(),
                    OrgUnitName = g.Select(x => x.HR_EMPLOYEES.HR_ORG_UNITS.Name).FirstOrDefault()
                })
                .ToListAsync();

            bool companyWide = approvedTimesheets.Any(a => a.OrgUnitID == null);
            var groupedData = countsByOrg
                .Where(g => companyWide || approvedTimesheets.Any(a => a.OrgUnitID == g.OrgUnitID))
                .Select(g => new PieceGroupRow { OrgUnitID = g.OrgUnitID, OrgUnitName = g.OrgUnitName ?? "Butun kompaniya", EntryCount = g.Count })
                .ToList();

            foreach (var a in approvedTimesheets)
            {
                if (a.OrgUnitID == null)
                {
                    if (groupedData.Count == 0)
                        groupedData.Add(new PieceGroupRow { OrgUnitID = null, OrgUnitName = "Butun kompaniya", EntryCount = 0 });
                }
                else if (!groupedData.Any(g => g.OrgUnitID == a.OrgUnitID))
                {
                    groupedData.Add(new PieceGroupRow
                    {
                        OrgUnitID = a.OrgUnitID,
                        OrgUnitName = HrLookups.NameOf(HrLookups.OrgUnits, a.OrgUnitID) ?? "Butun kompaniya",
                        EntryCount = 0
                    });
                }
            }

            groupedData = groupedData.OrderBy(x => x.OrgUnitName ?? "").ToList();

            var timesheetList = groupedData.Select(g => new TimesheetDropdownItem
            {
                ID = $"{year}-{month}-{(g.OrgUnitID.HasValue ? g.OrgUnitID.Value.ToString() : "")}",
                Name = $"{g.OrgUnitName} - {year}-{month:D2} ({g.EntryCount} ta yozuv)"
            }).ToList();

            if (groupedData.Count > 1)
            {
                timesheetList.Insert(0, new TimesheetDropdownItem
                {
                    ID = "ALL",
                    Name = $"Ushbu oydagi barcha tabellar ({groupedData.Count} ta tabel, {groupedData.Sum(g => g.EntryCount)} ta yozuv)"
                });
            }

            return timesheetList;
        }

        #endregion

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
