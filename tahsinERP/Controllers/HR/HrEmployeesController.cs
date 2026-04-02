using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels.HR;

namespace tahsinERP.Controllers.HR
{
    // yordamchi class uchun SQL query result
    public class HistoryWithDates
    {
        public int ID { get; set; }
        public DateTime EffectiveDateFrom { get; set; }
        public DateTime? EffectiveDateTo { get; set; }
        public string FromOrgUnitName { get; set; }
        public string FromPositionName { get; set; }
        public string Reason { get; set; }
    }

    [Authorize]
    public class HrEmployeesController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();
        private IUserActionLogger logger;

        public HrEmployeesController()
        {
            logger = new UserActionLogger(User?.Identity?.Name ?? "System");
        }

        // Olish so'rovi: HR/Employees
        public async Task<ActionResult> Index(int? orgUnitId, int? positionId, string status)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrEmployees"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            IQueryable<HR_EMPLOYEES> query = db.HR_EMPLOYEES
                .Include(e => e.HR_ORG_UNITS)
                .Include(e => e.HR_POSITIONS)
                .Include(e => e.HR_SALARY_GRADES);

            if (orgUnitId.HasValue)
            {
                query = query.Where(e => e.OrgUnitID == orgUnitId.Value);
            }

            if (positionId.HasValue)
            {
                query = query.Where(e => e.PositionID == positionId.Value);
            }

            // standart: show faqat faol employees (not dismissed)
            if (status == "InActive")
            {
                query = query.Where(e => !e.IsActive || e.DismissalDate != null);
            }
            else
            {
                // Show faqat faol employees (IsActive = true va DismissalDate is null)
                query = query.Where(e => e.IsActive && e.DismissalDate == null);
            }

            var employees = await query.OrderBy(e => e.FullName).ToListAsync();

            // Olish latest position uchun har bir employee dan history
            var employeeIds = employees.Select(e => e.ID).ToList();
            var latestHistory = await db.HR_EMPLOYEE_HISTORY
                .Include(h => h.HR_ORG_UNITS1) // ToOrgUnitID navigation
                .Include(h => h.HR_POSITIONS1) // ToPositionID navigation
                .Where(h => employeeIds.Contains(h.EmployeeID) && h.ActionType == "Transfer")
                .GroupBy(h => h.EmployeeID)
                .Select(g => new { 
                    EmployeeID = g.Key, 
                    LatestHistory = g.OrderByDescending(x => x.EffectiveDateFrom).FirstOrDefault() 
                })
                .ToListAsync();

