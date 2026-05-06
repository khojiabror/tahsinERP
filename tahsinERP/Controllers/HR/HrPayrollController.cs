using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels.HR;
using OfficeOpenXml;
using System.IO;

namespace tahsinERP.Controllers.HR
{
    [Authorize]
    public class HrPayrollController : Controller
    {
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

        private async Task SoftDeleteActivePayrollLinesAsync(int payrollId)
        {
            var activeLines = await db.HR_PAYROLL_LINES
                .Where(l => l.PayrollID == payrollId && (l.IsDeleted == null || l.IsDeleted == false))
                .ToListAsync();

            foreach (var line in activeLines)
            {
                line.IsDeleted = true;
                db.Entry(line).State = EntityState.Modified;
            }

            await db.SaveChangesAsync();
        }

        // Olish so'rovi: HR/Payroll
        public async Task<ActionResult> Index(int? year, int? month, int? orgUnitId, string status, string payrollType)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            year = year ?? DateTime.Now.Year;
            // Don't set standart month - allow null uchun "barcha months" filtrlash

            IQueryable<HR_PAYROLL_HEADERS> query = db.HR_PAYROLL_HEADERS
                .Where(p => p.IsDeleted == false || p.IsDeleted == null)
                .Include(p => p.HR_ORG_UNITS)
                .Include(p => p.HR_PAYROLL_LINES);

            if (year.HasValue)
            {
                query = query.Where(p => p.Year == year.Value);
            }

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

            var payrolls = await query.OrderByDescending(p => p.Year).ThenByDescending(p => p.Month).ToListAsync();

            var viewModels = payrolls.Select(p => new HrPayrollHeaderViewModel
            {
                ID = p.ID,
                Year = p.Year,
                Month = p.Month,
                OrgUnitID = p.OrgUnitID,
                OrgUnitName = p.HR_ORG_UNITS?.Name,
                Status = p.Status,
                TimesheetID = p.TimesheetID,
                CreatedDT = p.CreatedDT,
                ApprovedDT = p.ApprovedDT,
                PayrollType = p.PayrollType ?? "Hourly",
                TotalGrossSalary = p.HR_PAYROLL_LINES.Where(l => IsPayrollLineActive(l)).Sum(l => l.GrossSalary ?? 0),
                TotalNetSalary = p.HR_PAYROLL_LINES.Where(l => IsPayrollLineActive(l)).Sum(l => l.NetSalary ?? 0)
            }).ToList();

            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.PayrollType = payrollType;
            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnitId);

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

