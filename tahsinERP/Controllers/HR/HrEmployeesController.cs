using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Common;
using tahsinERP.Services.HR;
using tahsinERP.ViewModels.HR;

namespace tahsinERP.Controllers.HR
{
    [Authorize]
    public class HrEmployeesController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();
        private IUserActionLogger logger;
        private readonly HrEmployeeAllowanceService allowanceService;

        public HrEmployeesController()
        {
            logger = new UserActionLogger(User?.Identity?.Name ?? "System");
            allowanceService = new HrEmployeeAllowanceService(db);
        }

        private static string OrgUnitLabel(int? orgUnitId) =>
            orgUnitId.HasValue ? HrLookups.NameOf(HrLookups.OrgUnits, orgUnitId) ?? "-" : "Butun kompaniya";

        private static string PositionLabel(int? positionId) =>
            positionId.HasValue ? HrLookups.NameOf(HrLookups.Positions, positionId) ?? "-" : "-";

        // Olish so'rovi: HR/Employees
        public async Task<ActionResult> Index(int? orgUnitId, int? positionId, string status)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            IQueryable<HR_EMPLOYEES> query = db.HR_EMPLOYEES.AsNoTracking();

            if (orgUnitId.HasValue)
            {
                query = query.Where(e => e.OrgUnitID == orgUnitId.Value);
            }

            if (positionId.HasValue)
            {
                query = query.Where(e => e.PositionID == positionId.Value);
            }

            // standart: faqat faol (bo'shatilmagan) xodimlar
            if (status == "InActive")
            {
                query = query.Where(e => !e.IsActive || e.DismissalDate != null);
            }
            else
            {
                query = query.Where(e => e.IsActive && e.DismissalDate == null);
            }

            // Joriy bo'lim/lavozim — xodim yozuvining o'zi (avvalgi "oxirgi transfer" bloki har doim shunga qaytardi
            // va har xodimga lazy-load so'rov yuborardi, HRM-P11)
            var viewModels = await query
                .OrderBy(e => e.FullName)
                .Select(e => new HrEmployeeViewModel
                {
                    ID = e.ID,
                    TabNo = e.TabNo,
                    FullName = e.FullName,
                    OrgUnitID = e.OrgUnitID,
                    OrgUnitName = e.HR_ORG_UNITS.Name,
                    PositionID = e.PositionID,
                    PositionName = e.HR_POSITIONS.Name,
                    BaseSalary = e.BaseSalary,
                    MaterialResponsibilityPercent = 0,
                    SpecialAllowancePercent = 0,
                    SalaryGradeID = e.SalaryGradeID,
                    SalaryGradeName = e.HR_SALARY_GRADES.Name,
                    EmploymentDate = e.EmploymentDate,
                    DismissalDate = e.DismissalDate,
                    IsActive = e.IsActive
                })
                .ToListAsync();

            ViewBag.Status = status;
            ViewBag.OrgUnitID = HrLookups.SelectList(HrLookups.OrgUnits, orgUnitId);
            ViewBag.PositionID = HrLookups.SelectList(HrLookups.Positions, positionId);