            var viewModels = employees.Select(e => {
                var latestTransfer = latestHistory.FirstOrDefault(h => h.EmployeeID == e.ID)?.LatestHistory;
                
                // Use current position agar no transfer history, otherwise use latest transfer position
                var currentOrgUnitID = latestTransfer?.ToOrgUnitID ?? e.OrgUnitID;
                var currentPositionID = latestTransfer?.ToPositionID ?? e.PositionID;
                
                // Olish names
                var currentOrgUnit = latestTransfer?.HR_ORG_UNITS1 ?? e.HR_ORG_UNITS;
                var currentPosition = latestTransfer?.HR_POSITIONS1 ?? e.HR_POSITIONS;

                return new HrEmployeeViewModel
                {
                    ID = e.ID,
                    TabNo = e.TabNo,
                    FullName = e.FullName,
                    OrgUnitID = currentOrgUnitID,
                    OrgUnitName = currentOrgUnit?.Name,
                    PositionID = currentPositionID,
                    PositionName = currentPosition?.Name,
                    BaseSalary = e.BaseSalary,
                    SalaryGradeID = e.SalaryGradeID,
                    SalaryGradeName = e.HR_SALARY_GRADES?.Name,
                    EmploymentDate = e.EmploymentDate,
                    DismissalDate = e.DismissalDate,
                    IsActive = e.IsActive
                };
            }).ToList();

            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnitId);

            ViewBag.PositionID = new SelectList(
                await db.HR_POSITIONS.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name", positionId);

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

            var employee = await db.HR_EMPLOYEES
                .Include(e => e.HR_ORG_UNITS)
                .Include(e => e.HR_POSITIONS)
                .Include(e => e.HR_SALARY_GRADES)
                .FirstOrDefaultAsync(e => e.ID == id);

            if (employee == null)
            {
                return HttpNotFound();
            }

            var viewModel = new HrEmployeeViewModel
            {
                ID = employee.ID,
                TabNo = employee.TabNo,
                FullName = employee.FullName,
                OrgUnitID = employee.OrgUnitID,
                OrgUnitName = employee.HR_ORG_UNITS?.Name,
                PositionID = employee.PositionID,
                PositionName = employee.HR_POSITIONS?.Name,
                BaseSalary = employee.BaseSalary,
                SalaryGradeID = employee.SalaryGradeID,
                SalaryGradeName = employee.HR_SALARY_GRADES?.Name,
                EmploymentDate = employee.EmploymentDate,
                DismissalDate = employee.DismissalDate,
                IsActive = employee.IsActive
            };

            // History ni faqat history jadvalidan chiqaramiz.
            var historyList = new List<HrEmployeeHistoryViewModel>();
            var historyRecords = await db.HR_EMPLOYEE_HISTORY
                .Include(h => h.HR_ORG_UNITS)
                .Include(h => h.HR_POSITIONS)
                .Where(h => h.EmployeeID == id.Value)
                .OrderBy(h => h.EffectiveDateFrom)
                .ToListAsync();

            if (historyRecords.Any())
            {
                foreach (var h in historyRecords)
                {
                    historyList.Add(new HrEmployeeHistoryViewModel
                    {
                        FromOrgUnitName = h.HR_ORG_UNITS?.Name ?? "-",
                        FromPositionName = h.HR_POSITIONS?.Name ?? "-",
                        EffectiveDate = h.EffectiveDateFrom,
                        EffectiveDateTo = h.EffectiveDateTo,
                        Reason = h.Reason ?? "-",
                        IsCurrentRow = !h.EffectiveDateTo.HasValue
                    });
                }
            }
            else if (employee.EmploymentDate.HasValue && (employee.OrgUnitID.HasValue || employee.PositionID.HasValue))
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
            ViewBag.HistoryOrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", employee.OrgUnitID);
            ViewBag.HistoryPositionID = new SelectList(
                await db.HR_POSITIONS.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name", employee.PositionID);

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

            // Yaratish next TabNo automatically
            string nextTabNo = await GenerateNextTabNo();

            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name");

            ViewBag.PositionID = new SelectList(
                await db.HR_POSITIONS.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name");

            ViewBag.SalaryGradeID = new SelectList(
                await db.HR_SALARY_GRADES.OrderBy(sg => sg.GradeCode).ToListAsync(),
                "ID", "Name");

            ViewBag.NextTabNo = nextTabNo;

            return View();
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

            if (ModelState.IsValid)
            {
                // Yaratish TabNo automatically agar not provided yoki empty
                string tabNo = viewModel.TabNo;
                if (string.IsNullOrWhiteSpace(tabNo))
                {
                    tabNo = await GenerateNextTabNo();
                }

                // Tekshirish agar TabNo already mavjud
                var existing = await db.HR_EMPLOYEES
                    .FirstOrDefaultAsync(e => e.TabNo == tabNo);
                if (existing != null)
                {
                    ModelState.AddModelError("TabNo", "Табельный номер уже существует");
                    await PopulateViewBags(viewModel);
                    return View(viewModel);
                }

                var employee = new HR_EMPLOYEES
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
                    CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                    CreatedDT = DateTime.Now
                };

                db.HR_EMPLOYEES.Add(employee);
                await db.SaveChangesAsync();

                // Yaratish HrEmployeeHistory record uchun the new employee
                if (employee.OrgUnitID.HasValue && employee.PositionID.HasValue && employee.EmploymentDate.HasValue)
                {
                    var history = new HR_EMPLOYEE_HISTORY
                    {
                        EmployeeID = employee.ID,
                        ActionType = "Initial Employment",
                        FromOrgUnitID = employee.OrgUnitID,
                        FromPositionID = employee.PositionID,
                        ToOrgUnitID = null,
                        ToPositionID = null,
                        EffectiveDateFrom = employee.EmploymentDate.Value, // Employment start date
                        EffectiveDateTo = employee.DismissalDate, // Dismissal date (agar any)
                        Reason = "Ishga qabul qilingan",
                        CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                        CreatedDT = DateTime.Now
                    };

                    db.HR_EMPLOYEE_HISTORY.Add(history);
                    await db.SaveChangesAsync(); // saqlash history record
                    var orgUnit = await db.HR_ORG_UNITS.FindAsync(employee.OrgUnitID.Value);
                    var position = await db.HR_POSITIONS.FindAsync(employee.PositionID.Value);
                    var orgUnitName = orgUnit?.Name ?? "-";
                    var positionName = position?.Name ?? "-";
                    logger.Log("HrEmployees", "CreateHistory", $"Xodim: {employee.FullName} ({employee.TabNo ?? "-"}), Bo'lim: {orgUnitName}, Lavozim: {positionName}, Sabab: {history.Reason}");
                }

                var orgUnitNameCreate = employee.OrgUnitID.HasValue
                    ? (await db.HR_ORG_UNITS.FindAsync(employee.OrgUnitID.Value))?.Name ?? "-"
                    : "Butun kompaniya";
                var positionNameCreate = employee.PositionID.HasValue
                    ? (await db.HR_POSITIONS.FindAsync(employee.PositionID.Value))?.Name ?? "-"
                    : "-";
                logger.Log("HrEmployees", "Create", $"Xodim: {employee.FullName} ({employee.TabNo ?? "-"}), Bo'lim: {orgUnitNameCreate}, Lavozim: {positionNameCreate}");
                return RedirectToAction("Index");
            }

            await PopulateViewBags(viewModel);
            return View(viewModel);
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

            var viewModel = new HrEmployeeViewModel
            {
                ID = employee.ID,
                TabNo = employee.TabNo,
                FullName = employee.FullName,
                OrgUnitID = employee.OrgUnitID,
                PositionID = employee.PositionID,
                BaseSalary = employee.BaseSalary,
                SalaryGradeID = employee.SalaryGradeID,
                EmploymentDate = employee.EmploymentDate,
                DismissalDate = employee.DismissalDate,
                IsActive = employee.IsActive
            };

            await PopulateViewBags(viewModel);
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
                // Tekshirish agar TabNo already mavjud uchun another employee
                var existing = await db.HR_EMPLOYEES
                    .FirstOrDefaultAsync(e => e.TabNo == viewModel.TabNo && e.ID != viewModel.ID && e.IsActive);
                if (existing != null)
                {
                    ModelState.AddModelError("TabNo", "Табельный номер уже существует");
                    await PopulateViewBags(viewModel);
                    return View(viewModel);
                }

                var employee = await db.HR_EMPLOYEES.FindAsync(viewModel.ID);
                if (employee == null)
                {
                    return HttpNotFound();
                }

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

                db.Entry(employee).State = EntityState.Modified;
                await db.SaveChangesAsync();

                var orgUnitName = employee.OrgUnitID.HasValue
                    ? (await db.HR_ORG_UNITS.FindAsync(employee.OrgUnitID.Value))?.Name ?? "-"
                    : "Butun kompaniya";
                var positionName = employee.PositionID.HasValue
                    ? (await db.HR_POSITIONS.FindAsync(employee.PositionID.Value))?.Name ?? "-"
                    : "-";
                logger.Log("HrEmployees", "Edit", $"Xodim: {employee.FullName} ({employee.TabNo ?? "-"}), Bo'lim: {orgUnitName}, Lavozim: {positionName}");
                return RedirectToAction("Index");
            }

            await PopulateViewBags(viewModel);
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

            // Soft o'chirish - set IsActive ga false
            employee.IsActive = false;
            employee.DismissalDate = DateTime.Now;
            employee.UpdatedDT = DateTime.Now;

            db.Entry(employee).State = EntityState.Modified;
            await db.SaveChangesAsync();

            var orgUnitNameDelete = employee.OrgUnitID.HasValue
                ? (await db.HR_ORG_UNITS.FindAsync(employee.OrgUnitID.Value))?.Name ?? "-"
                : "Butun kompaniya";
            var positionNameDelete = employee.PositionID.HasValue
                ? (await db.HR_POSITIONS.FindAsync(employee.PositionID.Value))?.Name ?? "-"
                : "-";
            logger.Log("HrEmployees", "Delete", $"Xodim: {employee.FullName} ({employee.TabNo ?? "-"}), Bo'lim: {orgUnitNameDelete}, Lavozim: {positionNameDelete}");
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Generates the next TabNo by finding the highest TabNo that starts bilan '0' va adding 1
        /// Format: 4 digits bilan leading zeros (e.g., 0001, 0009, 0010, 0099, 0100)
        /// </summary>
        private async Task<string> GenerateNextTabNo()
        {
            // Olish barcha TabNo values that start bilan '0' (4-digit format)
            var tabNos = await db.HR_EMPLOYEES
                .Where(e => e.TabNo != null && e.TabNo.StartsWith("0") && e.TabNo.Length == 4)
                .Select(e => e.TabNo)
                .ToListAsync();

            if (tabNos == null || !tabNos.Any())
            {
            // 0 bilan boshlanuvchi TabNo mavjud emas, 0001 dan boshlaymiz
                return "0001";
            }

            // Barcha TabNo qiymatlarini butun songa o'tkazib, maksimalini topamiz
            int maxTabNo = 0;
            foreach (var tabNo in tabNos)
            {
                if (int.TryParse(tabNo, out int tabNoInt))
                {
                    if (tabNoInt > maxTabNo)
                    {
                        maxTabNo = tabNoInt;
                    }
                }
            }

            // 1 ga oshirib, oldi nol bilan 4 xonali formatga keltiramiz
            int nextTabNo = maxTabNo + 1;
            
            // Qiymat 9999 dan oshmasligini tekshiramiz (maksimal 4 xonali)
            if (nextTabNo > 9999)
            {
                throw new InvalidOperationException("Tabel raqami maksimal qiymatga (9999) yetdi. Iltimos, administratorga murojaat qiling.");
            }

            return nextTabNo.ToString("D4"); // 4 xonali, oldidan nol bilan format
        }

        private async Task PopulateViewBags(HrEmployeeViewModel viewModel)
        {
            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", viewModel.OrgUnitID);

            ViewBag.PositionID = new SelectList(
                await db.HR_POSITIONS.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name", viewModel.PositionID);

            ViewBag.SalaryGradeID = new SelectList(
                await db.HR_SALARY_GRADES.OrderBy(sg => sg.GradeCode).ToListAsync(),
                "ID", "Name", viewModel.SalaryGradeID);
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

            var employee = await db.HR_EMPLOYEES
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

            var employee = await db.HR_EMPLOYEES
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

            // EmploymentDate dan dismissalDate gacha bo'lgan oxirgi lavozim bo'yicha tarix yozuvi yaratish
            if (employee.OrgUnitID.HasValue && employee.PositionID.HasValue && employee.EmploymentDate.HasValue)
            {
                try
                {
                    // Entity Framework orqali HR_EMPLOYEE_HISTORY obyektini yaratish
                    var history = new HR_EMPLOYEE_HISTORY
                    {
                        EmployeeID = id,
                        ActionType = "History",
                        FromOrgUnitID = employee.OrgUnitID,
                        FromPositionID = employee.PositionID,
                        ToOrgUnitID = null,
                        ToPositionID = null,
                        EffectiveDateFrom = employee.EmploymentDate.Value, // Ish boshlagan sana
                        EffectiveDateTo = dismissalDate, // Ishdan bo'shatilgan sana
                        Reason = reason,
                        CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                        CreatedDT = DateTime.Now
                    };

                    db.HR_EMPLOYEE_HISTORY.Add(history);
                }
                catch (Exception ex)
                {
                    // Tarix yozuvi xatoligini qayd etib, xodim yangilanishini davom ettiramiz
                    System.Diagnostics.Debug.WriteLine($"Tarix yozuvini yaratishda xatolik: {ex.Message}");
                }
            }

            // Yangilash employee
            employee.DismissalDate = dismissalDate;
            employee.IsActive = false;
            employee.UpdatedDT = DateTime.Now;

            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Xatolik yuz berdi: {ex.Message}";
                return RedirectToAction("Dismiss", new { id });
            }

            var orgUnitNameDismiss = employee.OrgUnitID.HasValue
                ? (await db.HR_ORG_UNITS.FindAsync(employee.OrgUnitID.Value))?.Name ?? "-"
                : "Butun kompaniya";
            var positionNameDismiss = employee.PositionID.HasValue
                ? (await db.HR_POSITIONS.FindAsync(employee.PositionID.Value))?.Name ?? "-"
                : "-";
            logger.Log("HrEmployees", "Dismiss", $"Xodim: {employee.FullName} ({employee.TabNo ?? "-"}), Bo'lim: {orgUnitNameDismiss}, Lavozim: {positionNameDismiss}, Sana: {dismissalDate:dd.MM.yyyy}");
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

            var employee = await db.HR_EMPLOYEES
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
            ViewBag.ToOrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name");
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
            var positionIds = await db.HR_STAFFING_PLAN
                .Where(sp => sp.OrgUnitID == orgUnitId && sp.IsActive
                    && (sp.EffectiveTo == null || sp.EffectiveTo >= DateTime.Today)
                    && (sp.IsDeleted != true))
                .Select(sp => sp.PositionID)
                .Distinct()
                .ToListAsync();
            var positions = await db.HR_POSITIONS
                .Where(p => positionIds.Contains(p.ID) && p.IsActive)
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

            var employee = await db.HR_EMPLOYEES
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

            if (employee.OrgUnitID == toOrgUnitId && employee.PositionID == toPositionId)
            {
                TempData["Error"] = "Xodim allaqachon ushbu bo'lim va lavozimda";
                return RedirectToAction("Transfer", new { id });
            }

            // Tanlangan lavozim shu bo'limda mavjud ekanligini tekshirish (HR_STAFFING_PLAN)
            var positionInOrg = await db.HR_STAFFING_PLAN
                .AnyAsync(sp => sp.OrgUnitID == toOrgUnitId && sp.PositionID == toPositionId && sp.IsActive
                    && (sp.IsDeleted != true)
                    && (sp.EffectiveTo == null || sp.EffectiveTo >= DateTime.Today));
            if (!positionInOrg)
            {
                TempData["Error"] = "Tanlangan lavozim ushbu bo'lim uchun reja qilinmagan. Iltimos, faqat tanlangan bo'limdagi lavozimlardan birini tanlang.";
                return RedirectToAction("Transfer", new { id });
            }

            // Transfer mantiqi:
            // 1) oldingi ochiq history satrini yopish (to = effectiveDate - 1 kun),
            // 2) yangi bo'lim/lavozim uchun yangi history satrini ochish (from = effectiveDate, to = null).
            var openHistory = await db.HR_EMPLOYEE_HISTORY
                .Where(h => h.EmployeeID == id && h.EffectiveDateTo == null)
                .OrderByDescending(h => h.EffectiveDateFrom)
                .FirstOrDefaultAsync();

            if (openHistory != null)
            {
                var closeDate = effectiveDate.Date.AddDays(-1);
                if (closeDate >= openHistory.EffectiveDateFrom.Date)
                {
                    openHistory.EffectiveDateTo = closeDate;
                }
                else
                {
                    openHistory.EffectiveDateTo = openHistory.EffectiveDateFrom;
                }
            }
            else if (employee.OrgUnitID.HasValue && employee.PositionID.HasValue && employee.EmploymentDate.HasValue)
            {
                // History bo'lmagan eski ma'lumotlar uchun old periodni tiklab qo'yamiz.
                var closeDate = effectiveDate.Date.AddDays(-1);
                db.HR_EMPLOYEE_HISTORY.Add(new HR_EMPLOYEE_HISTORY
                {
                    EmployeeID = id,
                    ActionType = "Initial Employment",
                    FromOrgUnitID = employee.OrgUnitID,
                    FromPositionID = employee.PositionID,
                    ToOrgUnitID = null,
                    ToPositionID = null,
                    EffectiveDateFrom = employee.EmploymentDate.Value.Date,
                    EffectiveDateTo = closeDate >= employee.EmploymentDate.Value.Date ? (DateTime?)closeDate : employee.EmploymentDate.Value.Date,
                    Reason = "Transfer oldidan mavjud lavozim davri",
                    CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                    CreatedDT = DateTime.Now
                });
            }

            db.HR_EMPLOYEE_HISTORY.Add(new HR_EMPLOYEE_HISTORY
            {
                EmployeeID = id,
                ActionType = "Transfer",
                FromOrgUnitID = toOrgUnitId,
                FromPositionID = toPositionId,
                ToOrgUnitID = null,
                ToPositionID = null,
                EffectiveDateFrom = effectiveDate.Date,
                EffectiveDateTo = null,
                Reason = string.IsNullOrWhiteSpace(reason) ? "Transfer qilindi" : reason.Trim(),
                CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                CreatedDT = DateTime.Now
            });

            // Yangilash employee ga new position
            employee.OrgUnitID = toOrgUnitId;
            employee.PositionID = toPositionId;
            employee.EmploymentDate = effectiveDate; // New position start date
            employee.UpdatedDT = DateTime.Now;

            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Xatolik yuz berdi: {ex.Message}";
                return RedirectToAction("Transfer", new { id });
            }

            var newOrgUnit = await db.HR_ORG_UNITS.FindAsync(toOrgUnitId);
            var newPosition = await db.HR_POSITIONS.FindAsync(toPositionId);
            var newOrgUnitName = newOrgUnit?.Name ?? "-";
            var newPositionName = newPosition?.Name ?? "-";
            logger.Log("HrEmployees", "Transfer", $"Xodim: {employee.FullName} ({employee.TabNo ?? "-"}), YangiBo'lim: {newOrgUnitName}, YangiLavozim: {newPositionName}, Sana: {effectiveDate:dd.MM.yyyy}");
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