            // Olish approved hourly timesheets (non-piece-rate)
            var timesheets = await db.HR_TIMESHEET_HEADERS
                .Include(t => t.HR_ORG_UNITS)
                .Where(t => t.Year == year.Value && t.Month == month.Value && 
                    t.Status == "Approved" &&
                    (orgUnitId == null ? true : t.OrgUnitID == orgUnitId.Value))
                .OrderBy(t => t.HR_ORG_UNITS.Name)
                .ToListAsync();

            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.PayrollType = "Hourly";
            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnitId);
            ViewBag.TimesheetID = new SelectList(
                timesheets.Select(t => new { ID = t.ID, Name = $"{t.HR_ORG_UNITS?.Name ?? "Butun kompaniya"} - {t.Year}-{t.Month:D2}" }),
                "ID", "Name");

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

            // Tekshirish agar payroll already mavjud (o'chirilganlar hisobga olinmaydi)
            var existing = await db.HR_PAYROLL_HEADERS
                .FirstOrDefaultAsync(p => p.Year == year && p.Month == month &&
                    p.PayrollType == "Hourly" &&
                    (orgUnitId == null ? p.OrgUnitID == null : p.OrgUnitID == orgUnitId.Value) &&
                    (p.IsDeleted == null || p.IsDeleted == false));

            if (existing != null)
            {
                return RedirectToAction("Edit", new { id = existing.ID });
            }

            var deletedExisting = await db.HR_PAYROLL_HEADERS
                .FirstOrDefaultAsync(p => p.Year == year && p.Month == month &&
                    p.PayrollType == "Hourly" &&
                    (orgUnitId == null ? p.OrgUnitID == null : p.OrgUnitID == orgUnitId.Value) &&
                    p.IsDeleted == true);

            HR_PAYROLL_HEADERS payroll;
            if (deletedExisting != null)
            {
                deletedExisting.IsDeleted = false;
                deletedExisting.Status = "Draft";
                deletedExisting.TimesheetID = timesheetId;
                deletedExisting.CreatedByUserID = UserHelper.GetUserId(User.Identity.Name);
                deletedExisting.CreatedDT = DateTime.Now;
                db.Entry(deletedExisting).State = EntityState.Modified;
                await db.SaveChangesAsync();
                await SoftDeleteActivePayrollLinesAsync(deletedExisting.ID);
                payroll = deletedExisting;
            }
            else
            {
                payroll = new HR_PAYROLL_HEADERS
                {
                    Year = year,
                    Month = month,
                    OrgUnitID = orgUnitId,
                    Status = "Draft",
                    TimesheetID = timesheetId,
                    PayrollType = "Hourly",
                    CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                    CreatedDT = DateTime.Now,
                    IsDeleted = false
                };

                db.HR_PAYROLL_HEADERS.Add(payroll);
                await db.SaveChangesAsync();
            }

            var timesheet = await db.HR_TIMESHEET_HEADERS
                .Include(t => t.HR_TIMESHEET_LINES.Select(l => l.HR_EMPLOYEES))
                .FirstOrDefaultAsync(t => t.ID == timesheetId);

            if (timesheet == null || timesheet.Status != "Approved")
            {
                TempData["Error"] = "Tabel topilmadi yoki tasdiqlanmagan";
                return RedirectToAction("GenerateHourly");
            }

            // Yaratish payroll lines uchun hourly employees faqat
            await GenerateHourlyPayrollLines(payroll.ID, timesheet);

            var orgUnitName = timesheet?.HR_ORG_UNITS?.Name ?? "Butun kompaniya";
            logger.Log("HrPayroll", "GenerateHourly", $"Tabel: {timesheet.Year}-{timesheet.Month:D2}, Bo'lim: {orgUnitName}, Tur: Soatbay");
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
                List<object> timesheetList = new List<object>();

                if (payrollType == "PieceRate")
                {
                    timesheetList = (await BuildPieceRateTimesheetDropdownItemsAsync(year, month, orgUnitId)).Cast<object>().ToList();
                }
                else
                {
                    // Olish approved hourly timesheets (non-piece-rate)
                    var timesheets = await db.HR_TIMESHEET_HEADERS
                        .Include(t => t.HR_ORG_UNITS)
                        .Where(t => t.Year == year && t.Month == month && 
                            t.Status == "Approved" &&
                            (orgUnitId == null ? true : t.OrgUnitID == orgUnitId.Value))
                        .OrderBy(t => t.HR_ORG_UNITS.Name)
                        .ToListAsync();

                    timesheetList = timesheets.Select(t => new
                    {
                        ID = t.ID.ToString(),
                        Name = $"{t.HR_ORG_UNITS?.Name ?? "Butun kompaniya"} - {t.Year}-{t.Month:D2}"
                    }).Cast<object>().ToList();
                }

                return Json(new { success = true, data = timesheetList }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
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
            ViewBag.PayrollType = "PieceRate";
            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnitId);
            ViewBag.TimesheetID = new SelectList(
                pieceDropItems,
                "ID", "Name");

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

            // Tahlil qilish timesheetId (format: "Year-Month-OrgUnitID" yoki "barcha")
            if (string.IsNullOrEmpty(timesheetId))
            {
                TempData["Error"] = "Tabel tanlanmagan";
                return RedirectToAction("GeneratePieceRate", new { year, month, orgUnitId });
            }

            // Tekshirish agar "barcha" is selected - process barcha approved timesheets
            if (timesheetId == "ALL")
            {
                // Olish barcha approved timesheets uchun this month
                var approvedTimesheets = await db.HR_PIECE_RATE_TIMESHEET_APPROVALS
                    .Include(a => a.HR_ORG_UNITS)
                    .Where(a => a.Year == year && a.Month == month && 
                        a.Status == "Approved" && 
                        (a.IsDeleted == null || a.IsDeleted == false) &&
                        (orgUnitId == null ? true : (a.OrgUnitID == null || a.OrgUnitID == orgUnitId.Value)))
                    .ToListAsync();

                if (approvedTimesheets.Count == 0)
                {
                    TempData["Error"] = "Ushbu oy uchun tasdiqlangan tabel topilmadi";
                    return RedirectToAction("GeneratePieceRate", new { year, month, orgUnitId });
                }

                // Tekshirish agar payroll already mavjud (o'chirilganlar hisobga olinmaydi)
                var existing = await db.HR_PAYROLL_HEADERS
                    .FirstOrDefaultAsync(p => p.Year == year && p.Month == month &&
                        p.PayrollType == "PieceRate" &&
                        (orgUnitId == null ? p.OrgUnitID == null : p.OrgUnitID == orgUnitId.Value) &&
                        (p.IsDeleted == null || p.IsDeleted == false));

                HR_PAYROLL_HEADERS payroll = existing;
                if (payroll != null)
                {
                    if (payroll.Status != "Draft")
                    {
                        TempData["Error"] = "Mavjud ishbay hisob tasdiqlangan yoki o'tkazilgan. Qayta hisoblash uchun uni qoralama holatda yarating.";
                        return RedirectToAction("Edit", new { id = payroll.ID });
                    }

                    // Qoralama mavjud bo'lsa, tasdiqlangan tabel holatini to'liq qayta tortish uchun satrlarni yangidan quramiz.
                    await SoftDeleteActivePayrollLinesAsync(payroll.ID);
                }
                else
                {
                    var deletedExisting = await db.HR_PAYROLL_HEADERS
                        .FirstOrDefaultAsync(p => p.Year == year && p.Month == month &&
                            p.PayrollType == "PieceRate" &&
                            (orgUnitId == null ? p.OrgUnitID == null : p.OrgUnitID == orgUnitId.Value) &&
                            p.IsDeleted == true);

                    // Yaratish payroll header uchun barcha timesheets
                    if (deletedExisting != null)
                    {
                        deletedExisting.IsDeleted = false;
                        deletedExisting.Status = "Draft";
                        deletedExisting.TimesheetID = null;
                        deletedExisting.CreatedByUserID = UserHelper.GetUserId(User.Identity.Name);
                        deletedExisting.CreatedDT = DateTime.Now;
                        db.Entry(deletedExisting).State = EntityState.Modified;
                        await db.SaveChangesAsync();
                        await SoftDeleteActivePayrollLinesAsync(deletedExisting.ID);
                        payroll = deletedExisting;
                    }
                    else
                    {
                        payroll = new HR_PAYROLL_HEADERS
                        {
                            Year = year,
                            Month = month,
                            OrgUnitID = orgUnitId,
                            Status = "Draft",
                            TimesheetID = null, // Piece-rate doesn't use HR_TIMESHEET_HEADERS
                            PayrollType = "PieceRate",
                            CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                            CreatedDT = DateTime.Now,
                            IsDeleted = false
                        };

                        db.HR_PAYROLL_HEADERS.Add(payroll);
                        await db.SaveChangesAsync();
                    }
                }

                // Yaratish payroll lines uchun barcha approved timesheets (barcha org units combined)
                await GeneratePieceRatePayrollLinesFromWorkEntries(payroll.ID, year, month, orgUnitId);

                var orgUnitName = approvedTimesheets.FirstOrDefault()?.HR_ORG_UNITS?.Name ?? "Butun kompaniya";
                logger.Log("HrPayroll", "GeneratePieceRate", $"Tabel: {year}-{month:D2}, Bo'lim: {orgUnitName}, Tur: Ishbay, BarchaTabel: ha, TabelSoni: {approvedTimesheets.Count}");
                TempData["Success"] = $"{approvedTimesheets.Count} ta tasdiqlangan tabel uchun ish haqi hisobi yaratildi";
                return RedirectToAction("Edit", new { id = payroll.ID });
            }

            // Process single timesheet (existing logic)
            var parts = timesheetId.Split('-');
            if (parts.Length != 3)
            {
                TempData["Error"] = "Noto'g'ri tabel formati";
                return RedirectToAction("GeneratePieceRate", new { year, month, orgUnitId });
            }

            int parsedYear = int.Parse(parts[0]);
            int parsedMonth = int.Parse(parts[1]);
            int? parsedOrgUnitId = string.IsNullOrEmpty(parts[2]) ? (int?)null : int.Parse(parts[2]);

            if (parsedYear != year || parsedMonth != month)
            {
                TempData["Error"] = "Tabel ma'lumotlari mos kelmaydi";
                return RedirectToAction("GeneratePieceRate", new { year, month, orgUnitId });
            }

            // Forma bo'lim filtri: bo'lim tanlangan bo'lsa, tabel ham shu bo'limga tegishli bo'lishi kerak
            if (orgUnitId.HasValue && parsedOrgUnitId != orgUnitId.Value)
            {
                TempData["Error"] = "Tabel ma'lumotlari mos kelmaydi";
                return RedirectToAction("GeneratePieceRate", new { year, month, orgUnitId });
            }

            // Hisob tanlangan tabel (yil-oy-bo'lim) bo'yicha bo'ladi; "Butun kompaniya" filtrida ham bo'lim bo'yicha tabel tanlangan bo'lishi mumkin
            int? payrollOrgUnitId = parsedOrgUnitId;

            // Tekshirish agar payroll already mavjud (o'chirilganlar hisobga olinmaydi)
            var existingSingle = await db.HR_PAYROLL_HEADERS
                .FirstOrDefaultAsync(p => p.Year == year && p.Month == month &&
                    p.PayrollType == "PieceRate" &&
                    (payrollOrgUnitId == null ? p.OrgUnitID == null : p.OrgUnitID == payrollOrgUnitId) &&
                    (p.IsDeleted == null || p.IsDeleted == false));

            HR_PAYROLL_HEADERS payrollSingle = existingSingle;
            if (payrollSingle != null)
            {
                if (payrollSingle.Status != "Draft")
                {
                    TempData["Error"] = "Mavjud ishbay hisob tasdiqlangan yoki o'tkazilgan. Qayta hisoblash uchun uni qoralama holatda yarating.";
                    return RedirectToAction("Edit", new { id = payrollSingle.ID });
                }

                await SoftDeleteActivePayrollLinesAsync(payrollSingle.ID);
            }
            else
            {
                var deletedExistingSingle = await db.HR_PAYROLL_HEADERS
                    .FirstOrDefaultAsync(p => p.Year == year && p.Month == month &&
                        p.PayrollType == "PieceRate" &&
                        (payrollOrgUnitId == null ? p.OrgUnitID == null : p.OrgUnitID == payrollOrgUnitId) &&
                        p.IsDeleted == true);

                // Yaratish payroll header (no TimesheetID uchun piece-rate, as it's based on work entries)
                if (deletedExistingSingle != null)
                {
                    deletedExistingSingle.IsDeleted = false;
                    deletedExistingSingle.Status = "Draft";
                    deletedExistingSingle.TimesheetID = null;
                    deletedExistingSingle.CreatedByUserID = UserHelper.GetUserId(User.Identity.Name);
                    deletedExistingSingle.CreatedDT = DateTime.Now;
                    db.Entry(deletedExistingSingle).State = EntityState.Modified;
                    await db.SaveChangesAsync();
                    await SoftDeleteActivePayrollLinesAsync(deletedExistingSingle.ID);
                    payrollSingle = deletedExistingSingle;
                }
                else
                {
                    payrollSingle = new HR_PAYROLL_HEADERS
                    {
                        Year = year,
                        Month = month,
                        OrgUnitID = payrollOrgUnitId,
                        Status = "Draft",
                        TimesheetID = null, // Piece-rate doesn't use HR_TIMESHEET_HEADERS
                        PayrollType = "PieceRate",
                        CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                        CreatedDT = DateTime.Now,
                        IsDeleted = false
                    };

                    db.HR_PAYROLL_HEADERS.Add(payrollSingle);
                    await db.SaveChangesAsync();
                }
            }

            // Yaratish payroll lines uchun piece-rate employees based on work entries
            await GeneratePieceRatePayrollLinesFromWorkEntries(payrollSingle.ID, year, month, payrollOrgUnitId);

            var orgUnitNameSingle = payrollOrgUnitId.HasValue
                ? (await db.HR_ORG_UNITS.FindAsync(payrollOrgUnitId.Value))?.Name ?? "Butun kompaniya"
                : "Butun kompaniya";
            logger.Log("HrPayroll", "GeneratePieceRate", $"Tabel: {year}-{month:D2}, Bo'lim: {orgUnitNameSingle}, Tur: Ishbay");
            return RedirectToAction("Edit", new { id = payrollSingle.ID });
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

            logger.Log("HrPayroll", "Details", $"Hisob: {viewModel.Year}-{viewModel.Month:D2}, Bo'lim: {viewModel.OrgUnitName ?? "Butun kompaniya"}, Tur: {(viewModel.PayrollType == "PieceRate" ? "Ishbay" : "Soatbay")}");
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

            ViewBag.CurrencyItems = await db.CURRENCIES
                .Where(c => c.IsDeleted == null || c.IsDeleted == false)
                .OrderBy(c => c.Shortname)
                .ToListAsync();

            logger.Log("HrPayroll", "Edit", $"Hisob: {viewModel.Year}-{viewModel.Month:D2}, Bo'lim: {viewModel.OrgUnitName ?? "Butun kompaniya"}, Tur: {(viewModel.PayrollType == "PieceRate" ? "Ishbay" : "Soatbay")}");
            return View(viewModel);
        }

        private async Task<HrPayrollHeaderViewModel> BuildPayrollViewModelAsync(int id)
        {
            var payroll = await db.HR_PAYROLL_HEADERS
                .Include(p => p.HR_ORG_UNITS)
                .Include(p => p.HR_PAYROLL_LINES.Select(l => l.HR_EMPLOYEES.HR_POSITIONS))
                .Include(p => p.HR_PAYROLL_LINES.Select(l => l.HR_EMPLOYEES.HR_ORG_UNITS))
                .Include(p => p.HR_PAYROLL_LINES.Select(l => l.CURRENCy))
                .FirstOrDefaultAsync(p => p.ID == id);

            if (!IsPayrollHeaderActive(payroll))
            {
                return null;
            }

            var lines = payroll.HR_PAYROLL_LINES
                .Where(l => IsPayrollLineActive(l))
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
                PayrollType = payroll.PayrollType ?? "Hourly",
                TimesheetID = payroll.TimesheetID,
                CreatedByUserID = payroll.CreatedByUserID,
                CreatedDT = payroll.CreatedDT,
                ApprovedByUserID = payroll.ApprovedByUserID,
                ApprovedDT = payroll.ApprovedDT,
                Lines = lines
            };

            viewModel.TotalGrossSalary = viewModel.Lines.Sum(l => l.GrossSalary);
            viewModel.TotalNetSalary = viewModel.Lines.Sum(l => l.NetSalary);

            return viewModel;
        }

        // POST so'rovi: HR/Payroll/UpdateLine
        [HttpPost]
        public async Task<JsonResult> UpdateLine(int lineId, decimal? bonuses, decimal? penalties, string notes, int? currencyId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPayroll"))
            {
                return Json(new { success = false, message = "Ruxsat yo'q" });
            }

            try
            {
                var line = await db.HR_PAYROLL_LINES
                    .Include(l => l.HR_PAYROLL_HEADERS)
                    .Include(l => l.HR_EMPLOYEES)
                    .FirstOrDefaultAsync(l => l.ID == lineId);
                if (line == null || !IsPayrollLineActive(line))
                {
                    return Json(new { success = false, message = "Qator topilmadi" });
                }

                if (line.HR_PAYROLL_HEADERS == null || !IsPayrollHeaderActive(line.HR_PAYROLL_HEADERS) || line.HR_PAYROLL_HEADERS.Status != "Draft")
                {
                    return Json(new { success = false, message = "Faqat qoralama, o'chirilmagan hisob bo'yicha satrlarni tahrirlash mumkin" });
                }

                if (bonuses.HasValue)
                {
                    line.Bonuses = bonuses.Value;
                }

                if (penalties.HasValue)
                {
                    line.Penalties = penalties.Value;
                }

                if (notes != null)
                {
                    line.Notes = notes;
                }

                if (currencyId.HasValue)
                {
                    var ok = await db.CURRENCIES.AnyAsync(c => c.ID == currencyId.Value && (c.IsDeleted == null || c.IsDeleted == false));
                    if (!ok)
                    {
                        return Json(new { success = false, message = "Valyuta topilmadi" });
                    }
                    line.CurrencyID = currencyId.Value;
                }

                // Recalculate net salary
                line.NetSalary = (line.GrossSalary ?? 0) + (line.Bonuses ?? 0) - (line.Penalties ?? 0);

                db.Entry(line).State = EntityState.Modified;
                await db.SaveChangesAsync();

                await db.Entry(line).Reference(l => l.CURRENCy).LoadAsync();

                var employeeLabel = line.HR_EMPLOYEES == null
                    ? "-"
                    : $"{line.HR_EMPLOYEES.FullName} ({line.HR_EMPLOYEES.TabNo ?? "-"})";
                logger.Log("HrPayroll", "UpdateLine", $"Xodim: {employeeLabel}");
                return Json(new { success = true, netSalary = line.NetSalary, currencyCode = line.CURRENCy?.Shortname });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Olish so'rovi: HR/Payroll/Approve/5
        [HttpGet]
        public async Task<ActionResult> Approve(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var payroll = await db.HR_PAYROLL_HEADERS.FindAsync(id);
            if (!IsPayrollHeaderActive(payroll))
            {
                return HttpNotFound();
            }

            if (payroll.Status != "Draft")
            {
                TempData["Error"] = "Faqat 'Qoralama' holatidagi hisoblarni tasdiqlash mumkin";
                return RedirectToAction("Edit", new { id });
            }

            payroll.Status = "Approved";
            payroll.ApprovedByUserID = UserHelper.GetUserId(User.Identity.Name);
            payroll.ApprovedDT = DateTime.Now;

            db.Entry(payroll).State = EntityState.Modified;
            await db.SaveChangesAsync();

            var orgUnitName = payroll.HR_ORG_UNITS?.Name ?? "Butun kompaniya";
            logger.Log("HrPayroll", "Approve", $"Hisob: {payroll.Year}-{payroll.Month:D2}, Bo'lim: {orgUnitName}, Tur: {(payroll.PayrollType == "PieceRate" ? "Ishbay" : "Soatbay")}");
            TempData["Success"] = "Hisob muvaffaqiyatli tasdiqlandi";
            return RedirectToAction("Edit", new { id });
        }

        // POST so'rovi: HR/Payroll/Post/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Post(int id, int? accountId, int? categoryId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var payroll = await db.HR_PAYROLL_HEADERS
                .Include(p => p.HR_PAYROLL_LINES.Select(l => l.HR_EMPLOYEES))
                .Include(p => p.HR_PAYROLL_LINES.Select(l => l.CURRENCy))
                .FirstOrDefaultAsync(p => p.ID == id);

            if (!IsPayrollHeaderActive(payroll))
            {
                return HttpNotFound();
            }

            if (payroll.Status != "Approved")
            {
                TempData["Error"] = "Faqat tasdiqlangan hisoblarni o'tkazish mumkin";
                return RedirectToAction("Edit", new { id });
            }

            if (!accountId.HasValue || !categoryId.HasValue)
            {
                TempData["Error"] = "O'tkazish uchun hisob va toifani ko'rsating";
                return RedirectToAction("Edit", new { id });
            }

            try
            {
                var activeLines = payroll.HR_PAYROLL_LINES.Where(l => IsPayrollLineActive(l)).ToList();
                var totalAmount = activeLines.Sum(l => l.NetSalary ?? 0);
                var currencyId = activeLines.FirstOrDefault()?.CurrencyID ?? 1;

                var newId = new System.Data.Entity.Core.Objects.ObjectParameter("newID", typeof(int));
                db.sp_Fin_AddTransaction(
                    txnDate: DateTime.Now,
                    accountID: accountId.Value,
                    categoryID: categoryId.Value,
                    direction: (byte)1, // 1 = xarajat
                    amount: totalAmount,
                    currencyID: currencyId,
                    qty: null,
                    pricePerUnit: null,
                    counterpartyType: null,
                    counterpartyID: null,
                    counterpartyName: "Xodimlar",
                    purpose: $"Ish haqi hisobi {payroll.Year}-{payroll.Month:D2}",
                    sourceDocType: "HR_PAYROLL",
                    sourceDocID: id,
                    contractNo: null,
                    comment: $"Ish haqi hisobi o'tkazildi, ID: {id}",
                    createdByUserID: UserHelper.GetUserId(User.Identity.Name),
                    newID: newId
                );

                payroll.Status = "Posted";
                db.Entry(payroll).State = EntityState.Modified;
                await db.SaveChangesAsync();

                var orgUnitName = payroll.HR_ORG_UNITS?.Name ?? "Butun kompaniya";
                logger.Log("HrPayroll", "Post", $"Hisob: {payroll.Year}-{payroll.Month:D2}, Bo'lim: {orgUnitName}, TranzaksiyaID: {newId.Value}");
                TempData["Success"] = "Hisob moliya moduliga muvaffaqiyatli o'tkazildi";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"O'tkazishda xatolik: {ex.Message}";
                return RedirectToAction("Edit", new { id });
            }

            return RedirectToAction("Index");
        }

        // Olish so'rovi: HR/Payroll/Report/5
        public async Task<ActionResult> Report(int id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var payroll = await db.HR_PAYROLL_HEADERS
                .Include(p => p.HR_ORG_UNITS)
                .Include(p => p.HR_PAYROLL_LINES.Select(l => l.HR_EMPLOYEES.HR_POSITIONS))
                .Include(p => p.HR_PAYROLL_LINES.Select(l => l.CURRENCy))
                .FirstOrDefaultAsync(p => p.ID == id);

            if (!IsPayrollHeaderActive(payroll))
            {
                return HttpNotFound();
            }

            var viewModel = new HrPayrollHeaderViewModel
            {
                ID = payroll.ID,
                Year = payroll.Year,
                Month = payroll.Month,
                OrgUnitID = payroll.OrgUnitID,
                OrgUnitName = payroll.HR_ORG_UNITS?.Name,
                Status = payroll.Status,
                CreatedDT = payroll.CreatedDT,
                ApprovedDT = payroll.ApprovedDT
            };

            viewModel.Lines = payroll.HR_PAYROLL_LINES.Where(l => IsPayrollLineActive(l)).Select(l => new HrPayrollLineViewModel
            {
                ID = l.ID,
                EmployeeTabNo = l.HR_EMPLOYEES.TabNo,
                EmployeeFullName = l.HR_EMPLOYEES.FullName,
                PositionName = l.HR_EMPLOYEES.HR_POSITIONS?.Name,
                BaseSalary = l.BaseSalary ?? 0,
                HoursWorked = l.HoursWorked ?? 0,
                GrossSalary = l.GrossSalary ?? 0,
                Bonuses = l.Bonuses ?? 0,
                Penalties = l.Penalties ?? 0,
                NetSalary = l.NetSalary ?? 0,
                CurrencyCode = l.CURRENCy?.Shortname
            }).OrderBy(l => l.EmployeeTabNo).ToList();

            viewModel.TotalGrossSalary = viewModel.Lines.Sum(l => l.GrossSalary);
            viewModel.TotalNetSalary = viewModel.Lines.Sum(l => l.NetSalary);

            return View("~/Views/HrReports/PayrollReport.cshtml", viewModel);
        }

        // Olish so'rovi: HR/Payroll/Export/5
        public async Task<ActionResult> Export(int id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var payroll = await db.HR_PAYROLL_HEADERS
                .Include(p => p.HR_ORG_UNITS)
                .Include(p => p.HR_PAYROLL_LINES.Select(l => l.HR_EMPLOYEES.HR_POSITIONS))
                .Include(p => p.HR_PAYROLL_LINES.Select(l => l.CURRENCy))
                .FirstOrDefaultAsync(p => p.ID == id);

            if (!IsPayrollHeaderActive(payroll))
            {
                return HttpNotFound();
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add($"Hisob {payroll.Year}-{payroll.Month:D2}");

                worksheet.Cells[1, 1].Value = "№";
                worksheet.Cells[1, 2].Value = "Tab.№";
                worksheet.Cells[1, 3].Value = "F.I.O.";
                worksheet.Cells[1, 4].Value = "Lavozim";
                worksheet.Cells[1, 5].Value = "Asosiy maosh";
                worksheet.Cells[1, 6].Value = "Ishlangan soatlar";
                worksheet.Cells[1, 7].Value = "Hisoblangan";
                worksheet.Cells[1, 8].Value = "Mukofotlar";
                worksheet.Cells[1, 9].Value = "Jarimalar";
                worksheet.Cells[1, 10].Value = "To'lovga";
                worksheet.Cells[1, 11].Value = "Valyuta";

                // ma'lumot
                int row = 2;
                int lineNum = 1;
                foreach (var line in payroll.HR_PAYROLL_LINES.Where(l => IsPayrollLineActive(l)).OrderBy(l => l.HR_EMPLOYEES.TabNo))
                {
                    worksheet.Cells[row, 1].Value = lineNum++;
                    worksheet.Cells[row, 2].Value = line.HR_EMPLOYEES.TabNo;
                    worksheet.Cells[row, 3].Value = line.HR_EMPLOYEES.FullName;
                    worksheet.Cells[row, 4].Value = line.HR_EMPLOYEES.HR_POSITIONS?.Name;
                    worksheet.Cells[row, 5].Value = line.BaseSalary ?? 0;
                    worksheet.Cells[row, 6].Value = line.HoursWorked ?? 0;
                    worksheet.Cells[row, 7].Value = line.GrossSalary ?? 0;
                    worksheet.Cells[row, 8].Value = line.Bonuses ?? 0;
                    worksheet.Cells[row, 9].Value = line.Penalties ?? 0;
                    worksheet.Cells[row, 10].Value = line.NetSalary ?? 0;
                    worksheet.Cells[row, 11].Value = line.CURRENCy?.Shortname;

                    row++;
                }

                // Totals
                worksheet.Cells[row, 7].Value = payroll.HR_PAYROLL_LINES.Where(l => IsPayrollLineActive(l)).Sum(l => l.GrossSalary ?? 0);
                worksheet.Cells[row, 8].Value = payroll.HR_PAYROLL_LINES.Where(l => IsPayrollLineActive(l)).Sum(l => l.Bonuses ?? 0);
                worksheet.Cells[row, 9].Value = payroll.HR_PAYROLL_LINES.Where(l => IsPayrollLineActive(l)).Sum(l => l.Penalties ?? 0);
                worksheet.Cells[row, 10].Value = payroll.HR_PAYROLL_LINES.Where(l => IsPayrollLineActive(l)).Sum(l => l.NetSalary ?? 0);
                worksheet.Cells[row, 5].Value = "JAMI:";

                // Auto-fit ustunlar
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                var fileName = $"Hisob_{payroll.Year}_{payroll.Month:D2}.xlsx";
                var orgUnitName = payroll.HR_ORG_UNITS?.Name ?? "Butun kompaniya";
                logger.Log("HrPayroll", "Export", $"Hisob: {payroll.Year}-{payroll.Month:D2}, Bo'lim: {orgUnitName}, Tur: {(payroll.PayrollType == "PieceRate" ? "Ishbay" : "Soatbay")}");
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }

        /// <summary>
        /// Oylik bazaviy summa: avvalo xodimning asosiy maoshi, bo'sh bo'lsa tarif tarmog'i (lavozim yoki xodim) bo'yicha (min+max)/2.
        /// </summary>
        private static decimal ResolveMonthlyBaseFromGrade(HR_EMPLOYEES employee, HR_STAFFING_PLAN staffingPlan, DateTime refDate)
        {
            if (employee.BaseSalary.HasValue && employee.BaseSalary.Value > 0)
                return employee.BaseSalary.Value;

            HR_SALARY_GRADES grade = null;
            if (staffingPlan?.SalaryGradeID != null)
                grade = staffingPlan.HR_SALARY_GRADES;
            if (grade == null && employee.SalaryGradeID != null)
                grade = employee.HR_SALARY_GRADES;

            if (grade != null && (grade.IsDeleted == null || grade.IsDeleted == false) &&
                grade.EffectiveFrom.Date <= refDate.Date &&
                (grade.EffectiveTo == null || grade.EffectiveTo.Value.Date >= refDate.Date))
            {
                return (grade.MinSalary + grade.MaxSalary) / 2m;
            }

            return 0m;
        }

        private static decimal ResolveAllowancePercent(HrEmployeeAllowanceService.EmployeeAllowanceRow allowanceRow)
        {
            if (allowanceRow == null)
            {
                return 0m;
            }

            var material = allowanceRow.MaterialResponsibilityPercent < 0 ? 0 : allowanceRow.MaterialResponsibilityPercent;
            var special = allowanceRow.SpecialAllowancePercent < 0 ? 0 : allowanceRow.SpecialAllowancePercent;
            return material + special;
        }

        private async Task GenerateHourlyPayrollLines(int payrollId, HR_TIMESHEET_HEADERS timesheet)
        {
            var payroll = await db.HR_PAYROLL_HEADERS
                .Include(p => p.HR_ORG_UNITS)
                .FirstOrDefaultAsync(p => p.ID == payrollId);

            if (payroll == null)
            {
                throw new InvalidOperationException("Ish haqi sarlavhasi topilmadi.");
            }

            int createdCount = 0;

            var monthStart = new DateTime(payroll.Year, payroll.Month, 1);
            var monthEnd = new DateTime(payroll.Year, payroll.Month, DateTime.DaysInMonth(payroll.Year, payroll.Month));

            var timesheetLines = await db.HR_TIMESHEET_LINES
                .Include(l => l.HR_EMPLOYEES)
                .Include(l => l.HR_EMPLOYEES.HR_SALARY_GRADES)
                .Where(l => l.TimesheetID == timesheet.ID && (l.TotalWorkedHours ?? 0) > 0)
                .ToListAsync();

            var allowanceByEmployeeId = allowanceService.GetByEmployeeIds(
                timesheetLines.Where(x => x.HR_EMPLOYEES != null).Select(x => x.HR_EMPLOYEES.ID));

            var defaultCurrency = await db.CURRENCIES.OrderBy(c => c.ID).FirstOrDefaultAsync();
            int? defaultCurrencyID = defaultCurrency?.ID;

            if (!defaultCurrencyID.HasValue)
            {
                throw new Exception("Valyuta jadvalida hech qanday valyuta topilmadi. Iltimos, avval valyuta qo'shing.");
            }

            var monthFundHours = timesheetSettingsService.GetFundHoursOrDefault(timesheet.ID);

            foreach (var timesheetLine in timesheetLines)
            {
                var employee = timesheetLine.HR_EMPLOYEES;

                var staffingPlan = await db.HR_STAFFING_PLAN
                    .Include(sp => sp.HR_SALARY_GRADES)
                    .FirstOrDefaultAsync(sp =>
                        sp.OrgUnitID == employee.OrgUnitID &&
                        sp.PositionID == employee.PositionID &&
                        sp.IsActive &&
                        (sp.IsDeleted == null || sp.IsDeleted == false) &&
                        sp.EffectiveFrom <= monthEnd &&
                        (sp.EffectiveTo == null || sp.EffectiveTo >= monthStart));

                decimal monthlyBase = ResolveMonthlyBaseFromGrade(employee, staffingPlan, monthStart);
                decimal baseSalaryForLine = monthlyBase;
                decimal hoursWorked = timesheetLine.TotalWorkedHours ?? 0;
                decimal hourlyRate = 0;
                decimal grossSalary = 0;
                var allowancePercent = allowanceByEmployeeId.TryGetValue(employee.ID, out var allowanceRow)
                    ? ResolveAllowancePercent(allowanceRow)
                    : 0m;
                var allowanceAmount = monthlyBase * allowancePercent / 100m;
                var monthlyCompensation = monthlyBase + allowanceAmount;
                var overtimeHours = timesheetLine.OvertimeHours ?? 0;
                if (overtimeHours < 0)
                {
                    overtimeHours = 0;
                }
                var normalHours = Math.Max(0m, hoursWorked - overtimeHours);

                hourlyRate = monthFundHours > 0 ? monthlyCompensation / monthFundHours : 0;
                grossSalary = (normalHours * hourlyRate) + (overtimeHours * hourlyRate * 1.5m);

                var payrollLine = new HR_PAYROLL_LINES
                {
                    PayrollID = payrollId,
                    EmployeeID = employee.ID,
                    BaseSalary = baseSalaryForLine,
                    HoursWorked = hoursWorked,
                    PieceQty = 0,
                    HourlyRate = hourlyRate,
                    GrossSalary = grossSalary,
                    Bonuses = 0,
                    Penalties = 0,
                    NetSalary = grossSalary,
                    CurrencyID = defaultCurrencyID.Value,
                    Notes = allowancePercent > 0 ? $"Ustama: {allowancePercent:0.##}% (moddiy+maxsus)" : null
                };

                db.HR_PAYROLL_LINES.Add(payrollLine);
                createdCount++;
            }

            await db.SaveChangesAsync();
            var orgUnitName = payroll.HR_ORG_UNITS?.Name ?? "Butun kompaniya";
            logger.Log("HrPayroll", "GenerateHourlyLines", $"Hisob: {payroll.Year}-{payroll.Month:D2}, Bo'lim: {orgUnitName}, YangiSatrlar: {createdCount}");
        }

        private async Task GeneratePieceRatePayrollLines(int payrollId, HR_TIMESHEET_HEADERS timesheet)
        {
            var payroll = await db.HR_PAYROLL_HEADERS.FindAsync(payrollId);
            
            int createdCount = 0;

            // Olish faqat piece-rate employees
            var pieceRatePositionIds = await db.HR_STAFFING_PLAN
                .Where(sp => sp.IsActive && sp.IsPieceRate == true)
                .Select(sp => sp.PositionID)
                .Distinct()
                .ToListAsync();

            var timesheetLines = await db.HR_TIMESHEET_LINES
                .Include(l => l.HR_EMPLOYEES)
                .Where(l => l.TimesheetID == timesheet.ID &&
                           l.HR_EMPLOYEES.PositionID != null &&
                           pieceRatePositionIds.Contains(l.HR_EMPLOYEES.PositionID ?? 0))
                .ToListAsync();

            // Olish standart currency
            var defaultCurrency = await db.CURRENCIES.OrderBy(c => c.ID).FirstOrDefaultAsync();
            int? defaultCurrencyID = defaultCurrency?.ID;

            if (!defaultCurrencyID.HasValue)
            {
                throw new Exception("Valyuta jadvalida hech qanday valyuta topilmadi. Iltimos, avval valyuta qo'shing.");
            }

            foreach (var timesheetLine in timesheetLines)
            {
                var employee = timesheetLine.HR_EMPLOYEES;
                
                // Olish staffing plan uchun employee
                var staffingPlan = await db.HR_STAFFING_PLAN
                    .FirstOrDefaultAsync(sp => sp.OrgUnitID == employee.OrgUnitID &&
                                              sp.PositionID == employee.PositionID &&
                                              sp.IsActive &&
                                              sp.IsPieceRate == true &&
                                              sp.EffectiveFrom <= payroll.CreatedDT &&
                                              (sp.EffectiveTo == null || sp.EffectiveTo >= payroll.CreatedDT));

                decimal grossSalary = 0;
                decimal baseSalary = employee.BaseSalary ?? 0;
                decimal pieceQty = timesheetLine.TotalQuantity ?? 0;

                if (staffingPlan != null && staffingPlan.IsPieceRate)
                {
                    // Piece rate: calculate dan piece quantity
                    if (timesheetLine.ProductID.HasValue)
                    {
                        // Olish piece rate tariff uchun this product va position
                        var tariff = await db.HR_PIECE_RATE_TARIFFS
                            .Where(t => t.ProductID == timesheetLine.ProductID.Value &&
                                       (t.PositionID == employee.PositionID || t.PositionID == null) &&
                                       t.IsActive &&
                                       t.IsDeleted != true &&
                                       t.EffectiveFrom <= payroll.CreatedDT &&
                                       (t.EffectiveTo == null || t.EffectiveTo >= payroll.CreatedDT))
                            .OrderByDescending(t => t.PositionID) // Prefer position-specific over general
                            .FirstOrDefaultAsync();
                        
                        if (tariff != null)
                        {
                            grossSalary = pieceQty * tariff.RatePerUnit;
                        }
                        else
                        {
                            grossSalary = baseSalary; // Fallback ga base salary
                        }
                    }
                    else
                    {
                        // No product specified, use base salary
                        grossSalary = baseSalary;
                    }
                }
                else
                {
                    // No staffing plan, use base salary
                    grossSalary = baseSalary;
                }

                var payrollLine = new HR_PAYROLL_LINES
                {
                    PayrollID = payrollId,
                    EmployeeID = employee.ID,
                    BaseSalary = baseSalary,
                    HoursWorked = 0,
                    PieceQty = pieceQty,
                    HourlyRate = 0,
                    GrossSalary = grossSalary,
                    Bonuses = 0,
                    Penalties = 0,
                    NetSalary = grossSalary,
                    CurrencyID = defaultCurrencyID.Value
                };

                db.HR_PAYROLL_LINES.Add(payrollLine);
                createdCount++;
            }

            await db.SaveChangesAsync();
            var orgUnitName = payroll.HR_ORG_UNITS?.Name ?? "Butun kompaniya";
            logger.Log("HrPayroll", "GeneratePieceRateLines", $"Hisob: {payroll.Year}-{payroll.Month:D2}, Bo'lim: {orgUnitName}, YangiSatrlar: {createdCount}");
        }

        private async Task GeneratePieceRatePayrollLinesFromWorkEntries(int payrollId, int year, int month, int? orgUnitId)
        {
            var payroll = await db.HR_PAYROLL_HEADERS.FindAsync(payrollId);
            
            int createdCount = 0;

            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Olish work entries uchun this period
            IQueryable<HR_PIECE_RATE_WORK_ENTRIES> query = db.HR_PIECE_RATE_WORK_ENTRIES
                .Include(e => e.HR_EMPLOYEES)
                .Include(e => e.PRODUCT)
                .Include(e => e.HR_PIECE_RATE_OPERATIONS)
                .Where(e => e.WorkDate >= startDate &&
                            e.WorkDate <= endDate &&
                            (e.IsDeleted == null || e.IsDeleted == false));

            if (orgUnitId.HasValue)
            {
                query = query.Where(e => e.HR_EMPLOYEES.OrgUnitID == orgUnitId.Value);
            }

            var workEntries = await query.ToListAsync();

            // Olish standart currency
            var defaultCurrency = await db.CURRENCIES.OrderBy(c => c.ID).FirstOrDefaultAsync();
            int? defaultCurrencyID = defaultCurrency?.ID;

            if (!defaultCurrencyID.HasValue)
            {
                throw new Exception("Valyuta jadvalida hech qanday valyuta topilmadi. Iltimos, avval valyuta qo'shing.");
            }

            // Group by employee ga calculate total amounts
            var employeeGroups = workEntries
                .GroupBy(e => e.EmployeeID)
                .ToList();

            foreach (var employeeGroup in employeeGroups)
            {
                var firstEntry = employeeGroup.First();
                var employee = firstEntry.HR_EMPLOYEES;

                int? positionId = employee?.PositionID;
                decimal grossSalary = 0;
                decimal totalPieceQty = 0;

                // HrPieceRateTimesheets dagi hisoblash bilan bir xil usul: har bir entry uchun tarif topib summalash.
                foreach (var entry in employeeGroup)
                {
                    totalPieceQty += entry.Quantity;

                    var tariff = await db.HR_PIECE_RATE_TARIFFS
                        .Where(t => t.ProductID == entry.ProductID &&
                                   t.OperationID == entry.OperationID &&
                                   t.IsActive &&
                                   t.IsDeleted != true &&
                                   t.EffectiveFrom <= entry.WorkDate &&
                                   (t.EffectiveTo == null || t.EffectiveTo >= entry.WorkDate) &&
                                   (positionId.HasValue ? t.PositionID == positionId.Value : t.PositionID == null))
                        .OrderByDescending(t => t.PositionID) // Prefer position-specific
                        .FirstOrDefaultAsync();

                    // agar not found, try general tariff (PositionID == null)
                    if (tariff == null)
                    {
                        tariff = await db.HR_PIECE_RATE_TARIFFS
                            .Where(t => t.ProductID == entry.ProductID &&
                                       t.OperationID == entry.OperationID &&
                                       t.PositionID == null &&
                                       t.IsActive &&
                                       t.IsDeleted != true &&
                                       t.EffectiveFrom <= entry.WorkDate &&
                                       (t.EffectiveTo == null || t.EffectiveTo >= entry.WorkDate))
                            .FirstOrDefaultAsync();
                    }

                    if (tariff != null)
                    {
                        decimal entryAmount = entry.Quantity * tariff.RatePerUnit;
                        grossSalary += entryAmount;
                    }
                }

                // Tekshirish agar payroll line already mavjud (o'chirilgan qatorlar hisobga olinmaydi)
                var employeeId = firstEntry.EmployeeID;
                var existingLine = await db.HR_PAYROLL_LINES
                    .FirstOrDefaultAsync(l => l.PayrollID == payrollId && l.EmployeeID == employeeId && (l.IsDeleted == null || l.IsDeleted == false));

                if (existingLine == null)
                {
                    var payrollLine = new HR_PAYROLL_LINES
                    {
                        PayrollID = payrollId,
                        EmployeeID = employeeId,
                        BaseSalary = employee?.BaseSalary ?? 0,
                        HoursWorked = 0,
                        PieceQty = totalPieceQty,
                        HourlyRate = 0,
                        GrossSalary = grossSalary,
                        Bonuses = 0,
                        Penalties = 0,
                        NetSalary = grossSalary,
                        CurrencyID = defaultCurrencyID.Value
                    };

                    db.HR_PAYROLL_LINES.Add(payrollLine);
                    createdCount++;
                }
            }

            await db.SaveChangesAsync();
            var orgUnitName = payroll.HR_ORG_UNITS?.Name ?? "Butun kompaniya";
            logger.Log("HrPayroll", "GeneratePieceRateFromWorkEntries", $"Hisob: {year}-{month:D2}, Bo'lim: {orgUnitName}, YangiSatrlar: {createdCount}");
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

            var payroll = await db.HR_PAYROLL_HEADERS
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

            var payroll = await db.HR_PAYROLL_HEADERS.FindAsync(id);
            if (!IsPayrollHeaderActive(payroll))
            {
                TempData["Error"] = "Hisob topilmadi yoki allaqachon o'chirilgan";
                return RedirectToAction("Index");
            }

            // Tekshirish agar payroll is approved yoki posted - cannot o'chirish
            if (payroll.Status == "Approved" || payroll.Status == "Posted")
            {
                TempData["Error"] = "Tasdiqlangan yoki o'tkazilgan hisobni o'chirib bo'lmaydi";
                return RedirectToAction("Index");
            }

            // Soft o'chirish payroll lines birinchi
            var lines = await db.HR_PAYROLL_LINES.Where(l => l.PayrollID == id).ToListAsync();
            foreach (var line in lines)
            {
                line.IsDeleted = true;
                db.Entry(line).State = System.Data.Entity.EntityState.Modified;
            }

            payroll.IsDeleted = true;
            db.Entry(payroll).State = System.Data.Entity.EntityState.Modified;
            await db.SaveChangesAsync();

            logger.Log("HrPayroll", "Delete", $"Hisob: {payroll.Year}-{payroll.Month:D2}, Bo'lim: {payroll.HR_ORG_UNITS?.Name ?? "Butun kompaniya"}, Tur: {(payroll.PayrollType == "PieceRate" ? "Ishbay" : "Soatbay")}");
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Tasdiqda OrgUnitID null bo'lsa — butun kompaniya bo'yicha tasdiqlangan; har qanday bo'lim guruhiga mos keladi.
        /// </summary>
        private static bool PieceApprovalMatchesWorkGroup(HR_PIECE_RATE_TIMESHEET_APPROVALS a, int? workGroupOrgUnitId)
        {
            if (a.OrgUnitID == null) return true;
            return a.OrgUnitID == workGroupOrgUnitId;
        }

        private sealed class PieceRateGroupRow
        {
            public int Year { get; set; }
            public int Month { get; set; }
            public int? OrgUnitID { get; set; }
            public string OrgUnitName { get; set; }
            public int EntryCount { get; set; }
        }

        private sealed class PieceRateDropdownItem
        {
            public string ID { get; set; }
            public string Name { get; set; }
        }

        /// <summary>
        /// Ishbay dropdown: tasdiqlar + ish hajmi yozuvlari; tasdiq (null org) bo'lim yozuvlari bilan moslashadi.
        /// </summary>
        private async Task<List<PieceRateDropdownItem>> BuildPieceRateTimesheetDropdownItemsAsync(int year, int month, int? orgUnitIdFilter)
        {
            var approvedTimesheets = (await db.HR_PIECE_RATE_TIMESHEET_APPROVALS
                .Include(a => a.HR_ORG_UNITS)
                .Where(a => a.Year == year && a.Month == month &&
                    a.Status == "Approved" &&
                    (a.IsDeleted == null || a.IsDeleted == false) &&
                    (orgUnitIdFilter == null ? true : (a.OrgUnitID == null || a.OrgUnitID == orgUnitIdFilter.Value)))
                .ToListAsync())
                .OrderBy(a => a.HR_ORG_UNITS != null ? a.HR_ORG_UNITS.Name : "")
                .ToList();

            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            IQueryable<HR_PIECE_RATE_WORK_ENTRIES> query = db.HR_PIECE_RATE_WORK_ENTRIES
                .Include(e => e.HR_EMPLOYEES)
                .Include(e => e.HR_EMPLOYEES.HR_ORG_UNITS)
                .Where(e => e.WorkDate >= startDate &&
                            e.WorkDate <= endDate &&
                            (e.IsDeleted == null || e.IsDeleted == false));

            if (orgUnitIdFilter.HasValue)
            {
                query = query.Where(e => e.HR_EMPLOYEES.OrgUnitID == orgUnitIdFilter.Value);
            }

            var workEntries = await query.ToListAsync();

            var groupedFromWork = workEntries
                .GroupBy(e => new
                {
                    Year = e.WorkDate.Year,
                    Month = e.WorkDate.Month,
                    OrgUnitID = e.HR_EMPLOYEES.OrgUnitID
                })
                .Select(g => new PieceRateGroupRow
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    OrgUnitID = g.Key.OrgUnitID,
                    OrgUnitName = g.First().HR_EMPLOYEES?.HR_ORG_UNITS?.Name ?? "Butun kompaniya",
                    EntryCount = g.Count()
                })
                .Where(g => approvedTimesheets.Any(a => a.Year == g.Year && a.Month == g.Month && PieceApprovalMatchesWorkGroup(a, g.OrgUnitID)))
                .ToList();

            var groupedData = groupedFromWork.ToList();

            foreach (var a in approvedTimesheets)
            {
                if (a.OrgUnitID == null)
                {
                    if (groupedData.Count == 0)
                    {
                        groupedData.Add(new PieceRateGroupRow
                        {
                            Year = a.Year,
                            Month = a.Month,
                            OrgUnitID = null,
                            OrgUnitName = "Butun kompaniya",
                            EntryCount = 0
                        });
                    }
                }
                else
                {
                    if (!groupedData.Any(g => g.OrgUnitID == a.OrgUnitID))
                    {
                        groupedData.Add(new PieceRateGroupRow
                        {
                            Year = a.Year,
                            Month = a.Month,
                            OrgUnitID = a.OrgUnitID,
                            OrgUnitName = a.HR_ORG_UNITS?.Name ?? "Butun kompaniya",
                            EntryCount = 0
                        });
                    }
                }
            }

            groupedData = groupedData.OrderBy(x => x.OrgUnitName ?? "").ToList();

            var timesheetList = groupedData.Select(g => new PieceRateDropdownItem
            {
                ID = $"{g.Year}-{g.Month}-{(g.OrgUnitID.HasValue ? g.OrgUnitID.Value.ToString() : "")}",
                Name = $"{g.OrgUnitName} - {g.Year}-{g.Month:D2} ({g.EntryCount} ta yozuv)"
            }).ToList();

            if (groupedData.Count > 1)
            {
                var totalEntries = groupedData.Sum(g => g.EntryCount);
                timesheetList.Insert(0, new PieceRateDropdownItem
                {
                    ID = "ALL",
                    Name = $"Ushbu oydagi barcha tabellar ({groupedData.Count} ta tabel, {totalEntries} ta yozuv)"
                });
            }

            return timesheetList;
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

