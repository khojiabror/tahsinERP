using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels.HR;
using System.Collections.Generic;
using ClosedXML.Excel;
using System.IO;

namespace tahsinERP.Controllers.HR
{
    [Authorize]
    public class HrStaffingPlanController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();
        private IUserActionLogger logger;

        public HrStaffingPlanController()
        {
            logger = new UserActionLogger(User?.Identity?.Name ?? "System");
        }

        // Olish so'rovi: HR/StaffingPlan
        public async Task<ActionResult> Index(int? orgUnitId, int? positionId, bool? isActive)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            IQueryable<HR_STAFFING_PLAN> query = db.HR_STAFFING_PLAN
                .Where(sp => sp.IsDeleted == false)
                .Include(sp => sp.HR_ORG_UNITS)
                .Include(sp => sp.HR_POSITIONS)
                .Include(sp => sp.HR_SALARY_GRADES);

            if (orgUnitId.HasValue)
            {
                query = query.Where(sp => sp.OrgUnitID == orgUnitId.Value);
            }

            if (positionId.HasValue)
            {
                query = query.Where(sp => sp.PositionID == positionId.Value);
            }

            if (isActive.HasValue)
            {
                query = query.Where(sp => sp.IsActive == isActive.Value);
            }
            else
            {
                query = query.Where(sp => sp.IsActive);
            }

            var staffingPlans = await query.OrderBy(sp => sp.HR_ORG_UNITS.Name).ThenBy(sp => sp.HR_POSITIONS.Name).ToListAsync();

            // Olish barcha employee counts in one query ga avoid N+1 problem
            var employeeCounts = await db.HR_EMPLOYEES
                .Where(e => e.IsActive && e.OrgUnitID.HasValue && e.PositionID.HasValue)
                .GroupBy(e => new { e.OrgUnitID, e.PositionID })
                .Select(g => new { g.Key.OrgUnitID, g.Key.PositionID, Count = g.Count() })
                .ToListAsync();

            var viewModels = staffingPlans.Select(sp =>
            {
                var vm = new HrStaffingPlanViewModel
                {
                    ID = sp.ID,
                    OrgUnitID = sp.OrgUnitID,
                    OrgUnitName = sp.HR_ORG_UNITS?.Name,
                    PositionID = sp.PositionID,
                    PositionName = sp.HR_POSITIONS?.Name,
                    SalaryGradeID = sp.SalaryGradeID,
                    SalaryGradeName = sp.HR_SALARY_GRADES?.Name,
                    PlannedHeadcount = sp.PlannedHeadcount,
                    IsPieceRate = sp.IsPieceRate,
                    IsHourly = sp.IsHourly,
                    IsFixedSalary = sp.IsFixedSalary,
                    EffectiveFrom = sp.EffectiveFrom,
                    EffectiveTo = sp.EffectiveTo,
                    IsActive = sp.IsActive
                };

                // Olish actual headcount dan pre-loaded employee counts
                var countData = employeeCounts.FirstOrDefault(c => 
                    c.OrgUnitID == sp.OrgUnitID && c.PositionID == sp.PositionID);
                vm.ActualHeadcount = countData?.Count ?? 0;

                return vm;
            }).ToList();

            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", orgUnitId);

            ViewBag.PositionID = new SelectList(
                await db.HR_POSITIONS.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name", positionId);

            logger.Log("HrStaffingPlan", "Index");
            return View(viewModels);
        }

        // Olish so'rovi: HR/StaffingPlan/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var staffingPlan = await db.HR_STAFFING_PLAN
                .Where(sp => sp.ID == id && sp.IsDeleted == false)
                .Include(sp => sp.HR_ORG_UNITS)
                .Include(sp => sp.HR_POSITIONS)
                .Include(sp => sp.HR_SALARY_GRADES)
                .FirstOrDefaultAsync();

            if (staffingPlan == null)
            {
                return HttpNotFound();
            }

            var viewModel = new HrStaffingPlanViewModel
            {
                ID = staffingPlan.ID,
                OrgUnitID = staffingPlan.OrgUnitID,
                OrgUnitName = staffingPlan.HR_ORG_UNITS?.Name,
                PositionID = staffingPlan.PositionID,
                PositionName = staffingPlan.HR_POSITIONS?.Name,
                SalaryGradeID = staffingPlan.SalaryGradeID,
                SalaryGradeName = staffingPlan.HR_SALARY_GRADES?.Name,
                PlannedHeadcount = staffingPlan.PlannedHeadcount,
                IsPieceRate = staffingPlan.IsPieceRate,
                IsHourly = staffingPlan.IsHourly,
                IsFixedSalary = staffingPlan.IsFixedSalary,
                EffectiveFrom = staffingPlan.EffectiveFrom,
                EffectiveTo = staffingPlan.EffectiveTo,
                IsActive = staffingPlan.IsActive,
                ActualHeadcount = await db.HR_EMPLOYEES.CountAsync(e => 
                    e.OrgUnitID == staffingPlan.OrgUnitID && 
                    e.PositionID == staffingPlan.PositionID && 
                    e.IsActive)
            };

