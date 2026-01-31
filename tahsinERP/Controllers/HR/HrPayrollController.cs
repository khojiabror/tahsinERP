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

        public HrPayrollController()
        {
            logger = new UserActionLogger(User?.Identity?.Name ?? "System");
        }

        // GET: HR/Payroll
        public async Task<ActionResult> Index(int? year, int? month, int? orgUnitId, string status, string payrollType)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            year = year ?? DateTime.Now.Year;
            month = month ?? DateTime.Now.Month;

            IQueryable<HR_PAYROLL_HEADERS> query = db.HR_PAYROLL_HEADERS
                .Where(p => p.IsDeleted == false || p.IsDeleted == null)
                .Include(p => p.HR_ORG_UNITS);

            if (year.HasValue)
            {
                query = query.Where(p => p.Year == year.Value);
            }

            if (month.HasValue)
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
                TotalGrossSalary = p.HR_PAYROLL_LINES.Sum(l => l.GrossSalary ?? 0),
                TotalNetSalary = p.HR_PAYROLL_LINES.Sum(l => l.NetSalary ?? 0)
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

        // GET: HR/Payroll/GenerateHourly
        public async Task<ActionResult> GenerateHourly(int? year, int? month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            year = year ?? DateTime.Now.Year;
            month = month ?? DateTime.Now.Month;

            // Get approved hourly timesheets (non-piece-rate)
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

        // POST: HR/Payroll/GenerateHourly
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> GenerateHourly(int year, int month, int? orgUnitId, int timesheetId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Check if payroll already exists
            var existing = await db.HR_PAYROLL_HEADERS
                .FirstOrDefaultAsync(p => p.Year == year && p.Month == month && 
                    p.PayrollType == "Hourly" &&
                    (orgUnitId == null ? p.OrgUnitID == null : p.OrgUnitID == orgUnitId.Value));

            if (existing != null)
            {
                return RedirectToAction("Edit", new { id = existing.ID });
            }

            var timesheet = await db.HR_TIMESHEET_HEADERS
                .Include(t => t.HR_TIMESHEET_LINES.Select(l => l.HR_EMPLOYEES))
                .FirstOrDefaultAsync(t => t.ID == timesheetId);

            if (timesheet == null || timesheet.Status != "Approved")
            {
                TempData["Error"] = "Tabel topilmadi yoki tasdiqlanmagan";
                return RedirectToAction("GenerateHourly");
            }

            // Create payroll header
            var payroll = new HR_PAYROLL_HEADERS
            {
                Year = year,
                Month = month,
                OrgUnitID = orgUnitId,
                Status = "Draft",
                TimesheetID = timesheetId,
                PayrollType = "Hourly",
                CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                CreatedDT = DateTime.Now
            };

            db.HR_PAYROLL_HEADERS.Add(payroll);
            await db.SaveChangesAsync();

            // Generate payroll lines for hourly employees only
            await GenerateHourlyPayrollLines(payroll.ID, timesheet);

            logger.Log("HrPayroll", "GenerateHourly", $"ID: {payroll.ID}, TimesheetID: {timesheetId}");
            return RedirectToAction("Edit", new { id = payroll.ID });
        }

        // GET: HR/Payroll/GetTimesheets (AJAX endpoint)
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
                    // Get piece-rate work entries grouped by year, month, and org unit
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

                    // Group by year, month, and org unit
                    var groupedData = workEntries
                        .GroupBy(e => new { 
                            Year = e.WorkDate.Year, 
                            Month = e.WorkDate.Month,
                            OrgUnitID = e.HR_EMPLOYEES.OrgUnitID
                        })
                        .Select(g => new
                        {
                            Year = g.Key.Year,
                            Month = g.Key.Month,
                            OrgUnitID = g.Key.OrgUnitID,
                            OrgUnitName = g.First().HR_EMPLOYEES.HR_ORG_UNITS?.Name ?? "Butun kompaniya",
                            EntryCount = g.Count()
                        })
                        .OrderBy(x => x.OrgUnitName)
                        .ToList();

                    // Create timesheet list for dropdown
                    timesheetList = groupedData.Select(g => new
                    {
                        ID = $"{g.Year}-{g.Month}-{g.OrgUnitID}",
                        Name = $"{g.OrgUnitName} - {g.Year}-{g.Month:D2} ({g.EntryCount} ta yozuv)"
                    }).Cast<object>().ToList();
                }
                else
                {
                    // Get approved hourly timesheets (non-piece-rate)
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

        // GET: HR/Payroll/GeneratePieceRate
        public async Task<ActionResult> GeneratePieceRate(int? year, int? month, int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            year = year ?? DateTime.Now.Year;
            month = month ?? DateTime.Now.Month;

            // Get piece-rate work entries grouped by year, month, and org unit
            var startDate = new DateTime(year.Value, month.Value, 1);
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

            // Group by year, month, and org unit
            var groupedData = workEntries
                .GroupBy(e => new { 
                    Year = e.WorkDate.Year, 
                    Month = e.WorkDate.Month,
                    OrgUnitID = e.HR_EMPLOYEES.OrgUnitID
                })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    OrgUnitID = g.Key.OrgUnitID,
                    OrgUnitName = g.First().HR_EMPLOYEES.HR_ORG_UNITS?.Name ?? "Butun kompaniya",
                    EntryCount = g.Count()
                })
                .OrderBy(x => x.OrgUnitName)
                .ToList();

            // Create timesheet list for dropdown (using a composite key: Year-Month-OrgUnitID)
            var timesheetList = groupedData.Select(g => new
            {
                ID = $"{g.Year}-{g.Month}-{g.OrgUnitID}",
                Name = $"{g.OrgUnitName} - {g.Year}-{g.Month:D2} ({g.EntryCount} ta yozuv)"
            }).ToList();

            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.PayrollType = "PieceRate";
            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnitId);
            ViewBag.TimesheetID = new SelectList(
                timesheetList,
                "ID", "Name");

            return View("Generate");
        }

        // POST: HR/Payroll/GeneratePieceRate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> GeneratePieceRate(int year, int month, int? orgUnitId, string timesheetId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Parse timesheetId (format: "Year-Month-OrgUnitID")
            if (string.IsNullOrEmpty(timesheetId))
            {
                TempData["Error"] = "Tabel tanlanmagan";
                return RedirectToAction("GeneratePieceRate", new { year, month, orgUnitId });
            }

            var parts = timesheetId.Split('-');
            if (parts.Length != 3)
            {
                TempData["Error"] = "Noto'g'ri tabel formati";
                return RedirectToAction("GeneratePieceRate", new { year, month, orgUnitId });
            }

            int parsedYear = int.Parse(parts[0]);
            int parsedMonth = int.Parse(parts[1]);
            int? parsedOrgUnitId = string.IsNullOrEmpty(parts[2]) ? (int?)null : int.Parse(parts[2]);

            // Verify the parsed values match the form values
            if (parsedYear != year || parsedMonth != month || parsedOrgUnitId != orgUnitId)
            {
                TempData["Error"] = "Tabel ma'lumotlari mos kelmaydi";
                return RedirectToAction("GeneratePieceRate", new { year, month, orgUnitId });
            }

            // Check if payroll already exists
            var existing = await db.HR_PAYROLL_HEADERS
                .FirstOrDefaultAsync(p => p.Year == year && p.Month == month && 
                    p.PayrollType == "PieceRate" &&
                    (orgUnitId == null ? p.OrgUnitID == null : p.OrgUnitID == orgUnitId.Value));

            if (existing != null)
            {
                return RedirectToAction("Edit", new { id = existing.ID });
            }

            // Create payroll header (no TimesheetID for piece-rate, as it's based on work entries)
            var payroll = new HR_PAYROLL_HEADERS
            {
                Year = year,
                Month = month,
                OrgUnitID = orgUnitId,
                Status = "Draft",
                TimesheetID = null, // Piece-rate doesn't use HR_TIMESHEET_HEADERS
                PayrollType = "PieceRate",
                CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                CreatedDT = DateTime.Now
            };

            db.HR_PAYROLL_HEADERS.Add(payroll);
            await db.SaveChangesAsync();

            // Generate payroll lines for piece-rate employees based on work entries
            await GeneratePieceRatePayrollLinesFromWorkEntries(payroll.ID, year, month, orgUnitId);

            logger.Log("HrPayroll", "GeneratePieceRate", $"ID: {payroll.ID}, TimesheetID: {timesheetId}");
            return RedirectToAction("Edit", new { id = payroll.ID });
        }

        // GET: HR/Payroll/Edit/5
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

            var payroll = await db.HR_PAYROLL_HEADERS
                .Include(p => p.HR_ORG_UNITS)
                .Include(p => p.HR_PAYROLL_LINES.Select(l => l.HR_EMPLOYEES.HR_POSITIONS))
                .Include(p => p.HR_PAYROLL_LINES.Select(l => l.HR_EMPLOYEES.HR_ORG_UNITS))
                .Include(p => p.HR_PAYROLL_LINES.Select(l => l.CURRENCy))
                .FirstOrDefaultAsync(p => p.ID == id);

            if (payroll == null)
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
                PayrollType = payroll.PayrollType ?? "Hourly",
                TimesheetID = payroll.TimesheetID,
                CreatedByUserID = payroll.CreatedByUserID,
                CreatedDT = payroll.CreatedDT,
                ApprovedByUserID = payroll.ApprovedByUserID,
                ApprovedDT = payroll.ApprovedDT
            };

            viewModel.Lines = payroll.HR_PAYROLL_LINES.Select(l => new HrPayrollLineViewModel
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

            viewModel.TotalGrossSalary = viewModel.Lines.Sum(l => l.GrossSalary);
            viewModel.TotalNetSalary = viewModel.Lines.Sum(l => l.NetSalary);

            // For posting modal
            ViewBag.AccountList = new SelectList(
                await db.FIN_ACCOUNTS.Where(a => a.IsActive).OrderBy(a => a.Code).ToListAsync(),
                "ID", "Name");

            ViewBag.CategoryList = new SelectList(
                await db.FIN_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(),
                "ID", "Name");

            logger.Log("HrPayroll", "Edit", $"ID: {id}");
            return View(viewModel);
        }

        // POST: HR/Payroll/UpdateLine
        [HttpPost]
        public async Task<JsonResult> UpdateLine(int lineId, decimal? bonuses, decimal? penalties, string notes)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPayroll"))
            {
                return Json(new { success = false, message = "Нет прав доступа" });
            }

            try
            {
                var line = await db.HR_PAYROLL_LINES.FindAsync(lineId);
                if (line == null)
                {
                    return Json(new { success = false, message = "Строка не найдена" });
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

                // Recalculate net salary
                line.NetSalary = (line.GrossSalary ?? 0) + (line.Bonuses ?? 0) - (line.Penalties ?? 0);

                db.Entry(line).State = EntityState.Modified;
                await db.SaveChangesAsync();

                logger.Log("HrPayroll", "UpdateLine", $"LineID: {lineId}");
                return Json(new { success = true, netSalary = line.NetSalary });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: HR/Payroll/Approve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Approve(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var payroll = await db.HR_PAYROLL_HEADERS.FindAsync(id);
            if (payroll == null)
            {
                return HttpNotFound();
            }

            if (payroll.Status != "Draft")
            {
                TempData["Error"] = "Можно утверждать только расчеты в статусе 'Черновик'";
                return RedirectToAction("Edit", new { id });
            }

            payroll.Status = "Approved";
            payroll.ApprovedByUserID = UserHelper.GetUserId(User.Identity.Name);
            payroll.ApprovedDT = DateTime.Now;

            db.Entry(payroll).State = EntityState.Modified;
            await db.SaveChangesAsync();

            logger.Log("HrPayroll", "Approve", $"ID: {id}");
            return RedirectToAction("Index");
        }

        // POST: HR/Payroll/Post/5
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

            if (payroll == null)
            {
                return HttpNotFound();
            }

            if (payroll.Status != "Approved")
            {
                TempData["Error"] = "Можно провести только утвержденные расчеты";
                return RedirectToAction("Edit", new { id });
            }

            if (!accountId.HasValue || !categoryId.HasValue)
            {
                TempData["Error"] = "Необходимо указать счет и категорию для проводки";
                return RedirectToAction("Edit", new { id });
            }

            try
            {
                // Create financial transaction for total payroll amount
                var totalAmount = payroll.HR_PAYROLL_LINES.Sum(l => l.NetSalary ?? 0);
                var currencyId = payroll.HR_PAYROLL_LINES.FirstOrDefault()?.CurrencyID ?? 1;

                var newId = new System.Data.Entity.Core.Objects.ObjectParameter("newID", typeof(int));
                db.sp_Fin_AddTransaction(
                    txnDate: DateTime.Now,
                    accountID: accountId.Value,
                    categoryID: categoryId.Value,
                    direction: (byte)1, // 1 = Expense (расход)
                    amount: totalAmount,
                    currencyID: currencyId,
                    qty: null,
                    pricePerUnit: null,
                    counterpartyType: null,
                    counterpartyID: null,
                    counterpartyName: "Сотрудники",
                    purpose: $"Расчет заработной платы за {payroll.Year}-{payroll.Month:D2}",
                    sourceDocType: "HR_PAYROLL",
                    sourceDocID: id,
                    contractNo: null,
                    comment: $"Проведение расчета заработной платы ID: {id}",
                    createdByUserID: UserHelper.GetUserId(User.Identity.Name),
                    newID: newId
                );

                payroll.Status = "Posted";
                db.Entry(payroll).State = EntityState.Modified;
                await db.SaveChangesAsync();

                logger.Log("HrPayroll", "Post", $"ID: {id}, TransactionID: {newId.Value}");
                TempData["Success"] = "Расчет успешно проведен в финансовом модуле";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Ошибка при проведении: {ex.Message}";
                return RedirectToAction("Edit", new { id });
            }

            return RedirectToAction("Index");
        }

        // GET: HR/Payroll/Report/5
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

            if (payroll == null)
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

            viewModel.Lines = payroll.HR_PAYROLL_LINES.Select(l => new HrPayrollLineViewModel
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

        // GET: HR/Payroll/Export/5
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

            if (payroll == null)
            {
                return HttpNotFound();
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add($"Расчет {payroll.Year}-{payroll.Month:D2}");

                // Headers
                worksheet.Cells[1, 1].Value = "№";
                worksheet.Cells[1, 2].Value = "Таб.№";
                worksheet.Cells[1, 3].Value = "Ф.И.О.";
                worksheet.Cells[1, 4].Value = "Должность";
                worksheet.Cells[1, 5].Value = "Базовая зарплата";
                worksheet.Cells[1, 6].Value = "Отработано часов";
                worksheet.Cells[1, 7].Value = "Начислено";
                worksheet.Cells[1, 8].Value = "Премии";
                worksheet.Cells[1, 9].Value = "Штрафы";
                worksheet.Cells[1, 10].Value = "К выплате";
                worksheet.Cells[1, 11].Value = "Валюта";

                // Data
                int row = 2;
                int lineNum = 1;
                foreach (var line in payroll.HR_PAYROLL_LINES.OrderBy(l => l.HR_EMPLOYEES.TabNo))
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
                worksheet.Cells[row, 7].Value = payroll.HR_PAYROLL_LINES.Sum(l => l.GrossSalary ?? 0);
                worksheet.Cells[row, 8].Value = payroll.HR_PAYROLL_LINES.Sum(l => l.Bonuses ?? 0);
                worksheet.Cells[row, 9].Value = payroll.HR_PAYROLL_LINES.Sum(l => l.Penalties ?? 0);
                worksheet.Cells[row, 10].Value = payroll.HR_PAYROLL_LINES.Sum(l => l.NetSalary ?? 0);
                worksheet.Cells[row, 5].Value = "ИТОГО:";

                // Auto-fit columns
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                var fileName = $"Расчет_{payroll.Year}_{payroll.Month:D2}.xlsx";
                logger.Log("HrPayroll", "Export", $"ID: {id}");
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }

        private async Task GenerateHourlyPayrollLines(int payrollId, HR_TIMESHEET_HEADERS timesheet)
        {
            var payroll = await db.HR_PAYROLL_HEADERS.FindAsync(payrollId);
            
            // Get only hourly employees (non-piece-rate)
            var hourlyPositionIds = await db.HR_STAFFING_PLAN
                .Where(sp => sp.IsActive && sp.IsPieceRate == false)
                .Select(sp => sp.PositionID)
                .Distinct()
                .ToListAsync();

            var timesheetLines = await db.HR_TIMESHEET_LINES
                .Include(l => l.HR_EMPLOYEES)
                .Where(l => l.TimesheetID == timesheet.ID &&
                           (l.HR_EMPLOYEES.PositionID == null || hourlyPositionIds.Contains(l.HR_EMPLOYEES.PositionID ?? 0)))
                .ToListAsync();

            // Get default currency
            var defaultCurrency = await db.CURRENCIES.OrderBy(c => c.ID).FirstOrDefaultAsync();
            int? defaultCurrencyID = defaultCurrency?.ID;

            if (!defaultCurrencyID.HasValue)
            {
                throw new Exception("Valyuta jadvalida hech qanday valyuta topilmadi. Iltimos, avval valyuta qo'shing.");
            }

            foreach (var timesheetLine in timesheetLines)
            {
                var employee = timesheetLine.HR_EMPLOYEES;
                
                // Get staffing plan for employee
                var staffingPlan = await db.HR_STAFFING_PLAN
                    .FirstOrDefaultAsync(sp => sp.OrgUnitID == employee.OrgUnitID &&
                                              sp.PositionID == employee.PositionID &&
                                              sp.IsActive &&
                                              sp.IsPieceRate == false &&
                                              sp.EffectiveFrom <= payroll.CreatedDT &&
                                              (sp.EffectiveTo == null || sp.EffectiveTo >= payroll.CreatedDT));

                decimal grossSalary = 0;
                decimal baseSalary = employee.BaseSalary ?? 0;
                decimal hoursWorked = timesheetLine.TotalWorkedHours ?? 0;
                decimal hourlyRate = 0;

                if (staffingPlan != null)
                {
                    if (staffingPlan.IsFixedSalary)
                    {
                        // Fixed salary: prorated by worked days
                        int daysInMonth = DateTime.DaysInMonth(payroll.Year, payroll.Month);
                        decimal workedDays = timesheetLine.TotalWorkedDays ?? 0;
                        grossSalary = baseSalary * (workedDays / daysInMonth);
                    }
                    else if (staffingPlan.IsHourly)
                    {
                        // Hourly: calculate from hours
                        hourlyRate = baseSalary / 160; // Assuming 160 hours per month standard
                        grossSalary = hourlyRate * hoursWorked;
                    }
                }
                else
                {
                    // Default: fixed salary prorated
                    int daysInMonth = DateTime.DaysInMonth(payroll.Year, payroll.Month);
                    decimal workedDays = timesheetLine.TotalWorkedDays ?? 0;
                    grossSalary = baseSalary * (workedDays / daysInMonth);
                }

                // Add overtime (if applicable)
                decimal overtimeHours = timesheetLine.OvertimeHours ?? 0;
                if (overtimeHours > 0 && hourlyRate > 0)
                {
                    grossSalary += overtimeHours * hourlyRate * 1.5m; // 1.5x for overtime
                }

                var payrollLine = new HR_PAYROLL_LINES
                {
                    PayrollID = payrollId,
                    EmployeeID = employee.ID,
                    BaseSalary = baseSalary,
                    HoursWorked = hoursWorked,
                    PieceQty = 0,
                    HourlyRate = hourlyRate,
                    GrossSalary = grossSalary,
                    Bonuses = 0,
                    Penalties = 0,
                    NetSalary = grossSalary,
                    CurrencyID = defaultCurrencyID.Value
                };

                db.HR_PAYROLL_LINES.Add(payrollLine);
            }

            await db.SaveChangesAsync();
        }

        private async Task GeneratePieceRatePayrollLines(int payrollId, HR_TIMESHEET_HEADERS timesheet)
        {
            var payroll = await db.HR_PAYROLL_HEADERS.FindAsync(payrollId);
            
            // Get only piece-rate employees
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

            // Get default currency
            var defaultCurrency = await db.CURRENCIES.OrderBy(c => c.ID).FirstOrDefaultAsync();
            int? defaultCurrencyID = defaultCurrency?.ID;

            if (!defaultCurrencyID.HasValue)
            {
                throw new Exception("Valyuta jadvalida hech qanday valyuta topilmadi. Iltimos, avval valyuta qo'shing.");
            }

            foreach (var timesheetLine in timesheetLines)
            {
                var employee = timesheetLine.HR_EMPLOYEES;
                
                // Get staffing plan for employee
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
                    // Piece rate: calculate from piece quantity
                    if (timesheetLine.ProductID.HasValue)
                    {
                        // Get piece rate tariff for this product and position
                        var tariff = await db.HR_PIECE_RATE_TARIFFS
                            .Where(t => t.ProductID == timesheetLine.ProductID.Value &&
                                       (t.PositionID == employee.PositionID || t.PositionID == null) &&
                                       t.IsActive &&
                                       (t.IsDeleted == false || t.IsDeleted == null) &&
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
                            grossSalary = baseSalary; // Fallback to base salary
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
            }

            await db.SaveChangesAsync();
        }

        private async Task GeneratePieceRatePayrollLinesFromWorkEntries(int payrollId, int year, int month, int? orgUnitId)
        {
            var payroll = await db.HR_PAYROLL_HEADERS.FindAsync(payrollId);
            
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Get work entries for this period
            IQueryable<HR_PIECE_RATE_WORK_ENTRIES> query = db.HR_PIECE_RATE_WORK_ENTRIES
                .Include(e => e.HR_EMPLOYEES)
                .Include(e => e.PRODUCT)
                .Include(e => e.HR_PIECE_RATE_OPERATIONS)
                .Where(e => e.WorkDate >= startDate && e.WorkDate <= endDate);

            if (orgUnitId.HasValue)
            {
                query = query.Where(e => e.HR_EMPLOYEES.OrgUnitID == orgUnitId.Value);
            }

            var workEntries = await query.ToListAsync();

            // Get default currency
            var defaultCurrency = await db.CURRENCIES.OrderBy(c => c.ID).FirstOrDefaultAsync();
            int? defaultCurrencyID = defaultCurrency?.ID;

            if (!defaultCurrencyID.HasValue)
            {
                throw new Exception("Valyuta jadvalida hech qanday valyuta topilmadi. Iltimos, avval valyuta qo'shing.");
            }

            // Group by employee to calculate total amounts
            var employeeGroups = workEntries
                .GroupBy(e => e.EmployeeID)
                .ToList();

            foreach (var employeeGroup in employeeGroups)
            {
                var firstEntry = employeeGroup.First();
                var employee = firstEntry.HR_EMPLOYEES;
                
                if (employee == null) continue;

                // Get staffing plan for employee
                var staffingPlan = await db.HR_STAFFING_PLAN
                    .Where(sp => sp.OrgUnitID == employee.OrgUnitID &&
                                sp.PositionID == employee.PositionID &&
                                sp.IsActive &&
                                (sp.IsDeleted == false || sp.IsDeleted == null) &&
                                sp.IsPieceRate == true &&
                                sp.EffectiveFrom <= payroll.CreatedDT &&
                                (sp.EffectiveTo == null || sp.EffectiveTo >= payroll.CreatedDT))
                    .FirstOrDefaultAsync();

                decimal baseSalary = employee.BaseSalary ?? 0;
                decimal grossSalary = 0;
                decimal totalPieceQty = 0;

                // Calculate total piece-rate amount for this employee
                foreach (var entry in employeeGroup)
                {
                    int? positionId = employee.PositionID;
                    
                    // Get tariff for this product and operation
                    var tariff = await db.HR_PIECE_RATE_TARIFFS
                        .Where(t => t.ProductID == entry.ProductID &&
                                   t.OperationID == entry.OperationID &&
                                   t.IsActive &&
                                   (t.IsDeleted == false || t.IsDeleted == null) &&
                                   t.EffectiveFrom <= entry.WorkDate &&
                                   (t.EffectiveTo == null || t.EffectiveTo >= entry.WorkDate) &&
                                   (positionId.HasValue ? t.PositionID == positionId.Value : t.PositionID == null))
                        .OrderByDescending(t => t.PositionID) // Prefer position-specific
                        .FirstOrDefaultAsync();

                    // If not found, try general tariff (PositionID == null)
                    if (tariff == null)
                    {
                        tariff = await db.HR_PIECE_RATE_TARIFFS
                            .Where(t => t.ProductID == entry.ProductID &&
                                       t.OperationID == entry.OperationID &&
                                       t.PositionID == null &&
                                       t.IsActive &&
                                       (t.IsDeleted == false || t.IsDeleted == null) &&
                                       t.EffectiveFrom <= entry.WorkDate &&
                                       (t.EffectiveTo == null || t.EffectiveTo >= entry.WorkDate))
                            .FirstOrDefaultAsync();
                    }

                    if (tariff != null)
                    {
                        decimal entryAmount = entry.Quantity * tariff.RatePerUnit;
                        grossSalary += entryAmount;
                        totalPieceQty += entry.Quantity;
                    }
                }

                // If no tariff found for any entry, use base salary
                if (grossSalary == 0)
                {
                    grossSalary = baseSalary;
                }

                // Check if payroll line already exists for this employee
                var existingLine = await db.HR_PAYROLL_LINES
                    .FirstOrDefaultAsync(l => l.PayrollID == payrollId && l.EmployeeID == employee.ID);

                if (existingLine == null)
                {
                    var payrollLine = new HR_PAYROLL_LINES
                    {
                        PayrollID = payrollId,
                        EmployeeID = employee.ID,
                        BaseSalary = baseSalary,
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
                }
            }

            await db.SaveChangesAsync();
        }

        // GET: HR/Payroll/Delete/5
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

            if (payroll == null)
            {
                return HttpNotFound();
            }

            return View(payroll);
        }

        // POST: HR/Payroll/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPayroll"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var payroll = await db.HR_PAYROLL_HEADERS.FindAsync(id);
            if (payroll == null)
            {
                return HttpNotFound();
            }

            // Check if payroll is approved or posted - cannot delete
            if (payroll.Status == "Approved" || payroll.Status == "Posted")
            {
                TempData["Error"] = "Tasdiqlangan yoki o'tkazilgan hisobni o'chirib bo'lmaydi";
                return RedirectToAction("Index");
            }

            // Soft delete payroll lines first
            var lines = await db.HR_PAYROLL_LINES.Where(l => l.PayrollID == id).ToListAsync();
            foreach (var line in lines)
            {
                line.IsDeleted = true;
                db.Entry(line).State = System.Data.Entity.EntityState.Modified;
            }

            payroll.IsDeleted = true;
            db.Entry(payroll).State = System.Data.Entity.EntityState.Modified;
            await db.SaveChangesAsync();

            logger.Log("HrPayroll", "Delete", $"ID: {id}");
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

