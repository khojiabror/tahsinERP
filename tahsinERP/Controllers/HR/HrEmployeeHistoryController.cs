using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.HR;
using tahsinERP.ViewModels.HR;
using tahsinERP.I18n;

namespace tahsinERP.Controllers.HR
{
    [Authorize]
    public class HrEmployeeHistoryController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();
        private IUserActionLogger logger;
        private EmployeeTenureService tenureService;

        public HrEmployeeHistoryController()
        {
            logger = new UserActionLogger(User?.Identity?.Name ?? "System");
            tenureService = new EmployeeTenureService(db);
        }

        // Olish so'rovi: HR/EmployeeHistory/Index (Xodimlar staji)
        public async Task<ActionResult> Index(int? orgUnitId, int? positionId, DateTime? fromDate, DateTime? toDate, string status = null)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            IQueryable<HR_EMPLOYEES> employeesQuery = db.HR_EMPLOYEES.AsNoTracking();
            if (status == "active")
            {
                employeesQuery = employeesQuery.Where(e => e.IsActive);
            }
            else if (status == "inactive")
            {
                employeesQuery = employeesQuery.Where(e => !e.IsActive);
            }

            // Filtrlar SQL'da (avval hamma xodim va tarix yuklanib, xotirada filtrlanardi, HRM-P03).
            // Filtr berilsa — kamida bitta tarix qatori barcha filtrlarga mos keladigan xodimlar, faqat mos qatorlar bilan.
            bool anyFilter = orgUnitId.HasValue || positionId.HasValue || fromDate.HasValue || toDate.HasValue;
            IQueryable<HR_EMPLOYEE_HISTORY> matching = db.HR_EMPLOYEE_HISTORY;
            if (orgUnitId.HasValue) matching = matching.Where(h => h.FromOrgUnitID == orgUnitId.Value);
            if (positionId.HasValue) matching = matching.Where(h => h.FromPositionID == positionId.Value);
            if (fromDate.HasValue) matching = matching.Where(h => h.EffectiveDateFrom >= fromDate.Value);
            if (toDate.HasValue) matching = matching.Where(h => h.EffectiveDateFrom <= toDate.Value);
            if (anyFilter)
            {
                var matchingEmployeeIds = matching.Select(h => h.EmployeeID);
                employeesQuery = employeesQuery.Where(e => matchingEmployeeIds.Contains(e.ID));
            }

            var employees = await employeesQuery
                .Include(e => e.HR_ORG_UNITS)
                .Include(e => e.HR_POSITIONS)
                .OrderBy(e => e.FullName)
                .ToListAsync();

            var employeeIdsQuery = employeesQuery.Select(e => e.ID);
            var allHistory = await db.HR_EMPLOYEE_HISTORY.AsNoTracking()
                .Include(h => h.HR_ORG_UNITS)
                .Include(h => h.HR_POSITIONS)
                .Where(h => employeeIdsQuery.Contains(h.EmployeeID))
                .ToListAsync();
            var historyByEmployee = allHistory
                .GroupBy(h => h.EmployeeID)
                .ToDictionary(g => g.Key, g => g.OrderBy(h => h.EffectiveDateFrom).ToList());

            var groupedViewModels = new List<HrEmployeeHistoryGroupedViewModel>();
            foreach (var employee in employees)
            {
                List<HR_EMPLOYEE_HISTORY> records;
                if (!historyByEmployee.TryGetValue(employee.ID, out records))
                {
                    records = new List<HR_EMPLOYEE_HISTORY>();
                }

                // Jami staj — xotirada, birlashtirilgan davrlar bo'yicha (avval har xodimga 2 so'rov)
                var totalTenure = EmployeeTenureService.CalculateTotalTenure(employee, records);
                var positions = new List<HrEmployeeHistoryViewModel>();

                // Tarixsiz faol xodim — joriy lavozim (ID = 0: tahrirlanadigan tarix qatori yo'q)
                if (!records.Any() && employee.IsActive && employee.EmploymentDate.HasValue)
                {
                    var currentTenure = tenureService.CalculateTenure(employee.EmploymentDate.Value, DateTime.Now);
                    positions.Add(new HrEmployeeHistoryViewModel
                    {
                        ID = 0,
                        EmployeeID = employee.ID,
                        EmployeeName = employee.FullName,
                        TabNo = employee.TabNo,
                        FromOrgUnitName = employee.HR_ORG_UNITS?.Name,
                        FromPositionName = employee.HR_POSITIONS?.Name,
                        EffectiveDate = employee.EmploymentDate.Value,
                        EffectiveDateTo = null,
                        Reason = L.T("Hr_HistCurrentPosition"),
                        TenureYears = currentTenure.Years,
                        TenureMonths = currentTenure.Months,
                        TenureDays = currentTenure.Days
                    });
                }

                foreach (var h in records)
                {
                    if (orgUnitId.HasValue && h.FromOrgUnitID != orgUnitId.Value) continue;
                    if (positionId.HasValue && h.FromPositionID != positionId.Value) continue;
                    if (fromDate.HasValue && h.EffectiveDateFrom < fromDate.Value) continue;
                    if (toDate.HasValue && h.EffectiveDateFrom > toDate.Value) continue;

                    DateTime? effectiveDateTo = h.EffectiveDateTo;
                    if (!effectiveDateTo.HasValue)
                    {
                        if (employee.IsActive)
                        {
                            effectiveDateTo = DateTime.Now;
                        }
                        else
                        {
                            var nextRecord = records.FirstOrDefault(h2 => h2.EffectiveDateFrom > h.EffectiveDateFrom);
                            effectiveDateTo = nextRecord?.EffectiveDateFrom ?? employee.DismissalDate;
                        }
                    }

                    var tenureResult = tenureService.CalculateTenure(h.EffectiveDateFrom, effectiveDateTo);
                    positions.Add(new HrEmployeeHistoryViewModel
                    {
                        ID = h.ID,
                        EmployeeID = h.EmployeeID,
                        EmployeeName = employee.FullName,
                        TabNo = employee.TabNo,
                        FromOrgUnitName = h.HR_ORG_UNITS?.Name,
                        FromPositionName = h.HR_POSITIONS?.Name,
                        EffectiveDate = h.EffectiveDateFrom,
                        EffectiveDateTo = effectiveDateTo,
                        Reason = h.Reason,
                        TenureYears = tenureResult.Years,
                        TenureMonths = tenureResult.Months,
                        TenureDays = tenureResult.Days
                    });
                }

                if (positions.Any())
                {
                    groupedViewModels.Add(new HrEmployeeHistoryGroupedViewModel
                    {
                        EmployeeID = employee.ID,
                        EmployeeName = employee.FullName,
                        TabNo = employee.TabNo,
                        Positions = positions,
                        TotalTenureYears = totalTenure.Years,
                        TotalTenureMonths = totalTenure.Months,
                        TotalTenureDays = totalTenure.Days
                    });
                }
            }