            logger.Log("HrStaffingPlan", "Details", $"Bo'lim: {viewModel.OrgUnitName}, Lavozim: {viewModel.PositionName}");
            return View(viewModel);
        }

        // Olish so'rovi: HR/StaffingPlan/yaratish
        public async Task<ActionResult> Create(int? orgUnitId)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            await PopulateViewBags(null, orgUnitId);
            return View();
        }

        // POST so'rovi: HR/StaffingPlan/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(HrStaffingPlanViewModel viewModel)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                var staffingPlan = new HR_STAFFING_PLAN
                {
                    OrgUnitID = viewModel.OrgUnitID,
                    PositionID = viewModel.PositionID,
                    SalaryGradeID = viewModel.SalaryGradeID,
                    PlannedHeadcount = viewModel.PlannedHeadcount,
                    IsPieceRate = viewModel.IsPieceRate,
                    IsHourly = viewModel.IsHourly,
                    IsFixedSalary = viewModel.IsFixedSalary,
                    EffectiveFrom = viewModel.EffectiveFrom,
                    EffectiveTo = viewModel.EffectiveTo,
                    IsActive = viewModel.IsActive
                };

                db.HR_STAFFING_PLAN.Add(staffingPlan);
                await db.SaveChangesAsync();

                var orgUnitName = (await db.HR_ORG_UNITS.FindAsync(staffingPlan.OrgUnitID))?.Name ?? "-";
                var positionName = (await db.HR_POSITIONS.FindAsync(staffingPlan.PositionID))?.Name ?? "-";
                logger.Log("HrStaffingPlan", "Create", $"Bo'lim: {orgUnitName}, Lavozim: {positionName}, RejaSoni: {staffingPlan.PlannedHeadcount}");
                return RedirectToAction("Index");
            }

            await PopulateViewBags(viewModel, null);
            return View(viewModel);
        }

        // Olish so'rovi: HR/StaffingPlan/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var staffingPlan = await db.HR_STAFFING_PLAN.FindAsync(id);
            if (staffingPlan == null)
            {
                return HttpNotFound();
            }

            var viewModel = new HrStaffingPlanViewModel
            {
                ID = staffingPlan.ID,
                OrgUnitID = staffingPlan.OrgUnitID,
                PositionID = staffingPlan.PositionID,
                SalaryGradeID = staffingPlan.SalaryGradeID,
                PlannedHeadcount = staffingPlan.PlannedHeadcount,
                IsPieceRate = staffingPlan.IsPieceRate,
                IsHourly = staffingPlan.IsHourly,
                IsFixedSalary = staffingPlan.IsFixedSalary,
                EffectiveFrom = staffingPlan.EffectiveFrom,
                EffectiveTo = staffingPlan.EffectiveTo,
                IsActive = staffingPlan.IsActive
            };

            await PopulateViewBags(viewModel, null);
            return View(viewModel);
        }

        // POST so'rovi: HR/StaffingPlan/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(HrStaffingPlanViewModel viewModel)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                var staffingPlan = await db.HR_STAFFING_PLAN.FindAsync(viewModel.ID);
                if (staffingPlan == null)
                {
                    return HttpNotFound();
                }

                staffingPlan.OrgUnitID = viewModel.OrgUnitID;
                staffingPlan.PositionID = viewModel.PositionID;
                staffingPlan.SalaryGradeID = viewModel.SalaryGradeID;
                staffingPlan.PlannedHeadcount = viewModel.PlannedHeadcount;
                staffingPlan.IsPieceRate = viewModel.IsPieceRate;
                staffingPlan.IsHourly = viewModel.IsHourly;
                staffingPlan.IsFixedSalary = viewModel.IsFixedSalary;
                staffingPlan.EffectiveFrom = viewModel.EffectiveFrom;
                staffingPlan.EffectiveTo = viewModel.EffectiveTo;
                staffingPlan.IsActive = viewModel.IsActive;

                db.Entry(staffingPlan).State = EntityState.Modified;
                await db.SaveChangesAsync();

                var orgUnitName = (await db.HR_ORG_UNITS.FindAsync(staffingPlan.OrgUnitID))?.Name ?? "-";
                var positionName = (await db.HR_POSITIONS.FindAsync(staffingPlan.PositionID))?.Name ?? "-";
                logger.Log("HrStaffingPlan", "Edit", $"Bo'lim: {orgUnitName}, Lavozim: {positionName}, RejaSoni: {staffingPlan.PlannedHeadcount}");
                return RedirectToAction("Index");
            }

            await PopulateViewBags(viewModel, null);
            return View(viewModel);
        }

        // POST so'rovi: HR/StaffingPlan/o'chirish/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var staffingPlan = await db.HR_STAFFING_PLAN.FindAsync(id);
            if (staffingPlan == null)
            {
                return HttpNotFound();
            }

            staffingPlan.IsDeleted = true;
            db.Entry(staffingPlan).State = System.Data.Entity.EntityState.Modified;
            await db.SaveChangesAsync();

            var orgUnitName = staffingPlan.HR_ORG_UNITS?.Name ?? "-";
            var positionName = staffingPlan.HR_POSITIONS?.Name ?? "-";
            logger.Log("HrStaffingPlan", "Delete", $"Bo'lim: {orgUnitName}, Lavozim: {positionName}");
            return RedirectToAction("Index");
        }

        // Olish so'rovi: HR/StaffingPlan/GenerateFromEmployees
        public async Task<ActionResult> GenerateFromEmployees()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Olish barcha faol employees
            var employees = await db.HR_EMPLOYEES
                .Where(e => e.IsActive && e.OrgUnitID.HasValue && e.PositionID.HasValue)
                .ToListAsync();

            var proposals = new List<HrStaffingPlanViewModel>();

            foreach (var emp in employees)
            {
                // Tekshirish agar staffing plan already mavjud
                var existing = await db.HR_STAFFING_PLAN
                    .FirstOrDefaultAsync(sp => sp.OrgUnitID == emp.OrgUnitID.Value &&
                                              sp.PositionID == emp.PositionID.Value &&
                                              sp.IsActive);

                if (existing == null)
                {
                    proposals.Add(new HrStaffingPlanViewModel
                    {
                        OrgUnitID = emp.OrgUnitID.Value,
                        OrgUnitName = emp.HR_ORG_UNITS?.Name,
                        PositionID = emp.PositionID.Value,
                        PositionName = emp.HR_POSITIONS?.Name,
                        SalaryGradeID = emp.SalaryGradeID,
                        PlannedHeadcount = 1,
                        IsFixedSalary = true,
                        EffectiveFrom = emp.EmploymentDate ?? DateTime.Now,
                        IsActive = true
                    });
                }
            }

            ViewBag.Proposals = proposals;
            logger.Log("HrStaffingPlan", "GenerateFromEmployees");
            return View(proposals);
        }

        // POST so'rovi: HR/StaffingPlan/CreateFromProposals
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateFromProposals(List<HrStaffingPlanViewModel> proposals)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (proposals != null && proposals.Any())
            {
                foreach (var proposal in proposals.Where(p => p.IsActive))
                {
                    var staffingPlan = new HR_STAFFING_PLAN
                    {
                        OrgUnitID = proposal.OrgUnitID,
                        PositionID = proposal.PositionID,
                        SalaryGradeID = proposal.SalaryGradeID,
                        PlannedHeadcount = proposal.PlannedHeadcount,
                        IsPieceRate = proposal.IsPieceRate,
                        IsHourly = proposal.IsHourly,
                        IsFixedSalary = proposal.IsFixedSalary,
                        EffectiveFrom = proposal.EffectiveFrom,
                        EffectiveTo = proposal.EffectiveTo,
                        IsActive = true
                    };

                    db.HR_STAFFING_PLAN.Add(staffingPlan);
                }

                await db.SaveChangesAsync();
                logger.Log("HrStaffingPlan", "CreateFromProposals", $"YaratilganRejaSoni: {proposals.Count(p => p.IsActive)}");
            }

            return RedirectToAction("Index");
        }

        // Olish so'rovi: HR/StaffingPlan/Import
        public ActionResult Import()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            return View();
        }

        // POST so'rovi: HR/StaffingPlan/Import
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Import(HttpPostedFileBase file)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (file == null || file.ContentLength == 0)
            {
                TempData["Error"] = "Файл не выбран";
                return View();
            }

            try
            {
                using (var workbook = new XLWorkbook(file.InputStream))
                {
                    var worksheet = workbook.Worksheet(1);
                    var rowCount = worksheet.LastRowNumber();

                    var importedData = new List<Dictionary<string, object>>();

                    // Skip header qator, start dan qator 2
                    for (int row = 2; row <= rowCount; row++)
                    {
                        var rowData = new Dictionary<string, object>();
                        rowData["RowNumber"] = row;
                        rowData["TabNo"] = worksheet.Cell(row, 3).GetRawValue()?.ToString(); // ustun C - Таб.№
                        rowData["FullName"] = worksheet.Cell(row, 2).GetRawValue()?.ToString(); // ustun B - F.I.Sh
                        rowData["Position"] = worksheet.Cell(row, 4).GetRawValue()?.ToString(); // ustun D - Lavozim
                        rowData["BaseSalary"] = worksheet.Cell(row, 5).GetRawValue()?.ToString(); // ustun E - Начисление

                        if (!string.IsNullOrWhiteSpace(rowData["TabNo"]?.ToString()))
                        {
                            importedData.Add(rowData);
                        }
                    }

                    TempData["ImportedData"] = importedData;
                    logger.Log("HrStaffingPlan", "Import", $"ImportQatorSoni: {importedData.Count}");
                    return View("ImportPreview", importedData);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Import xatosi: {ex.Message}";
                return View();
            }
        }

        // POST so'rovi: HR/StaffingPlan/ImportConfirm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ImportConfirm()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrStaffingPlan"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var importedData = TempData["ImportedData"] as List<Dictionary<string, object>>;
            
            if (importedData == null || !importedData.Any())
            {
                TempData["Error"] = "Ma'lumotlar topilmadi";
                return RedirectToAction("Import");
            }

            try
            {
                int successCount = 0;
                int errorCount = 0;

                foreach (var row in importedData)
                {
                    try
                    {
                        var tabNo = row["TabNo"]?.ToString();
                        var fullName = row["FullName"]?.ToString();
                        var positionName = row["Position"]?.ToString();
                        var baseSalaryStr = row["BaseSalary"]?.ToString();

                        if (string.IsNullOrWhiteSpace(tabNo) || string.IsNullOrWhiteSpace(positionName))
                        {
                            errorCount++;
                            continue;
                        }

                        // Find employee by TabNo
                        var employee = await db.HR_EMPLOYEES
                            .FirstOrDefaultAsync(e => e.TabNo == tabNo && e.IsActive);

                        if (employee == null)
                        {
                            errorCount++;
                            continue;
                        }

                        // Find position
                        var position = await db.HR_POSITIONS
                            .FirstOrDefaultAsync(p => p.Name == positionName && p.IsActive);

                        if (position == null)
                        {
                            errorCount++;
                            continue;
                        }

                        // Tekshirish agar staffing plan already mavjud
                        var existingPlan = await db.HR_STAFFING_PLAN
                            .FirstOrDefaultAsync(sp => sp.OrgUnitID == employee.OrgUnitID && 
                                                       sp.PositionID == position.ID && 
                                                       sp.IsActive);

                        if (existingPlan == null)
                        {
                            var staffingPlan = new HR_STAFFING_PLAN
                            {
                                OrgUnitID = employee.OrgUnitID ?? 1,
                                PositionID = position.ID,
                                PlannedHeadcount = 1,
                                IsFixedSalary = true,
                                IsHourly = false,
                                IsPieceRate = false,
                                EffectiveFrom = DateTime.Now,
                                IsActive = true
                            };

                            db.HR_STAFFING_PLAN.Add(staffingPlan);
                            successCount++;
                        }
                    }
                    catch
                    {
                        errorCount++;
                    }
                }

                await db.SaveChangesAsync();
                logger.Log("HrStaffingPlan", "ImportConfirm", $"MuvaffaqiyatliSoni: {successCount}, XatolarSoni: {errorCount}");

                if (successCount > 0)
                {
                    TempData["Success"] = $"{successCount} ta pozitsiya muvaffaqiyatli qo'shildi";
                }
                if (errorCount > 0)
                {
                    TempData["Warning"] = $"{errorCount} ta qatorda xatolik yuz berdi";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Saqlashda xatolik: {ex.Message}";
                return RedirectToAction("Import");
            }
        }

        private async Task PopulateViewBags(HrStaffingPlanViewModel viewModel, int? orgUnitId)
        {
            ViewBag.OrgUnitID = new SelectList(
                await db.HR_ORG_UNITS.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync(),
                "ID", "Name", viewModel?.OrgUnitID ?? orgUnitId);

            ViewBag.PositionID = new SelectList(
                await db.HR_POSITIONS.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name", viewModel?.PositionID);

            ViewBag.SalaryGradeID = new SelectList(
                await db.HR_SALARY_GRADES.OrderBy(sg => sg.GradeCode).ToListAsync(),
                "ID", "Name", viewModel?.SalaryGradeID);
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