            logger.Log("HrEmployees", "Index");
            return View(viewModels);
        }

        // Olish so'rovi: HR/Employees/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var employee = await db.HR_EMPLOYEES.AsNoTracking()
                .Include(e => e.HR_ORG_UNITS)
                .Include(e => e.HR_POSITIONS)
                .Include(e => e.HR_SALARY_GRADES)
                .FirstOrDefaultAsync(e => e.ID == id);

            if (employee == null)
            {
                return HttpNotFound();
            }

            var viewModel = ToViewModel(employee);
            viewModel.OrgUnitName = employee.HR_ORG_UNITS?.Name;
            viewModel.PositionName = employee.HR_POSITIONS?.Name;
            viewModel.SalaryGradeName = employee.HR_SALARY_GRADES?.Name;
            ApplyAllowances(viewModel);

            // Tarix faqat history jadvalidan
            var historyList = await db.HR_EMPLOYEE_HISTORY.AsNoTracking()
                .Where(h => h.EmployeeID == id.Value)
                .OrderBy(h => h.EffectiveDateFrom)
                .Select(h => new HrEmployeeHistoryViewModel
                {
                    FromOrgUnitName = h.HR_ORG_UNITS.Name ?? "-",
                    FromPositionName = h.HR_POSITIONS.Name ?? "-",
                    EffectiveDate = h.EffectiveDateFrom,
                    EffectiveDateTo = h.EffectiveDateTo,
                    Reason = h.Reason ?? "-",
                    IsCurrentRow = !h.EffectiveDateTo.HasValue
                })
                .ToListAsync();

            if (historyList.Count == 0 && employee.EmploymentDate.HasValue && (employee.OrgUnitID.HasValue || employee.PositionID.HasValue))
            {
                historyList.Add(new HrEmployeeHistoryViewModel
                {
                    FromOrgUnitName = employee.HR_ORG_UNITS?.Name ?? "-",
                    FromPositionName = employee.HR_POSITIONS?.Name ?? "-",
                    EffectiveDate = employee.EmploymentDate.Value,
                    EffectiveDateTo = employee.DismissalDate,
                    Reason = "-",
                    IsCurrentRow = !employee.DismissalDate.HasValue
                });
            }

            ViewBag.History = historyList;
            ViewBag.HistoryOrgUnitID = HrLookups.SelectList(HrLookups.OrgUnits, employee.OrgUnitID);
            ViewBag.HistoryPositionID = HrLookups.SelectList(HrLookups.Positions, employee.PositionID);

            logger.Log("HrEmployees", "Details", $"Xodim: {viewModel.FullName} ({viewModel.TabNo ?? "-"})");
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddTenure(int employeeId, int fromOrgUnitId, int fromPositionId, DateTime effectiveDateFrom, DateTime? effectiveDateTo, string reason)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var employee = await db.HR_EMPLOYEES.FindAsync(employeeId);
            if (employee == null)
            {
                TempData["Error"] = "Xodim topilmadi";
                return RedirectToAction("Index");
            }

            if (effectiveDateTo.HasValue && effectiveDateTo.Value.Date < effectiveDateFrom.Date)
            {
                TempData["Error"] = "Tugash sanasi boshlanish sanasidan oldin bo'lmasligi kerak";
                return RedirectToAction("Details", new { id = employeeId });
            }

            // Agar yangi satr ochiq davr bo'lsa, oldingi ochiq satrni yopib qo'yamiz.
            if (!effectiveDateTo.HasValue)
            {
                var openHistory = await db.HR_EMPLOYEE_HISTORY
                    .Where(h => h.EmployeeID == employeeId && h.EffectiveDateTo == null)
                    .OrderByDescending(h => h.EffectiveDateFrom)
                    .FirstOrDefaultAsync();

                if (openHistory != null && openHistory.EffectiveDateFrom.Date <= effectiveDateFrom.Date)
                {
                    openHistory.EffectiveDateTo = effectiveDateFrom.Date.AddDays(-1);
                }
            }

            var history = new HR_EMPLOYEE_HISTORY
            {
                EmployeeID = employeeId,
                ActionType = "ManualTenure",
                FromOrgUnitID = fromOrgUnitId,
                FromPositionID = fromPositionId,
                ToOrgUnitID = null,
                ToPositionID = null,
                EffectiveDateFrom = effectiveDateFrom.Date,
                EffectiveDateTo = effectiveDateTo?.Date,
                Reason = string.IsNullOrWhiteSpace(reason) ? "Qo'lda staj qo'shildi" : reason.Trim(),
                CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                CreatedDT = DateTime.Now
            };

            db.HR_EMPLOYEE_HISTORY.Add(history);
            await db.SaveChangesAsync();

            logger.Log("HrEmployees", "AddTenure", $"Xodim: {employee.FullName} ({employee.TabNo ?? "-"}), Bo'lim: {OrgUnitLabel(fromOrgUnitId)}, Lavozim: {PositionLabel(fromPositionId)}");
            TempData["Success"] = "Staj davri muvaffaqiyatli qo'shildi";
            return RedirectToAction("Details", new { id = employeeId });
        }

        // Olish so'rovi: HR/Employees/yaratish
        public async Task<ActionResult> Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var viewModel = new HrEmployeeViewModel
            {
                MaterialResponsibilityPercent = 0,
                SpecialAllowancePercent = 0,
                IsActive = true
            };
            PopulateViewBags(viewModel);
            ViewBag.NextTabNo = await GenerateNextTabNo();

            return View(viewModel);
        }

        // POST so'rovi: HR/Employees/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(HrEmployeeViewModel viewModel)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (!ModelState.IsValid)
            {
                PopulateViewBags(viewModel);
                return View(viewModel);
            }

            HR_EMPLOYEES employee;
            HR_EMPLOYEE_HISTORY history = null;
            // Xodim + ustama + tarix — bitta tranzaksiyada; TabNo qulf ostida (HRM-M02, HRM-B17)
            using (var tx = db.Database.BeginTransaction())
            {
                await DocumentNumberService.AcquireLockAsync(db, "HR_EMPLOYEES.TabNo");

                string tabNo = string.IsNullOrWhiteSpace(viewModel.TabNo) ? await GenerateNextTabNo() : viewModel.TabNo.Trim();
                if (await db.HR_EMPLOYEES.AnyAsync(e => e.TabNo == tabNo))
                {
                    tx.Rollback();
                    ModelState.AddModelError("TabNo", "Табельный номер уже существует");
                    PopulateViewBags(viewModel);
                    return View(viewModel);
                }

                var userId = UserHelper.GetUserId(User.Identity.Name);
                employee = new HR_EMPLOYEES
                {
                    TabNo = tabNo,
                    FullName = viewModel.FullName,
                    OrgUnitID = viewModel.OrgUnitID,
                    PositionID = viewModel.PositionID,
                    BaseSalary = viewModel.BaseSalary,
                    SalaryGradeID = viewModel.SalaryGradeID,
                    EmploymentDate = viewModel.EmploymentDate,
                    DismissalDate = viewModel.DismissalDate,
                    IsActive = viewModel.IsActive,
                    CreatedByUserID = userId,
                    CreatedDT = DateTime.Now
                };

                db.HR_EMPLOYEES.Add(employee);
                await db.SaveChangesAsync();

                allowanceService.Upsert(
                    employee.ID,
                    viewModel.MaterialResponsibilityPercent ?? 0,
                    viewModel.SpecialAllowancePercent ?? 0);

                if (employee.OrgUnitID.HasValue && employee.PositionID.HasValue && employee.EmploymentDate.HasValue)
                {
                    history = new HR_EMPLOYEE_HISTORY
                    {
                        EmployeeID = employee.ID,
                        ActionType = EmployeeHistoryService.ActionInitial,
                        FromOrgUnitID = employee.OrgUnitID,
                        FromPositionID = employee.PositionID,
                        ToOrgUnitID = null,
                        ToPositionID = null,
                        EffectiveDateFrom = employee.EmploymentDate.Value,
                        EffectiveDateTo = employee.DismissalDate,
                        Reason = "Ishga qabul qilingan",
                        CreatedByUserID = userId,
                        CreatedDT = DateTime.Now
                    };
                    db.HR_EMPLOYEE_HISTORY.Add(history);
                    await db.SaveChangesAsync();
                }

                tx.Commit();
            }

            if (history != null)
            {
                logger.Log("HrEmployees", "CreateHistory", $"Xodim: {employee.FullName} ({employee.TabNo ?? "-"}), Bo'lim: {OrgUnitLabel(employee.OrgUnitID)}, Lavozim: {PositionLabel(employee.PositionID)}, Sabab: {history.Reason}");
            }
            logger.Log("HrEmployees", "Create", $"Xodim: {employee.FullName} ({employee.TabNo ?? "-"}), Bo'lim: {OrgUnitLabel(employee.OrgUnitID)}, Lavozim: {PositionLabel(employee.PositionID)}");
            return RedirectToAction("Index");
        }

        // Olish so'rovi: HR/Employees/Edit/5
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

            var employee = await db.HR_EMPLOYEES.FindAsync(id);
            if (employee == null)
            {
                return HttpNotFound();
            }

            var viewModel = ToViewModel(employee);
            ApplyAllowances(viewModel);

            PopulateViewBags(viewModel);
            return View(viewModel);
        }

        // POST so'rovi: HR/Employees/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(HrEmployeeViewModel viewModel)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                // Bazadagi IX_HR_EMPLOYEES_TabNo (UNIQUE, IsActive = 1 filtri) bilan bir xil qoida (HRM-B17):
                // ishlayotgan xodimlar orasida takrorlanmasin. Bo'shaganlarning raqami qayta berilgan bo'lishi mumkin —
                // avvalgi qattiq tekshiruv shunday xodimlarni umuman tahrirlashga yo'l qo'ymasdi.
                viewModel.TabNo = viewModel.TabNo?.Trim();
                var duplicate = viewModel.IsActive && await db.HR_EMPLOYEES
                    .AnyAsync(e => e.TabNo == viewModel.TabNo && e.ID != viewModel.ID && e.IsActive);
                if (duplicate)
                {
                    ModelState.AddModelError("TabNo", "Табельный номер уже существует");
                    PopulateViewBags(viewModel);
                    return View(viewModel);
                }

                var employee = await db.HR_EMPLOYEES.FindAsync(viewModel.ID);
                if (employee == null)
                {
                    return HttpNotFound();
                }

                using (var tx = db.Database.BeginTransaction())
                {
                    employee.TabNo = viewModel.TabNo;
                    employee.FullName = viewModel.FullName;
                    employee.OrgUnitID = viewModel.OrgUnitID;
                    employee.PositionID = viewModel.PositionID;
                    employee.BaseSalary = viewModel.BaseSalary;
                    employee.SalaryGradeID = viewModel.SalaryGradeID;
                    employee.EmploymentDate = viewModel.EmploymentDate;
                    employee.DismissalDate = viewModel.DismissalDate;
                    employee.IsActive = viewModel.IsActive;
                    employee.UpdatedDT = DateTime.Now;
                    await db.SaveChangesAsync();

                    allowanceService.Upsert(
                        employee.ID,
                        viewModel.MaterialResponsibilityPercent ?? 0,
                        viewModel.SpecialAllowancePercent ?? 0);
                    tx.Commit();
                }

                logger.Log("HrEmployees", "Edit", $"Xodim: {employee.FullName} ({employee.TabNo ?? "-"}), Bo'lim: {OrgUnitLabel(employee.OrgUnitID)}, Lavozim: {PositionLabel(employee.PositionID)}");
                return RedirectToAction("Index");
            }

            PopulateViewBags(viewModel);
            return View(viewModel);
        }

        // POST so'rovi: HR/Employees/o'chirish/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var employee = await db.HR_EMPLOYEES.FindAsync(id);
            if (employee == null)
            {
                return HttpNotFound();
            }

            // Soft o'chirish — nofaol + bo'shatish sanasi; joriy tarix davri yopiladi (HRM-B06)
            var today = DateTime.Today;
            await EmployeeHistoryService.CloseCurrentPeriodAsync(db, employee, today,
                EmployeeHistoryService.ActionHistory, "O'chirildi", UserHelper.GetUserId(User.Identity.Name));
            employee.IsActive = false;
            employee.DismissalDate = employee.DismissalDate ?? today;
            employee.UpdatedDT = DateTime.Now;
            await db.SaveChangesAsync();

            logger.Log("HrEmployees", "Delete", $"Xodim: {employee.FullName} ({employee.TabNo ?? "-"}), Bo'lim: {OrgUnitLabel(employee.OrgUnitID)}, Lavozim: {PositionLabel(employee.PositionID)}");
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Keyingi TabNo: barcha raqamli TabNo'larning eng kattasi + 1, kamida 4 xonali (0001, 0010, 0100, 1000...).
        /// Avval faqat "0" bilan boshlanuvchi 4 xonali qiymatlar olinardi — 0999 dan keyin har safar 1000 qaytardi.
        /// Create'da DocumentNumberService qulfi ostida chaqiriladi (HRM-B17).
        /// </summary>
        private async Task<string> GenerateNextTabNo()
        {
            var tabNos = await db.HR_EMPLOYEES
                .Where(e => e.TabNo != null && e.TabNo.Length <= 9)
                .Select(e => e.TabNo)
                .ToListAsync();

            int maxTabNo = 0;
            foreach (var tabNo in tabNos)
            {
                int value;
                if (int.TryParse(tabNo.Trim(), out value) && value > maxTabNo)
                {
                    maxTabNo = value;
                }
            }

            return (maxTabNo + 1).ToString("D4");
        }

        private static HrEmployeeViewModel ToViewModel(HR_EMPLOYEES employee)
        {
            return new HrEmployeeViewModel
            {
                ID = employee.ID,
                TabNo = employee.TabNo,
                FullName = employee.FullName,
                OrgUnitID = employee.OrgUnitID,
                PositionID = employee.PositionID,
                BaseSalary = employee.BaseSalary,
                MaterialResponsibilityPercent = 0,
                SpecialAllowancePercent = 0,
                SalaryGradeID = employee.SalaryGradeID,
                EmploymentDate = employee.EmploymentDate,
                DismissalDate = employee.DismissalDate,
                IsActive = employee.IsActive
            };
        }

        private void ApplyAllowances(HrEmployeeViewModel viewModel)
        {
            var allowances = allowanceService.GetByEmployeeId(viewModel.ID);
            if (allowances != null)
            {
                viewModel.MaterialResponsibilityPercent = allowances.MaterialResponsibilityPercent;
                viewModel.SpecialAllowancePercent = allowances.SpecialAllowancePercent;
            }
        }

        private void PopulateViewBags(HrEmployeeViewModel viewModel)
        {
            ViewBag.OrgUnitID = HrLookups.SelectList(HrLookups.OrgUnits, viewModel.OrgUnitID);
            ViewBag.PositionID = HrLookups.SelectList(HrLookups.Positions, viewModel.PositionID);
            ViewBag.SalaryGradeID = HrLookups.SelectList(HrLookups.SalaryGrades, viewModel.SalaryGradeID);
        }

        // Olish so'rovi: HR/Employees/Dismiss/5
        public async Task<ActionResult> Dismiss(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var employee = await db.HR_EMPLOYEES.AsNoTracking()
                .Include(e => e.HR_ORG_UNITS)
                .Include(e => e.HR_POSITIONS)
                .FirstOrDefaultAsync(e => e.ID == id);

            if (employee == null)
            {
                return HttpNotFound();
            }

            if (employee.DismissalDate != null)
            {
                TempData["Error"] = "Bu xodim allaqachon ishdan bo'shatilgan";
                return RedirectToAction("Index");
            }

            ViewBag.Employee = employee;
            return View();
        }

        // POST so'rovi: HR/Employees/Dismiss/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Dismiss(int id, DateTime dismissalDate, string reason)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var employee = await db.HR_EMPLOYEES.FirstOrDefaultAsync(e => e.ID == id);
            if (employee == null)
            {
                return HttpNotFound();
            }

            if (employee.DismissalDate != null)
            {
                TempData["Error"] = "Bu xodim allaqachon ishdan bo'shatilgan";
                return RedirectToAction("Index");
            }

            if (employee.EmploymentDate.HasValue && dismissalDate.Date < employee.EmploymentDate.Value.Date)
            {
                TempData["Error"] = "Bo'shatish sanasi ishga qabul sanasidan oldin bo'lishi mumkin emas";
                return RedirectToAction("Dismiss", new { id });
            }

            // Joriy (ochiq) davr bo'shatish sanasi bilan yopiladi; avval yangi ustma-ust "History" qatori qo'shilib,
            // ochiq qator qolardi va xodim keyingi tabellarga tushardi (HRM-B06)
            await EmployeeHistoryService.CloseCurrentPeriodAsync(db, employee, dismissalDate,
                EmployeeHistoryService.ActionHistory, reason, UserHelper.GetUserId(User.Identity.Name));

            employee.DismissalDate = dismissalDate;
            employee.IsActive = false;
            employee.UpdatedDT = DateTime.Now;

            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[HrEmployees.Dismiss] " + ex);
                TempData["Error"] = "Saqlashda xatolik yuz berdi";
                return RedirectToAction("Dismiss", new { id });
            }

            logger.Log("HrEmployees", "Dismiss", $"Xodim: {employee.FullName} ({employee.TabNo ?? "-"}), Bo'lim: {OrgUnitLabel(employee.OrgUnitID)}, Lavozim: {PositionLabel(employee.PositionID)}, Sana: {dismissalDate:dd.MM.yyyy}, Sabab: {reason}");
            TempData["Success"] = "Xodim muvaffaqiyatli ishdan bo'shatildi";
            return RedirectToAction("Index");
        }

        // Olish so'rovi: HR/Employees/Transfer/5
        public async Task<ActionResult> Transfer(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var employee = await db.HR_EMPLOYEES.AsNoTracking()
                .Include(e => e.HR_ORG_UNITS)
                .Include(e => e.HR_POSITIONS)
                .FirstOrDefaultAsync(e => e.ID == id);

            if (employee == null)
            {
                return HttpNotFound();
            }

            if (employee.DismissalDate != null)
            {
                TempData["Error"] = "Ishdan bo'shatilgan xodimni transfer qilib bo'lmaydi";
                return RedirectToAction("Index");
            }

            ViewBag.Employee = employee;
            ViewBag.FromOrgUnitID = employee.OrgUnitID;
            ViewBag.FromPositionID = employee.PositionID;
            ViewBag.ToOrgUnitID = HrLookups.SelectList(HrLookups.OrgUnits);
            // Lavozimlar bo'lim tanlangach AJAX orqali yuklanadi; boshida bo'sh
            ViewBag.ToPositionID = new SelectList(Enumerable.Empty<SelectListItem>(), "Value", "Text");

            return View();
        }

        /// <summary>
        /// Tanlangan bo'lim uchun faqat shu bo'limdagi lavozimlarni qaytaradi (HR_STAFFING_PLAN orqali).
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> GetPositionsByOrgUnit(int orgUnitId)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrEmployees"))
            {
                return new HttpStatusCodeResult(403);
            }
            var today = DateTime.Today;
            var positions = await db.HR_POSITIONS.AsNoTracking()
                .Where(p => p.IsActive &&
                            db.HR_STAFFING_PLAN.Any(sp => sp.PositionID == p.ID && sp.OrgUnitID == orgUnitId && sp.IsActive
                                && (sp.EffectiveTo == null || sp.EffectiveTo >= today)
                                && sp.IsDeleted != true))
                .OrderBy(p => p.Name)
                .Select(p => new { id = p.ID, name = p.Name })
                .ToListAsync();
            return Json(positions, JsonRequestBehavior.AllowGet);
        }

        // POST so'rovi: HR/Employees/Transfer/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Transfer(int id, int toOrgUnitId, int toPositionId, DateTime effectiveDate, string reason)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var employee = await db.HR_EMPLOYEES.FirstOrDefaultAsync(e => e.ID == id);
            if (employee == null)
            {
                return HttpNotFound();
            }

            if (employee.DismissalDate != null)
            {
                TempData["Error"] = "Ishdan bo'shatilgan xodimni transfer qilib bo'lmaydi";
                return RedirectToAction("Index");
            }

            if (employee.OrgUnitID == toOrgUnitId && employee.PositionID == toPositionId)
            {
                TempData["Error"] = "Xodim allaqachon ushbu bo'lim va lavozimda";
                return RedirectToAction("Transfer", new { id });
            }

            // Tanlangan lavozim shu bo'limda mavjud ekanligini tekshirish (HR_STAFFING_PLAN)
            var today = DateTime.Today;
            var positionInOrg = await db.HR_STAFFING_PLAN
                .AnyAsync(sp => sp.OrgUnitID == toOrgUnitId && sp.PositionID == toPositionId && sp.IsActive
                    && (sp.IsDeleted != true)
                    && (sp.EffectiveTo == null || sp.EffectiveTo >= today));
            if (!positionInOrg)
            {
                TempData["Error"] = "Tanlangan lavozim ushbu bo'lim uchun reja qilinmagan. Iltimos, faqat tanlangan bo'limdagi lavozimlardan birini tanlang.";
                return RedirectToAction("Transfer", new { id });
            }

            var userId = UserHelper.GetUserId(User.Identity.Name);

            // 1) joriy davr (effectiveDate - 1) bilan yopiladi (tarixsiz eski ma'lumot uchun davr tiklanadi),
            // 2) yangi bo'lim/lavozim uchun ochiq davr ochiladi.
            await EmployeeHistoryService.CloseCurrentPeriodAsync(db, employee, effectiveDate.Date.AddDays(-1),
                EmployeeHistoryService.ActionInitial, "Transfer oldidan mavjud lavozim davri", userId);

            db.HR_EMPLOYEE_HISTORY.Add(new HR_EMPLOYEE_HISTORY
            {
                EmployeeID = id,
                ActionType = EmployeeHistoryService.ActionTransfer,
                FromOrgUnitID = toOrgUnitId,
                FromPositionID = toPositionId,
                ToOrgUnitID = null,
                ToPositionID = null,
                EffectiveDateFrom = effectiveDate.Date,
                EffectiveDateTo = null,
                Reason = string.IsNullOrWhiteSpace(reason) ? "Transfer qilindi" : reason.Trim(),
                CreatedByUserID = userId,
                CreatedDT = DateTime.Now
            });

            // EmploymentDate (ishga qabul sanasi) o'zgarmaydi — yangi lavozim boshlanishi faqat tarixda (HRM-B06)
            employee.OrgUnitID = toOrgUnitId;
            employee.PositionID = toPositionId;
            employee.UpdatedDT = DateTime.Now;

            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[HrEmployees.Transfer] " + ex);
                TempData["Error"] = "Saqlashda xatolik yuz berdi";
                return RedirectToAction("Transfer", new { id });
            }

            logger.Log("HrEmployees", "Transfer", $"Xodim: {employee.FullName} ({employee.TabNo ?? "-"}), YangiBo'lim: {OrgUnitLabel(toOrgUnitId)}, YangiLavozim: {PositionLabel(toPositionId)}, Sana: {effectiveDate:dd.MM.yyyy}");
            TempData["Success"] = "Xodim muvaffaqiyatli transfer qilindi";
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