            var viewModels = groupedViewModels.SelectMany(g => g.Positions).ToList();

            ViewBag.OrgUnitID = HrLookups.SelectList(HrLookups.OrgUnits, orgUnitId);
            ViewBag.PositionID = HrLookups.SelectList(HrLookups.Positions, positionId);
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.Status = status;
            ViewBag.GroupedViewModels = groupedViewModels;

            logger.Log("HrEmployeeHistory", "Index");
            return View(viewModels);
        }

        // Olish so'rovi: HR/EmployeeHistory/yaratish
        public async Task<ActionResult> Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            await PopulateViewBags();
            logger.Log("HrEmployeeHistory", "Create");
            return View();
        }

        private static DateTime? ParseFormDate(string value)
        {
            DateTime parsed;
            return !string.IsNullOrEmpty(value) && DateTime.TryParse(value, out parsed) ? parsed : (DateTime?)null;
        }

        // POST so'rovi: HR/EmployeeHistory/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(HR_EMPLOYEE_HISTORY history)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (!ModelState.IsValid)
            {
                await PopulateViewBags(history);
                return View(history);
            }

            var effectiveDateTo = ParseFormDate(Request.Form["EffectiveDateTo"]);
            if (effectiveDateTo.HasValue && effectiveDateTo.Value < history.EffectiveDateFrom)
            {
                ModelState.AddModelError("", L.T("Hr_EndBeforeStart"));
                await PopulateViewBags(history);
                return View(history);
            }

            var employee = await db.HR_EMPLOYEES.FindAsync(history.EmployeeID);

            // EF orqali (EffectiveDateTo entity'da bor — avvalgi raw SQL INSERT keraksiz edi, HRM-M01)
            db.HR_EMPLOYEE_HISTORY.Add(new HR_EMPLOYEE_HISTORY
            {
                EmployeeID = history.EmployeeID,
                ActionType = EmployeeHistoryService.ActionHistory,
                FromOrgUnitID = history.FromOrgUnitID,
                FromPositionID = history.FromPositionID,
                ToOrgUnitID = null,
                ToPositionID = null,
                EffectiveDateFrom = history.EffectiveDateFrom,
                EffectiveDateTo = effectiveDateTo,
                Reason = history.Reason,
                CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                CreatedDT = DateTime.Now
            });

            // Tugash sanasi xodimning bo'shatish sanasidan keyin bo'lsa — bo'shatish sanasi yangilanadi (avvalgi xatti-harakat)
            if (employee != null && effectiveDateTo.HasValue &&
                (!employee.DismissalDate.HasValue || effectiveDateTo.Value > employee.DismissalDate.Value))
            {
                employee.DismissalDate = effectiveDateTo.Value;
                employee.IsActive = false;
                employee.UpdatedDT = DateTime.Now;
            }

            await db.SaveChangesAsync();

            logger.Log("HrEmployeeHistory", "Create", $"Xodim: {employee?.FullName ?? "-"} ({employee?.TabNo ?? "-"}), Bo'lim: {HrLookups.NameOf(HrLookups.OrgUnits, history.FromOrgUnitID) ?? "-"}, Lavozim: {HrLookups.NameOf(HrLookups.Positions, history.FromPositionID) ?? "-"}, Sabab: {history.Reason ?? "-"}");
            TempData["Success"] = L.T("Hr_HistAdded");
            return RedirectToAction("Index");
        }

        // Olish so'rovi: HR/EmployeeHistory/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var history = await db.HR_EMPLOYEE_HISTORY.AsNoTracking().FirstOrDefaultAsync(h => h.ID == id.Value);
            if (history == null)
            {
                return HttpNotFound();
            }

            ViewBag.EffectiveDateTo = history.EffectiveDateTo?.ToString("yyyy-MM-dd");
            ViewBag.EffectiveDateFrom = history.EffectiveDateFrom.ToString("yyyy-MM-dd");
            await PopulateViewBags(history);

            var employee = await db.HR_EMPLOYEES.AsNoTracking().Where(e => e.ID == history.EmployeeID)
                .Select(e => new { e.FullName, e.TabNo }).FirstOrDefaultAsync();
            logger.Log("HrEmployeeHistory", "Edit", $"Xodim: {employee?.FullName ?? "-"} ({employee?.TabNo ?? "-"}), Sana: {history.EffectiveDateFrom:dd.MM.yyyy}");
            return View(history);
        }

        // POST so'rovi: HR/EmployeeHistory/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(HR_EMPLOYEE_HISTORY history)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (!ModelState.IsValid)
            {
                await PopulateViewBags(history);
                return View(history);
            }

            var existingHistory = await db.HR_EMPLOYEE_HISTORY.FindAsync(history.ID);
            if (existingHistory == null)
            {
                return HttpNotFound();
            }

            var effectiveDateTo = ParseFormDate(Request.Form["EffectiveDateTo"]);
            if (effectiveDateTo.HasValue && effectiveDateTo.Value < history.EffectiveDateFrom)
            {
                ModelState.AddModelError("", L.T("Hr_EndBeforeStart"));
                await PopulateViewBags(history);
                ViewBag.EffectiveDateTo = effectiveDateTo?.ToString("yyyy-MM-dd");
                ViewBag.EffectiveDateFrom = history.EffectiveDateFrom.ToString("yyyy-MM-dd");
                return View(history);
            }

            // EF orqali (avval raw SQL UPDATE va xatoni yashiradigan try/catch, HRM-M01)
            existingHistory.EmployeeID = history.EmployeeID;
            existingHistory.FromOrgUnitID = history.FromOrgUnitID;
            existingHistory.FromPositionID = history.FromPositionID;
            // Formadagi tur va "Yangi bo'lim/lavozim" avval saqlanmasdi; yangi qiymatlar faqat ko'chirishda ma'noli
            bool isTransfer = history.ActionType == "Transfer";
            existingHistory.ActionType = history.ActionType;
            existingHistory.ToOrgUnitID = isTransfer ? history.ToOrgUnitID : null;
            existingHistory.ToPositionID = isTransfer ? history.ToPositionID : null;
            existingHistory.EffectiveDateFrom = history.EffectiveDateFrom;
            existingHistory.EffectiveDateTo = effectiveDateTo;
            existingHistory.Reason = history.Reason;
            await db.SaveChangesAsync();

            var employee = await db.HR_EMPLOYEES.AsNoTracking().Where(e => e.ID == history.EmployeeID)
                .Select(e => new { e.FullName, e.TabNo }).FirstOrDefaultAsync();
            logger.Log("HrEmployeeHistory", "Edit", $"Xodim: {employee?.FullName ?? "-"} ({employee?.TabNo ?? "-"}), Sana: {history.EffectiveDateFrom:dd.MM.yyyy}");
            TempData["Success"] = L.T("Hr_HistUpdated");
            return RedirectToAction("Index");
        }

        private async Task PopulateViewBags(HR_EMPLOYEE_HISTORY history = null)
        {
            ViewBag.EmployeeID = new SelectList(
                await db.HR_EMPLOYEES.AsNoTracking()
                    .OrderBy(e => e.FullName)
                    .Select(e => new { e.ID, e.FullName })
                    .ToListAsync(),
                "ID", "FullName", history?.EmployeeID);

            // Tarix eski (nofaol) bo'lim/lavozimlarni ham ko'rsatishi kerak — shuning uchun to'liq ro'yxat
            var orgUnits = await db.HR_ORG_UNITS.AsNoTracking().OrderBy(o => o.Name).Select(o => new { o.ID, o.Name }).ToListAsync();
            var positions = await db.HR_POSITIONS.AsNoTracking().OrderBy(p => p.Name).Select(p => new { p.ID, p.Name }).ToListAsync();

            ViewBag.FromOrgUnitID = new SelectList(orgUnits, "ID", "Name", history?.FromOrgUnitID);
            ViewBag.FromPositionID = new SelectList(positions, "ID", "Name", history?.FromPositionID);
            ViewBag.ToOrgUnitID = new SelectList(orgUnits, "ID", "Name", history?.ToOrgUnitID);
            ViewBag.ToPositionID = new SelectList(positions, "ID", "Name", history?.ToPositionID);
        }

        // Avval Dispose yo'q edi — har so'rovda DbContext ochiq qolardi (HRM-P03)
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
